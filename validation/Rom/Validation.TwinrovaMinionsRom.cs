using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTwinrovaMinionsRom()
    {
        var animationCounter=typeof(EnemyAnimationPlayer).GetField("_frameCounter",BindingFlags.NonPublic|BindingFlags.Instance)!;
        foreach(bool batched in new[] {false,true})
        foreach(int id in new[] {0x5d,0x5e})
        foreach(int item in new[] {-1,5,3,7,0x15})
        {
            if(id==0x5d&&item==0x15)continue;
            ReinitializeGameplayForValidation();LoadValidationRoom(5,0xf5);
            var placed=_entities.EntityAdapters<TwinrovaRoomEntity>().Single();var world=placed.Actor.World;_entities.RetireEntity(placed);
            var boss=new MergedTwinrovaRoomEntity(new(world,new(0x78,0x48),0));
            _entities.RegisterEnemySlot(boss,0);_entities.AddEntity(boss);
            _player.WarpTo(new(0x78,0x78));_player.Face(Vector2I.Up);_transitions.ResetCamera();_inventory.EquipA(0);_inventory.EquipB(0);
            StepGameplayUpdates(1,Vector2.Zero);_dialogue.Close();
            typeof(MergedTwinrovaCharacter).GetProperty("Counter",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(boss.Actor,255);
            var child=new TwinrovaMinionRoomEntity(new(world,id,0,new(0x98,0x68),-256,8));
            _entities.TryAllocateEnemy(_=>child);
            var seed=_random.CaptureState();
            var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0x78,0x78){HostileEnemiesEnabled=true,HostilePartsEnabled=true,PostObjectCollisionsEnabled=false};
            rom.PrepareRoomBackground();rom.InitializeLinkGameplay();rom.InitializeLinkWalkingAnimation();rom[0xcd17]=0xd0;
            rom[0xd080]=1;rom[0xd081]=1;rom[0xd084]=8;rom[0xd086]=255;rom[0xd08b]=0x48;rom[0xd08d]=0x78;rom[0xd0a9]=20;rom[0xd0b0]=210;rom[0xd0b4]=5;
            rom[0xd180]=1;rom[0xd181]=(byte)id;rom[0xd18b]=0x68;rom[0xd18d]=0x98;rom[0xd18f]=255;rom[0xd189]=8;rom[0xd196]=0x80;rom[0xd197]=0xd0;
            rom[0xcc08]=0xdb;rom[0xcc0a]=0xdc;rom[0xcc0c]=0xdd;rom[0xcc0e]=0xde;rom[0xcc10]=0xdf;rom[0xcc12]=0x9d;
            Vector2 camera=-_transitions.WorldToGameplayScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            var sounds=_sound.AttachPlayRequestAudit();int update=0;
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:Compare);
            Step(20);
            if(item>=0)
            {
                int damage=item==7?6:2;
                rom.HitEnemyWithItem(1,item,damage,origin:child.Actor.Position);child.SetLinkSwordState(SwordActionState.Swing,2,item);
                FailIf(!child.ApplySwordHit(child.Actor.CollisionBounds,child.Actor.Position,damage,EnemyKnockbackStrength.None,new List<RoomEntitySpawn>()),"Twinrova minion fixture collision was rejected.");
                _runtimeState.SetWramByte(WramAddress.wDisabledObjects,2);rom[0xcc8a]=2;Step(3);
                _runtimeState.SetWramByte(WramAddress.wDisabledObjects,0);rom[0xcc8a]=0;
            }
            Step(190);
            FailIf(id==0x5e&&item==7&&!child.Actor.Killed,
                "Twinrova bat lethal hit must execute enemyDie_uncounted after its pending-hit visit.");
            if(id==0x5d)
            {
                boss.Actor.Health=0;rom[0xd0a9]=0;
                // Retain the parent's pending-hit branch for this one visit,
                // so the test observes the child's native health-zero gate.
                boss.Actor.PublishHit(0);rom[0xd0aa]=0x80;
                Step(1);FailIf(!child.Finished,"Twinrova ice did not retire with its dead parent.");
            }
            _sound.SetRequestObserver(null);

            void Compare()
            {
                rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter);rom.AdvanceCameraPosition();rom.AdvanceScreenShake();update++;
                var a=child.Actor;const int at=0xd180;
                FailIf(child.Finished!=(rom[at]==0),$"Twinrova minion${id:x2},update{update}: lifetime differs.");
                if(!child.Finished)
                    FailIf(a.State!=rom[at+4]||a.Counter!=rom[at+6]||a.Counter2!=rom[at+7]||a.Angle!=rom[at+9]||a.Speed!=rom[at+16]||
                        a.Position!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||a.Health!=rom[at+0x29]||
                        a.InvincibilityCounter!=unchecked((sbyte)rom[at+0x2b])||a.KnockbackCounter!=rom[at+0x2d]||a.Palette!=(rom[at+0x1b]&7)||
                        (int)animationCounter.GetValue(a.Animation)! !=rom[at+0x20],
                        $"Twinrova minion${id:x2},update{update}: state={a.State}/{rom[at+4]},counter={a.Counter}/{rom[at+6]},counter2={a.Counter2}/{rom[at+7]},angle={a.Angle}/{rom[at+9]},pos={a.Position}/{rom.Word(at+12)/256f},{rom.Word(at+10)/256f},palette={a.Palette}/{rom[at+0x1b]},inv={a.InvincibilityCounter}/{unchecked((sbyte)rom[at+0x2b])},anim={animationCounter.GetValue(a.Animation)}/{rom[at+0x20]}.");
                FailIf(RandomDiffers(seed,rom[0xff94],rom[0xff95],rom.RandomCalls)||!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Twinrova minion${id:x2},update{update}: RNG/audio differs {string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
                _player.ApplyInteractionInvincibility(128);rom[0xd02b]=128;
            }
        }
        GD.Print("Validated Twinrova bat/ice initialization, charge counters, movement, palettes, bounces, frozen collisions and parent cleanup against the clean ROM.");
    }
}
