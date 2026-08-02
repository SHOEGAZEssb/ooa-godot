using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom05bCompanionTutorial()
    {
        const int group = 0;
        const int room = 0x5b;
        var database = new CompanionTutorialDatabase();
        CompanionTutorialRecord record =
            database.GetRoomRecords(group, room).Single();
        byte flagMask = (byte)(1 << record.FlagBit);

        using (_saveData.BeginMutation())
        {
            _saveData.WriteWramByte(
                record.FlagAddress,
                (byte)(_saveData.ReadWramByte(record.FlagAddress) & ~flagMask));
        }
        CompanionRuntimeState.Begin(
            _runtimeState,
            CompanionRuntimeState.MooshId,
            room,
            new Vector2(record.X - 8, record.Y),
            direction: 1);
        LoadValidationRoom(group, room);

        CompanionTutorialRoomEntity tutorial =
            _entities.Entities<CompanionTutorialRoomEntity>().Single();
        FailIf(
            tutorial.Record != record || tutorial.Position != new Vector2(0x60, 0x68) ||
            _entities.Entities<MooshCompanionRoomEntity>().Count != 1 ||
            _dialogue.IsOpen,
            "Room 0:5b did not create source object `$d0:$04 after the live " +
            "SPECIALOBJECT_MOOSH owner at marker (0x60,0x68).");

        StepRoomEventFrames(1);
        FailIf(
            tutorial.State != 1 || tutorial.TextShown || _dialogue.IsOpen,
            "INTERAC_COMPANION_TUTORIAL state 0 did not consume exactly its " +
            "first update without showing text.");

        int tutorialSoundRequests = _sound.PlayRequestsFor(0xc5);
        StepRoomEventFrames(1);
        FailIf(
            tutorial.State != 2 || !tutorial.TextShown || !_dialogue.IsOpen ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(record.Message),
            "Mounted Moosh did not show imported TX_2207 on the state-1 update.");
        _dialogue.AdvanceCharacterClockForValidation(2.0 / 60.0);
        FailIf(
            _sound.PlayRequestsFor(0xc5) != tutorialSoundRequests + 1,
            "TX_2207 did not execute its leading source `\\sfx(0xc5)` cue.");
        _dialogue.Close();

        var spawns = new List<RoomEntitySpawn>();
        RoomEntityFrame frame = new(_player, _entities.FrameCounter, false);
        CompanionRuntimeState.Update(
            _runtimeState,
            CompanionRuntimeState.MooshId,
            room,
            new Vector2(record.X, record.Y),
            direction: 1);
        tutorial.UpdateFrame(frame, spawns);
        FailIf(
            tutorial.Finished ||
            (_saveData.ReadWramByte(record.FlagAddress) & flagMask) != 0,
            "Room 0:5b completed tutorial bit `$04 when Moosh merely equalled " +
            "the source X marker.");

        CompanionRuntimeState.Update(
            _runtimeState,
            CompanionRuntimeState.MooshId,
            room,
            new Vector2(record.X + 1, record.Y),
            direction: 1);
        tutorial.UpdateFrame(frame, spawns);
        FailIf(
            !tutorial.Finished ||
            (_saveData.ReadWramByte(record.FlagAddress) & flagMask) == 0,
            "Moosh crossing strictly right of X=$60 did not set " +
            "wCompanionTutorialTextShown bit `$04 and delete `$d0:$04.");

        StepRoomEventFrames(1);
        FailIf(
            _entities.Entities<CompanionTutorialRoomEntity>().Count != 0,
            "Completed room 0:5b companion tutorial was not removed.");

        LoadValidationRoom(group, room);
        StepRoomEventFrames(2);
        FailIf(
            _dialogue.IsOpen ||
            _entities.Entities<CompanionTutorialRoomEntity>().Count != 0,
            "wCompanionTutorialTextShown bit `$04 did not suppress TX_2207 on " +
            "room 0:5b re-entry.");

        using (_saveData.BeginMutation())
        {
            _saveData.WriteWramByte(
                record.FlagAddress,
                (byte)(_saveData.ReadWramByte(record.FlagAddress) & ~flagMask));
        }
        CompanionRuntimeState.Clear(
            _runtimeState, CompanionRuntimeState.MooshId);
        const int leftNeighbor = 0x5a;
        CompanionRuntimeState.Begin(
            _runtimeState,
            CompanionRuntimeState.MooshId,
            leftNeighbor,
            new Vector2(0x88, record.Y),
            direction: 1);
        LoadValidationRoom(group, leftNeighbor);
        if (!_rooms.TryGetNeighbor(Vector2I.Right, out int target) ||
            target != room)
        {
            throw new System.InvalidOperationException(
                "Room 0:5a did not resolve room 0:5b as its source-derived " +
                "right neighbor.");
        }
        _transitions.BeginScroll(_player, Vector2I.Right, target);
        CompanionTutorialRoomEntity incomingTutorial =
            _entities.Entities<CompanionTutorialRoomEntity>().Single();
        FailIf(
            incomingTutorial.State != 1 || incomingTutorial.TextShown ||
            _dialogue.IsOpen,
            "Destination preload did not run only `$d0:$04 state 0 while " +
            "room 0:5b was frozen during scrolling.");
        for (int frameIndex = 0;
            frameIndex < 60 && _transitions.ScrollActive;
            frameIndex++)
        {
            _transitions.UpdateScroll(1.0 / 60.0);
            _entities.Update(1.0 / 60.0, _player);
        }
        if (!_dialogue.IsOpen)
            StepRoomEventFrames(1);
        FailIf(
            _transitions.ScrollActive || _rooms.CurrentRoom.Id != room ||
            incomingTutorial.State != 2 || !incomingTutorial.TextShown ||
            !_dialogue.IsOpen,
            "Scrolling into room 0:5b did not resume the preloaded tutorial " +
            "at state 1 with the retained mounted Moosh owner.");
        _dialogue.Close();

        GD.Print(
            "Validated room 0:5b INTERAC_COMPANION_TUTORIAL `$d0:$04, " +
            "state-1 mounted TX_2207/`\\sfx(0xc5), strict companion-X crossing, " +
            "persistent tutorial bit `$04, deletion, re-entry suppression, " +
            "and destination-scroll state-0 preload.");
    }
}
