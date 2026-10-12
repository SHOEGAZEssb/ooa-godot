using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateJabuIronMaskRom()
    {
        foreach(bool batch in new[]{false,true})
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(5,0x6e);
            _inventory.EquipA(0); _inventory.EquipB(0); _player.WarpTo(new(208,136));
            FailIf(_collision.Collides(_player.Position),"Iron Mask fixture requires unchanged floor in room$5:$6e.");
            var slots=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_enemySlots");
            foreach(var pair in slots.Where(pair=>pair.Key.Node is not IronMaskCharacter).ToArray())
            {
                // Isolate the two original placements without invoking an unrelated death/drop path.
                SomariaPrivate<List<IRoomEntity>>(_entities,"_activeEntities").Remove(pair.Key);
                SomariaPrivate<HashSet<int>>(_entities,"_reservedEnemySlots").Remove(pair.Value);
                slots.Remove(pair.Key);pair.Key.Node.Free();
            }
            var masks=slots.Where(pair=>pair.Key.Node is IronMaskCharacter).ToArray();
            FailIf(masks.Length!=2 || !masks.Select(pair=>pair.Key.Node.Position).SequenceEqual(new Vector2[]{new(72,136),new(72,152)}),
                "Room$5:$6e source requires two counted Iron Masks at YX$88/$48 and $98/$48.");
            var rom=new EnemyStatusRom(_currentRoom,_saveData,_random.Calls);
            rom[0xcc08]=ValidationRom.LoadCleanUs().Span[0xfdd4b+0x1c*4]; rom[0xcdd1]=2;
            var seed=_random.CaptureState(); rom[0xff94]=seed.Rng1;rom[0xff95]=seed.Rng2;
            foreach(var (entity,slot) in masks)
            {
                int address=0xd080+slot*256;
                rom[address]=(byte)(1|(slot<<4));rom[address+1]=0x1c;
                rom.Word(address+10,(int)entity.Node.Position.Y*256);rom.Word(address+12,(int)entity.Node.Position.X*256);
            }
            bool frozen=true;_entities.TextActiveSource=()=>frozen;int update=0,turns=0;
            void Compare()
            {
                rom[0xcba0]=(byte)(frozen?1:0);rom.Update(_entities.FrameCounter,_player.EnemyContactPosition);update++;
                foreach(var (entity,slot) in masks)
                {
                    var enemy=(IronMaskCharacter)entity.Node;int address=0xd080+slot*256;
                    FailIf(enemy.State!=rom[address+4] || enemy.Counter1!=rom[address+6] || enemy.Angle!=rom[address+9] ||
                        enemy.CollisionMode!=rom[address+0x25] || enemy.Position!=new Vector2(rom.Word(address+12)/256f,rom.Word(address+10)/256f) ||
                        SomariaPrivate<int>(enemy.Animation,"_frameCounter")!=rom[address+0x20] || enemy.AnimationParameter!=rom[address+0x21],
                        $"Iron Mask$1c:$00 slot${slot:x2}, update{update}, batch={batch}: runtime state/counter/angle/mode={enemy.State}/{enemy.Counter1}/${enemy.Angle:x2}/${enemy.CollisionMode:x2}, XY={enemy.Position}, animation={SomariaPrivate<int>(enemy.Animation,"_frameCounter")}/{enemy.AnimationParameter}; ROM={rom[address+4]}/{rom[address+6]}/${rom[address+9]:x2}/${rom[address+0x25]:x2}, XY={rom.Word(address+12)/256f},{rom.Word(address+10)/256f}, animation={rom[address+0x20]}/{rom[address+0x21]}.");
                    if(enemy.State==8)turns++;
                }
                var actual=_random.CaptureState();
                FailIf(actual.Rng1!=rom[0xff94] || actual.Rng2!=rom[0xff95] || actual.Calls-seed.Calls!=rom.RandomCalls,
                    $"Iron Mask room$5:$6e update{update} must preserve shared route RNG: runtime=${actual.Rng2:x2}{actual.Rng1:x2}/{actual.Calls-seed.Calls}, ROM=${rom[0xff95]:x2}{rom[0xff94]:x2}/{rom.RandomCalls}.");
            }
            StepGameplayUpdates(3,Vector2.Zero,batched:batch,afterUpdate:Compare);frozen=false;
            StepGameplayUpdates(150,Vector2.Zero,batched:batch,afterUpdate:Compare);
            FailIf(turns==0,"Iron Mask trace must exercise standing, walking, turning and wall movement.");
        }
        GD.Print("Validated Dungeon7 Iron Mask routes, rear vulnerability, animation and shared RNG against clean-US updates on original room geometry in split/batched gameplay.");
    }
}
