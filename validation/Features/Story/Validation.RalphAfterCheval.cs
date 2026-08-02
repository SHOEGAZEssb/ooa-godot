using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom179RalphAfterCheval()
    {
        const int group = 1;
        const int roomId = 0x79;
        const int sourceGroup = 2;
        const int sourceRoomId = 0x0f;

        RalphAfterChevalEvent roomEvent = _roomEvents.RalphAfterCheval;
        RalphAfterChevalEventDatabase database = roomEvent.Database;
        RalphAfterChevalEventRecord record = database.Record;
        bool originalTalked =
            _saveData.HasGlobalFlag(record.TalkedGlobalFlag);
        bool originalRoomFlag = _saveData.HasRoomFlag(
            group, roomId, (byte)record.RoomFlag);

        RalphAfterChevalCharacter Ralph() =>
            _entities.Entities<RalphAfterChevalCharacter>().Single();

        _saveData.SetRoomFlag(
            group, roomId, (byte)record.RoomFlag, value: false);
        _saveData.SetGlobalFlag(record.TalkedGlobalFlag, value: false);
        LoadValidationRoom(group, roomId);
        FailIf(
            Ralph().Active || roomEvent.HasState,
            "Room 1:79 did not delete Ralph when global flag $43 was clear.");

        _saveData.SetGlobalFlag(record.TalkedGlobalFlag);
        LoadValidationRoom(group, roomId);
        FailIf(
            Ralph().Active || roomEvent.HasState,
            "Room 1:79 did not require active warp destination $17.");

        var warps = new WarpDatabase();
        OracleRoomData sourceRoom = _world.LoadRoom(sourceGroup, sourceRoomId);
        FailIf(
            !warps.TryGetEdgeWarp(
                sourceGroup,
                sourceRoomId,
                Vector2I.Down,
                new Vector2(0x50, sourceRoom.Height + 2),
                new Vector2(sourceRoom.Width, sourceRoom.Height),
                out Warp warp) ||
            warp is not
            {
                SourcePosition: -1,
                EdgeMask: 4,
                SourceTransition: 3,
                DestinationGroup: group,
                DestinationRoom: roomId,
                DestinationPosition: 0x17,
                DestinationParameter: 0,
                DestinationTransition: 1
            },
            $"Room 2:0f did not retain its source exit into 1:79/$17 ({warp}).");

        var trace = new ValidationCutsceneTrace();
        _roomEvents.CommandTraceSink = trace;
        LoadValidationRoom(sourceGroup, sourceRoomId);
        _player.WarpTo(new Vector2(0x50, sourceRoom.Height + 2));
        _player.Face(Vector2I.Down);
        _transitions.ApplyWarp(_player, warp);
        int transitionUpdates = 0;
        while (IsTransitioning && transitionUpdates++ < 120)
            UpdateRoomWarpTransition(1.0 / 60.0);
        FailIf(
            IsTransitioning || transitionUpdates >= 120 ||
            _activeGroup != group || _currentRoom.Id != roomId,
            "Room 2:0f -> 1:79 warp did not finish through destination $17.");

        RalphAfterChevalCharacter ralph = Ralph();
        FailIf(
            ralph.Record is not { Id: 0x37, SubId: 0x10, Var03: 0x00 } ||
            ralph.Position != new Vector2(0x78, 0x90) ||
            ralph.Record.SpriteName != "spr_ralph_1" ||
            ralph.Record.TileBase != 0 || ralph.Record.Palette != 1 ||
            ralph.FacingVector != Vector2I.Down ||
            ralph.AnimationRate != 0.0f ||
            !ralph.Active || !roomEvent.HasState ||
            roomEvent.CurrentCommandIndex != 0 || roomEvent.Counter != 89 ||
            roomEvent.Substate != 0 || roomEvent.FacingBit ||
            !roomEvent.BlocksGameplay || !roomEvent.MenusDisabled ||
            !_player.CutsceneControlled,
            "Room 1:79 did not preserve Ralph's placement, source-state-0 " +
            "script update, or $81 input/menu lock " +
            $"(record={ralph.Record.Id:x2}:{ralph.Record.SubId:x2}/" +
            $"{ralph.Record.Var03:x2}, pos={ralph.Position}, " +
            $"facing={ralph.FacingVector}, rate={ralph.AnimationRate}, " +
            $"active={ralph.Active}, state={roomEvent.HasState}, " +
            $"command={roomEvent.CurrentCommandIndex}, counter={roomEvent.Counter}, " +
            $"substate={roomEvent.Substate}, facingBit={roomEvent.FacingBit}, " +
            $"blocks={roomEvent.BlocksGameplay}, menus={roomEvent.MenusDisabled}, " +
            $"link={_player.CutsceneControlled}).");

        // Source substate 0 calls interactionAnimate both before and after the
        // script. Including state 0's two calls, frame 0 lasts seven ordinary
        // event updates and changes on the next call pair.
        StepRoomEventFrames(5);
        FailIf(
            ralph.CurrentAnimationFrame != 0,
            "Ralph's substate-0 animation advanced before 16 source calls.");
        StepRoomEventFrames(1);
        FailIf(
            ralph.CurrentAnimationFrame != 1,
            "Ralph's substate-0 double animation missed its 16-call boundary.");

        StepRoomEventFrames(82);
        FailIf(
            roomEvent.CurrentCommandIndex != 0 || roomEvent.Counter != 1 ||
            roomEvent.FacingBit,
            "ralphSubid10Script did not retain the exact initial wait-90 boundary.");
        StepRoomEventFrames(1);
        FailIf(
            roomEvent.CurrentCommandIndex != 2 ||
            _sound.ActiveMusic != record.Music || roomEvent.FacingBit,
            "Ralph did not select MUS_RALPH immediately after wait 90.");

        StepRoomEventFrames(1);
        FailIf(
            roomEvent.CurrentCommandIndex != 3 || !roomEvent.FacingBit,
            "Ralph's xorcfc0bit did not toggle and yield before SPEED_200.");
        _player.WarpTo(new Vector2(0x6c, 0x18));
        _player.Face(Vector2I.Left);
        StepRoomEventFrames(1);
        FailIf(
            _player.FacingVector != Vector2I.Right ||
            roomEvent.CurrentCommandIndex != 4,
            "ralphTurnLinkTowardSelf lost its right-facing $0c threshold or " +
            "SPEED_200 yield boundary.");
        StepRoomEventFrames(1);
        FailIf(
            roomEvent.Counter != 0x18,
            "Ralph did not set up moveup $18 one update after SPEED_200.");

        StepRoomEventFrames(23);
        FailIf(
            ralph.Position != new Vector2(0x78, 0x62) ||
            roomEvent.Counter != 1 || roomEvent.Substate != 0,
            "SPEED_200 moveup $18 did not apply exactly 23 two-pixel moves.");
        StepRoomEventFrames(2);
        FailIf(
            roomEvent.CurrentCommandIndex != 6 || roomEvent.Substate != 1,
            "setsubstate $ff did not increment Ralph from substate 0 to 1 " +
            "and yield.");
        StepRoomEventFrames(1);
        FailIf(
            roomEvent.CurrentCommandIndex != 7,
            "Ralph did not yield SPEED_100 after entering substate 1.");

        while ((ContextFrameCounter() & 0x07) != 0x07)
            StepRoomEventFrames(1);
        Vector2 dustSourcePosition = ralph.Position;
        StepRoomEventFrames(1);
        PuzzlePuffEffect dust = _entities.Entities<PuzzlePuffEffect>()
            .Single(puff => puff.ElapsedUpdates == 1);
        FailIf(
            !dust.Flickers || !dust.FlickerVisibleOnEvenUpdates ||
            !dust.Visible ||
            dust.Position != dustSourcePosition + new Vector2(0x04, 0x08),
            "Ralph substate 1 did not create silent flickering $05:$81 dust " +
            "at offset $0804 on the global eight-update boundary.");
        StepRoomEventFrames(1);
        FailIf(
            dust.ElapsedUpdates != 2 || !dust.Visible,
            "Ralph's $05:$81 dust did not retain its first visible state-1 phase.");
        StepRoomEventFrames(1);
        FailIf(
            dust.ElapsedUpdates != 3 || dust.Visible,
            "Ralph's $05:$81 dust did not flicker on the following update.");

        while ((ContextFrameCounter() & 0x07) != 0x07)
            StepRoomEventFrames(1);
        Vector2 secondDustSourcePosition = ralph.Position;
        StepRoomEventFrames(1);
        PuzzlePuffEffect secondDust = _entities.Entities<PuzzlePuffEffect>()
            .Single(puff => puff.ElapsedUpdates == 1);
        FailIf(
            !secondDust.Flickers ||
            secondDust.FlickerVisibleOnEvenUpdates ||
            secondDust.Position !=
                secondDustSourcePosition + new Vector2(0x04, 0x08),
            "Ralph's second live puff did not take even source slot $d2 " +
            "after odd slot $d1.");
        StepRoomEventFrames(1);
        FailIf(
            secondDust.ElapsedUpdates != 2 || secondDust.Visible,
            "Ralph's even-slot $d2 puff did not invert the first puff's " +
            "state-1 flicker phase.");

        int substateGuard = 0;
        while (roomEvent.Substate != 2 && substateGuard++ < 100)
            StepRoomEventFrames(1);
        FailIf(
            substateGuard >= 100 ||
            ralph.Position != new Vector2(0x78, 0x33) ||
            roomEvent.CurrentCommandIndex != 11,
            "Ralph did not finish SPEED_100/SPEED_080 movement at high-byte " +
            "$33 and increment substate 1 to 2.");

        _player.WarpTo(new Vector2(0x6d, 0x18));
        _player.Face(Vector2I.Left);
        StepRoomEventFrames(1);
        FailIf(
            _player.FacingVector != Vector2I.Down,
            "ralphTurnLinkTowardSelf did not face down inside the right-side " +
            "$0c horizontal threshold.");
        _player.WarpTo(new Vector2(0x85, 0x18));
        StepRoomEventFrames(1);
        FailIf(
            _player.FacingVector != Vector2I.Left,
            "ralphTurnLinkTowardSelf lost its complemented left-side threshold.");

        int dialogueGuard = 0;
        while (!_dialogue.IsOpen && dialogueGuard++ < 40)
            StepRoomEventFrames(1);
        FailIf(
            dialogueGuard >= 40 ||
            _dialogue.CurrentMessage != DialogueBox.PlainText(
                ((CutsceneShowTextCommand)database.Commands[12]).Message) ||
            roomEvent.CurrentCommandIndex != 13,
            "Ralph did not display imported TX_2a20 after wait 30.");
        int frozenAnimation = ralph.CurrentAnimationFrame;
        int frozenCounter = roomEvent.Counter;
        StepRoomEventFrames(10);
        FailIf(
            ralph.CurrentAnimationFrame != frozenAnimation ||
            roomEvent.Counter != frozenCounter,
            "Ralph updated while wTextIsActive instead of using the source " +
            "state-0-only reduced interaction pass.");
        _dialogue.Close();

        int downMoveGuard = 0;
        while (roomEvent.CurrentCommandIndex != 16 && downMoveGuard++ < 40)
            StepRoomEventFrames(1);
        FailIf(
            downMoveGuard >= 40 || roomEvent.Substate != 0,
            "Ralph did not reset substate 0 and yield before movedown $38.");
        StepRoomEventFrames(1);
        FailIf(
            roomEvent.Counter != 0x38 ||
            ralph.FacingVector != Vector2I.Down,
            "Ralph did not set up SPEED_200 movedown $38 after the substate yield.");
        StepRoomEventFrames(55);
        FailIf(
            ralph.Position != new Vector2(0x78, 0xa1) ||
            roomEvent.Counter != 1,
            "Ralph's movedown $38 did not apply exactly 55 two-pixel moves.");
        StepRoomEventFrames(2);
        FailIf(
            !_saveData.HasRoomFlag(group, roomId, (byte)record.RoomFlag) ||
            roomEvent.CurrentCommandIndex != 18 ||
            !roomEvent.BlocksGameplay || !roomEvent.MenusDisabled,
            "ralphEndCutscene did not set room flag $40 before its final wait 30.");

        StepRoomEventFrames(31);
        FailIf(
            !roomEvent.HasState || !ralph.Active || !roomEvent.BlocksGameplay ||
            !roomEvent.MenusDisabled || !_player.CutsceneControlled ||
            _sound.ActiveMusic == record.Music ||
            roomEvent.CurrentCommandIndex != 20,
            "Ralph did not yield after resetting room music at the final " +
            "wait-30 boundary.");
        StepRoomEventFrames(1);
        FailIf(
            roomEvent.HasState || ralph.Active || roomEvent.BlocksGameplay ||
            roomEvent.MenusDisabled || _player.CutsceneControlled,
            "Ralph did not restore input/menu state and delete himself one " +
            "update after resetmusic.");

        CutsceneCommandTraceEntry[] starts = trace.Entries.Where(entry =>
            entry.Phase == CutsceneCommandTracePhase.Started).ToArray();
        FailIf(
            starts.Length != database.Commands.Count ||
            starts.Any(entry =>
                entry.Source.Script != "ralphSubid10Script" ||
                entry.Source.SourceLine <= 0),
            "Ralph-after-Cheval typed trace lost a command or source line.");

        LoadValidationRoom(group, roomId);
        FailIf(
            Ralph().Active || roomEvent.HasState,
            "Room flag $40 did not suppress Ralph on completed re-entry.");

        _saveData.SetGlobalFlag(record.TalkedGlobalFlag, originalTalked);
        _saveData.SetRoomFlag(
            group, roomId, (byte)record.RoomFlag, originalRoomFlag);
        _roomEvents.CommandTraceSink = null;
        _dialogue.Close();
        LoadValidationRoom(0, 0x11);

        GD.Print("Validated room 1:79 Ralph $37:$10: flag-$43/destination-$17 " +
            "predicate, source-state-0 update, wait 90, MUS_RALPH, exact " +
            "SPEED_200/100/080 movement, $cfc0 Link facing, substate-1 " +
            "flickering dust, TX_2a20 freeze, room flag $40, and input/music restore.");
    }

    private int ContextFrameCounter() => _entities.FrameCounter;
}
