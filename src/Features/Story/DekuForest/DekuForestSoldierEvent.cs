using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Runs roomSpecificCode3 and INTERAC_SOLDIER $40:$0a in past room $81 after
/// Link obtains the Mystery Seeds.
/// </summary>
internal sealed class DekuForestSoldierEvent :
    CutsceneCommandHost,
    IRoomEntryEvent,
    IUpdatesDuringDialogueRoomEvent,
    ICutsceneCommandHost
{
    private const string SoldierActor = "Soldier";

    private readonly RoomEventContext _context;
    private readonly DekuForestSoldierEventDatabase _database = new();
    private readonly DekuForestSoldierEventRecord _record;
    private readonly CutsceneCommandRunner _runner;
    private NpcCharacter? _soldier;
    private NpcCharacter? _exclamation;
    private Vector2 _precisePosition;
    private bool _triggered;
    private bool _menusDisabled;
    private bool _warpRequested;
    private bool _exclamationFresh;
    private int _exclamationCounter;
    private int _effectSerial;

    internal DekuForestSoldierEvent(RoomEventContext context)
    {
        _context = context;
        _record = _database.Record;
        _runner = new CutsceneCommandRunner(this);
    }

    public bool HasState => _runner.Active;
    public bool BlocksGameplay => _triggered;
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
                $"The Deku Forest soldier cannot arm in " +
                $"{_context.Rooms.ActiveGroup:x}:{room.Id:x2}.");
        }

        OracleSaveData save = _context.Rooms.SaveData;
        if (save.HasRoomFlag(
                _record.Group, _record.Room, (byte)_record.RoomFlag) ||
            !_context.Inventory.HasTreasure(_record.TriggerTreasure))
        {
            return;
        }

        _soldier = _context.Entities.Spawn<NpcCharacter>(
            new CutsceneNpcSpawn(
                _database.CreateSoldierRecord(),
                "DekuForestSoldier"));
        _soldier.SetAnimationRate(0.0f);
        _soldier.SetScriptVisible(false);
        _precisePosition = _soldier.Position;
        _runner.Start(_database.Commands);
    }

    public void UpdateFrame()
    {
        if (_soldier is null || !_runner.Active)
            return;

        // soldierUpdateAnimationAndRunScript calls
        // interactionAnimateBasedOnSpeed before interactionRunScript. At
        // SPEED_180 it performs a second animation update only while
        // counter2 is nonzero.
        int animationUpdates = 1;
        int speed = _runner.ActorSpeed(SoldierActor);
        if (_runner.Counter != 0 && speed >= 0x28)
            animationUpdates++;
        _soldier.AdvanceAnimationUpdates(animationUpdates);

        _runner.AdvanceFrame();
        UpdateExclamation();
        if (_warpRequested)
            ApplyWarp();
    }

    public void UpdateDuringDialogueFrame() => UpdateFrame();

    public void Cancel()
    {
        if (_triggered)
            _context.Player.EndCutsceneControl();
        if (_soldier is not null &&
            GodotObject.IsInstanceValid(_soldier))
        {
            _soldier.SetActive(false);
        }
        RetireExclamation();
        _soldier = null;
        _runner.Clear();
        _triggered = false;
        _menusDisabled = false;
        _warpRequested = false;
        _precisePosition = Vector2.Zero;
    }

    RoomEventContext ICutsceneCommandHost.Context => _context;

    public override bool HasActorBinding(CutsceneActorId actor) =>
        actor.Value == SoldierActor;

    public override void SetMenuEnabled(bool enabled)
    {
        if (enabled)
        {
            throw new InvalidOperationException(
                "soldierSubid0aScript only supports disablemenu.");
        }
        _menusDisabled = true;
    }

    public override bool MemoryEquals(string binding, int value)
    {
        if (binding != "PlayerY")
        {
            throw new InvalidOperationException(
                $"soldierSubid0aScript cannot read '{binding}'.");
        }
        return OracleObjectPosition.HighByte(_context.Player.Position.Y) == value;
    }

    public override void ShowText(int textId, string message)
    {
        if (textId != _record.TextId || message != _record.Text)
        {
            throw new InvalidOperationException(
                $"soldierSubid0aScript requested TX_{textId:x4}; " +
                $"expected TX_{_record.TextId:x4}.");
        }
        _context.ShowDialogue(message);
    }

    public override void SetActorAnimation(
        string actor,
        int animation,
        string encodedAnimation)
    {
        if (encodedAnimation != _record.Animation(animation))
        {
            throw new InvalidOperationException(
                $"Soldier animation ${animation:x2} changed after import.");
        }
        RequireSoldier(actor).SetScriptAnimation(encodedAnimation);
    }

    public override void SetActorMovementAnimation(
        string actor,
        int angle,
        string encodedAnimation)
    {
        int animation = angle switch
        {
            0x00 => 0,
            0x08 => 1,
            _ => throw new InvalidOperationException(
                $"Unsupported soldier movement angle ${angle:x2}.")
        };
        if (encodedAnimation != _record.Animation(animation))
        {
            throw new InvalidOperationException(
                $"Soldier movement animation for angle ${angle:x2} changed after import.");
        }
        RequireSoldier(actor).SetScriptAnimation(encodedAnimation);
    }

    public override void MoveActorAtSpeed(string actor, int speed, int angle)
    {
        bool supported =
            speed == _record.SlowSpeed && angle == 0x08 ||
            speed == _record.FastSpeed && angle == 0x00;
        if (!supported)
        {
            throw new InvalidOperationException(
                $"Unsupported soldier movement ${speed:x2}/${angle:x2}.");
        }
        RequireSoldier(actor).SetStatePosition(
            OracleObjectMovement.Shared.ApplySpeed(
                ref _precisePosition, speed, angle));
    }

    public override void WriteMemory(string binding, int value)
    {
        if (binding != "DisabledObjects" || value != 0x01)
        {
            throw new InvalidOperationException(
                $"soldierSubid0aScript cannot write '{binding}'=${value:x2}.");
        }
        if (!_triggered)
        {
            throw new InvalidOperationException(
                "wDisabledObjects was written before dropLinkHeldItem took cutscene control.");
        }
    }

    public override void OrRoomFlag(int flag)
    {
        if (flag != _record.RoomFlag)
        {
            throw new InvalidOperationException(
                $"soldierSubid0aScript cannot OR room flag ${flag:x2}.");
        }
        _context.Rooms.SaveData.SetRoomFlag(
            _record.Group, _record.Room, (byte)flag);
    }

    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "ObjectSetVisible82":
                RequireSoldier(SoldierActor).SetScriptVisible(true);
                return;

            case "DropLinkHeldItem":
                _context.Player.BeginCutsceneControl();
                _triggered = true;
                return;

            case "CreateExclamationMark":
                CreateExclamation();
                return;

            default:
                throw new InvalidOperationException(
                    $"Unknown soldierSubid0aScript native handler '{handler}'.");
        }
    }

    public override void ScriptEnded() => _warpRequested = true;

    private NpcCharacter RequireSoldier(string actor)
    {
        if (actor != SoldierActor || _soldier is null)
        {
            throw new InvalidOperationException(
                $"Unknown Deku Forest soldier actor '{actor}'.");
        }
        return _soldier;
    }

    private void CreateExclamation()
    {
        NpcCharacter soldier = RequireSoldier(SoldierActor);
        if (_exclamation is not null)
        {
            throw new InvalidOperationException(
                "soldierSubid0aScript created a second exclamation mark.");
        }

        int y = OracleObjectPosition.HighByte(soldier.Position.Y) +
            _record.EffectY;
        int x = OracleObjectPosition.HighByte(soldier.Position.X) +
            _record.EffectX;
        _exclamation = _context.Entities.Spawn<NpcCharacter>(
            new CutsceneNpcSpawn(
                _database.CreateExclamationRecord(y, x),
                $"DekuForestExclamation{_effectSerial++}"));
        _exclamation.SetAnimationRate(0.0f);
        _exclamationCounter = _record.EffectFrames;
        _exclamationFresh = true;
        _context.Sound.PlaySound(_record.ClinkSound);
    }

    private void UpdateExclamation()
    {
        if (_exclamation is null)
            return;
        if (_exclamationFresh)
        {
            // INTERAC_EXCLAMATION_MARK state 0 initializes and reveals the
            // object without decrementing counter1 or animating it.
            _exclamationFresh = false;
            return;
        }
        if (_exclamationCounter <= 1)
        {
            RetireExclamation();
            return;
        }

        _exclamationCounter--;
        _exclamation.AdvanceAnimationUpdates(1);
    }

    private void RetireExclamation()
    {
        if (_exclamation is not null &&
            GodotObject.IsInstanceValid(_exclamation))
        {
            _exclamation.SetActive(false);
        }
        _exclamation = null;
        _exclamationCounter = 0;
        _exclamationFresh = false;
    }

    private void ApplyWarp()
    {
        _warpRequested = false;
        _triggered = false;
        _menusDisabled = false;
        RetireExclamation();
        if (_soldier is not null &&
            GodotObject.IsInstanceValid(_soldier))
        {
            _soldier.SetActive(false);
        }
        _soldier = null;
        _context.Transitions.ApplyWarp(
            _context.Player, _database.CreateWarp());
    }
}
