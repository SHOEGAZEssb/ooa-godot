using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Runs INTERAC_BOY $3c:$03/$04 and INTERAC_BOY_2 $3f:$02 in their room-object
/// order. The original uses three native handlers and three scripts sharing
/// wTmpcfc0.genericCutscene.cfd1, so this deliberately remains a concurrent
/// native state machine rather than a flattened command stream.
/// </summary>
internal sealed class GraveyardGhostKidsEvent : IRoomEntryEvent
{

    private readonly RoomEventContext _context;
    private readonly GraveyardGhostKidsEventDatabase _database = new();
    private readonly GraveyardGhostKidsEventDatabaseEventRecord _record;
    private ChildLane? _red;
    private ChildLane? _green;
    private ChildLane? _blue;
    private bool _active;
    private int _signal;

    public GraveyardGhostKidsEvent(RoomEventContext context)
    {
        _context = context;
        _record = _database.Record;
    }

    public bool HasState => _active;
    public bool BlocksGameplay => _active;
    internal bool Completed => _context.Rooms.SaveData.HasRoomFlag(
        _record.Group, _record.Room, (byte)_record.RoomFlag);
    internal int Signal => _signal;
    internal ChildStage RedStage => _red?.Stage ?? ChildStage.Inactive;
    internal ChildStage GreenStage => _green?.Stage ?? ChildStage.Inactive;
    internal ChildStage BlueStage => _blue?.Stage ?? ChildStage.Inactive;
    internal int RedCounter => _red?.Counter ?? 0;
    internal int GreenCounter => _green?.Counter ?? 0;
    internal int BlueCounter => _blue?.Counter ?? 0;
    internal int GreenZFixed => _green?.ZFixed ?? 0;
    internal int BlueZFixed => _blue?.ZFixed ?? 0;
    internal GraveyardGhostKidsEventDatabaseEventRecord Record => _record;
    internal GraveyardGhostKidsEventDatabase Database => _database;

    public bool Matches(int group, OracleRoomData room) =>
        group == _record.Group && room.Id == _record.Room &&
        !_context.Rooms.SaveData.HasRoomFlag(
            _record.Group, _record.Room, (byte)_record.RoomFlag);

    public void Start(OracleRoomData room)
    {
        Cancel();
        if (room.Id != _record.Room || Completed)
            return;

        NpcCharacter redActor = _context.RequireNpc(
            _record.Group, _record.Room, _record.RedId, _record.RedSubId,
            "INTERAC_BOY red ghost-scene child");
        NpcCharacter greenActor = _context.RequireNpc(
            _record.Group, _record.Room, _record.GreenId, _record.GreenSubId,
            "INTERAC_BOY green ghost-scene child");
        NpcCharacter blueActor = _context.RequireNpc(
            _record.Group, _record.Room, _record.BlueId, _record.BlueSubId,
            "INTERAC_BOY_2 ghost-scene child");

        redActor.SetBasePalette(_record.RedPalette);
        PrepareActor(redActor, _record.RedInitialAnimation);
        PrepareActor(greenActor, _record.GreenInitialAnimation);
        PrepareActor(blueActor, _record.BlueInitialAnimation);

        _red = new ChildLane(ChildRole.Red, redActor)
        {
            Stage = ChildStage.WaitForSignal
        };
        _green = new ChildLane(ChildRole.Green, greenActor)
        {
            Stage = ChildStage.InitialWait,
            Counter = _record.GreenInitialWait
        };
        _blue = new ChildLane(ChildRole.Blue, blueActor)
        {
            Stage = ChildStage.WaitForSignal
        };
        _signal = 0;
        _active = true;
        _context.Player.BeginCutsceneControl();
    }

    public void UpdateFrame()
    {
        if (!_active || _red is null || _green is null || _blue is null)
            return;

        // parseGivenObjectData preserves this exact order in group0Map7bObjectData.
        UpdateRed(_red);
        UpdateGreen(_green);
        UpdateBlue(_blue);

        if (_red.Stage != ChildStage.Finished ||
            _green.Stage != ChildStage.Finished ||
            _blue.Stage != ChildStage.Finished)
        {
            return;
        }

        _context.Rooms.SaveData.SetRoomFlag(
            _record.Group, _record.Room, (byte)_record.RoomFlag);
        _context.Player.EndCutsceneControl();
        _active = false;
    }

    public void Cancel()
    {
        if (_active)
            _context.Player.EndCutsceneControl();
        RestoreActor(_red);
        RestoreActor(_green);
        RestoreActor(_blue);
        _red = null;
        _green = null;
        _blue = null;
        _signal = 0;
        _active = false;
    }

