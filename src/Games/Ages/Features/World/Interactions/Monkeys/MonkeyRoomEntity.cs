using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// Native INTERAC_MONKEY $39:$05. Initialization allocates children before
// its two RNG calls; each child initializes at its own physical slot visit.
internal sealed class MonkeyRoomEntity : CutsceneCommandHost, IRoomEntity, IFixedRoomEntity,
    IRoomEntityLifetime, IPlayerInteractable, IRoomBlocker, IObjectCollisionHeightRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    private readonly DungeonObjectRecord _record;
    private readonly MonkeyDatabase _data;
    private readonly RoomEntityManager _owner;
    private readonly OracleSaveData _save;
    private readonly NpcCharacter _actor;
    private readonly CutsceneCommandRunner _script;
    private Player? _player;
    private bool _initialized, _pressed, _ownsInput;
    private int _z, _speedZ, _jumpSpeed, _substate, _direction, _hops, _turnCounter, _airAnimation, _textId;
    private Vector2 _precise;
    public Node2D Node => _actor;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;
    public int CollisionZ => _z >> 8;
    internal int Index => _record.Var03;
    internal int ZFixed => _z;
    internal int SpeedZ => _speedZ;
    internal int Substate => _substate;
    public override bool DialogueOpen => _owner.IsTextActive();
    public override bool ScriptExecutionBlocked => _owner.IsScriptTextActive() || _player?.IsDying == true;

    internal MonkeyRoomEntity(DungeonObjectRecord record, MonkeyDatabase data, RoomEntityManager owner, OracleSaveData save)
    {
        _record = record; _data = data; _owner = owner; _save = save;
        _actor = data.CreateActor(record); _script = new(this);
        _actor.Retired += _ => {
            _script.Clear();
            // Room cancellation can clear the object mask before retiring its NPCs.
            if (_ownsInput && _player is not null && owner.RuntimeState.ReadWramByte(AgesWramAddress.wDisabledObjects) is 0 or 0x81)
                NativeScriptInputControl.SetEnabled(owner.RuntimeState, _player, true);
        };
    }
    public void SetTransitionDrawOffset(Vector2 offset) => _actor.SetTransitionDrawOffset(offset);
    public bool BlocksLink(Vector2 center) => _initialized && !Finished && _actor.BlocksLinkCenter(center);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { if (!_initialized) Initialize(); return Finished ? ScreenTransitionPresentation.Hidden : ScreenTransitionPresentation.Visible; }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        _player = frame.Player;
        if (!_initialized) { Initialize(); return; }
        if (Index is 0 or 1 or 2 or 4 && OracleObjectMath.UpdateSpeedZ(ref _z, ref _speedZ, 0x10)) _speedZ = _jumpSpeed;
        _script.AdvanceFrame();
        if (Index == 9)
        {
            int airborne = unchecked((byte)((_z >> 8) - 3)) < 0xfa ? 1 : 0;
            if (airborne != _airAnimation) { _airAnimation = airborne; _actor.SetAnimationCounter(1); _actor.AdvanceAnimationUpdates(1); }
            _actor.PushPlayerAwayAndUpdateDrawPriority(frame.Player);
            UpdateWalking();
        }
        else _actor.AnimateAsNpcOneUpdate(frame.Player);
        _actor.SetScriptDrawOffset(Vector2.Down * (_z >> 8));
    }
    private void Initialize()
    {
        _initialized = true;
        if (!_save.HasGlobalFlag(AgesGlobalFlag.SavedNayru)) { Finished = true; return; }
        if (Index == 0)
            for (int index = 9; index != 0; index--)
                if (!_owner.TryCreateSavedMonkey(_record with { Var03 = index })) break;
        var placement = _data.Placement(Index); _precise = placement.Position;
        _actor.SetStatePosition(_precise); _actor.SetScriptAnimation(_data.Animation(0x39, placement.Animation));
        int animationOffset = _owner.Random.Next().Value & 15;
        _actor.AdjustInitialAnimationCounter(animationOffset - 7);
        _speedZ = _jumpSpeed = _data.Jump(_owner.Random.Next().Value & 3);
        if (Index == 8)
        {
            _actor.SetScriptAnimation(_data.Animation(0x39, 7));
            if (_owner.InteractionSlotAvailable)
                _owner.AddEntity(new AccessoryRoomEntity(_data.CreateActor(_record, accessory: true), _actor, () => Finished,
                    () => _z, index => _data.Animation(0x63, index), _data.BowtieOffsets, actor => actor.SetBasePalette(2)));
        }
        _actor.SetBasePalette(Index == 5 ? 2 : 3); _actor.Visible = true;
        _actor.TimeWarpSolidMarked = Index != 9;
        _script.Start(_data.Script(Index == 8));
    }
    private void UpdateWalking()
    {
        switch (_substate)
        {
            case 0: _direction = 8; _substate = 1; _speedZ = -0x100; break;
            case 1:
                if (!OracleObjectMath.UpdateSpeedZ(ref _z, ref _speedZ, 0x20))
                    _actor.Position = NativeObjectMovement.ApplySpeed(_owner.RuntimeState, ref _precise, ObjectSpeed.Speed100, _direction);
                else { _speedZ = -0x100; if (++_hops == 3) { _substate = 2; _turnCounter = 16; } }
                break;
            case 2:
                if (--_turnCounter != 0) break;
                _turnCounter = 16; _substate = 3; _direction ^= 0x10; _speedZ = -0x100;
                _actor.SetScriptAnimation(_data.Animation(0x39, (_direction & 0x10) == 0 ? 8 : 3)); break;
            case 3: if (--_turnCounter == 0) { _hops = 0; _substate = 1; } break;
            default: throw new InvalidOperationException($"{_record.Source}: monkey9 substate ${_substate:x2}.");
        }
    }
    public bool TryInteract(Player player)
    {
        if (Finished || !_initialized || !_actor.ScriptButtonSensitive || !_actor.CanTalkTo(player)) return false;
        _player = player; _pressed = true; player.ApplyObjectInteractionGrace();
        return true;
    }
    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value == "Monkey";
    public override void InitializeActorCollisionRadii(string actor) => _actor.InitializeCollisionRadii();
    public override void SetActorButtonSensitive(string actor) => _actor.SetScriptButtonSensitive(true);
    public override bool TryConsumeActorButton(CutsceneActorId actor)
    {
        bool pressed = _pressed; _pressed = false;
        if (pressed) _player!.ApplyInteractionInvincibility(0x80);
        return pressed;
    }
    public override int ReadMemory(string binding) => binding switch {
        "Interaction.zh" => unchecked((byte)(_z >> 8)),
        "GLOBALFLAG_FINISHEDGAME" => _save.HasGlobalFlag(AgesGlobalFlag.FinishedGame) ? 1 : 0,
        _ => throw UnsupportedCommand(binding) };
    public override bool MemoryEquals(string binding, int value) => ReadMemory(binding) == value;
    public override void RunNativeHandler(string handler)
    {
        if (handler != "scriptHelp.monkey_decideTextIndex") throw UnsupportedCommand(handler);
        _textId = (_save.HasGlobalFlag(AgesGlobalFlag.FinishedGame) ? 0x570d : 0x5708) + (_owner.Random.Next().Value & 3);
    }
    public override void ShowLoadedText() => ShowText(_textId, _data.Text(_textId));
    public override void ShowText(int textId, string message, int? position) => ShowText(textId, message);
    public override void ShowText(int textId, string message) => _owner.OnRoomEntityDialogueRequested(textId, message, _player!.Position);
    public override void SetInputEnabled(bool enabled)
    {
        NativeScriptInputControl.SetEnabled(_owner.RuntimeState, _player!, enabled); _ownsInput = !enabled;
    }
}
