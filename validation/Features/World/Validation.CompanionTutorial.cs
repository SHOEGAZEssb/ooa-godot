using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private static void ValidateCompanionWallMasks()
    {
        ValidateSpecialObjectWallMovement();
        // commonCode.s:specialObjectCheckFacingWall, including its original
        // vertical-mask selection for angles $19-$1f.
        int[] masks =
        [
            0xc0, 0xc3, 0xc3, 0xc3, 0xc3, 0xc3, 0xc3, 0xc3,
            0x03, 0x33, 0x33, 0x33, 0x33, 0x33, 0x33, 0x33,
            0x30, 0x3c, 0x3c, 0x3c, 0x3c, 0x3c, 0x3c, 0x3c,
            0x0c, 0x3c, 0x3c, 0x3c, 0x3c, 0x3c, 0x3c, 0x3c
        ];
        for (int walls = 0; walls <= 0xff; walls++)
        {
            FailIf(CompanionMovement.FacingWallMask(0xff, walls) != 0,
                $"Idle companion angle $ff collided with wall mask ${walls:x2}.");
            for (int angle = 0; angle < masks.Length; angle++)
                FailIf(CompanionMovement.FacingWallMask(angle, walls) != (walls & masks[angle]),
                    $"Companion angle ${angle:x2} selected the wrong walls from ${walls:x2}.");
        }
        GD.Print("Validated all companion direction and adjacent-wall bitmask combinations.");
    }

    private static void ValidateSpecialObjectWallMovement()
    {
        // Independent transcription of bank0.slideAngleTable and
        // link.s:@bitsToCheck, not the runtime's sector arithmetic.
        int[] slide = [0x80,0x80,1,2,2,2,3,0x24,0x24,0x24,5,6,6,6,7,0x48,
            0x48,0x48,9,10,10,10,11,0x1c,0x1c,0x1c,13,14,14,14,15,0x80];
        int[] masks = [0xcf,0xc3,0xc3,0xc3,0xc3,0xc3,0xc3,0xc3,
            0xf3,0x33,0x33,0x33,0x33,0x33,0x33,0x33,
            0x3f,0x3c,0x3c,0x3c,0x3c,0x3c,0x3c,0x3c,
            0xfc,0xcc,0xcc,0xcc,0xcc,0xcc,0xcc,0xcc];
        // bank3.objectSpeedTable .dwsin/.dwcos at SPEED_100, truncated words.
        int[] sine = [0,49,97,142,181,212,236,251,256,251,236,212,181,142,97,49,
            0,-49,-97,-142,-181,-212,-236,-251,-256,-251,-236,-212,-181,-142,-97,-49];
        for (int walls = 0; walls < 256; walls++)
        for (int angle = 0; angle < 32; angle++)
        {
            int direction = angle;
            int blocked = walls;
            if ((slide[angle] & 3) == 0)
            {
                (int mask, int value, int result)[] probes = slide[angle] switch
                {
                    0x80 => [(0xc3,0x80,8),(0xcc,0x40,24)],
                    0x48 => [(0x33,0x20,8),(0x3c,0x10,24)],
                    0x24 => [(0xc3,1,0),(0x33,2,16)],
                    _ => [(0xcc,4,0),(0x3c,8,16)]
                };
                foreach (var probe in probes)
                    if ((walls & probe.mask) == probe.value)
                    {
                        direction = probe.result;
                        blocked = 0;
                        break;
                    }
            }
            blocked &= masks[direction];
            foreach (Vector2 origin in new[] { new Vector2(64.5f,72.25f), new Vector2(0.125f,255.875f) })
            {
                int x = (int)(origin.X * 256), y = (int)(origin.Y * 256);
                if ((blocked & 15) == 0) x = (x + sine[direction]) & 0xffff;
                if ((blocked & 0xf0) == 0) y = (y - sine[(direction + 8) & 31]) & 0xffff;
                Vector2 actual = origin;
                SpecialObjectMovement.ApplySpeed(ref actual, 0x28, angle, walls);
                FailIf(actual != new Vector2(x / 256.0f, y / 256.0f),
                    $"specialObjectUpdatePosition angle ${angle:x2}, walls ${walls:x2}, origin {origin}: {actual}.");
            }
        }
        Vector2 idle = new(10.5f,20.25f);
        SpecialObjectMovement.ApplySpeed(ref idle, 0x28, 0xff, 0xff);
        FailIf(idle != new Vector2(10.5f,20.25f), "Special-object angle $ff must not move.");
    }

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