    private void UpdateRed(ChildLane lane)
    {
        switch (lane.Stage)
        {
            case ChildStage.WaitForSignal:
                if (_signal != 2)
                {
                    Animate(lane, 1);
                    return;
                }
                lane.Stage = ChildStage.RedFreezeWait;
                lane.Counter = _record.RedFreezeWait;
                return;

            case ChildStage.RedFreezeWait:
                if (--lane.Counter > 0)
                    return;
                ShowDialogue(lane, 2);
                return;

            case ChildStage.Dialogue:
                if (_context.DialogueOpen)
                    return;
                ContinueRedAfterDialogue(lane);
                return;

            case ChildStage.RedPostFirstWait:
                if (--lane.Counter > 0)
                    return;
                SetAnimation(lane, _record.RedLeftAnimation);
                lane.Stage = ChildStage.RedTurnLeftWait;
                lane.Counter = _record.RedFreezeWait;
                return;

            case ChildStage.RedTurnLeftWait:
                if (--lane.Counter > 0)
                    return;
                ShowDialogue(lane, 3);
                return;

            case ChildStage.RedPostSecondWait:
                if (--lane.Counter > 0)
                    return;
                SetAnimation(lane, _record.RedUpAnimation);
                lane.Stage = ChildStage.RedTurnUpWait;
                lane.Counter = _record.RedFreezeWait;
                return;

            case ChildStage.RedTurnUpWait:
                if (--lane.Counter > 0)
                    return;
                ShowDialogue(lane, 4);
                return;

            case ChildStage.RedFinalWait:
                if (--lane.Counter > 0)
                    return;
                _signal = 3;
                BeginShake(lane);
                return;

            case ChildStage.Shaking:
                UpdateShake(lane);
                return;
            case ChildStage.Fleeing:
                UpdateFlee(lane);
                return;
            case ChildStage.FleeEndPending:
                FinishFlee(lane);
                return;
        }
    }

    private void UpdateGreen(ChildLane lane)
    {
        switch (lane.Stage)
        {
            case ChildStage.InitialWait:
                Animate(lane, 1);
                if (--lane.Counter <= 0)
                    BeginJump(lane);
                return;
            case ChildStage.Jumping:
                UpdateJump(lane);
                return;
            case ChildStage.InstallPostJumpWait:
                lane.Stage = ChildStage.PostJumpWait;
                lane.Counter = _record.PostJumpWait;
                Animate(lane, 1);
                return;
            case ChildStage.PostJumpWait:
                if (--lane.Counter <= 0)
                    ShowDialogue(lane, 0);
                Animate(lane, 1);
                return;
            case ChildStage.Dialogue:
                if (_context.DialogueOpen)
                {
                    Animate(lane, 1);
                    return;
                }
                lane.Stage = ChildStage.PostDialogueWait;
                lane.Counter = _record.GreenPostTextWait;
                Animate(lane, 1);
                return;
            case ChildStage.PostDialogueWait:
                if (--lane.Counter <= 0)
                {
                    _signal = 1;
                    lane.Stage = ChildStage.WaitForSignal;
                }
                Animate(lane, 1);
                return;
            case ChildStage.WaitForSignal:
                if (_signal == 3)
                    BeginShake(lane);
                else
                    Animate(lane, 1);
                return;
            case ChildStage.Shaking:
                UpdateShake(lane);
                return;
            case ChildStage.Fleeing:
                UpdateFlee(lane);
                return;
            case ChildStage.FleeEndPending:
                FinishFlee(lane);
                return;
        }
    }

    private void UpdateBlue(ChildLane lane)
    {
        switch (lane.Stage)
        {
            case ChildStage.WaitForSignal:
                if (_signal == 1)
                {
                    // @@substate0 animates before testing cfd1.
                    Animate(lane, 1);
                    BeginJump(lane);
                }
                else if (_signal == 3)
                {
                    BeginShake(lane);
                }
                else
                {
                    Animate(lane, 1);
                }
                return;
            case ChildStage.Jumping:
                UpdateJump(lane);
                return;
            case ChildStage.InstallPostJumpWait:
                lane.Stage = ChildStage.PostJumpWait;
                lane.Counter = _record.PostJumpWait;
                Animate(lane, 1);
                return;
            case ChildStage.PostJumpWait:
                if (--lane.Counter <= 0)
                    ShowDialogue(lane, 1);
                Animate(lane, 1);
                return;
            case ChildStage.Dialogue:
                if (_context.DialogueOpen)
                {
                    Animate(lane, 1);
                    return;
                }
                _signal = 2;
                lane.Stage = ChildStage.WaitForSignal;
                Animate(lane, 1);
                return;
            case ChildStage.Shaking:
                UpdateShake(lane);
                return;
            case ChildStage.Fleeing:
                UpdateFlee(lane);
                return;
            case ChildStage.FleeEndPending:
                FinishFlee(lane);
                return;
        }
    }

    private void ContinueRedAfterDialogue(ChildLane lane)
    {
        switch (lane.DialogueIndex)
        {
            case 2:
                lane.Stage = ChildStage.RedPostFirstWait;
                lane.Counter = _record.RedPostTextWait;
                break;
            case 3:
                lane.Stage = ChildStage.RedPostSecondWait;
                lane.Counter = _record.RedPostTextWait;
                break;
            case 4:
                lane.Stage = ChildStage.RedFinalWait;
                lane.Counter = _record.RedFinalWait;
                break;
            default:
                throw new InvalidOperationException(
                    $"Unexpected red child dialogue index {lane.DialogueIndex}.");
        }
    }

