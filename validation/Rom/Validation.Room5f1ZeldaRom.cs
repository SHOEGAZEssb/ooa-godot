using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom5f1ZeldaRom()
    {
        VerifyRoom5f1ZeldaScrollInitialization();
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(5, 0xf1, 0x80);
            LoadValidationRoom(5, 0xf1);
            _player.WarpTo(new(0x78, 0x70)); _player.Face(Vector2I.Up);
            var zelda = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0xad);
            var rom = new SomariaRom(_saveData, _random.CaptureState(), _currentRoom, 0, 0x78, 0x70);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            int address = 0xd040 + 0x100 * _entities.InteractionSlot(zelda);
            rom[address] = 1; rom[address + 1] = 0xad;
            rom[address + 11] = 0x42; rom[address + 13] = 0x78;
            int updates = 0;
            Step(3);
            ApproachLibraryActor(zelda, batched, movement =>
            {
                int angle = movement == Vector2.Up ? 0 : movement == Vector2.Right ? 8 :
                    movement == Vector2.Down ? 16 : movement == Vector2.Left ? 24 : 0xff;
                int held = angle == 0 ? 0x40 : angle == 8 ? 0x10 : angle == 16 ? 0x80 : angle == 24 ? 0x20 : 0;
                Native(held, held, angle);
            }, () => PositionLibraryRomAtPlayer(rom), () => _entities.FindTalkTarget(_player) == zelda);
            Step(1, press: true);
            Step(222);
            FailIf(!_dialogue.IsOpen || rom[0xcba0] == 0 || _dialogue.SourceTextId != 0x3d05 ||
                (rom[0xcba2] | (rom[0xcba3] - 4) << 8) != 0x3d05,
                "Room $5:$f1 Zelda rescue did not reach TX_3d05 with the clean ROM.");
            Step(12);
            _dialogue.Close(); rom[0xcba0] = 0;
            Step(60);
            FailIf(rom[0xcc04] != 0, "Native Zelda triggered $0f before the 60-update post-text wait expired.");
            Step(1);
            FailIf(rom[0xcc04] != 0x0f,
                "Native Zelda must trigger CUTSCENE_ROOM_OF_RITES_COLLAPSE $0f on the next update.");
            FailIf(!_roomEvents.Get<BlackTowerEndingEvent>().RitesCollapseActive,
                "Physically approached Zelda must hand her completed rescue script to the shared $0f ending owner.");
            _roomEvents.Get<BlackTowerEndingEvent>().Cancel();
            _roomEvents.Get<RoomOfRitesZeldaEvent>().Cancel();

            void Step(int count, bool press = false) => StepGameplayUpdates(count, Vector2.Zero,
                press ? ["attack"] : [], press ? ["attack"] : [], batched, () =>
                {
                    Native(press ? 1 : 0, press ? 1 : 0, 0xff);
                    press = false;
                });
            void Native(int pressed, int held, int angle)
            {
                rom.UpdateGameplay(pressed, held, angle, _entities.FrameCounter);
                CompareInteractionOamRom(rom, zelda, address, $"Room $5:$f1 Zelda update {++updates}");
                FailIf(zelda.CurrentAnimationCounter != rom[address + 0x20] ||
                    zelda.CurrentAnimationParameter != rom[address + 0x21] ||
                    zelda.ZIndex != ObjectDrawPriority.FromVisible(rom[address + 0x1a]) ||
                    zelda.ObjectCollisionBounds.Size != new Vector2(rom[address + 0x27] * 2, rom[address + 0x26] * 2) ||
                    _entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects) != rom[0xcc8a] ||
                    _dialogue.IsOpen != (rom[0xcba0] != 0),
                    $"Room $5:$f1 Zelda update {updates}: native animation parameter/counter, priority, radii, object mask or text eligibility differs.");
            }
        }
        GD.Print("Validated room $5:$f1 Zelda OAM, animation counters/parameters, priority, hitbox, A approach, text/mask timing and post-text $0f trigger against executed clean-US object updates with split/batched gameplay.");
    }
    private void VerifyRoom5f1ZeldaScrollInitialization()
    {
        foreach(var fixture in new[]{(Cleared:false,Batched:false),(Cleared:true,Batched:true)})
        {
            ReinitializeGameplayForValidation();_saveData.SetRoomFlag(5,0xf1,0x80,fixture.Cleared);
            for(int repeat=0;repeat<2;repeat++)
            {
                LoadValidationRoom(5,0xf2);_player.WarpTo(new(0x78,24));
                int walked=0;while(!_transitions.IsTransitioning&&walked++<48)StepGameplayUpdates(1,Vector2I.Up);
                FailIf(!_transitions.IsTransitioning||_currentRoom.Id!=0xf1,"Zelda scroll fixture did not enter 5:f1 through its north neighbor.");
                var actor=_entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0xad);
                var rom=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,0x78,0x70);
                rom.InitializeLinkGameplay();
                int at=0xd040+0x100*_entities.InteractionSlot(actor);
                rom[at]=1;rom[at+1]=0xad;rom[at+11]=0x42;rom[at+13]=0x78;
                rom.UpdateGameplay(0,0,0xff,0);
                string initialPose=SomariaPrivate<string>(actor,"_scriptAnimationSource");
                // zelda_state0 falls through interactionAnimate/runScript.
                // The first run yields after setanimation $05. Collision radii
                // and the button wait belong to later eligible object updates.
                void CheckPose()
                {
                    if(_transitions.IsTransitioning)CompareInteractionOamRom(rom,actor,at,"Zelda first visible scroll pose");
                    else FailIf(SomariaPrivate<string>(actor,"_scriptAnimationSource")!=initialPose,
                        "Zelda changed her initial pose after the scroll completed without an A interaction.");
                    FailIf(!actor.Visible||_dialogue.IsOpen||_runtimeState.ReadWramByte(WramAddress.wDisabledObjects)!=0,
                        $"Zelda scroll init: visible={actor.Visible},button={actor.ScriptButtonSensitive},radii={actor.ObjectCollisionBounds.Size},text={_dialogue.IsOpen},mask=${_runtimeState.ReadWramByte(WramAddress.wDisabledObjects):x2}.");
                    if(_transitions.IsTransitioning)
                    {
                        FailIf(actor.CurrentAnimationCounter!=rom[at+0x20],"Zelda's destination animation advanced during scrolling.");
                        FailIf(actor.ScriptButtonSensitive||actor.ObjectCollisionBounds.Size!=new Vector2(rom[at+0x27]*2,rom[at+0x26]*2),
                            "Zelda advanced past the source setanimation yield while scrolling.");
                    }
                }
                CheckPose();int ticks=0;
                while(_transitions.IsTransitioning&&ticks++<180)
                    StepGameplayUpdates(4,Vector2.Zero,batched:fixture.Batched,afterUpdate:CheckPose);
                FailIf(_transitions.IsTransitioning,"Zelda scroll did not complete.");
                StepGameplayUpdates(2,Vector2.Zero,afterUpdate:CheckPose);
                FailIf(!actor.ScriptButtonSensitive||actor.ObjectCollisionBounds.Size!=new Vector2(8,16),
                    "Zelda must resume her collision setup and A-button wait after scrolling.");
            }
        }
    }
}
