using Godot;
using System;

namespace oracleofages;

/// <summary>Runs the one-shot first arrival in past room $1:$39.</summary>
internal sealed class EnterPastEvent : IRoomEvent
{
    internal enum EventStage
    {
        Inactive,
        Begin,
        InstallIntroWait,
        IntroWait,
        PreJumpWait,
        BeginJump,
        Jump,
        InstallPostJumpWait,
        PostJumpWait,
        Dialogue,
        PostTextWait,
        StartFirstDown,
        FirstDown,
        Right,
        SecondDown,
        StartSlowDown,
        SlowDown,
        StartFinalDown,
        FinalDown
    }

    private readonly RoomEventContext _context;
    private readonly EnterPastEventDatabase.EnterPastEventRecord _record =
        new EnterPastEventDatabase().Record;
    private readonly RoomEventTimeline _timeline = new();
    private NpcCharacter? _villager;
    private Vector2 _precisePosition;
    private EventStage _stage;
    private int _counter;
    private int _zFixed;
    private int _speedZ;
    private int _speed;

    public EnterPastEvent(RoomEventContext context)
    {
        _context = context;
        if (_record.GlobalFlag != OracleSaveData.GlobalFlagEnterPastCutsceneDone)
        {
            throw new InvalidOperationException(
                $"Enter-past event uses global flag ${_record.GlobalFlag:x2}, expected $41.");
        }
        if (_record.JumpSound != OracleSoundEngine.SndJump)
        {
            throw new InvalidOperationException(
                $"Enter-past event uses jump sound ${_record.JumpSound:x2}, expected $53.");
        }
    }

    public bool HasState => _timeline.Active;
    public bool BlocksGameplay => HasState;
    internal bool Completed =>
        _context.Rooms.SaveData.HasGlobalFlag(_record.GlobalFlag);
    internal EventStage Stage => _stage;
    internal int Counter => _counter;
    internal int ZFixed => _zFixed;
    internal EnterPastEventDatabase.EnterPastEventRecord Record => _record;

    public bool Matches(int group, OracleRoomData room) =>
        group == _record.Group && room.Id == _record.Room;

    public void Start()
    {
        ResetState();
        _villager = _context.RequireNpc(
            _record.Group,
            _record.Room,
            _record.InteractionId,
            _record.SubId,
            "INTERAC_MALE_VILLAGER");

        // The leading jumpifglobalflagset redirects to stubScript, whose
        // scriptend makes @runSubid0d delete the interaction on re-entry.
        if (Completed)
        {
            _villager.SetActive(false);
            return;
        }

        _precisePosition = _villager.Position;
        _stage = EventStage.Begin;
        BuildTimeline();
    }

    public void UpdateFrame()
    {
        if (_villager is null || !_timeline.Active)
            return;

        _timeline.AdvanceFrame();
        if (!_timeline.Active)
            return;

        // @runSubid0d calls interactionAnimateBasedOnSpeed after its script.
        // It always advances once, plus once more at SPEED_100 while a
        // counter2 movement is active.
        int animationUpdates = 1;
        if (_counter != 0 && _speed >= _record.FastSpeed)
            animationUpdates++;
        _villager.AdvanceAnimationUpdates(animationUpdates);
        _villager.PreventPlayerPassing(_context.Player);
        _villager.UpdateDrawPriority(_context.Player.Position);
    }

    public void Cancel()
    {
        if (_villager is not null)
        {
            _villager.SetScriptDrawOffset(Vector2.Zero);
            _villager.SetAnimationRate(1.0f);
        }
        _villager = null;
        ResetState();
    }

