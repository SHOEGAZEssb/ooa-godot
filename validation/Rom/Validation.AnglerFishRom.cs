using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateAnglerFishRom()
    {
        foreach(bool batch in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(5,0x4a);
            _inventory.EquipA(0);_inventory.EquipB(0);_player.WarpTo(new(24,152));
            FailIf(_collision.Collides(_player.Position),"Angler Fish fixture requires source floor in room$5:$4a.");
            var slots=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_enemySlots");
            var body=_entities.Entities<AnglerFishCharacter>().Single();
            FailIf(body.Position!=new Vector2(104,56) || body.Health!=30 || _entities.RoomEnemyCount!=1,
                "Room$5:$4a requires counted Angler Fish$76:$00 at Y$38/X$68, HP30.");
            var seed=_random.CaptureState();var rom=new EnemyStatusRom(_currentRoom,_saveData,seed.Calls);
            rom[0xcc08]=0xb8;rom[0xcc0a]=0xa5;rom[0xcc0c]=0x78;rom[0xcc0e]=0xa7;
            rom[0xd080]=1;rom[0xd081]=0x76;rom[0xd08b]=56;rom[0xd08d]=104;
            var interactions=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_interactionSlots");
            int ladder=0xd040+interactions.Single(pair=>pair.Key is JabuLadderRoomEntity).Value*256;
            rom[ladder]=1;rom[ladder+1]=0x90;rom[ladder+2]=0x0b;rom[ladder+11]=8;rom[ladder+13]=0xd8;
            rom[0xd000]=1;rom[0xd004]=1;rom[0xd024]=0x80;rom[0xd029]=0x38;
            rom[0xd026]=rom[0xd027]=6;rom[0xff94]=seed.Rng1;rom[0xff95]=seed.Rng2;
            rom[0xca4a]=_saveData.GetRoomFlags(5,0x4a);rom[0xca4d]=_saveData.GetRoomFlags(5,0x4d);
            int update=0;var states=new HashSet<int>();
            void Compare()
            {
                rom.Update(_entities.FrameCounter,_player.Position);update++;
                foreach(var (entity,slot) in slots.Where(pair=>pair.Key.Node is AnglerFishCharacter))
                {
                    var actor=(AnglerFishCharacter)entity.Node;int s=0xd080+slot*256;
                    FailIf(rom[s]==0 || actor.SubId!=rom[s+2] || actor.State!=rom[s+4] || actor.Counter1!=rom[s+6] ||
                        actor.Counter2!=rom[s+7] || actor.Angle!=rom[s+9] || actor.Direction!=rom[s+8] || actor.Speed!=rom[s+0x10] ||
                        actor.Health!=rom[s+0x29] || actor.Position!=new Vector2(rom.Word(s+12)/256f,rom.Word(s+10)/256f) ||
                        actor.Visible!=((rom[s+0x1a]&0x80)!=0) || actor.AnimationParameter!=rom[s+0x21] ||
                        (actor.InvincibilityCounter&255)!=rom[s+0x2b],
                        $"Angler slot${slot:x2} update{update}, batch={batch}: runtime sub/state/counts/angle/dir/speed/HP/XY/visible/anim={actor.SubId}/{actor.State}/{actor.Counter1},{actor.Counter2}/{actor.Angle}/{actor.Direction}/${actor.Speed:x2}/{actor.Health}/{actor.Position}/{actor.Visible}/{actor.AnimationParameter}; ROM={rom[s+2]}/{rom[s+4]}/{rom[s+6]},{rom[s+7]}/{rom[s+9]}/{rom[s+8]}/${rom[s+0x10]:x2}/{rom[s+0x29]}/{rom.Word(s+12)/256f},{rom.Word(s+10)/256f}/{rom[s+0x1a]:x2}/{rom[s+0x21]}.");
                }
                states.Add(body.State);
                var rng=_random.CaptureState();
                FailIf(rng.Rng1!=rom[0xff94] || rng.Rng2!=rom[0xff95] || rng.Calls-seed.Calls!=rom.RandomCalls || _entities.RoomEnemyCount!=rom[0xcdd1],
                    $"Angler update{update}: shared RNG/count differs {rng.Calls-seed.Calls}/{rom.RandomCalls}, {_entities.RoomEnemyCount}/{rom[0xcdd1]}.");
                foreach(byte packed in new byte[]{0x80,0x90})FailIf(_currentRoom.GetPackedStorageMetatile(packed)!=rom[0xcf00+packed],
                    $"Angler update{update}: original entrance tile${packed:x2} differs: runtime=${_currentRoom.GetPackedStorageMetatile(packed):x2}, ROM=${rom[0xcf00+packed]:x2}.");
                for(int row=0;row<10;row++)FailIf(_currentRoom.GetPackedStorageMetatile((byte)(row*16+13))!=rom[0xcf0d+row*16],
                    $"Angler update{update}: ladder row${row:x2} differs.");
                FailIf(_saveData.GetRoomFlags(5,0x4a)!=rom[0xca4a] || _saveData.GetRoomFlags(5,0x4d)!=rom[0xca4d],
                    $"Angler update{update}: persistent ladder/portal flags differ.");
            }
            StepGameplayUpdates(360,Vector2.Zero,batched:batch,afterUpdate:Compare);
            FailIf(!new[]{8,9,10,11,12}.All(states.Contains),"Angler ROM trace must cover entry, descent, bouncing and bubble attack.");
            // Resolve one declared Scent Seed through the native collision
            // table; subsequent deflation and reinflation run in gameplay.
            var main=slots.Single(pair=>pair.Key.Node==body);
            rom.HitWithSword(ItemCollisionType.ScentSeed,0,main.Value);
            FailIf(!((IItemCollisionHittableRoomEntity)main.Key).ApplyItemCollision((RoomEntityItemCollision)ItemCollisionType.ScentSeed,
                body.CollisionBounds,body.Position,0,new List<RoomEntitySpawn>()),"Angler body must accept Scent Seed deflation.");
            StepGameplayUpdates(240,Vector2.Zero,batched:batch,afterUpdate:Compare);
            FailIf(!new[]{13,14,15}.All(states.Contains) || body.State is 13 or 14 or 15,
                $"Scent Seed must complete the native deflation/wait/reinflation states and restore the fight: states={string.Join(',',states)}, state={body.State}, c={body.Counter1}, XY={body.Position}.");
            // The three-pixel antenna has its own HP. Killing it must kill the
            // body, wait for the counted explosion and release the real ladder.
            var weak=slots.Single(pair=>pair.Key.Node is AnglerFishCharacter {IsAntenna:true});
            var antenna=(AnglerFishCharacter)weak.Key.Node;
            FailIf(antenna.Health!=30,"Angler weak spot retains independent source HP30.");
            rom.HitWithSword(ItemCollisionType.L1Sword,30,weak.Value);
            ((ILinkSwordStateAwareRoomEntity)weak.Key).SetLinkSwordState(SwordActionState.Swing,1);
            FailIf(!((ISwordHittableRoomEntity)weak.Key).ApplySwordHit(antenna.CollisionBounds,antenna.Position-Vector2.Right,30,
                EnemyKnockbackStrength.Normal,new List<RoomEntitySpawn>()),"Angler antenna must accept native sword damage.");
            StepGameplayUpdates(300,Vector2.Zero,batched:batch,afterUpdate:Compare);
            FailIf(!body.IsDead || _entities.RoomEnemyCount!=0 || !_saveData.HasRoomFlag(5,0x4a,0x80) || !_saveData.HasRoomFlag(5,0x4d,0x80),
                $"Angler death must finish its counted explosion and persist both ladder/portal room flags: dead={body.IsDead}, count={_entities.RoomEnemyCount}, flags=${_saveData.GetRoomFlags(5,0x4a):x2}/${_saveData.GetRoomFlags(5,0x4d):x2}.");
        }
        GD.Print("Validated clean-US Angler Fish entry, antenna allocation, side-view bounce/bubbles, Scent Seed deflation and independent weak-spot death through split/batched gameplay.");
    }
}
