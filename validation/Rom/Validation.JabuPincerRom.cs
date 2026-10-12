using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateJabuPincerRom()
    {
        foreach(bool batch in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(5,0x57);
            _inventory.EquipA(0);_inventory.EquipB(0);_player.WarpTo(new(184,56));
            FailIf(_collision.Collides(_player.Position),"Pincer fixture requires original reachable floor in room$5:$57.");
            var slots=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_enemySlots");
            var spawners=slots.Where(pair=>pair.Key.Node is PincerCharacter).ToArray();
            FailIf(spawners.Length!=2 || !spawners.Select(pair=>pair.Key.Node.Position).SequenceEqual(new Vector2[]{new(136,56),new(120,136)}),
                "Room$5:$57 source requires two Pincer spawners at YX$38/$88 and $88/$78.");
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,5,184,56)
                {HostileEnemiesEnabled=true,HostilePartsEnabled=true};
            rom.InitializeLinkGameplay();
            rom[0xcc08]=ValidationRom.LoadCleanUs().Span[0xfdd4b+0x45*4];rom[0xcdd1]=2;
            rom[0xff94]=seed.Rng1;rom[0xff95]=seed.Rng2;
            foreach(var (entity,slot) in spawners)
            {
                int address=0xd080+slot*256;var source=((PincerRoomEntity)entity).Source;
                rom[address]=(byte)(1|(source.KillableEnemyIndex<<4));rom[address+1]=0x45;
                rom.Word(address+10,(int)entity.Node.Position.Y*256);rom.Word(address+12,(int)entity.Node.Position.X*256);
            }
            int update=0,attacks=0,cooldowns=0;
            void Compare(int held=0,int angle=0xff)
            {
                rom.UpdateGameplay(0,held,angle,_entities.FrameCounter);update++;
                var live=slots.Where(pair=>pair.Key.Node is PincerCharacter).ToArray();
                int nativeCount=Enumerable.Range(0,16).Count(slot=>rom[0xd080+slot*256]!=0);
                FailIf(live.Length!=nativeCount || _entities.RoomEnemyCount!=rom[0xcdd1],
                    $"Pincer update{update}: ordered slots/count differ {live.Length}/{nativeCount}, {_entities.RoomEnemyCount}/{rom[0xcdd1]}.");
                foreach(var (entity,slot) in live)
                {
                    var enemy=(PincerCharacter)entity.Node;int address=0xd080+slot*256;
                    FailIf(enemy.Record.SubId!=rom[address+2] || enemy.State!=rom[address+4] || enemy.Counter1!=rom[address+6] ||
                        enemy.Angle!=rom[address+9] || enemy.Extended!=rom[address+0x33] || enemy.CollisionMode!=rom[address+0x25] ||
                        enemy.Position!=new Vector2(rom.Word(address+12)/256f,rom.Word(address+10)/256f) ||
                        enemy.Visible!=((rom[address+0x1a]&0x80)!=0) || enemy.AnimationParameter!=rom[address+0x21],
                        $"Pincer slot${slot:x2} update{update}, batch={batch}: runtime subid/state/counter/angle/extent/mode={enemy.Record.SubId}/{enemy.State}/{enemy.Counter1}/{enemy.Angle}/{enemy.Extended}/${enemy.CollisionMode:x2}, XY={enemy.Position}, visible/anim={enemy.Visible}/{enemy.AnimationParameter}; ROM={rom[address+2]}/{rom[address+4]}/{rom[address+6]}/{rom[address+9]}/{rom[address+0x33]}/${rom[address+0x25]:x2}, XY={rom.Word(address+12)/256f},{rom.Word(address+10)/256f}, visible/anim=${rom[address+0x1a]:x2}/{rom[address+0x21]}.");
                    if(enemy.Record.SubId==1 && enemy.State==12 && enemy.Counter1==8)attacks++;
                    if(enemy.Record.SubId==1 && enemy.State==14 && enemy.Counter1==30)cooldowns++;
                }
                var actual=_random.CaptureState();
                FailIf(actual.Rng1!=rom[0xff94] || actual.Rng2!=rom[0xff95] || actual.Calls-seed.Calls!=rom.RandomCalls,
                    $"Pincer update{update} must preserve shared allocation RNG.");
                FailIf(_player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f),
                    $"Pincer update{update}: Link movement/contact differs {_player.PrecisePosition}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}.");
            }
            StepGameplayUpdates(4,Vector2.Zero,batched:batch,afterUpdate:()=>Compare());
            // Enter the native strict distance-$28 gate along the unchanged corridor.
            StepGameplayUpdates(16,Vector2.Left,MenuRomActions(0x20),batched:batch,afterUpdate:()=>Compare(0x20,24));
            StepGameplayUpdates(120,Vector2.Zero,batched:batch,afterUpdate:()=>Compare());
            StepGameplayUpdates(16,Vector2.Left,MenuRomActions(0x20),batched:batch,afterUpdate:()=>Compare(0x20,24));
            StepGameplayUpdates(120,Vector2.Zero,batched:batch,afterUpdate:()=>Compare());
            FailIf(attacks<2 || cooldowns<2,$"Pincer ROM trace requires repeated extension/hold/retraction/cooldown, saw {attacks}/{cooldowns}.");
        }
        GD.Print("Validated Dungeon7 Pincer ordered allocation, head/body arc positions, approach gate and repeated attack/cooldown against clean-US updates in split/batched gameplay.");
    }
}
