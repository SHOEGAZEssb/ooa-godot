using Godot;
using System;

namespace oracleofages;

/// <summary>Runs Ralph's one-shot portal departure in room $0:$39.</summary>
internal sealed class RalphPortalEvent : IRoomEvent
{
    private readonly RoomEventContext _context;
    private readonly RalphPortalEventDatabase.RalphPortalEventRecord _record =
        new RalphPortalEventDatabase().Record;
    private readonly RoomEventTimeline _timeline = new();
    private NpcCharacter? _ralph;
    private bool _waitingForScroll;
    private bool _flickering;
    private int _counter;

    public RalphPortalEvent(RoomEventContext context)
    {
        _context = context;
        if (_record.GlobalFlag != OracleSaveData.GlobalFlagRalphEnteredPortal)
        {
            throw new InvalidOperationException(
                $"Ralph portal event uses global flag ${_record.GlobalFlag:x2}, expected $40.");
        }
    }

    public bool HasState => _waitingForScroll || _timeline.Active;
    public bool BlocksGameplay => HasState;
    internal bool WaitingForScroll => _waitingForScroll;
    internal bool Flickering => _flickering;
    internal int Counter => _counter;
    internal bool Completed => _context.Rooms.SaveData.HasGlobalFlag(_record.GlobalFlag);

    public bool Matches(int group, OracleRoomData room) =>
        group == _record.Group && room.Id == _record.Room;

    public void Start()
    {
        _timeline.Clear();
        _waitingForScroll = false;
        _flickering = false;
        _counter = 0;
        _ralph = _context.RequireNpc(
            _record.Group,
            _record.Room,
            _record.InteractionId,
            _record.SubId,
            "INTERAC_RALPH");

        // @initSubid0d deletes Ralph unless wScreenTransitionDirection is
        // DIR_RIGHT ($01). It also deletes him after the one-shot flag is set.
        Vector2I requiredDirection = DirectionFromOriginalValue(_record.EntryDirection);
        if (Completed || !_context.Transitions.ScrollActive ||
            _context.Transitions.ScrollDirection != requiredDirection)
        {
            _ralph.SetActive(false);
            return;
        }

        _waitingForScroll = true;
    }

    public void UpdateFrame()
    {
        if (_waitingForScroll)
        {
            // Destination objects do not update during a screen scroll. On
            // the first object update afterward, the script disables input
            // and installs its 40-frame counter.
            _waitingForScroll = false;
            _context.Player.BeginCutsceneControl();
            BuildTimeline();
            return;
        }

        _timeline.AdvanceFrame();
    }

    public void Cancel()
    {
        _ralph = null;
        _timeline.Clear();
        _waitingForScroll = false;
        _flickering = false;
        _counter = 0;
    }

    private void BuildTimeline()
    {
        _timeline.Clear();
        _counter = _record.IntroDelayFrames;
        _timeline.Wait(
            _record.IntroDelayFrames,
            counterChanged: remaining => _counter = remaining,
            elapsed: () => _context.ShowDialogue(_record.Text));
        _timeline.WaitUntil(
            () => !_context.DialogueOpen,
            completed: () => _counter = _record.PostTextFrames);
        _timeline.Wait(
            _record.PostTextFrames,
            counterChanged: remaining => _counter = remaining,
            elapsed: () => _ralph!.SetScriptAnimation(_record.MovementAnimation));

        // setanimation, setspeed, setangle, and applyspeed each consume one
        // interaction-script update. Speed and angle are applied by the move
        // callback, so these two command slots intentionally have no mutation.
        _timeline.Yield();
        _timeline.Yield();
        _timeline.Do(() => _counter = _record.MovementCounter);

        // interactionRunScript decrements counter2 first and applies speed
        // only while the result is nonzero. applyspeed $11 thus advances Ralph
        // 16 pixels, from x=$18 to the portal at $28.
        _timeline.Wait(
            _record.MovementCounter,
            counterChanged: remaining =>
            {
                _counter = remaining;
                if (remaining > 0)
                    _ralph!.Position += MovementDelta();
            });
        _timeline.Do(() => _ralph!.SetScriptAnimation(_record.PortalAnimation));
        _timeline.Do(() => _counter = _record.FlickerFrames);

        // SND_MYSTERY_SEED is deferred with the audio system, but its script
        // command still occupies this update.
        _timeline.Yield();
        _timeline.Wait(
            _record.FlickerFrames,
            counterChanged: remaining =>
            {
                _flickering = true;
                _counter = remaining;
                // objectFlickerVisibility with b=$01 uses wFrameCounter bit 0.
                _ralph!.Visible = (_context.Entities.FrameCounter & 0x01) != 0;
            },
            elapsed: Finish);
    }

    private void Finish()
    {
        _context.Rooms.SaveData.SetGlobalFlag(_record.GlobalFlag);
        _ralph!.SetActive(false);
        _timeline.Clear();
        _waitingForScroll = false;
        _flickering = false;
        _context.Player.EndCutsceneControl();
    }

    private Vector2 MovementDelta()
    {
        if (_record.Speed != 0x28)
        {
            throw new InvalidOperationException(
                $"Unsupported Ralph object speed ${_record.Speed:x2}; expected SPEED_100 ($28).");
        }
        return OracleObjectMath.StrictCardinalVector(_record.Angle);
    }

    private static Vector2I DirectionFromOriginalValue(int direction) => direction switch
    {
        0 => Vector2I.Up,
        1 => Vector2I.Right,
        2 => Vector2I.Down,
        3 => Vector2I.Left,
        _ => throw new InvalidOperationException(
            $"Unsupported wScreenTransitionDirection value ${direction:x2}.")
    };
}
