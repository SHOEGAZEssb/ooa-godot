using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// INTERAC $6b:$02 and the $37:$01 child share cfc0 and advance at their
// allocated interaction slots, including a child created later in this pass.
internal sealed class PalaceApproachRoomEntity : CutsceneCommandHost, IRoomEntity, IFixedRoomEntity,
    IRoomEntityLifetime, IPlayerRestriction, IRoomBlocker,
    IScreenTransitionPreloadRoomEntity, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly RoomEntityManager _owner;
    private readonly RoomSession _rooms;
    private readonly PalaceApproachDatabase _data;
    private readonly NpcCharacter? _ralph;
    private readonly CutsceneCommandRunner _runner;
    private Player _player = null!;
    private ICollection<RoomEntitySpawn> _spawns = null!;
    private Vector2 _precise;
    private int _animationLock, _z, _speedZ, _radiusY, _radiusX;
    private bool _initialized, _inputLocked, _landed;
    public Node2D Node { get; }
    public bool Finished { get; private set; }
    internal bool IsRalph => _ralph is not null;
    internal int ZFixed => _z;
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;
    public bool DisablesSword => _inputLocked;
    public bool DisablesItems => _inputLocked;
    public bool DisablesMovement => _inputLocked;
    public bool DisablesMenus => _inputLocked;
    public override bool DialogueOpen => _owner.IsScriptTextActive();
    public override bool ScriptExecutionBlocked => DialogueOpen || _player.IsDying;
    internal PalaceApproachRoomEntity(RoomEntityManager owner, RoomSession rooms, PalaceApproachDatabase data, bool ralph = false)
    {
        _owner = owner; _rooms = rooms; _data = data;
        if (ralph)
        {
            _ralph = NpcCharacter.CreateFromRecord(data.Ralph);
            _ralph.SetAnimationRate(0); _ralph.SetBlocksLink(true);
            Node = _ralph; _precise = _ralph.Position;
        }
        else Node = new Node2D { Position = new(0x50, 0x38), Visible = false };
        Node.TreeExiting += ReleaseInput;
        _runner = new(this); _runner.Start(ralph ? data.RalphCommands : data.TriggerCommands);
    }
    public void SetTransitionDrawOffset(Vector2 offset) => _ralph?.SetTransitionDrawOffset(offset);
    public bool BlocksLink(Vector2 center) => _ralph?.BlocksLinkCenter(center) == true;
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (IsRalph)
            throw new InvalidOperationException("INTERAC $37:$01 state 0 requires Link for ralphTurnLinkTowardSelf.");
        Initialize();
        return ScreenTransitionPresentation.Hidden;
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(Player? player, ICollection<RoomEntitySpawn> spawns)
    {
        if (!IsRalph) return PrepareForScreenTransition(spawns);
        if (!_initialized)
        {
            if (player is null)
                throw new InvalidOperationException("INTERAC $37:$01 state 0 requires Link for ralphTurnLinkTowardSelf.");
            _player = player;
            _spawns = spawns;
            Advance();
        }
        return Node.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }
    private void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        // miscellaneous1.s:interaction6b_subid02 loads its script and returns
        // from state 0 without dispatching it; the controller stays invisible.
        if (_ralph is null)
            Finished = _owner.Inventory?.HasTreasure(TreasureId.MysterySeeds) == true;
        else
            _ralph.ShowInitializedPresentation();
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        _player = frame.Player; _spawns = spawns;
        Advance();
    }
    private void Advance()
    {
        if (!_initialized)
        {
            Initialize();
            if (_ralph is null) return;
        }
        _runner.AdvanceFrame();
        if (_ralph is not null && !Finished)
        {
            // ralphTurnLinkTowardSelf uses byte arithmetic, including CPL's
            // off-by-one on the left side, then queues state $08 every pass.
            int dx = (((int)_ralph.Position.X + 16) & 255) - (((int)_player.Position.X + 16) & 255);
            int magnitude = dx < 0 ? (~dx) & 255 : dx;
            _player.Face(magnitude < 12 ? Vector2I.Down : dx < 0 ? Vector2I.Left : Vector2I.Right);
            _player.RequestState08Control(this);
            if (_animationLock == 0) _ralph.AdvanceAnimationUpdates(2);
            _ralph.PushPlayerAwayAndUpdateDrawPriority(_player);
        }
    }
    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value == (IsRalph ? "Ralph" : "Trigger");
    public override bool RoomFlagSet(int flag) => _rooms.SaveData.HasRoomFlag(_rooms.ActiveGroup, _rooms.CurrentRoom.Id, (byte)flag);
    public override void OrRoomFlag(int flag) => _rooms.SaveData.SetRoomFlag(_rooms.ActiveGroup, _rooms.CurrentRoom.Id, (byte)flag);
    public override int ReadMemory(string binding) => binding switch
    {
        "BombReward" => _rooms.SaveData.HasRoomFlag(0, 0x83, 0x80) ? 1 : 0,
        "ApproachSignal" => _owner.RuntimeState.ReadWramByte(0xcfc0),
        "Landed" => _landed ? 1 : 0,
        _ => throw UnsupportedCommand($"read palace approach '{binding}'")
    };
    public override bool MemoryEquals(string binding, int value) => ReadMemory(binding) == value;
    public override void WriteMemory(string binding, int value)
    {
        if (binding != "ApproachSignal") throw UnsupportedCommand($"write palace approach '{binding}'");
        _owner.RuntimeState.SetWramByte(0xcfc0, (byte)value);
    }
    public override void SetActorCollisionRadii(string actor, int radiusY, int radiusX) { _radiusY = radiusY; _radiusX = radiusX; }
    public override bool GateOpen(string gate) => gate == "LinkCollision" ?
        Player.EnemyCollisionOverlaps(_player.Position,
            new Rect2(Node.Position - new Vector2(_radiusX, _radiusY), new Vector2(_radiusX * 2, _radiusY * 2))) :
        throw UnsupportedCommand($"read palace approach gate '{gate}'");
    public override void SetInputEnabled(bool enabled)
    {
        _inputLocked = !enabled;
        _owner.RuntimeState.SetWramByte(AgesWramAddress.wDisabledObjects, enabled ? (byte)0 : (byte)0x81);
        _owner.RuntimeState.SetWramByte(AgesWramAddress.wMenuDisabled, enabled ? (byte)0 : (byte)0x80);
        if (enabled) _player.EndCutsceneControl();
        else { _player.ClearNativeItemParents(); _player.DropHeldItemsForScript(); }
    }
    public override void SetActorAnimation(string actor, int animation, string encodedAnimation) => _ralph!.SetScriptAnimation(encodedAnimation);
    public override void SetActorMovementAnimation(string actor, int angle, string encodedAnimation) => _ralph!.SetScriptAnimation(encodedAnimation);
    public override void MoveActorAtSpeed(string actor, int speed, int angle) => _ralph!.SetStatePosition(NativeObjectMovement.ApplySpeed(_owner.RuntimeState, ref _precise, speed, angle));
    public override void WriteObjectByte(string actor, int address, int value)
    {
        if (address != 0x7f) throw UnsupportedCommand($"write Ralph object${address:x2}");
        _animationLock = value;
    }
    public override void ShowText(int textId, string message, int? textboxPosition) =>
        _owner.OnRoomEntityDialogueRequested(textId, message, _ralph!.Position);
    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "CheckBombReward":
                _owner.RuntimeState.SetWramByte(0xcddb, ReadMemory("BombReward") == 0 ? (byte)0xa0 : (byte)0x20); return;
            case "ForceState08": _player.PutOnGroundForScript(); _player.RequestState08Control(this); return;
            case "SpawnRalph":
                if (_owner.InteractionSlotAvailable) _spawns.Add(new PalaceApproachRalphSpawn()); return;
            case "RalphMusic": _owner.SetNativeActiveMusic(0x35); _owner.OnSoundRequested(0x35); return;
            case "ResetRoomMusic": _owner.OnRoomMusicRequested(_rooms.ActiveGroup, _rooms.CurrentRoom.Id); return;
            case "BeginJump": _speedZ = -0x200; _owner.OnSoundRequested(SoundId.SndJump); return;
            case "UpdateGravity":
                _z += _speedZ; _speedZ = unchecked((short)(_speedZ + 0x30));
                _landed = _z >= 0; if (_landed) _z = 0;
                _ralph!.SetScriptDrawOffset(Vector2.Down * (_z >> 8));
                _owner.RuntimeState.SetWramByte(0xcddb, _landed ? (byte)0x80 : (byte)0); return;
            default: throw UnsupportedCommand($"run palace approach native '{handler}'");
        }
    }
    public override void ScriptEnded() { Finished = true; _ralph?.SetActive(false); }
    private void ReleaseInput()
    {
        if (!_inputLocked) return;
        _inputLocked = false;
        _player.EndCutsceneControl();
        _owner.RuntimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0);
        _owner.RuntimeState.SetWramByte(AgesWramAddress.wMenuDisabled, 0);
    }
}

internal sealed record PalaceApproachRalphSpawn : RoomEntitySpawn;
