using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateTwinrovaAttackPartsRom()
    {
        var animationCounter=typeof(EnemyAnimationPlayer).GetField("_frameCounter",BindingFlags.NonPublic|BindingFlags.Instance)!;
        foreach(bool batched in new[] {false,true})
        foreach(int id in new[] {0x4c,0x4e})
        foreach(int item in new[] {-1,4,5,3})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(5,0xf5);
            var placed=_entities.EntityAdapters<TwinrovaRoomEntity>().Single();var world=placed.Actor.World;_entities.RetireEntity(placed);
            _roomEvents.Get<RoomOfRitesBattleEvent>().ReloadArena(id==0x4c?1:2);
            _player.WarpTo(new(0x78,0x78));_player.Face(Vector2I.Up);_transitions.ResetCamera();_inventory.EquipA(0);_inventory.EquipB(0);
            FailIf(_currentRoom.GetTerrainInfo(_player.Position).Hazard!=HazardType.None||_collision.Collides(_player.Position),
                "Twinrova attack fixture must place Link on the original arena's safe floor.");
            var part=new TwinrovaAttackPart(world,id,0,new(0x78,0x58),0,16);_entities.AddEntity(part);
            var seed=_random.CaptureState();
            var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0x78,0x78){HostilePartsEnabled=true,PostObjectCollisionsEnabled=false};
            rom.PrepareRoomBackground();rom.InitializeLinkGameplay();rom.InitializeLinkWalkingAnimation();rom[0xcd17]=0xd0;
            rom[0xd0c0]=1;rom[0xd0c1]=(byte)id;rom[0xd0cb]=0x58;rom[0xd0cd]=0x78;rom[0xd0c9]=16;
            rom[0xcc08]=0x9d;rom[0xcc0a]=0xde;
            Vector2 camera=-_transitions.WorldToGameplayScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            var sounds=_sound.AttachPlayRequestAudit();int update=0;bool sawChildren=false,sawSnow=false;
            var seen=new Dictionary<int,TwinrovaAttackPart>();
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:Compare);
            Step(22);
            if(item>=0)
            {
                rom.HitPartWithItem(0,item);part.SetLinkSwordState(SwordActionState.Swing,2,item);
                bool accepted=part.ApplySwordHit(part.CollisionBounds,_player.Position,2,EnemyKnockbackStrength.None,new List<RoomEntitySpawn>());
                FailIf(accepted!=(item==3||id==0x4e&&item==5),"Twinrova flame/snowball native sword/shield masks differ.");
                _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects,8);rom[0xcc8a]=8;Step(3);
                FailIf(part.Finished,"Frozen Twinrova attack discarded its pending collision before the next PART visit.");
                _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects,0);rom[0xcc8a]=0;
            }
            Step(160);
            _sound.SetRequestObserver(null);
            FailIf(!part.Finished||item<0&&id==0x4c&&!sawChildren||id==0x4e&&!sawSnow,
                "Twinrova attack fixture missed source splitting, snow debris or terminal retirement.");

            void Compare()
            {
                rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter);rom.AdvanceCameraPosition();rom.AdvanceScreenShake();update++;
                FailIf(_player.Position!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f),
                    $"Twinrova attack update{update}: fixture Link differs {_player.Position}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f},nativeState={rom[0xd004]:x2}:{rom[0xd005]:x2},SVBK={rom[0xff70]},tile={rom[0xcc9a]:x2},terrain={_currentRoom.GetTerrainInfo(_player.Position)},activeColl={_currentRoom.ActiveCollisions}/{rom[0xcc33]},flags={_currentRoom.TilesetFlags}/{rom[0xcc34]}.");
                for(int slot=0;slot<16;slot++)
                {
                    int at=0xd0c0+slot*256;var actor=_entities.ResolvePartSlot(slot) as TwinrovaAttackPart;
                    FailIf((actor is not null)!=(rom[at]!=0&&rom[at+1] is 0x4c or 0x4e),$"Twinrova attack update{update}: PART slot${slot:x2} occupancy differs; native={rom[at]:x2}:{rom[at+1]:x2},pos={rom.Word(at+12)/256f},{rom.Word(at+10)/256f},angle={rom[at+9]},managed={actor is not null},last={seen.GetValueOrDefault(slot)?.Position}.");
                    if(actor is null)continue;
                    seen[slot]=actor;
                    sawChildren|=actor.Subid==1;
                    FailIf(actor.Id!=rom[at+1]||actor.Subid!=rom[at+2]||actor.State!=rom[at+4]||actor.Counter!=rom[at+6]||
                        actor.Angle!=rom[at+9]||actor.Speed!=rom[at+16]||actor.InvincibilityCounter!=unchecked((sbyte)rom[at+0x2b])||
                        actor.Position!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||actor.ZFixed!=unchecked((short)rom.Word(at+14))||
                        actor.Visible!=((rom[at+0x1a]&128)!=0)||actor.CollisionEnabled!=((rom[at+0x24]&128)!=0)||
                        (int)animationCounter.GetValue(actor.Animation)! !=rom[at+0x20]||actor.AnimationParameter!=rom[at+0x21],
                        $"Twinrova attack update{update},slot${slot:x2}: state={actor.State}/{rom[at+4]},count={actor.Counter}/{rom[at+6]},angle={actor.Angle}/{rom[at+9]},speed={actor.Speed}/{rom[at+16]},collision={actor.CollisionEnabled}/{rom[at+0x24]:x2},pos={actor.Position}/{rom.Word(at+12)/256f},{rom.Word(at+10)/256f},anim={animationCounter.GetValue(actor.Animation)}/{rom[at+0x20]},Link={_player.Position}/{rom[0xd00d]},{rom[0xd00b]}.");
                }
                foreach(var snow in _entities.EntityAdapters<SnowDebrisRoomEntity>())
                {
                    sawSnow=true;int at=0xd040+_entities.NativeInteractionSlot(snow)*256;
                    FailIf(rom[at+1]!=9||snow.Position!=new Vector2(rom[at+13],rom[at+11])||snow.Visible!=((rom[at+0x1a]&128)!=0),
                        $"Twinrova snow debris update{update}: source identity, position or visibility differs.");
                }
                FailIf(RandomDiffers(seed,rom[0xff94],rom[0xff95],rom.RandomCalls)||!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Twinrova attack update{update},id${id:x2},item{item}: RNG/audio differs; sounds={string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
                _player.ApplyInteractionInvincibility(128);rom[0xd02b]=128;
            }
        }
        GD.Print("Validated Twinrova flames/snowballs, charge/flight, same-pass child splitting, source hit masks, frozen pending collisions and snow debris against the clean ROM.");
    }
}
