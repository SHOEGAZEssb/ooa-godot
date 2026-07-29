using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_POE $59:$00 and poeScript in present rooms $0:$7c and $2:$2e.
/// </summary>
internal sealed class PoeEvent :
    InteractiveCutsceneCommandHost, IRoomEntryEvent, ICutsceneCommandHost
{
    private const string ActorName = "Poe";
    private const string VariantBinding = "PoeVariant";
    private readonly RoomEventContext _context;
    private readonly PoeEventDatabase _database = new();
    private readonly PoeEventRecord _record;
    private readonly CutsceneCommandRunner _runner;
    private PoeCharacter? _poe;
    private Vector2 _precisePosition;
    private bool _buttonSensitive;
    private bool _buttonPressed;

    public PoeEvent(RoomEventContext context)
    {
        _context = context;
        _record = _database.Record;
        _runner = new CutsceneCommandRunner(this);
    }

    public bool HasState => _runner.Active;
    public bool BlocksGameplay => InputLeaseHeld;
    protected override RoomEventContext InputContext => _context;
    internal PoeCharacter? Actor => _poe;
    internal int CurrentCommandIndex =>
        _runner.CurrentCommand?.Source.CommandIndex ?? -1;
    internal int CurrentCommandUpdates => _runner.CurrentCommandUpdates;
    internal int Counter => _runner.Counter;
    internal bool ButtonSensitive => _buttonSensitive;
    internal bool InputDisabled => InputLeaseHeld;
    internal PoeEventDatabase Database => _database;

    public bool Matches(int group, OracleRoomData room) =>
        IsOverworldRoom(group, room.Id) || IsTombRoom(group, room.Id);

    public void Start(OracleRoomData room)
    {
        _ = room;
        ResetState();

        var candidates = new List<PoeCharacter>();
        foreach (PoeCharacter poe in _context.Entities.Entities<PoeCharacter>())
        {
            if ((IsOverworldRoom(poe.Record.Group, poe.Record.Room) ||
                 IsTombRoom(poe.Record.Group, poe.Record.Room)) &&
                poe.Record.Id == _record.InteractionId &&
                poe.Record.SubId == _record.SubId)
            {
                candidates.Add(poe);
            }
        }

        foreach (PoeCharacter candidate in candidates)
        {
            bool selected = VariantVisible(candidate.Record);
            if (!selected)
            {
                candidate.SetActive(false);
                continue;
            }
            if (_poe is not null)
            {
                throw new InvalidOperationException(
                    "A Poe event room initialized more than one visible " +
                    "INTERAC_POE.");
            }
            _poe = candidate;
        }
        if (_poe is null)
            return;

        _poe.SetActive(true);
        _poe.SetDisappearing(false);
        _poe.SetNoFace(false);
        _poe.ResetNativeNpcFacingState();
        _precisePosition = _poe.Position;
        _runner.Start(_database.Commands);

        // State 0 loads poeScript, falls through to state 1, runs the script
        // once, then calls npcFaceLinkAndAnimate later in the same update.
        for (int update = 0; update < _record.InitialScriptUpdates; update++)
        {
            _runner.AdvanceFrame();
            _poe.UpdatePoe(_context.Player);
        }
    }

    public void UpdateFrame()
    {
        if (_poe is null || !_runner.Active)
            return;
        _runner.AdvanceFrame();
        _poe.UpdatePoe(_context.Player);
    }

    public bool TryInteractNpc(NpcCharacter npc)
    {
        if (!_runner.Active || !_buttonSensitive || InputLeaseHeld ||
            _poe?.Disappearing != false || !ReferenceEquals(npc, _poe))
        {
            return false;
        }
        _buttonPressed = true;
        return true;
    }

    public void Cancel()
    {
        if (_poe is not null)
        {
            _poe.SetScriptButtonSensitive(false);
            _poe.SetActive(false);
        }
        ResetState();
    }

    RoomEventContext ICutsceneCommandHost.Context => _context;
    bool ICutsceneCommandHost.HasActorBinding(CutsceneActorId actor) =>
        actor.Value == ActorName;

    bool ICutsceneCommandHost.MemoryEquals(string binding, int value) =>
        ReadScriptMemory(binding) == value;

    int ICutsceneCommandHost.ReadMemory(string binding) =>
        ReadScriptMemory(binding);

    bool ICutsceneCommandHost.TryConsumeActorButton(CutsceneActorId actor)
    {
        _ = RequirePoe(actor.Value);
        if (!_buttonPressed)
            return false;
        _buttonPressed = false;
        return true;
    }

    void ICutsceneCommandHost.ShowText(int textId, string message)
    {
        if (textId is < 0x0b00 or > 0x0b02)
        {
            throw new InvalidOperationException(
                $"poeScript requested unknown TX_{textId:x4}.");
        }
        _context.ShowDialogue(message);
    }

    void ICutsceneCommandHost.SetActorAnimation(
        string actor,
        int animation,
        string encodedAnimation)
    {
        if (encodedAnimation != _record.Animation(animation))
        {
            throw new InvalidOperationException(
                $"Poe animation ${animation:x2} payload diverged from metadata.");
        }
        RequirePoe(actor).SetScriptAnimation(encodedAnimation);
    }

    void ICutsceneCommandHost.SetActorCollisionRadii(
        string actor,
        int radiusY,
        int radiusX)
    {
        if (radiusY != _record.CollisionRadiusY ||
            radiusX != _record.CollisionRadiusX)
        {
            throw new InvalidOperationException(
                $"poeScript initialized unexpected collision radii " +
                $"${radiusY:x2}/${radiusX:x2}.");
        }
        RequirePoe(actor).SetCollisionRadii(radiusY, radiusX);
    }

    void ICutsceneCommandHost.SetActorButtonSensitive(string actor)
    {
        RequirePoe(actor).SetScriptButtonSensitive(true);
        _buttonSensitive = true;
    }

    void ICutsceneCommandHost.MoveActorAtSpeed(
        string actor,
        int speed,
        int angle)
    {
        if (speed != _record.Speed100)
        {
            throw new InvalidOperationException(
                $"poeScript requested unexpected movement speed ${speed:x2}.");
        }
        RequirePoe(actor).Position =
            OracleObjectMovement.Shared.ApplySpeed(
                ref _precisePosition, speed, angle);
    }

    void ICutsceneCommandHost.SetActorVisible(string actor, bool visible) =>
        RequirePoe(actor).Visible = visible;

    void ICutsceneCommandHost.WriteObjectByte(
        string actor,
        int address,
        int value)
    {
        PoeCharacter poe = RequirePoe(actor);
        if (address == _record.FlickerAddress &&
            value == _record.FlickerCount)
        {
            poe.SetDisappearing(true);
            return;
        }
        if (address == 0x3f && value == 1)
        {
            poe.SetNoFace(true);
            return;
        }
        throw new InvalidOperationException(
            $"poeScript wrote unexpected object byte ${address:x2}=${value:x2}.");
    }

    void ICutsceneCommandHost.GiveItem(int treasureId, int parameter)
    {
        if (treasureId != _record.RewardTreasure ||
            parameter != _record.RewardParameter)
        {
            throw new InvalidOperationException(
                $"poeScript requested unexpected reward " +
                $"${treasureId:x2}:${parameter:x2}.");
        }
        PoeCharacter poe = RequirePoe(ActorName);
        if (!IsOverworldRoom(poe.Record.Group, poe.Record.Room) ||
            poe.Record.Var03 != _record.FinalVariant)
        {
            throw new InvalidOperationException(
                "Only room 0:7c's final Poe may grant the Poe Clock.");
        }
        _context.GrantScriptTreasure(
            _record.Group,
            _record.Room,
            treasureId,
            parameter,
            _record.RewardObject,
            "scriptHelper.s:poeScript giveitem TREASURE_TRADEITEM,$00");
    }

    void ICutsceneCommandHost.PlaySound(int sound)
    {
        if (sound != _record.PoofSound)
        {
            throw new InvalidOperationException(
                $"poeScript requested unexpected sound ${sound:x2}.");
        }
        _context.Sound.PlaySound(sound);
    }

    void ICutsceneCommandHost.OrRoomFlag(int flag)
    {
        if (flag != _record.ProgressFlag)
        {
            throw new InvalidOperationException(
                $"poeScript cannot OR room flag ${flag:x2}.");
        }
        PoeCharacter poe = RequirePoe(ActorName);
        _context.Rooms.SaveData.SetRoomFlag(
            poe.Record.Group, poe.Record.Room, (byte)flag);
    }

    void ICutsceneCommandHost.ScriptEnded()
    {
        _buttonSensitive = false;
        _buttonPressed = false;
        if (_poe is not null)
        {
            _poe.SetScriptButtonSensitive(false);
            _poe.SetActive(false);
        }
    }

    private int ReadScriptMemory(string binding)
    {
        if (binding != VariantBinding || _poe is null)
            throw new InvalidOperationException($"poeScript cannot read '{binding}'.");
        return _poe.Record.Var03;
    }

    private bool VariantVisible(NpcRecord actor)
    {
        bool progress = _context.Rooms.SaveData.HasRoomFlag(
            _record.Group, _record.Room, (byte)_record.ProgressFlag);
        bool tomb = _context.Rooms.SaveData.HasRoomFlag(
            _record.TombGroup, _record.TombRoom, (byte)_record.ProgressFlag);
        bool item = _context.Rooms.SaveData.HasRoomFlag(
            _record.Group, _record.Room, (byte)_record.ItemFlag);
        if (IsOverworldRoom(actor.Group, actor.Room))
        {
            return actor.Var03 == _record.FirstVariant
                ? !progress && !tomb
                : actor.Var03 == _record.FinalVariant
                    ? !item && progress && tomb
                    : false;
        }
        return IsTombRoom(actor.Group, actor.Room) &&
            actor.Var03 == _record.TombVariant &&
            progress && !tomb;
    }

    private bool IsOverworldRoom(int group, int room) =>
        group == _record.Group && room == _record.Room;

    private bool IsTombRoom(int group, int room) =>
        group == _record.TombGroup && room == _record.TombRoom;

    private PoeCharacter RequirePoe(string actor)
    {
        if (actor != ActorName || _poe is null)
            throw new InvalidOperationException($"Unknown Poe command actor '{actor}'.");
        return _poe;
    }

    private void ResetState()
    {
        ReleaseInputControl();
        _poe = null;
        _precisePosition = Vector2.Zero;
        _buttonSensitive = false;
        _buttonPressed = false;
        _runner.Clear();
    }
}
