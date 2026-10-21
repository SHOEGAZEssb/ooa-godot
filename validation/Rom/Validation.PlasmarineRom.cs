using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePlasmarineRom()
    {
        foreach(bool batch in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(5,0x62);
            _inventory.EquipA(0);_inventory.EquipB(0);_player.WarpTo(new(120,152));
            FailIf(_collision.Collides(_player.Position),"Plasmarine fixture requires original floor in room$5:$62.");
            // Keep Link protected while this bounded encounter fixture compares
            // native enemy/part dispatch. Item exchanges have a separate test.
            typeof(Player).GetField("_enemyInvincibilityFrames",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(_player,100000f);
            var slots=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_enemySlots");
            var parts=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_partSlots");
            var boss=_entities.Entities<PlasmarineCharacter>().Single();
            FailIf(boss.Position!=new Vector2(80,0) || boss.Health!=7 || _entities.RoomEnemyCount!=1,
                "Plasmarine$7e must enter source room$5:$62 at Y$00/X$50 with seven HP.");
            var seed=_random.CaptureState();var rom=new EnemyStatusRom(_currentRoom,_saveData,seed.Calls);
            byte[] headers=[0xcb,0xcc,0xcd,0x8e,0xa7,0x78];
            for(int index=0;index<headers.Length;index++)rom[0xcc08+index*2]=headers[index];
            rom[0xd080]=1;rom[0xd081]=0x7e;rom[0xd08d]=80;
            rom[0xff94]=seed.Rng1;rom[0xff95]=seed.Rng2;
            int update=0,projectiles=0;var states=new HashSet<int>();
            int bossAddress=0xd080+slots.Single(pair=>pair.Key.Node==boss).Value*256;
            void Compare()
            {
                rom.Update(_entities.FrameCounter,_player.Position);update++;
                int s=bossAddress;
                states.Add(boss.State);
                FailIf(!boss.IsDead && (boss.State!=rom[s+4] || boss.Counter1!=rom[s+6] || boss.Counter2!=rom[s+7] || boss.Angle!=rom[s+9] ||
                    boss.Speed!=rom[s+16] || boss.Health!=rom[s+0x29] || boss.ColorIndex!=rom[s+0x32] || boss.CurrentOamFlags!=rom[s+0x1c] || boss.Palette!=rom[s+0x1b] ||
                    boss.ProjectileCount!=rom[s+0x34] || boss.CollisionMode!=rom[s+0x25] || boss.AnimationParameter!=rom[s+0x21] ||
                    (boss.InvincibilityCounter&255)!=rom[s+0x2b] || boss.Position!=new Vector2(rom.Word(s+12)/256f,rom.Word(s+10)/256f)),
                    $"Plasmarine update{update}, batch={batch}: runtime state={boss.State}, c={boss.Counter1}/{boss.Counter2}, angle/speed={boss.Angle}/${boss.Speed:x2}, HP/color/pal={boss.Health}/{boss.ColorIndex}/{boss.Palette}, projectile/mode/anim={boss.ProjectileCount}/${boss.CollisionMode:x2}/{boss.AnimationParameter}, XY={boss.Position}; ROM={rom[s+4]}, c={rom[s+6]}/{rom[s+7]}, angle/speed={rom[s+9]}/${rom[s+16]:x2}, HP/color/pal={rom[s+0x29]}/{rom[s+0x32]}/{rom[s+0x1c]}, projectile/mode/anim={rom[s+0x34]}/${rom[s+0x25]:x2}/{rom[s+0x21]}, XY={rom.Word(s+12)/256f},{rom.Word(s+10)/256f}.");
                foreach(var (entity,slot) in parts.Where(pair=>pair.Key.Node is PlasmarineProjectile))
                {
                    var shot=(PlasmarineProjectile)entity.Node;int p=0xd0c0+slot*256;if(shot.State==1 && shot.Counter1==150)projectiles++;
                    FailIf(rom[p]==0 || shot.SubId!=rom[p+2] || shot.State!=rom[p+4] || shot.Counter1!=rom[p+6] || shot.Counter2!=rom[p+7] ||
                        shot.Speed!=rom[p+16] || shot.Angle!=rom[p+9] || shot.Visible!=((rom[p+0x1a]&128)!=0) ||
                        shot.Position!=new Vector2(rom.Word(p+12)/256f,rom.Word(p+10)/256f),
                        $"Plasmarine PART$43 slot${slot:x2} update{update}: runtime state/counters/angle/speed/XY={shot.State}/{shot.Counter1},{shot.Counter2}/{shot.Angle}/${shot.Speed:x2}/{shot.Position}; ROM={rom[p+4]}/{rom[p+6]},{rom[p+7]}/{rom[p+9]}/${rom[p+16]:x2}/{rom.Word(p+12)/256f},{rom.Word(p+10)/256f}.");
                }
                FailIf(RandomDiffers(seed, rom[0xff94], rom[0xff95], rom.RandomCalls),
                    $"Plasmarine update{update}: global RNG differs {_random.CaptureState().Calls-seed.Calls}/{rom.RandomCalls}.");
            }
            StepGameplayUpdates(600,Vector2.Zero,batched:batch,afterUpdate:Compare);
            FailIf(!new[]{8,9,10,11,12,13,14,15}.All(states.Contains) || projectiles==0,
                $"Plasmarine trace must cover centre arrival, charge, shock and projectile cycle: states={string.Join(',',states)}, projectiles={projectiles}.");
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batch,afterUpdate:Compare);
            void ReturnShot()
            {
                int wait=0;
                while(!parts.Keys.Any(entity=>entity.Node is PlasmarineProjectile {State:1,Counter1:150}) && wait++<500)Step(1);
                FailIf(wait>=500 || boss.InvincibilityCounter!=0 || boss.CollisionMode!=0x4f,"Plasmarine must emit a reachable ordinary-color projectile.");
                var target=slots.Single(pair=>pair.Key.Node==boss);
                int before=boss.Health,color=boss.ColorIndex;
                rom.HitWithSword(ItemCollisionType.L1Sword,99,target.Value);
                ((ILinkSwordStateAwareRoomEntity)target.Key).SetLinkSwordState(SwordActionState.Swing,1);
                FailIf(!((ISwordHittableRoomEntity)target.Key).ApplySwordHit(boss.CollisionBounds,boss.Position-Vector2.Right,99,
                    EnemyKnockbackStrength.Normal,new List<RoomEntitySpawn>()),"Plasmarine must accept the sword color-switch collision.");
                Step(1);
                FailIf(boss.Health!=before || boss.ColorIndex!=(color^1),"Sword contact must change color without removing Plasmarine HP.");
                Step(40);
                FailIf(boss.Health!=before-1,"The opposite-color homing projectile must remove exactly one HP.");
            }
            ReturnShot();
            // A declared one-HP final round also exercises the source's faster
            // projectile branch without replaying six identical damage cycles.
            boss.Health=1;rom[bossAddress+0x29]=1;
            ReturnShot();Step(220);
            FailIf(!boss.IsDead || _entities.RoomEnemyCount!=0 || !_saveData.HasRoomFlag(5,0x62,0x80),
                "Plasmarine's last returned projectile must release the counted death effect and shared boss reward.");
        }
        GD.Print("Validated clean-US Plasmarine centre arrival, charge deceleration, shock colors, projectile firing/homing and shared RNG in split/batched gameplay.");
    }
}
