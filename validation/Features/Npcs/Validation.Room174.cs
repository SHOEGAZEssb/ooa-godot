using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom174PastOldLady()
    {
        const double frame = 1.0 / 60.0;
        using var fixture = new NpcInteractionValidationFixture(
            this, "Room174PastOldLadyValidation", 1, 0x74);
        OracleSaveData save = fixture.Save;
        RoomSession rooms = fixture.Rooms;
        RoomEntityManager manager = fixture.Manager;
        InteractionController interactions = fixture.Interactions;
        DialogueBox dialogue = fixture.Dialogue;

        manager.LoadRoom(1, rooms.CurrentRoom);
        NpcCharacter oldLady =
            manager.Entities<NpcCharacter>().Single();
        FailIf(
            oldLady.BaseRecord is not
            {
                Group: 1,
                Room: 0x74,
                Id: 0x45,
                SubId: 0x00,
                Var03: 0x00,
                TextId: 0x180a,
                TileBase: 0x1c,
                Palette: 3,
                DefaultAnimation: 4,
                CanFace: false,
                Implementation:
                    NpcImplementationClassification.OrdinaryGeneric
            } ||
            oldLady.Position != new Vector2(0x38, 0x58) ||
            !oldLady.Active ||
            !oldLady.Visible ||
            oldLady.FacingVector != Vector2I.Down ||
            oldLady.CurrentAnimationOpaquePixels == 0 ||
            PlainWords(oldLady.Message) !=
                "My husband gave Queen Ambi that which pleases her, " +
                "but he was still taken to work on the Black Tower. " +
                "I want him back.",
            "Room 1:74 did not load past old lady $45:$00 at $58,$38 " +
            "with palette $03, animation $04, and TX_180a.");

        FailIf(
            oldLady.ObjectCollisionBounds.Size != new Vector2(12.0f, 12.0f) ||
            oldLady.ObjectCollisionBounds.GetCenter() != oldLady.Position ||
            oldLady.LinkBlockingBounds.Size != new Vector2(24.0f, 24.0f) ||
            oldLady.LinkBlockingBounds.GetCenter() != oldLady.Position ||
            !manager.BlocksLink(oldLady.Position) ||
            manager.BlocksLink(oldLady.Position + Vector2.Down * 12.0f),
            "Room 1:74's old lady did not retain the ordinary $06/$06 " +
            "solid collision and strict 12-pixel Link boundary.");

        ulong initialFrameHash = oldLady.CurrentAnimationPixelHash;
        _player.WarpTo(
            oldLady.Position + Vector2.Left * 20.0f,
            recordSafe: false);
        for (int update = 0; update < 15; update++)
            manager.Update(frame, _player);
        FailIf(
            oldLady.CurrentAnimationFrame != 0 ||
            oldLady.FacingVector != Vector2I.Down,
            "Room 1:74's old lady advanced animation $04 early or faced Link.");
        manager.Update(frame, _player);
        FailIf(
            oldLady.CurrentAnimationFrame != 1 ||
            oldLady.CurrentAnimationPixelHash == initialFrameHash ||
            oldLady.FacingVector != Vector2I.Down,
            "Room 1:74's old lady did not reach animation $04's second " +
            "pose after exactly 16 updates.");
        for (int update = 0; update < 16; update++)
            manager.Update(frame, _player);
        FailIf(
            oldLady.CurrentAnimationFrame != 0 ||
            oldLady.CurrentAnimationPixelHash != initialFrameHash,
            "Room 1:74's two-pose animation $04 did not loop after 32 updates.");

        _player.WarpTo(
            oldLady.Position + Vector2.Down * 12.0f,
            recordSafe: false);
        _player.Face(Vector2I.Up);
        FailIf(
            !oldLady.CanTalkTo(_player) ||
            !interactions.TryInteract(_player) ||
            !dialogue.IsOpen ||
            PlainWords(dialogue.CurrentMessage) !=
                "My husband gave Queen Ambi that which pleases her, " +
                "but he was still taken to work on the Black Tower. " +
                "I want him back." ||
            oldLady.FacingVector != Vector2I.Down,
            "Room 1:74's fixed-facing old lady was not A-button sensitive " +
            "with TX_180a.");

        int dialogueFrame = oldLady.CurrentAnimationFrame;
        manager.Update(16.0 / 60.0, _player);
        FailIf(
            oldLady.CurrentAnimationFrame != dialogueFrame,
            "wTextIsActive did not freeze room 1:74's ordinary old-lady animation.");
        dialogue.Close();
        interactions.Update(frame, _player);

        save.SetGlobalFlag(OracleSaveData.GlobalFlag0b);
        FailIf(
            !oldLady.Active ||
            !oldLady.Visible ||
            oldLady.TextId != 0x180a,
            "An unrelated global flag hid room 1:74's old lady or changed TX_180a.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame);
        FailIf(
            oldLady.Active ||
            oldLady.Visible ||
            oldLady.CanTalkTo(_player) ||
            manager.BlocksLink(oldLady.Position),
            "GLOBALFLAG_FINISHEDGAME did not live-delete room 1:74's old lady.");

        save.SetGlobalFlag(
            OracleSaveData.GlobalFlagFinishedGame,
            value: false);
        FailIf(
            !oldLady.Active ||
            !oldLady.Visible ||
            oldLady.TextId != 0x180a ||
            oldLady.BaseRecord.TextId != 0x180a,
            "Clearing GLOBALFLAG_FINISHEDGAME did not restore room 1:74's " +
            "immutable TX_180a actor.");

        save.SetGlobalFlag(OracleSaveData.GlobalFlagFinishedGame);
        manager.LoadRoom(1, rooms.CurrentRoom);
        oldLady = manager.Entities<NpcCharacter>().Single();
        FailIf(
            oldLady.Active ||
            oldLady.Visible ||
            oldLady.Position != new Vector2(0x38, 0x58) ||
            oldLady.BaseRecord.TextId != 0x180a,
            "Room 1:74 finished-game re-entry did not retain the suppressed " +
            "old-lady placement record.");

        fixture.Dispose();
        GD.Print(
            "Validated room 1:74 past old lady $45:$00 placement, TX_180a, " +
            "palette $03, fixed animation $04 cadence, solidity, talkability, " +
            "textbox freeze, unrelated flags, live refresh, and finished-game " +
            "re-entry suppression.");
    }
}