    private void BuildTimeline()
    {
        _timeline.Clear();
        _timeline.Do(() =>
        {
            // State 0 runs jumpifglobalflagset and
            // setdisabledobjectsto11 in the interaction's first update.
            _context.Player.BeginCutsceneControl();
            _villager!.SetAnimationRate(0.0f);
            _stage = EventStage.InstallIntroWait;
        });
        _timeline.Do(() =>
        {
            _counter = _record.IntroWaitFrames;
            _stage = EventStage.IntroWait;
        });
        _timeline.Wait(
            _record.IntroWaitFrames,
            counterChanged: remaining => _counter = remaining,
            elapsed: () =>
            {
                // disableinput continues directly into wait 40 on the
                // update where the original counter1 reaches zero.
                _context.Player.BeginCutsceneControl();
                _counter = _record.PreJumpWaitFrames;
                _stage = EventStage.PreJumpWait;
            });
        _timeline.Wait(
            _record.PreJumpWaitFrames,
            counterChanged: remaining => _counter = remaining,
            elapsed: () => _stage = EventStage.BeginJump);
        _timeline.WaitUntil(AdvanceJump);
        _timeline.Do(() =>
        {
            _counter = _record.PostJumpWaitFrames;
            _stage = EventStage.PostJumpWait;
        });
        _timeline.Wait(
            _record.PostJumpWaitFrames,
            counterChanged: remaining => _counter = remaining,
            elapsed: () =>
            {
                _context.ShowDialogue(_record.Text);
                _stage = EventStage.Dialogue;
            });
        _timeline.WaitUntil(
            () => !_context.DialogueOpen,
            completed: () =>
            {
                // The script resumes and installs wait 30 on the first
                // interaction update after the textbox closes.
                _counter = _record.PostTextWaitFrames;
                _stage = EventStage.PostTextWait;
            });
        _timeline.Wait(
            _record.PostTextWaitFrames,
            counterChanged: remaining => _counter = remaining,
            elapsed: () =>
            {
                _speed = _record.FastSpeed;
                _stage = EventStage.StartFirstDown;
            });
        _timeline.Do(() =>
        {
            StartMove(Vector2I.Down, _record.FirstDownCounter, EventStage.FirstDown);
        });
        EnqueueMove(
            Vector2I.Down,
            _record.FirstDownCounter,
            () => StartMove(Vector2I.Right, _record.RightCounter, EventStage.Right));
        EnqueueMove(
            Vector2I.Right,
            _record.RightCounter,
            () => StartMove(Vector2I.Down, _record.SecondDownCounter, EventStage.SecondDown));
        EnqueueMove(
            Vector2I.Down,
            _record.SecondDownCounter,
            () =>
            {
                _speed = _record.SlowSpeed;
                _stage = EventStage.StartSlowDown;
            });
        _timeline.Do(() =>
        {
            _counter = _record.SlowDownCounter;
            _stage = EventStage.SlowDown;
        });
        EnqueueMove(
            Vector2I.Down,
            _record.SlowDownCounter,
            () =>
            {
                _speed = _record.FastSpeed;
                _stage = EventStage.StartFinalDown;
            });
        _timeline.Do(() =>
        {
            _counter = _record.FinalDownCounter;
            _stage = EventStage.FinalDown;
        });
        EnqueueMove(Vector2I.Down, _record.FinalDownCounter, Finish);
    }

    private bool AdvanceJump()
    {
        if (_stage == EventStage.BeginJump)
        {
            _zFixed = 0;
            _speedZ = _record.JumpSpeedZ;
            _context.Sound.PlaySound(_record.JumpSound);
        }

        bool landed = OracleObjectMath.UpdateSpeedZ(
            ref _zFixed, ref _speedZ, _record.JumpGravity);
        _villager!.SetScriptDrawOffset(new Vector2(0, _zFixed >> 8));
        if (landed)
        {
            _villager.SetScriptDrawOffset(Vector2.Zero);
            _stage = EventStage.InstallPostJumpWait;
        }
        else
        {
            _stage = EventStage.Jump;
        }
        return landed;
    }

    private void StartMove(Vector2I direction, int counter, EventStage stage)
    {
        _counter = counter;
        _stage = stage;
        _villager!.SetScriptAnimation(direction == Vector2I.Right
            ? _record.RightAnimation
            : _record.DownAnimation);
    }

    private void EnqueueMove(Vector2I direction, int counter, Action completed)
    {
        _timeline.Wait(
            counter,
            counterChanged: remaining =>
            {
                _counter = remaining;
                if (remaining == 0)
                    return;
                float pixels = SpeedPixelsPerUpdate(_speed);
                _precisePosition += (Vector2)direction * pixels;
                _villager!.Position = OracleObjectMath.ToPixelPosition(_precisePosition);
            },
            elapsed: completed);
    }

    private void Finish()
    {
        _context.Rooms.SaveData.SetGlobalFlag(_record.GlobalFlag);
        _villager!.SetScriptDrawOffset(Vector2.Zero);
        _villager.SetActive(false);
        _context.Player.EndCutsceneControl();
        _stage = EventStage.Inactive;
        _counter = 0;
        _zFixed = 0;
        _speedZ = 0;
    }

    private void ResetState()
    {
        _timeline.Clear();
        _stage = EventStage.Inactive;
        _counter = 0;
        _zFixed = 0;
        _speedZ = 0;
        _speed = 0;
    }

    private static float SpeedPixelsPerUpdate(int speed) => speed / 40.0f;
}
