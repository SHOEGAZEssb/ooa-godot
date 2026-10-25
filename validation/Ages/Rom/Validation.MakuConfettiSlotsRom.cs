using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateMakuConfettiSlotsRom()
    {
        foreach(bool batch in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(5,0xf5);
            foreach(var twin in _entities.EntityAdapters<TwinrovaRoomEntity>().ToArray())_entities.RetireEntity(twin);
            _player.WarpTo(new(0x78,0x58));_player.Face(Vector2I.Up);_transitions.ResetCamera();_inventory.EquipA(0);_inventory.EquipB(0);
            var seed=_random.CaptureState();
            var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0x78,0x58){PostObjectCollisionsEnabled=false};
            rom.EnableVramDma();rom.CreateMenuView().LoadHudGraphics();
            rom.InitializeLinkGameplay();rom.InitializeLinkWalkingAnimation();rom[0xcd17]=0xd0;
            Vector2 camera=-_transitions.WorldToGameplayScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            var parent=new MakuConfettiRoomEntity(_entities,new());_entities.AddEntity(parent);
            FailIf(_entities.NativeInteractionSlot(parent)!=2,"Native getFreeInteractionSlot must retain reserved slots $d0/$d1.");
            rom[0xd240]=1;rom[0xd241]=0x62;
            var sounds=_sound.AttachPlayRequestAudit();int update=0;bool sawFlower=false,sawSparkle=false;
            var checkedDirections=new System.Collections.Generic.HashSet<int>();
            StepGameplayUpdates(30,Vector2.Zero,batched:batch,afterUpdate:Compare);
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects,2);rom[0xcc8a]=2;
            StepGameplayUpdates(3,Vector2.Zero,batched:batch,afterUpdate:Compare);
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects,0);rom[0xcc8a]=0;
            StepGameplayUpdates(420,Vector2.Zero,batched:batch,afterUpdate:Compare);
            _sound.SetRequestObserver(null);
            FailIf(!sawFlower||!sawSparkle||!parent.Finished||parent.SpawnCount!=5||checkedDirections.Count!=2,
                "INTERAC$62 must admit five native flowers and their $84:$02 sparkle children.");
            void Compare()
            {
                rom.UpdateGameplay(0,0,255,_entities.FrameCounter);rom.AdvanceCameraPosition();rom.AdvanceScreenShake();update++;
                foreach(var flower in _entities.EntityAdapters<MakuConfettiRoomEntity>())
                {
                    int slot=_entities.InteractionSlot(flower.Node),at=0xd040+slot*256;var m=flower.Motion;
                    sawFlower|=flower.Var03!=0;
                    if(flower.Var03!=0&&checkedDirections.Add(flower.Direction))
                        CompareInteractionSpritePixelsRom(rom,flower.CurrentTexture,flower.CurrentDrawOffset,at,2,
                            $"Maku flower direction{flower.Direction}");
                    FailIf(rom[at]==0||rom[at+1]!=0x62||flower.Var03!=rom[at+3]||flower.State!=rom[at+4]||
                        flower.SpawnCount!=rom[at+6]||flower.SoundCounter!=rom[at+7]||flower.Delay!=rom[at+0x37]||
                        flower.SparkleCounter!=rom[at+0x3a]||flower.Direction!=rom[at+8]||
                        flower.Var03!=0&&((m.Y&65535)!=rom.Word(at+10)||(m.X&65535)!=rom.Word(at+12)||
                            m.SpeedY!=unchecked((short)rom.Word(at+16))||m.SpeedX!=unchecked((short)rom.Word(at+18))||
                            m.AccelerationY!=unchecked((short)rom.Word(at+0x3c))||m.AccelerationX!=unchecked((short)rom.Word(at+0x3e))),
                        $"INTERAC$62 update{update},slot${slot:x2}: var03/state={flower.Var03}:{flower.State}/{rom[at+3]}:{rom[at+4]},delay={flower.Delay}/{rom[at+0x37]},Y={m.Y&65535}/{rom.Word(at+10)},X={m.X&65535}/{rom.Word(at+12)}.");
                }
                foreach(var sparkle in _entities.Entities<InteractionSparkleEffect>())
                {
                    sawSparkle=true;int at=0xd040+_entities.InteractionSlot(sparkle)*256;
                    FailIf(rom[at]==0||rom[at+1]!=0x84||rom[at+2]!=2||sparkle.Subid!=2||
                        sparkle.Position!=new Vector2(rom[at+13],rom[at+11])||sparkle.AnimationParameter!=rom[at+0x21],
                        $"Confetti sparkle update{update}: native slot, subid, position or terminal parameter differs.");
                }
                int nativeFlowers=Enumerable.Range(0,16).Count(s=>rom[0xd040+s*256]!=0&&rom[0xd041+s*256]==0x62);
                FailIf(nativeFlowers!=_entities.EntityAdapters<MakuConfettiRoomEntity>().Count()||
                    RandomDiffers(seed,rom[0xff94],rom[0xff95],rom.RandomCalls)||!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Confetti update{update}: count={_entities.EntityAdapters<MakuConfettiRoomEntity>().Count()}/{nativeFlowers}, RNG/audio={string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
            }
        }
        GD.Print("Validated native present confetti slots, ordered flower/sparkle allocation, component arithmetic, direction changes, sound counters and mask freeze against the clean ROM.");
    }
}
