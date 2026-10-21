using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateVeranFaceRom()
    {
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(5,0xd7);
            var face=_entities.EntityAdapters<VeranFaceRoomEntity>().Single();
            FailIf(face.Node.Position!=new Vector2(0x78,0x48),"Room $5:d7 must place INTERAC $2d:$00 from its main object stream.");
            _inventory.EquipA(0);_inventory.EquipB(0);_player.WarpTo(new(0x78,0x78));_player.Face(Vector2I.Down);_transitions.ResetCamera();
            var seed=_random.CaptureState();var rom=new SomariaRom(_saveData,seed,_currentRoom,0,0x78,0x78){CompanionDispatchEnabled=true};
            rom.InitializeLinkGameplay();rom[0xd008]=2;rom[0xd040]=1;rom[0xd041]=0x2d;rom[0xd04b]=0x48;rom[0xd04d]=0x78;
            rom[0xcc08]=0x62; // Native graphics-header residency, independent of the texture cache.
            rom[0xcd16]=0;rom[0xcd17]=0xd0;Vector2 camera=-_entities.ToScreen(Vector2.Zero);rom[0xffaa]=(byte)camera.Y;rom[0xffac]=(byte)camera.X;
            int update=0,generation=0,hold=0,texts=0;var sounds=_sound.AttachPlayRequestAudit();bool sawVisible=false;
            void Compare()
            {
                rom.AdvanceWarpPalette();rom.UpdateGameplay(0,0,0xff,++update);rom.AdvanceCameraPosition();
                if(face.State==2&&rom[0xcc4b]!=0)
                {
                    FailIf(rom[0xcc4b]!=3,"Veran face must request native CUTSCENE_03 before the post-object warp phase consumes it.");
                    rom.ApplyRequestedWarp();
                }
                FailIf(face.State!=rom[0xd044]||face.Actor.CurrentAnimationCounter!=rom[0xd060]||
                    face.Node.Visible!=((rom[0xd05a]&128)!=0)||_player.PrecisePosition!=new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f)||
                    CarriedObjectMotion.DirectionIndex(_player.FacingVector)!=rom[0xd008]||
                    _runtimeState.ReadWramByte(WramAddress.wDisabledObjects)!=rom[WramAddress.wDisabledObjects]||
                    _runtimeState.ReadWramByte(WramAddress.wMenuDisabled)!=rom[WramAddress.wMenuDisabled]||!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Veran face $2d update {update}, batch={batched}: port state/anim/visible/dir={face.State}/{face.Actor.CurrentAnimationCounter}/{face.Node.Visible}/{_player.FacingVector}, ROM={rom[0xd044]}/{rom[0xd060]}/{rom[0xd05a]:x2}/{rom[0xd008]}, masks={_runtimeState.ReadWramByte(WramAddress.wDisabledObjects):x2}/{rom[WramAddress.wDisabledObjects]:x2}, SFX={string.Join(',',sounds.Requests)}/{string.Join(',',rom.Sounds)}.");
                sawVisible|=face.Node.Visible;
                if(generation!=rom.TextGeneration)
                {
                    FailIf(rom.Word(0xcba2)!=0x5a13||!_dialogue.IsOpen||texts!=0||
                        !_dialogue.CurrentMessage.Replace('\n',' ').Contains("to taste despair",System.StringComparison.Ordinal),
                        "The face must show the source TX_5613 threat with its full imported formatting.");
                    texts++;generation=rom.TextGeneration;hold=5;
                }
                if(hold>0&&--hold==0){_dialogue.Close();rom[0xcba0]=0;}
                FailIf(RandomDiffers(seed, rom[0xff94], rom[0xff95], rom.RandomCalls),
                    "The face scene must preserve the native RNG stream.");
            }
            for(int bound=0;bound<180&&face.State!=2;bound+=2)StepGameplayUpdates(2,Vector2.Zero,batched:batched,afterUpdate:Compare);
            FailIf(face.State!=2||texts!=1||!sawVisible||!_transitions.IsTransitioning||rom[0xc2ef]!=3||
                rom[0xcc47]!=0x84||rom[0xcc48]!=0xd4||rom[0xcc49]!=0x0c||rom[0xcc4a]!=0x67||rom[0xcc4b]!=0||
                _runtimeState.ReadWramByte(0xcc50)!=0,
                $"The completed threat must install $84,$d4,$0c,$67,$03, clear wcc50 and start the native fade after its object pass: state={face.State}, texts={texts}, visible={sawVisible}, transition={_transitions.IsTransitioning}, cutscene={rom[0xc2ef]:x2}, warp={rom[0xcc47]:x2},{rom[0xcc48]:x2},{rom[0xcc49]:x2},{rom[0xcc4a]:x2},{rom[0xcc4b]:x2}, cc50={_runtimeState.ReadWramByte(0xcc50):x2}.");
            // The actual transition must deliver the existing imported boss,
            // with no feature-specific room loader or duplicate enemy spawn.
            for(int wait=0;wait<180&&(_rooms.CurrentRoom.Id!=0xd4||_transitions.IsTransitioning);wait++)StepGameplayUpdates(1,Vector2.Zero);
            FailIf(_rooms.ActiveGroup!=4||_rooms.CurrentRoom.Id!=0xd4||_entities.EntityAdapters<VeranFinalRoomEntity>().Count()!=1,
                "Veran's face must hand off to the final arena's existing enemy placement.");
            LoadValidationRoom(5,0xd7);StepGameplayUpdates(4,Vector2.Zero,batched:batched);
            FailIf(_entities.EntityAdapters<VeranFaceRoomEntity>().Count()!=1,"Re-entry must allocate one fresh face interaction.");
            LoadValidationRoom(4,0xd5);StepGameplayUpdates(2,Vector2.Zero);
            FailIf(_entities.EntityAdapters<VeranFaceRoomEntity>().Any()||_transitions.IsTransitioning||_entities.PlayerMenusDisabled,
                $"Room replacement must retire the face and its pending warp: faces={_entities.EntityAdapters<VeranFaceRoomEntity>().Count()}, transition={_transitions.IsTransitioning}, menus={_entities.PlayerMenusDisabled}, mask={_runtimeState.ReadWramByte(WramAddress.wDisabledObjects):x2}/{_runtimeState.ReadWramByte(WramAddress.wMenuDisabled):x2}, room={_rooms.ActiveGroup:x}:{_rooms.CurrentRoom.Id:x2}.");
        }
    }
}
