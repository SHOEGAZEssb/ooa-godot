using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom5f1Zelda()
    {
        // mainData.s placement and scripts.s:zeldaSubid00Script expectations
        // are independent of the generated command stream used by production.
        foreach (bool batched in new[] { false, true })
        foreach (bool cleared in new[] { true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(5, 0xf1, 0x80, cleared);
            LoadValidationRoom(5, 0xf1);
            int musicRequests = _sound.PlayRequestsFor(0x38);
            Step(3);
            NpcCharacter zelda = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0xad);
            FailIf(zelda.Record is not { SubId: 0, X: 0x78, Y: 0x42,
                    Implementation: NpcImplementationClassification.EventOwned } ||
                !zelda.Visible || zelda.Record.CanFace || zelda.ObjectCollisionBounds.Size != new Vector2(8, 16) ||
                _entities.BlocksLink(zelda.Position) || zelda.TimeWarpSolidMarked ||
                zelda.CurrentAnimationOpaquePixels == 0 || !zelda.ScriptButtonSensitive,
                "Room $5:$f1 Zelda $ad:$00 lost native placement, animation, non-solid state or $08/$04 A-button radii.");
            FailIf(_entities.RuntimeState.ReadWramByte(WramAddress.wDisableScreenTransitions) != (cleared ? 1 : 0) ||
                cleared && (_sound.NativeActiveMusic != 0 || _sound.PlayRequestsFor(0x38) != musicRequests + 1),
                "Room $5:$f1 Zelda room flag $80 did not gate transition/music initialization.");

            ApproachLibraryActor(zelda, batched, counterpartReachable: () => _entities.FindTalkTarget(_player) == zelda);
            Vector2 position = _player.Position;
            Press();
            FailIf(Mask() != 0x11 || _dialogue.IsOpen || _player.CutsceneControlled,
                "Room $5:$f1 A selection must write only disabled-object mask $11 and retain Link's normal handler.");
            Step(221);
            FailIf(_dialogue.IsOpen || _player.Position != position || Mask() != 0x11,
                "Room $5:$f1 Zelda advanced before setanimation/wait 220 completed or allowed Link movement.");
            Step();
            FailIf(!_dialogue.IsOpen || _dialogue.SourceTextId != 0x3d05 ||
                !PlainWords(_dialogue.CurrentMessage).Contains("mindless, raging Ganon", StringComparison.Ordinal) ||
                !PlainWords(_dialogue.CurrentMessage).Contains("Let us return to the others!", StringComparison.Ordinal),
                "Room $5:$f1 Zelda did not open complete TX_3d05 on source update 222 after A selection.");
            int frame = zelda.CurrentAnimationFrame;
            Step(70);
            FailIf(zelda.CurrentAnimationFrame != frame || Mask() != 0x11,
                "Room $5:$f1 Zelda animation/script wait ran during text.");
            _dialogue.Close();
            Step(60); // first update preloads 60; only 59 decrements follow.
            Step();
            var ending=_roomEvents.Get<BlackTowerEndingEvent>();
            FailIf(!ending.RitesCollapseActive||ending.State!=0,"Room $5:$f1 must hand off to $0f after exactly 60 post-text decrements.");
            ending.Cancel();ending.Cancel();
            var owner = _roomEvents.Get<RoomOfRitesZeldaEvent>();
            owner.Cancel(); owner.Cancel();
            FailIf(Mask() != 0 || _entities.RuntimeState.ReadWramByte(WramAddress.wDisableScreenTransitions) != 0 ||
                owner.HasState || zelda.ScriptButtonSensitive,
                "Room $5:$f1 repeated cancellation retained raw controls, transitions or the A target.");
            LoadValidationRoom(5, 0xf0);
            LoadValidationRoom(5, 0xf1);
            Step(3);
            zelda = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0xad);
            ApproachLibraryActor(zelda, batched, counterpartReachable: () => _entities.FindTalkTarget(_player) == zelda);
            Press();
            FailIf(Mask() != 0x11 || _dialogue.IsOpen, "Room $5:$f1 Zelda could not be activated again after re-entry.");
            LoadValidationRoom(5, 0xf0);
            FailIf(Mask() != 0 || _roomEvents.Get<RoomOfRitesZeldaEvent>().HasState,
                "Room $5:$f1 exit during Zelda's wait retained control or event state.");
            LoadValidationRoom(5, 0xf1);
            Step(3);
            zelda = _entities.Entities<NpcCharacter>().Single(n => n.Record.Id == 0xad);
            ApproachLibraryActor(zelda, batched, counterpartReachable: () => _entities.FindTalkTarget(_player) == zelda);
            Press(); Step(222);
            FailIf(!_dialogue.IsOpen, "Room $5:$f1 Zelda text cancellation fixture did not reach TX_3d05.");
            LoadValidationRoom(5, 0xf0);
            FailIf(_dialogue.IsOpen || Mask() != 0,
                "Room $5:$f1 exit during Zelda's text retained its modal or control mask.");

            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched: batched);
            void Press() => StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
            int Mask() => _entities.RuntimeState.ReadWramByte(WramAddress.wDisabledObjects);
        }
        // Zero wActiveMusic suppresses the saved cue independently of flag $80.
        ReinitializeGameplayForValidation();
        _saveData.SetRoomFlag(5, 0xf1, 0x80);
        LoadValidationRoom(5, 0xf1);
        _sound.SetNativeActiveMusic(0);
        int requests = _sound.PlayRequestsFor(0x38);
        StepGameplayUpdates(4, Vector2.Zero);
        FailIf(_sound.PlayRequestsFor(0x38) != requests || _sound.NativeActiveMusic != 0,
            "Room $5:$f1 Zelda replayed $38 despite zero native room-selection music.");
        GD.Print("Validated room $5:$f1 Zelda native initialization, source text/wait boundaries, collision approach, repeated entry, cancellation and collapse handoff with split/batched gameplay updates.");
    }
}