    private void BeginJump(ChildLane lane)
    {
        lane.Stage = ChildStage.Jumping;
        lane.ZFixed = 0;
        lane.SpeedZ = _record.JumpSpeedZ;
        _context.Sound.PlaySound(_record.JumpSound);
    }

    private void UpdateJump(ChildLane lane)
    {
        if (!OracleObjectMath.UpdateSpeedZ(
            ref lane.ZFixed, ref lane.SpeedZ, _record.JumpGravity))
        {
            lane.Actor.SetScriptDrawOffset(new Vector2(0, lane.ZFixed >> 8));
            return;
        }

        lane.Actor.SetScriptDrawOffset(Vector2.Zero);
        lane.Stage = ChildStage.InstallPostJumpWait;
    }

    private void ShowDialogue(ChildLane lane, int index)
    {
        GraveyardGhostKidsEventDatabaseTextRecord text = _database.Texts[index];
        string expectedActor = lane.Role.ToString();
        if (text.Order != index || text.Actor != expectedActor)
        {
            throw new InvalidOperationException(
                $"Spirit's Grave text {index} belongs to {text.Actor}, not {expectedActor}.");
        }
        lane.DialogueIndex = index;
        lane.Stage = ChildStage.Dialogue;
        _context.ShowDialogue(text.Message);
    }

    private void BeginShake(ChildLane lane)
    {
        lane.Stage = ChildStage.Shaking;
        lane.Counter = _record.ShakeFrames;
        UpdateShake(lane);
    }

    private void UpdateShake(ChildLane lane)
    {
        int xOffset = (_context.Entities.NextRandomValue() & 1) - 1;
        lane.PrecisePosition = new Vector2(
            lane.BaseX + xOffset,
            lane.PrecisePosition.Y);
        lane.Actor.SetStatePosition(lane.PrecisePosition);
        if (--lane.Counter > 0)
            return;

        lane.Stage = ChildStage.Fleeing;
        lane.Counter = _record.FleeCounter;
        SetAnimation(lane, _record.FleeAnimation);
        _context.Sound.PlaySound(_record.FleeSound);
        // interactionAnimateBasedOnSpeed calls interactionAnimate three times
        // at SPEED_200 while counter2 is nonzero.
        Animate(lane, 3);
    }

    private void UpdateFlee(ChildLane lane)
    {
        lane.Counter--;
        if (lane.Counter > 0)
        {
            lane.PrecisePosition +=
                OracleObjectMath.StrictCardinalVector(_record.FleeAngle) *
                (_record.FleeSpeed / 40.0f);
            lane.Actor.SetStatePosition(
                OracleObjectMath.ToPixelPosition(lane.PrecisePosition));
            Animate(lane, 3);
            return;
        }

        // counter2's zero update does not dispatch scriptend and only performs
        // the unconditional first animation call.
        Animate(lane, 1);
        lane.Stage = ChildStage.FleeEndPending;
    }

    private static void FinishFlee(ChildLane lane)
    {
        // scriptend is processed on the following update, after which
        // boyRunSubid03 animates once and performs the screen-boundary check.
        lane.Actor.AdvanceAnimationUpdates(1);
        if (OracleObjectMath.IsInsideOriginalScreenBoundary(lane.Actor.Position))
        {
            throw new InvalidOperationException(
                $"Spirit's Grave {lane.Role} child remained on-screen after moveright.");
        }
        lane.Actor.SetActive(false);
        lane.Stage = ChildStage.Finished;
    }

    private static void RestoreActor(ChildLane? lane)
    {
        if (lane is null)
            return;
        lane.Actor.SetScriptDrawOffset(Vector2.Zero);
        lane.Actor.SetAnimationRate(1.0f);
    }

    private static string Animation(NpcCharacter actor, int animation) => animation switch
    {
        0 => actor.Record.UpAnimation,
        1 => actor.Record.RightAnimation,
        2 => actor.Record.DownAnimation,
        3 => actor.Record.LeftAnimation,
        _ => throw new InvalidOperationException(
            $"Unsupported child animation ${animation:x2}.")
    };

    private static void PrepareActor(NpcCharacter actor, int animation)
    {
        actor.SetScriptDrawOffset(Vector2.Zero);
        actor.SetScriptAnimation(Animation(actor, animation));
        actor.SetAnimationRate(0.0f);
    }

    private static void SetAnimation(ChildLane lane, int animation) =>
        lane.Actor.SetScriptAnimation(Animation(lane.Actor, animation));

    private static void Animate(ChildLane lane, int updates) =>
        lane.Actor.AdvanceAnimationUpdates(updates);
}
