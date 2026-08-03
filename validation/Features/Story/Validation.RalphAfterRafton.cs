using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom197RalphAfterRafton()
    {
        const int group = 1;
        const int roomId = 0x97;

        RalphAfterRaftonEvent roomEvent = _roomEvents.RalphAfterRafton;
        RalphAfterRaftonEventDatabase database = roomEvent.Database;
        RalphAfterRaftonEventRecord record = database.Record;
        bool originalRequired =
            _saveData.HasGlobalFlag(record.RequiredGlobalFlag);
        bool originalRoomFlag = _saveData.HasRoomFlag(
            group, roomId, (byte)record.RoomFlag);

        RalphAfterRaftonCharacter Ralph() =>
            _entities.Entities<RalphAfterRaftonCharacter>().Single();

        _saveData.SetRoomFlag(
            group, roomId, (byte)record.RoomFlag, value: false);
        _saveData.SetGlobalFlag(record.RequiredGlobalFlag, value: false);
        LoadValidationRoom(group, roomId);
        FailIf(
            Ralph().Active || roomEvent.HasState,
            "Room 1:97 did not delete Ralph while global flag $15 was clear.");

        _saveData.SetGlobalFlag(record.RequiredGlobalFlag);
        _saveData.SetRoomFlag(group, roomId, (byte)record.RoomFlag);
        LoadValidationRoom(group, roomId);
        FailIf(
            Ralph().Active || roomEvent.HasState,
            "Room flag $40 did not suppress Ralph $37:$03 in room 1:97.");

        _saveData.SetRoomFlag(
            group, roomId, (byte)record.RoomFlag, value: false);
        var trace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = trace;
        int ralphMusicStarts =
            _sound.PlayRequestsFor(OracleSoundEngine.MusRalph);
        LoadValidationRoom(group, roomId);

        RalphAfterRaftonCharacter ralph = Ralph();
        FailIf(
            ralph.Record is not { Id: 0x37, SubId: 0x03, Var03: 0x00 } ||
            ralph.Position != new Vector2(0x38, 0x38) ||
            ralph.Record.SpriteName != "spr_ralph_1" ||
            ralph.Record.TileBase != 0 || ralph.Record.Palette != 1 ||
            ralph.AnimationRate != 0.0f ||
            ralph.FacingVector != Vector2I.Right ||
            ralph.CurrentScriptAnimationSource != record.Animation(3) ||
            !ralph.Active || !roomEvent.HasState ||
            roomEvent.Substate != 0 ||
            roomEvent.Counter != record.LookCounter ||
            roomEvent.NativeDirection != record.InitialDirection ||
            roomEvent.CurrentCommandIndex != -1 ||
            !roomEvent.BlocksGameplay || !roomEvent.MenusDisabled ||
            !_player.CutsceneControlled ||
            _sound.ActiveMusic != OracleSoundEngine.MusRalph ||
            _sound.PlayRequestsFor(OracleSoundEngine.MusRalph) !=
                ralphMusicStarts + 1,
            "Room 1:97 did not preserve Ralph's placement, source-state-0 " +
            "$78 counter, animation-$03/direction-$01 split, $01 input/menu " +
            "lock, or roomSpecificCode7 MUS_RALPH override.");

        int updatesToLookFlip = 16 - (_entities.FrameCounter & 0x0f);
        StepRoomEventFrames(updatesToLookFlip - 1);
        FailIf(
            roomEvent.NativeDirection != record.InitialDirection,
            "Ralph changed direction before the global 16-update boundary.");
        StepRoomEventFrames(1);
        FailIf(
            roomEvent.NativeDirection !=
                (record.InitialDirection ^ record.LookDirectionXor) ||
            ralph.CurrentScriptAnimationSource != record.Animation(3),
            "Ralph did not XOR direction $01 by $02 on wFrameCounter & $0f == 0.");

        StepRoomEventFrames(record.LookCounter - updatesToLookFlip - 1);
        FailIf(
            roomEvent.Substate != 0 || roomEvent.Counter != 1,
            "Ralph's native $78 look counter ended before its zero update.");
        StepRoomEventFrames(1);
        FailIf(
            roomEvent.Substate != 1 ||
            roomEvent.Counter != record.PostLookWait ||
            ralph.CurrentScriptAnimationSource != record.Animation(2),
            "Ralph did not install animation $02 and wait $1e on the look zero update.");

        int jumpSounds = _sound.PlayRequestsFor(record.JumpSound);
        StepRoomEventFrames(record.PostLookWait - 1);
        FailIf(
            roomEvent.Substate != 1 || roomEvent.Counter != 1 ||
            _sound.PlayRequestsFor(record.JumpSound) != jumpSounds,
            "Ralph's pre-jump wait lost its final counter boundary.");
        StepRoomEventFrames(1);
        FailIf(
            roomEvent.Substate != 2 || roomEvent.ZFixed != 0 ||
            roomEvent.SpeedZ != record.JumpSpeedZ ||
            _sound.PlayRequestsFor(record.JumpSound) != jumpSounds + 1,
            "startJump did not apply -$01c0 and SND_JUMP on its own update.");

        StepRoomEventFrames(1);
        FailIf(
            roomEvent.Substate != 2 || roomEvent.ZFixed != -0x01c0 ||
            roomEvent.SpeedZ != -0x01a0 ||
            ralph.ScriptDrawOffset != new Vector2(0, -2),
            "Ralph's first gravity-$20 update lost its exact 8.8 Z arithmetic.");
        StepRoomEventFrames(27);
        FailIf(
            roomEvent.Substate != 2 || roomEvent.ZFixed >= 0,
            "Ralph landed before the 29th objectUpdateSpeedZ_paramC call.");
        StepRoomEventFrames(1);
        FailIf(
            roomEvent.Substate != 3 ||
            roomEvent.Counter != record.LandingWait ||
            roomEvent.ZFixed != 0 || roomEvent.SpeedZ != 0x01c0 ||
            ralph.ScriptDrawOffset != Vector2.Zero,
            "Ralph did not clamp Z and install the $0a landing wait on update 29.");

        StepRoomEventFrames(record.LandingWait - 1);
        FailIf(
            _dialogue.IsOpen || roomEvent.Counter != 1,
            "Ralph displayed native TX_2a0a before the landing counter reached zero.");
        StepRoomEventFrames(1);
        FailIf(
            !_dialogue.IsOpen ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(record.NativeText) ||
            roomEvent.Substate != 4 ||
            roomEvent.Counter != record.PostTextWait,
            "Ralph did not display imported TX_2a0a on the landing zero update.");
        StepRoomEventFrames(5);
        FailIf(
            roomEvent.Counter != record.PostTextWait,
            "Ralph decremented the post-text wait while wTextIsActive was nonzero.");
        _dialogue.Close();

        StepRoomEventFrames(record.PostTextWait - 1);
        FailIf(
            roomEvent.Substate != 4 || roomEvent.Counter != 1,
            "Ralph's post-text $1e wait ended one update early.");
        StepRoomEventFrames(1);
        FailIf(
            roomEvent.Substate != 5 ||
            roomEvent.Counter != record.ApproachCounter ||
            ralph.CurrentScriptAnimationSource != record.Animation(2),
            "Ralph did not install SPEED_100/down/$30 after TX_2a0a.");

        StepRoomEventFrames(record.ApproachCounter - 1);
        FailIf(
            roomEvent.Substate != 5 || roomEvent.Counter != 1 ||
            ralph.Position != new Vector2(0x38, 0x67),
            "Ralph did not apply exactly 47 SPEED_100 downward moves.");
        StepRoomEventFrames(1);
        FailIf(
            roomEvent.Substate != 6 || roomEvent.Counter != record.AlignWait ||
            ralph.Position != new Vector2(0x38, 0x67),
            "Ralph's approach zero update moved or missed the $06 alignment wait.");

        _player.WarpTo(new Vector2(0x44, 0x70));
        StepRoomEventFrames(record.AlignWait - 1);
        FailIf(
            roomEvent.Substate != 6 || roomEvent.Counter != 1,
            "Ralph aligned to Link before the $06 wait reached zero.");
        StepRoomEventFrames(1);
        FailIf(
            roomEvent.Substate != 7 || roomEvent.Counter != 0x0c ||
            ralph.CurrentScriptAnimationSource != record.Animation(1),
            "Ralph did not select the source rightward $0c X-alignment.");
        StepRoomEventFrames(0x0b);
        FailIf(
            roomEvent.Substate != 7 || roomEvent.Counter != 1 ||
            ralph.Position != new Vector2(0x43, 0x67),
            "Ralph's X-alignment did not stop one pixel short of Link.");
        StepRoomEventFrames(1);
        FailIf(
            roomEvent.Substate != 8 || roomEvent.CurrentCommandIndex != 0 ||
            roomEvent.ScriptCounter != 0,
            "Ralph did not install ralphSubid03Script on the alignment zero update.");

        StepRoomEventFrames(1);
        FailIf(
            roomEvent.CurrentCommandIndex != 0 || roomEvent.ScriptCounter != 6,
            "ralphSubid03Script wait 6 did not use its setup-only update.");
        StepRoomEventFrames(5);
        FailIf(
            roomEvent.CurrentCommandIndex != 0 || roomEvent.ScriptCounter != 1,
            "ralphSubid03Script wait 6 ended before its zero update.");
        StepRoomEventFrames(1);
        FailIf(
            roomEvent.CurrentCommandIndex != 2 ||
            ralph.CurrentScriptAnimationSource != record.Animation(2),
            "setanimation $02 did not yield after wait 6.");

        int dialogueGuard = 0;
        while (!_dialogue.IsOpen && dialogueGuard++ < 20)
            StepRoomEventFrames(1);
        FailIf(
            dialogueGuard >= 20 ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(
                ((CutsceneShowTextCommand)database.Commands[3]).Message) ||
            roomEvent.CurrentCommandIndex != 4,
            "Ralph did not display imported TX_2a0b after wait 10.");
        int frozenFrame = ralph.CurrentAnimationFrame;
        StepRoomEventFrames(4);
        FailIf(
            ralph.CurrentAnimationFrame != frozenFrame ||
            roomEvent.CurrentCommandIndex != 4,
            "Ralph advanced while TX_2a0b kept wTextIsActive nonzero.");
        _dialogue.Close();

        dialogueGuard = 0;
        while (!_dialogue.IsOpen && dialogueGuard++ < 50)
            StepRoomEventFrames(1);
        FailIf(
            dialogueGuard >= 50 ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(
                ((CutsceneShowTextCommand)database.Commands[7]).Message) ||
            roomEvent.CurrentCommandIndex != 8 ||
            ralph.CurrentScriptAnimationSource != record.Animation(0),
            "Ralph did not wait 20, turn up, and display imported TX_2a06.");
        _dialogue.Close();

        int movementGuard = 0;
        while (roomEvent.CurrentCommandIndex != 10 && movementGuard++ < 20)
            StepRoomEventFrames(1);
        FailIf(
            movementGuard >= 20 || roomEvent.ScriptCounter != 0,
            "Ralph did not yield SPEED_200 before moveup $44.");
        StepRoomEventFrames(1);
        FailIf(
            roomEvent.ScriptCounter != record.ExitCounter ||
            ralph.CurrentScriptAnimationSource != record.Animation(0) ||
            ralph.CurrentAnimationFrame != 0,
            "moveup $44 did not use a setup-only counter update.");
        StepRoomEventFrames(5);
        FailIf(
            ralph.CurrentAnimationFrame != 0,
            "SPEED_200 departure animation advanced before 16 source calls.");
        StepRoomEventFrames(1);
        FailIf(
            ralph.CurrentAnimationFrame != 1,
            "SPEED_200 departure animation missed its sixth three-call update.");
        StepRoomEventFrames(record.ExitCounter - 1 - 6);
        FailIf(
            roomEvent.ScriptCounter != 1 ||
            ralph.Position != new Vector2(0x43, 0xe1),
            "Ralph did not apply exactly 67 SPEED_200 upward moves with byte wrap.");
        StepRoomEventFrames(1);
        FailIf(
            roomEvent.CurrentCommandIndex != 11 || roomEvent.ScriptCounter != 0,
            "moveup $44 did not yield on its zero update.");

        int fades = _sound.PlayRequestsFor(record.FadeSound);
        StepRoomEventFrames(1);
        FailIf(
            roomEvent.CurrentCommandIndex != 12 ||
            _sound.PlayRequestsFor(record.FadeSound) != fades + 1,
            "Ralph did not request SNDCTRL_FAST_FADEOUT after leaving the screen.");
        StepRoomEventFrames(1);
        FailIf(
            roomEvent.ScriptCounter != 30,
            "Ralph's final wait 30 did not use its setup-only update.");
        StepRoomEventFrames(29);
        FailIf(
            roomEvent.ScriptCounter != 1 ||
            _saveData.HasRoomFlag(group, roomId, (byte)record.RoomFlag) ||
            !roomEvent.HasState || !roomEvent.BlocksGameplay ||
            !roomEvent.MenusDisabled,
            "Ralph ended or set room flag $40 before the final wait zero update.");
        StepRoomEventFrames(1);
        FailIf(
            !roomEvent.HasState || ralph.Active || !roomEvent.BlocksGameplay ||
            !roomEvent.MenusDisabled || !_player.CutsceneControlled ||
            !_saveData.HasRoomFlag(group, roomId, (byte)record.RoomFlag) ||
            roomEvent.CurrentCommandIndex != 14,
            "Ralph did not set room flag $40 and yield before enableinput " +
            "(the live imported predicate should hide the off-screen actor) " +
            $"(state={roomEvent.HasState}, actor={ralph.Active}, " +
            $"blocks={roomEvent.BlocksGameplay}, menus={roomEvent.MenusDisabled}, " +
            $"link={_player.CutsceneControlled}, flag=" +
            $"{_saveData.HasRoomFlag(group, roomId, (byte)record.RoomFlag)}, " +
            $"command={roomEvent.CurrentCommandIndex}).");
        int musicRestarts = _sound.PlayRequestsFor(record.CompletionMusic);
        StepRoomEventFrames(1);
        FailIf(
            roomEvent.HasState || ralph.Active || roomEvent.BlocksGameplay ||
            roomEvent.MenusDisabled || _player.CutsceneControlled ||
            _sound.ActiveMusic != record.CompletionMusic ||
            _sound.PlayRequestsFor(record.CompletionMusic) != musicRestarts + 1,
            "Ralph did not restore input/music and delete himself one update " +
            "after orroomflag $40 yielded.");
        StepRoomEventFrames(80);
        FailIf(
            _sound.ActiveMusic != record.CompletionMusic,
            "Ralph's past-overworld music restart did not cancel the fast fade.");

        CutsceneCommandTraceEntry[] starts = trace.Entries.Where(entry =>
            entry.Phase == CutsceneCommandTracePhase.Started).ToArray();
        FailIf(
            starts.Length != database.Commands.Count ||
            starts.Any(entry =>
                entry.Source.Script != "ralphSubid03Script" ||
                entry.Source.SourceLine <= 0),
            "Ralph-after-Rafton typed trace lost a command or source line.");

        LoadValidationRoom(group, roomId);
        FailIf(
            Ralph().Active || roomEvent.HasState,
            "Completed room flag $40 did not suppress Ralph on re-entry.");

        _roomEvents.CommandTraceSink = null;
        _saveData.SetRoomFlag(
            group, roomId, (byte)record.RoomFlag, value: false);
        LoadValidationRoom(0, 0x11);
        LoadValidationRoom(group, roomId);
        AdvanceRalphAfterRaftonToAlignment(roomEvent);
        ralph = Ralph();
        _player.WarpTo(new Vector2(ralph.Position.X, 0x70));
        StepRoomEventFrames(record.AlignWait);
        FailIf(
            roomEvent.Substate != 8 || roomEvent.CurrentCommandIndex != 0 ||
            ralph.Position != new Vector2(0x38, 0x67),
            "Ralph's equal-X branch did not install the script without movement.");

        LoadValidationRoom(0, 0x11);
        LoadValidationRoom(group, roomId);
        AdvanceRalphAfterRaftonToAlignment(roomEvent);
        ralph = Ralph();
        _player.WarpTo(new Vector2(0x2c, 0x70));
        StepRoomEventFrames(record.AlignWait);
        FailIf(
            roomEvent.Substate != 7 || roomEvent.Counter != 0x0c ||
            ralph.CurrentScriptAnimationSource != record.Animation(3),
            "Ralph did not select the source leftward $0c X-alignment.");
        StepRoomEventFrames(0x0b);
        FailIf(
            roomEvent.Substate != 7 || roomEvent.Counter != 1 ||
            ralph.Position != new Vector2(0x2d, 0x67),
            "Ralph's leftward alignment did not stop one pixel right of Link.");
        StepRoomEventFrames(1);
        FailIf(
            roomEvent.Substate != 8 || roomEvent.CurrentCommandIndex != 0,
            "Ralph's leftward alignment did not install ralphSubid03Script.");

        _saveData.SetGlobalFlag(record.RequiredGlobalFlag, originalRequired);
        _saveData.SetRoomFlag(
            group, roomId, (byte)record.RoomFlag, originalRoomFlag);
        _dialogue.Close();
        LoadValidationRoom(0, 0x11);

        GD.Print("Validated room 1:97 Ralph $37:$03: flag-$15/room-$40 " +
            "predicates, roomSpecificCode7 music, exact native counters and " +
            "jump, TX_2a0a, SPEED_100 " +
            "approach/X alignment, ralphSubid03Script dialogue and SPEED_200 " +
            "departure, fade, persistence, input, and past-overworld music restore.");
    }

    private void AdvanceRalphAfterRaftonToAlignment(
        RalphAfterRaftonEvent roomEvent)
    {
        int guard = 0;
        while (roomEvent.Substate != 6 && guard++ < 400)
        {
            StepRoomEventFrames(1);
            if (_dialogue.IsOpen)
                _dialogue.Close();
        }
        FailIf(
            guard >= 400 || roomEvent.Counter != roomEvent.Database.Record.AlignWait,
            "Room 1:97 did not reach Ralph's X-alignment wait within 400 updates.");
    }
}
