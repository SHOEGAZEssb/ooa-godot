using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom20fCheval()
    {
        const int group = 2;
        const int roomId = 0x0f;

        ChevalEvent chevalEvent = _roomEvents.Cheval;
        ChevalEventDatabase database = chevalEvent.Database;
        ChevalEventRecord record = database.Record;
        bool originalTalked =
            _saveData.HasGlobalFlag(record.TalkedGlobalFlag);
        bool originalRope =
            _inventory.HasTreasure(record.ChevalRopeTreasure);
        if (originalRope)
            _inventory.LoseTreasure(record.ChevalRopeTreasure);
        _saveData.SetGlobalFlag(record.TalkedGlobalFlag, value: false);

        ChevalCharacter Cheval() =>
            _entities.Entities<ChevalCharacter>().Single();

        void PositionForTalk(int x = 0x50)
        {
            _player.WarpTo(new Vector2(x, 0x54));
            _player.Face(Vector2I.Up);
        }

        void ExpectDialogue(int textId, string phase)
        {
            FailIf(
                !_dialogue.IsOpen ||
                _dialogue.CurrentMessage !=
                    DialogueBox.PlainText(database.DialogueText(textId)),
                $"Cheval {phase} did not display expanded TX_{textId:x4}.");
        }

        var trace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = trace;

        LoadValidationRoom(group, 0x0e);
        OracleRoomData incomingRoom = _world.LoadRoom(group, roomId);
        _entities.BeginScreenTransition(
            group, incomingRoom, new Vector2(incomingRoom.Width, 0));
        ChevalCharacter incoming = Cheval();
        FailIf(
            !incoming.Active || !incoming.Visible,
            "Room 2:0f Cheval did not explicitly preload his visible " +
            "screen-transition presentation.");
        _entities.FinishScreenTransition();

        LoadValidationRoom(group, roomId);
        ChevalCharacter cheval = Cheval();
        FailIf(
            cheval.Record is not { Id: 0x6a, SubId: 0x00, Var03: 0x00 } ||
            cheval.Position != new Vector2(0x50, 0x40) ||
            cheval.Record.SpriteName != "spr_oldzora_cheval" ||
            cheval.Record.TileBase != 0x1a || cheval.Record.Palette != 0 ||
            cheval.Record.DefaultAnimation != 0 ||
            cheval.FacingVector != Vector2I.Up ||
            cheval.AnimationRate != 0.0f ||
            cheval.CurrentAnimationTextureSize != new Vector2I(32, 32) ||
            !chevalEvent.HasState || chevalEvent.CurrentCommandIndex != 0 ||
            chevalEvent.ButtonSensitive,
            "Room 2:0f did not preserve INTERAC_CHEVAL's source placement, " +
            "graphics, initial animation, or zero-update script installation.");

        PositionForTalk();
        FailIf(
            _entities.FindTalkTarget(_player) is not null,
            "Cheval became A-sensitive before initcollisions ran.");
        StepRoomEventFrames(1);
        FailIf(
            !chevalEvent.ButtonSensitive ||
            chevalEvent.CurrentCommandIndex != 2 ||
            cheval.ObjectCollisionBounds.Size != new Vector2(12, 24),
            "Cheval did not preserve initcollisions followed by the yielding " +
            "$0c/$06 collision-radii command boundary.");

        PositionForTalk(0x55);
        FailIf(
            _entities.FindTalkTarget(_player) != cheval,
            "Cheval rejected an A-button probe five pixels inside his X radius.");
        PositionForTalk(0x56);
        FailIf(
            _entities.FindTalkTarget(_player) is not null,
            "Cheval accepted an A-button probe at the strict six-pixel X boundary.");

        PositionForTalk();
        StepRoomEventFrames(1);
        FailIf(
            chevalEvent.CurrentCommandIndex != 3,
            "Cheval without the rope did not select @dontHaveChevalRope.");
        FailIf(
            !_interactions.TryInteract(_player),
            "Cheval's no-rope loop was not reachable through the normal A-button path.");
        StepRoomEventFrames(1);
        ExpectDialogue(0x270c, "no-rope greeting");
        FailIf(
            _saveData.HasGlobalFlag(record.TalkedGlobalFlag) ||
            chevalEvent.CurrentCommandIndex != 5 ||
            chevalEvent.BlocksGameplay || _player.CutsceneControlled,
            "Cheval set global flag $43 before TX_270c completed or froze input.");

        StepRoomEventFrames(27);
        FailIf(
            cheval.CurrentAnimationFrame != 1 ||
            _saveData.HasGlobalFlag(record.TalkedGlobalFlag),
            "interactionAnimateAsNpc did not retain Cheval's exact 30-update " +
            "animation boundary during dialogue, or the script advanced early.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        FailIf(
            !_saveData.HasGlobalFlag(record.TalkedGlobalFlag) ||
            chevalEvent.CurrentCommandIndex != 3 ||
            !trace.Saw(
                "GlobalFlag", value: record.TalkedGlobalFlag),
            "Cheval did not set GLOBALFLAG_TALKED_TO_CHEVAL after TX_270c " +
            "and return to his no-rope A-button loop.");

        _saveData.SetGlobalFlag(record.TalkedGlobalFlag, value: false);
        _inventory.GiveTreasure(record.ChevalRopeTreasure, 0);
        LoadValidationRoom(group, roomId);
        cheval = Cheval();
        PositionForTalk();
        StepRoomEventFrames(2);
        FailIf(
            chevalEvent.CurrentCommandIndex != 7 ||
            _entities.FindTalkTarget(_player) != cheval,
            "Cheval Rope ownership did not select @gotChevalRope.");
        FailIf(
            !_interactions.TryInteract(_player),
            "Cheval's rope-owned loop was not reachable through A-button routing.");
        StepRoomEventFrames(1);
        ExpectDialogue(0x270d, "rope-owned greeting");
        FailIf(
            _dialogue.CurrentMessage.Contains("\\call", System.StringComparison.Ordinal) ||
            _saveData.HasGlobalFlag(record.TalkedGlobalFlag),
            "Cheval exposed TX_270b's storage-time call or set flag $43 " +
            "before TX_270d completed.");
        _dialogue.Close();
        StepRoomEventFrames(1);
        FailIf(
            !_saveData.HasGlobalFlag(record.TalkedGlobalFlag) ||
            chevalEvent.CurrentCommandIndex != 7 ||
            chevalEvent.BlocksGameplay || _player.CutsceneControlled,
            "Cheval did not set flag $43 after TX_270d and return to the " +
            "rope-owned loop without taking input control.");

        CutsceneCommandTraceEntry[] commandStarts = trace.Entries.Where(entry =>
            entry.Phase == CutsceneCommandTracePhase.Started).ToArray();
        FailIf(
            commandStarts.Length == 0 ||
            commandStarts.Any(entry =>
                entry.Source.Script != "cheval_subid00Script" ||
                entry.Source.SourceLine <= 0),
            "Cheval's runtime did not retain typed source-line command traces.");

        if (!originalRope)
            _inventory.LoseTreasure(record.ChevalRopeTreasure);
        _saveData.SetGlobalFlag(record.TalkedGlobalFlag, originalTalked);
        _roomEvents.CommandTraceSink = null;
        _dialogue.Close();

        GD.Print("Validated room 2:0f Cheval $6a:$00: source placement and " +
            "animation, transition preload, one-update collision setup, exact " +
            "$0c/$06 A-button geometry, Cheval Rope branch, TX_270b call " +
            "expansion, dialogue-time animation, and post-text global flag $43.");
    }
}
