using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoomOfRitesBouldersRom()
    {
        foreach(bool batch in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(5,0xf5);
            foreach(var twin in _entities.EntityAdapters<TwinrovaRoomEntity>().ToArray())_entities.RetireEntity(twin);
            _player.WarpTo(new(0x78,0x58));_player.Face(Vector2I.Up);_transitions.ResetCamera();_inventory.EquipA(0);_inventory.EquipB(0);
            var seed=_random.CaptureState();
            var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0x78,0x58){HostilePartsEnabled=true,PostObjectCollisionsEnabled=false};
            rom.InitializeLinkGameplay();rom.InitializeLinkWalkingAnimation();rom[0xcd17]=0xd0;
            Vector2 camera=-_transitions.WorldToGameplayScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            var parent=new RoomOfRitesBoulderRoomEntity(_entities,new());_entities.AddEntity(parent);
            rom[0xd0c0]=1;rom[0xd0c1]=0x54;
            var sounds=_sound.AttachPlayRequestAudit();int update=0;bool sawChild=false;
            StepGameplayUpdates(6,Vector2.Zero,batched:batch,afterUpdate:Compare);
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects,4);rom[0xcc8a]=4;
            StepGameplayUpdates(3,Vector2.Zero,batched:batch,afterUpdate:Compare);
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects,0);rom[0xcc8a]=0;
            StepGameplayUpdates(192,Vector2.Zero,batched:batch,afterUpdate:Compare);
            _sound.SetRequestObserver(null);
            FailIf(!sawChild||!parent.Finished||_entities.EntityAdapters<RoomOfRitesBoulderRoomEntity>().Any(),
                "PART$54 must spawn independently falling children and retire its 150-update parent and final falls.");
            void Compare()
            {
                rom.UpdateGameplay(0,0,255,_entities.FrameCounter);rom.AdvanceCameraPosition();rom.AdvanceScreenShake();update++;
                for(int slot=0;slot<16;slot++)
                {
                    int at=0xd0c0+slot*256;var rock=_entities.ResolvePartSlot(slot) as RoomOfRitesBoulderRoomEntity;
                    FailIf((rock is not null)!=(rom[at]!=0&&rom[at+1]==0x54),$"PART$54 update{update}: native slot${slot:x2} admission/lifetime differs.");
                    if(rock is null)continue;
                    sawChild|=rock.Subid==1;
                    FailIf(rock.Subid!=rom[at+2]||rock.State!=rom[at+4]||rock.Counter!=rom[at+6]||
                        rock.Position!=new Vector2(rom.Word(at+12)/256f,rom.Word(at+10)/256f)||
                        rock.ZFixed!=unchecked((short)rom.Word(at+14))||rock.SpeedZ!=unchecked((short)rom.Word(at+20))||
                        rock.Visible!=((rom[at+26]&128)!=0)||rock.AnimationCounter!=rom[at+32]||rock.AnimationParameter!=rom[at+33],
                        $"PART$54 update{update},slot${slot:x2}: state={rock.Subid}:{rock.State}:{rock.Counter}/{rom[at+2]}:{rom[at+4]}:{rom[at+6]},pos={rock.Position}/{rom.Word(at+12)/256f},{rom.Word(at+10)/256f},Z={rock.ZFixed}/{unchecked((short)rom.Word(at+14))},anim={rock.AnimationCounter}/{rom[at+32]}.");
                }
                FailIf(RandomDiffers(seed,rom[0xff94],rom[0xff95],rom.RandomCalls)||!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"PART$54 update{update}: RNG/debris/audio handoff differs {string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
            }
        }
        GD.Print("Validated PART$54 spawner/child slots, 150-update lifetime, camera-relative RNG, fixed falling motion, animation, mask freeze and shared debris against the clean ROM.");
    }
}
