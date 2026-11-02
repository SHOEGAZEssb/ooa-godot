
namespace Oracles;

// INTERAC_FAIRY_HIDING_MINIGAME $6c. The invisible parent occupies its slot
// through child allocation; a failed spawn leaves its native state unchanged.
internal sealed class FairyHidingMinigameRoomEntity : RoomCutsceneCommandHost,
    INativeInteractionRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IScreenTransitionPreloadRoomEntity, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IPlayerInteractable
{
    private readonly FairyHidingControllerRecord _record;
    private readonly FairiesWoodsDatabase _data;
    private readonly OracleSaveData _save;
    private readonly OracleRuntimeState _memory;
    private readonly Func<FairiesWoodsEvent?> _ownerSource;
    private readonly CutsceneCommandRunner _runner;
    private FairiesWoodsHiddenSpotRecord _hidden;
    private byte _originalTile;
    private int _hiddenCounter;
    private bool _controlTransferred;
    private bool _buttonSensitive;
    internal bool PressedAButton { get; private set; }
    internal int State { get; private set; }
    internal int Counter => _record.SubId == 1 && State < 3 ? _hiddenCounter : _runner.NativeCounter1;
    public bool Finished { get; private set; }
    public bool NativeInitializationPending => State == 0 && !Finished;
    public bool BlocksGameplay => InputControlHeld;
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;
    public Node2D? PresentationNode => null;
    private FairiesWoodsEvent Owner => _ownerSource() ??
        throw new NotSupportedException($"{_record.Source}: $6c:${_record.SubId:x2} requires its Ages story binding.");
    public override RoomEventContext Context => Owner.Context;
    internal FairiesWoodsStage Stage => State == 0 ? FairiesWoodsStage.ControllerInitialize : _record.SubId switch
    {
        0 => State == 1 ? FairiesWoodsStage.StartPending : FairiesWoodsStage.IntroScript,
        1 => State switch { 1 => FairiesWoodsStage.HiddenWatch, 2 => FairiesWoodsStage.HiddenSpawn, _ => FairiesWoodsStage.RevealScript },
        _ => FairiesWoodsStage.ExitScript
    };

    internal FairyHidingMinigameRoomEntity(FairyHidingControllerRecord record, FairiesWoodsDatabase data,
        OracleSaveData save, OracleRuntimeState memory, Func<FairiesWoodsEvent?> ownerSource)
    {
        _record = record; _data = data; _save = save; _memory = memory; _ownerSource = ownerSource;
        _runner = new(this);
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }
    public bool TryInteract(Player player)
    {
        if (Finished || !_buttonSensitive || PressedAButton ||
            !NativeAButtonProbe.Contains(player, new(_record.X, _record.Y), _data.Event.ExitRadiusY, _data.Event.ExitRadiusX)) return false;
        PressedAButton = true;
        player.ApplyObjectInteractionGrace();
        return true;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Advance();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (State == 0 && !Finished) Advance();
        return ScreenTransitionPresentation.Hidden;
    }

    private byte Active => _memory.ReadWramByte(_data.Event.ActiveAddress);
    private byte Found => _memory.ReadWramByte(_data.Event.FoundAddress);
    private void Advance()
    {
        if (Finished) return;
        if (State == 0)
        {
            if (_save.HasGlobalFlag(_data.Event.CompletionFlag) ||
                (_record.SubId == 0 ? !_save.HasTreasure(_data.Event.EssenceTreasure) : (Active & 1) == 0))
            { Finished = true; return; }
            Owner.RegisterController(this);
            switch (_record.SubId)
            {
                case 0:
                    if (Active == 0) { State = 1; return; }
                    if (Found == 7)
                    {
                        SetNativeMenuDisabled(0x80); SetDisabledObjects(0x21);
                        Context.Player.Face(Vector2I.Up);
                        TransferControl();
                    }
                    // All three allocate, even when the child's state0 will
                    // delete it. Failure retries from index2 on the next visit.
                    for (int index = 2; index >= 0; index--)
                        if (!Owner.TrySpawnDiscoveredFairy(index)) return;
                    Finished = true;
                    return;
                case 1:
                    if (!_data.TryHiddenSpot(_record.Room, out _hidden) ||
                        _hidden.PackedPosition != ((_record.Y & 0xf0) | (_record.X >> 4)))
                        throw new InvalidOperationException($"{_record.Source}: hidden position is absent from fairyHidingMinigame.s:@table.");
                    if ((Found & (1 << (_hidden.FairyIndex - 3))) != 0) { Finished = true; return; }
                    _originalTile = Context.Rooms.CurrentRoom.GetMetatile(new(_record.X, _record.Y));
                    _memory.SetWramByte(_data.Event.SignalAddress, 0);
                    _hiddenCounter = _data.Event.HiddenDelay; State = 1;
                    return;
                case 2:
                    State = 1; _runner.Start(_data.ExitCommands);
                    return;
            }
        }
        if (_record.SubId == 0 && State == 1)
        {
            if (!LinkIsVulnerable()) return;
            BeginMinigameInteraction();
            Context.Player.Face((Vector2I)OracleObjectMath.StrictCardinalVector(_memory.ReadField("wScreenTransitionDirection") * 8));
            _memory.SetWramByte(_data.Event.ActiveAddress, 1);
            State = 2; _runner.Start(_data.IntroCommands);
            return;
        }
        if (_record.SubId == 1)
        {
            if (State == 1)
            {
                if (Context.Rooms.CurrentRoom.GetMetatile(new(_record.X, _record.Y)) == _originalTile) return;
                _hiddenCounter = (_hiddenCounter - 1) & 0xff;
                if (_hiddenCounter != 0 || !LinkIsVulnerable()) return;
                BeginMinigameInteraction(); EventResources.ScriptInput.SetScreenTransitionsDisabled(1);
                State = 2; // Native state1 falls through to the checked reveal allocation.
            }
            if (State == 2)
            {
                if (Owner.TrySpawnFlight(_hidden.FairyIndex) is null) return;
                Context.Entities.TryCreateInteraction(new PuzzlePuffSpawn(new(_record.X, _record.Y), _data.Event.PuffSound));
                State = 3; _runner.Start(_data.RevealCommands);
                return;
            }
        }
        _runner.AdvanceFrame();
    }

    private bool LinkIsVulnerable() => Context.Player.NativeObjectVulnerable &&
        _memory.ReadField("wMenuDisabled") == 0 && _memory.ReadField("wDisableLinkCollisionsAndMenu") == 0;
    private void BeginMinigameInteraction()
    {
        SetNativeMenuDisabled(0x80); SetDisabledObjects(0x21);
        Context.Player.DropHeldItemsForScript(); Context.Player.ClearNativeItemParents();
    }
    private void TransferControl()
    {
        _controlTransferred = true;
        Owner.RequestNativeCutscene(EventResources.ScriptInput);
    }
    internal void Cancel()
    {
        _runner.Clear(); Finished = true;
        if (!_controlTransferred) ReleaseInputControl();
        Context.Player.CancelNativeForcedMovement(this);
    }

    public override bool HasActorBinding(CutsceneActorId actor) => actor.Value == "FairyExit";
    public override bool MemoryEquals(string binding, int value) => ReadMemory(binding) == value;
    public override int ReadMemory(string binding) => binding == "FairySignal"
        ? _memory.ReadWramByte(_data.Event.SignalAddress) : throw UnsupportedCommand($"read '{binding}'");
    public override void WriteMemory(string binding, int value)
    {
        if (binding != "FairySignal") throw UnsupportedCommand($"write '{binding}'");
        _memory.SetWramByte(_data.Event.SignalAddress, (byte)value);
    }
    public override bool TextOptionEquals(int value) =>
        EventResources.RequireDialogueChoice("Fairy exit choice has no completed result.") == value;
    public override void ShowText(int textId, string message)
    {
        if (_data.Text(textId).Message != message) throw UnsupportedCommand($"show inconsistent TX_{textId:x4}");
        Owner.ShowSourceText(textId);
    }
    public override void SetActorCollisionRadii(string actor, int radiusY, int radiusX)
    {
        if (actor != "FairyExit" || radiusY != _data.Event.ExitRadiusY || radiusX != _data.Event.ExitRadiusX)
            throw UnsupportedCommand($"set collision radii for '{actor}'");
    }
    public override void SetActorButtonSensitive(string actor)
    {
        if (actor != "FairyExit") throw UnsupportedCommand($"set button-sensitive actor '{actor}'");
        _buttonSensitive = true;
    }
    public override void RunNativeHandler(string handler)
    {
        if (handler.StartsWith("SpawnForestFairy:", StringComparison.Ordinal))
        { Owner.TrySpawnFlight(int.Parse(handler.AsSpan(handler.IndexOf(':') + 1))); return; }
        switch (handler)
        {
            case "ShowFairyFoundText": Owner.ShowSourceText(0x1105 + System.Numerics.BitOperations.PopCount(Found)); return;
            case "MoveLinkBackLeft": Context.Player.RequestNativeForcedMovement(0x08, ObjectAngle.Left, 3, this); return;
            default: throw UnsupportedCommand($"run '{handler}'");
        }
    }
    public override bool UpdateNativeHandler(string handler, CutsceneActorId? actor, int commandUpdate, int frames, string payload)
    {
        if (handler != "WaitForExitCollision" || actor is not { Value: "FairyExit" } || frames != 1 || payload.Length != 0)
            throw UnsupportedCommand($"update '{handler}'");
        Vector2 delta = Context.Player.Position - new Vector2(_record.X, _record.Y);
        bool collided = Mathf.Abs(delta.Y) < _data.Event.ExitRadiusY + NpcCharacter.LinkCollisionRadius &&
            Mathf.Abs(delta.X) < _data.Event.ExitRadiusX + NpcCharacter.LinkCollisionRadius;
        if (collided) Context.Player.ApplyInteractionInvincibility(0x80);
        return collided;
    }
    public override void ScriptEnded()
    {
        if (_record.SubId == 0) TransferControl();
        else if (_record.SubId == 1)
        {
            byte found = (byte)(Found | (1 << (_hidden.FairyIndex - 3)));
            _memory.SetWramByte(_data.Event.FoundAddress, found);
            if (found != 7)
            {
                SetNativeMenuDisabled(0); SetDisabledObjects(0); EventResources.ScriptInput.SetScreenTransitionsDisabled(0);
            }
            else
            {
                Context.Entities.OnRoomWarpRequested(_data.ReturnWarp with { SourceGroup = _record.Group, SourceRoom = _record.Room });
                // The full reload owns the remaining native control bytes.
                RelinquishInputControl();
            }
        }
        else
            for (int offset = 0; offset < 0x10; offset++) _memory.SetWramByte(_data.Event.ActiveAddress + offset, 0);
        Finished = true;
    }
}
