using Godot;
using System;

namespace oracleofages;

/// <summary>
/// INTERAC_RALPH $37:$03 and ralphSubid03Script after giving Rafton the rope.
/// </summary>
internal sealed class RalphAfterRaftonEvent :
    InteractiveCutsceneCommandHost,
    IRoomEntryEvent,
    ICutsceneCommandHost
{
    private const string ActorName = "Ralph";

    private readonly RoomEventContext _context;
    private readonly RalphAfterRaftonEventDatabase _database = new();
    private readonly RalphAfterRaftonEventRecord _record;
    private readonly CutsceneCommandRunner _runner;
    private RalphAfterRaftonCharacter? _ralph;
    private Vector2 _precisePosition;
    private int _substate;
    private int _counter;
    private int _nativeDirection;
    private int _speed;
    private int _angle;
    private int _zFixed;
    private int _speedZ;
    private bool _active;
    private bool _menusDisabled;

    internal RalphAfterRaftonEvent(RoomEventContext context)
    {
        _context = context;
        _record = _database.Record;
        _runner = new CutsceneCommandRunner(this);
    }

    public RoomEventContext Context => _context;
    protected override RoomEventContext InputContext => _context;
    public bool HasState => _active;
    public bool BlocksGameplay => InputLeaseHeld;
    internal RalphAfterRaftonEventDatabase Database => _database;
    internal RalphAfterRaftonCharacter? Actor => _ralph;
    internal int Substate => _substate;
    internal int Counter => _counter;
    internal int NativeDirection => _nativeDirection;
    internal int ZFixed => _zFixed;
    internal int SpeedZ => _speedZ;
    internal int CurrentCommandIndex =>
        _runner.CurrentCommand?.Source.CommandIndex ?? -1;
    internal int ScriptCounter => _runner.Counter;
    internal bool MenusDisabled => _menusDisabled;

    public bool Matches(int group, OracleRoomData room) =>
        group == _record.Group && room.Id == _record.Room;

    public void Start(OracleRoomData room)
    {
        Cancel();
        if (_context.Rooms.ActiveGroup != _record.Group ||
            room.Id != _record.Room)
        {
            throw new InvalidOperationException(
                $"INTERAC_RALPH $37:$03 cannot initialize in " +
                $"{_context.Rooms.ActiveGroup:x}:{room.Id:x2}.");
        }

        NpcCharacter npc = _context.RequireNpc(
            _record.Group,
            _record.Room,
            _record.InteractionId,
            _record.SubId,
            "INTERAC_RALPH $37:$03");
        RalphAfterRaftonCharacter ralph =
            npc as RalphAfterRaftonCharacter ??
            throw new InvalidOperationException(
                "Room 1:97 instantiated Ralph $37:$03 without its native actor.");

        OracleSaveData save = _context.Rooms.SaveData;
        if (!save.HasGlobalFlag(_record.RequiredGlobalFlag) ||
            save.HasRoomFlag(
                _record.Group, _record.Room, (byte)_record.RoomFlag))
        {
            ralph.SetActive(false);
            return;
        }

        _ralph = ralph;
        _precisePosition = ralph.Position;
        _substate = 0;
        _counter = _record.LookCounter;
        _nativeDirection = _record.InitialDirection;
        _speed = 0;
        _angle = 0;
        _zFixed = 0;
        _speedZ = 0;
        _active = true;
        _menusDisabled = _record.MenuDisabled != 0;
        SetInputEnabled(enabled: false);

        // ralphState0 returns after initialization instead of dispatching
        // ralphSubid03, so this imported count is normally zero.
        for (int update = 0; update < _record.InitialNativeUpdates; update++)
            UpdateFrame();
    }

    public void UpdateFrame()
    {
        if (!_active || _ralph is null)
            return;

        switch (_substate)
        {
            case 0:
                UpdateLookingAround();
                return;
            case 1:
                UpdatePreJumpWait();
                return;
            case 2:
                UpdateJump();
                return;
            case 3:
                UpdateLandingWait();
                return;
            case 4:
                UpdatePostTextWait();
                return;
            case 5:
                UpdateApproach();
                return;
            case 6:
                UpdateAlignmentWait();
                return;
            case 7:
                UpdateAlignment();
                return;
            case 8:
                UpdateScript();
                return;
            default:
                throw new InvalidOperationException(
                    $"INTERAC_RALPH $37:$03 entered invalid substate ${_substate:x2}.");
        }
    }

    public void Cancel()
    {
        ReleaseInputControl();
        if (_ralph is not null)
        {
            _ralph.SetScriptDrawOffset(Vector2.Zero);
            _ralph.SetAnimationRate(1.0f);
        }
        _runner.Clear();
        _ralph = null;
        _precisePosition = Vector2.Zero;
        _substate = 0;
        _counter = 0;
        _nativeDirection = 0;
        _speed = 0;
        _angle = 0;
        _zFixed = 0;
        _speedZ = 0;
        _active = false;
        _menusDisabled = false;
    }

    public override bool HasActorBinding(CutsceneActorId actor) =>
        actor.Value == ActorName;

    public override void SetInputEnabled(bool enabled)
    {
        base.SetInputEnabled(enabled);
        if (enabled)
            _menusDisabled = false;
    }

    public override void ShowText(int textId, string message)
    {
        string expected = textId switch
        {
            0x2a0b => ((CutsceneShowTextCommand)_database.Commands[3]).Message,
            0x2a06 => ((CutsceneShowTextCommand)_database.Commands[7]).Message,
            _ => throw new InvalidOperationException(
                $"ralphSubid03Script requested unimported TX_{textId:x4}.")
        };
        if (message != expected)
        {
            throw new InvalidOperationException(
                $"ralphSubid03Script TX_{textId:x4} payload changed.");
        }
        _context.ShowDialogue(message);
    }

    public override void SetActorAnimation(
        string actor,
        int animation,
        string encodedAnimation)
    {
        RalphAfterRaftonCharacter ralph = RequireRalph(actor);
        if (encodedAnimation != _record.Animation(animation))
        {
            throw new InvalidOperationException(
                $"ralphSubid03Script animation ${animation:x2} changed.");
        }
        ralph.SetRalphAnimation(animation, _record);
    }

    public override void SetActorMovementAnimation(
        string actor,
        int angle,
        string encodedAnimation)
    {
        if (angle != 0x00 || encodedAnimation != _record.Animation(0))
        {
            throw new InvalidOperationException(
                $"ralphSubid03Script requested unsupported movement angle ${angle:x2}.");
        }
        RequireRalph(actor).SetRalphAnimation(0, _record);
    }

    public override void MoveActorAtSpeed(string actor, int speed, int angle)
    {
        if (speed != _record.Speed200 || angle != 0x00)
        {
            throw new InvalidOperationException(
                $"ralphSubid03Script requested unsupported movement " +
                $"${speed:x2}/${angle:x2}.");
        }
        RalphAfterRaftonCharacter ralph = RequireRalph(actor);
        ralph.SetStatePosition(OracleObjectMovement.Shared.ApplySpeed(
            ref _precisePosition, speed, angle));
    }

    public override void PlaySound(int sound)
    {
        if (sound != _record.FadeSound)
        {
            throw new InvalidOperationException(
                $"ralphSubid03Script requested unexpected sound ${sound:x2}.");
        }
        base.PlaySound(sound);
    }

    public override void OrRoomFlag(int flag)
    {
        if (flag != _record.RoomFlag)
        {
            throw new InvalidOperationException(
                $"ralphSubid03Script cannot OR room flag ${flag:x2}.");
        }
        _context.Rooms.SaveData.SetRoomFlag(
            _record.Group, _record.Room, (byte)flag);
    }

    public override void ScriptEnded()
    {
        if (!_context.Rooms.SaveData.HasRoomFlag(
                _record.Group, _record.Room, (byte)_record.RoomFlag) ||
            InputLeaseHeld || _menusDisabled)
        {
            throw new InvalidOperationException(
                "Ralph-after-Rafton script ended before restoring input/menu state.");
        }

        // ralphSubid03 writes both active-music bytes and calls playSound
        // unconditionally, cancelling the still-running fast fade.
        _context.Sound.PlaySound(_record.CompletionMusic);
        RequireRalph(ActorName).SetActive(false);
        _active = false;
    }

    private void UpdateLookingAround()
    {
        if (--_counter == 0)
        {
            _counter = _record.PostLookWait;
            SetNativeAnimation(2);
            _substate = 1;
            return;
        }

        if ((_context.Entities.FrameCounter & _record.LookFrameMask) != 0)
            return;
        _nativeDirection ^= _record.LookDirectionXor;
        SetNativeAnimation(_nativeDirection);
    }

    private void UpdatePreJumpWait()
    {
        if (--_counter != 0)
            return;
        _substate = 2;
        _zFixed = 0;
        _speedZ = _record.JumpSpeedZ;
        _context.Sound.PlaySound(_record.JumpSound);
    }

    private void UpdateJump()
    {
        RalphAfterRaftonCharacter ralph = RequireRalph(ActorName);
        ralph.AdvanceRalphAnimation(1);
        if (!OracleObjectMath.UpdateSpeedZ(
                ref _zFixed, ref _speedZ, _record.JumpGravity))
        {
            ralph.SetScriptDrawOffset(new Vector2(0, _zFixed >> 8));
            return;
        }

        ralph.SetScriptDrawOffset(Vector2.Zero);
        _substate = 3;
        _counter = _record.LandingWait;
    }

    private void UpdateLandingWait()
    {
        if (--_counter != 0)
            return;
        _counter = _record.PostTextWait;
        _substate = 4;
        _context.ShowDialogue(_record.NativeText);
    }

    private void UpdatePostTextWait()
    {
        if (--_counter != 0)
            return;
        _counter = _record.ApproachCounter;
        _substate = 5;
        _angle = _record.DownAngle;
        _speed = _record.Speed100;
        SetNativeAnimation(2);
    }

    private void UpdateApproach()
    {
        RalphAfterRaftonCharacter ralph = RequireRalph(ActorName);
        ralph.AdvanceRalphAnimation(2);
        if (--_counter != 0)
        {
            ralph.SetStatePosition(OracleObjectMovement.Shared.ApplySpeed(
                ref _precisePosition, _speed, _angle));
            return;
        }
        _counter = _record.AlignWait;
        _substate = 6;
    }

    private void UpdateAlignmentWait()
    {
        if (--_counter != 0)
            return;

        _counter = 0x0a;
        _substate = 7;
        int linkX = OracleObjectPosition.HighByte(_context.Player.Position.X);
        int ralphX = OracleObjectPosition.HighByte(
            RequireRalph(ActorName).Position.X);
        int difference = (linkX - ralphX) & 0xff;
        if (difference == 0)
        {
            StartScript();
            return;
        }

        if (linkX >= ralphX)
        {
            _angle = 0x08;
            SetNativeAnimation(1);
        }
        else
        {
            difference = (-difference) & 0xff;
            _angle = 0x18;
            SetNativeAnimation(3);
        }
        _counter = difference;
    }

    private void UpdateAlignment()
    {
        RalphAfterRaftonCharacter ralph = RequireRalph(ActorName);
        ralph.AdvanceRalphAnimation(2);
        if (--_counter != 0)
        {
            ralph.SetStatePosition(OracleObjectMovement.Shared.ApplySpeed(
                ref _precisePosition, _speed, _angle));
            return;
        }
        StartScript();
    }

    private void StartScript()
    {
        _substate = 8;
        _runner.Start(_database.Commands);
    }

    private void UpdateScript()
    {
        RalphAfterRaftonCharacter ralph = RequireRalph(ActorName);
        int animationUpdates = 1;
        if (_runner.CurrentCommand is CutsceneMoveCommand && _runner.Counter != 0)
        {
            int speed = _runner.ActorSpeed(new CutsceneActorId(ActorName));
            animationUpdates = speed < _record.Speed100
                ? 1
                : speed < _record.Speed200 ? 2 : 3;
        }
        ralph.AdvanceRalphAnimation(animationUpdates);
        _runner.AdvanceFrame();
    }

    private void SetNativeAnimation(int animation) =>
        RequireRalph(ActorName).SetRalphAnimation(animation, _record);

    private RalphAfterRaftonCharacter RequireRalph(string actor)
    {
        if (actor != ActorName || _ralph is null)
        {
            throw new InvalidOperationException(
                $"Unknown Ralph-after-Rafton command actor '{actor}'.");
        }
        return _ralph;
    }
}
