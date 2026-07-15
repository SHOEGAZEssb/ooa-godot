using Godot;
using System;

namespace oracleofages;

/// <summary>Runs Ralph's one-shot portal departure in room $0:$39.</summary>
internal sealed class RalphPortalEvent : IRoomEvent
{
    private enum Stage
    {
        None,
        WaitingForScroll,
        IntroDelay,
        Text,
        PostText,
        SetSpeed,
        SetAngle,
        StartMovement,
        Moving,
        MovementFinished,
        SetFlickerCounter,
        PlayPortalSound,
        BeginFlicker,
        Flickering
    }

    private readonly RoomEventContext _context;
    private readonly RalphPortalEventDatabase.RalphPortalEventRecord _record =
        new RalphPortalEventDatabase().Record;
    private Stage _stage;
    private NpcCharacter? _ralph;
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

    public bool HasState => _stage != Stage.None;
    public bool BlocksGameplay => HasState;
    internal bool WaitingForScroll => _stage == Stage.WaitingForScroll;
    internal bool Flickering => _stage == Stage.Flickering;
    internal int Counter => _counter;
    internal bool Completed => _context.Rooms.SaveData.HasGlobalFlag(_record.GlobalFlag);

    public bool Matches(int group, OracleRoomData room) =>
        group == _record.Group && room.Id == _record.Room;

    public void Start()
    {
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

        _stage = Stage.WaitingForScroll;
        _counter = 0;
    }

    public void UpdateFrame()
    {
        switch (_stage)
        {
            case Stage.WaitingForScroll:
                // Destination objects do not update during a screen scroll.
                // On the first object update afterward, the script disables
                // input and installs its 40-frame counter.
                _context.Player.BeginCutsceneControl();
                BeginWait(Stage.IntroDelay, _record.IntroDelayFrames);
                break;
            case Stage.IntroDelay:
                if (CountDown())
                {
                    _stage = Stage.Text;
                    _context.ShowDialogue(_record.Text);
                }
                break;
            case Stage.Text:
                if (!_context.DialogueOpen)
                    BeginWait(Stage.PostText, _record.PostTextFrames);
                break;
            case Stage.PostText:
                if (CountDown())
                {
                    _ralph!.SetScriptAnimation(_record.MovementAnimation);
                    _stage = Stage.SetSpeed;
                }
                break;
            case Stage.SetSpeed:
                // setanimation, setspeed, setangle, and applyspeed each
                // consume one interaction-script update.
                _stage = Stage.SetAngle;
                break;
            case Stage.SetAngle:
                _stage = Stage.StartMovement;
                break;
            case Stage.StartMovement:
                BeginWait(Stage.Moving, _record.MovementCounter);
                break;
            case Stage.Moving:
                // interactionRunScript decrements counter2 first and applies
                // speed only while the result is nonzero. applyspeed $11 thus
                // advances Ralph 16 pixels, from x=$18 to the portal at $28.
                _counter--;
                if (_counter > 0)
                    _ralph!.Position += MovementDelta();
                else
                    _stage = Stage.MovementFinished;
                break;
            case Stage.MovementFinished:
                _ralph!.SetScriptAnimation(_record.PortalAnimation);
                _stage = Stage.SetFlickerCounter;
                break;
            case Stage.SetFlickerCounter:
                _counter = _record.FlickerFrames;
                _stage = Stage.PlayPortalSound;
                break;
            case Stage.PlayPortalSound:
                // SND_MYSTERY_SEED is deferred with the audio system, but its
                // script command still occupies this update.
                _stage = Stage.BeginFlicker;
                break;
            case Stage.BeginFlicker:
                _stage = Stage.Flickering;
                UpdateFlickerFrame();
                break;
            case Stage.Flickering:
                UpdateFlickerFrame();
                break;
        }
    }

    public void Cancel()
    {
        _ralph = null;
        _stage = Stage.None;
    }

    private void BeginWait(Stage stage, int frames)
    {
        _stage = stage;
        _counter = frames;
    }

    private bool CountDown()
    {
        if (_counter > 0)
            _counter--;
        return _counter == 0;
    }

    private void UpdateFlickerFrame()
    {
        // objectFlickerVisibility with b=$01 uses wFrameCounter bit 0.
        _ralph!.Visible = (_context.Entities.FrameCounter & 0x01) != 0;
        _counter--;
        if (_counter == 0)
            Finish();
    }

    private void Finish()
    {
        _context.Rooms.SaveData.SetGlobalFlag(_record.GlobalFlag);
        _ralph!.SetActive(false);
        _stage = Stage.None;
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
