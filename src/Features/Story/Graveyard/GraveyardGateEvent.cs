using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Runs interactiondcSubid01Script after room 0:5c's Graveyard Key keyhole
/// writes cfc0 bit 0. The controller stays armed without blocking gameplay
/// until that signal arrives.
/// </summary>
internal sealed class GraveyardGateEvent : IRoomEntryEvent, ICutsceneCommandHost
{

    private readonly RoomEventContext _context;
    private readonly GraveyardGateEventDatabase _database = new();
    private readonly GraveyardGateEventDatabaseEventRecord _record;
    private readonly CutsceneCommandRunner _runner;
    private GraveyardGateEventEventStage _stage;
    private int _shakeCounter;
    private bool _inputEnabled = true;

    internal GraveyardGateEvent(RoomEventContext context)
    {
        _context = context;
        _record = _database.Record;
        _runner = new CutsceneCommandRunner(this);
    }

    public bool HasState => _stage is GraveyardGateEventEventStage.WaitingForKeyhole or GraveyardGateEventEventStage.Running;
    public bool BlocksGameplay => _stage == GraveyardGateEventEventStage.Running;
    internal GraveyardGateEventEventStage Stage => _stage;
    internal int Counter => _runner.Counter;
    internal int CurrentCommandIndex =>
        _runner.CurrentCommand?.Source.CommandIndex ?? -1;
    internal int CurrentCommandUpdates => _runner.CurrentCommandUpdates;
    internal int ShakeCounter => _shakeCounter;
    internal GraveyardGateEventDatabaseEventRecord Record => _record;

    public bool Matches(int group, OracleRoomData room) =>
        group == _record.Group && room.Id == _record.Room &&
        !_context.Rooms.SaveData.HasRoomFlag(group, room.Id, _record.RoomFlag);

    public void Start(OracleRoomData room)
    {
        Cancel();
        if (room.Id != _record.Room || _context.Rooms.ActiveGroup != _record.Group)
        {
            throw new InvalidOperationException(
                $"Graveyard gate event cannot start in " +
                $"{_context.Rooms.ActiveGroup:x}:{room.Id:x2}.");
        }
        _stage = GraveyardGateEventEventStage.WaitingForKeyhole;
    }

    internal bool CanTrigger(int group, int room) =>
        group == _record.Group && room == _record.Room &&
        _stage == GraveyardGateEventEventStage.WaitingForKeyhole;

    internal void RetireCompletedControllerOnRoomLoad()
    {
        if (_stage == GraveyardGateEventEventStage.Completed)
            _stage = GraveyardGateEventEventStage.Inactive;
    }

    internal void Trigger(int group, int room)
    {
        if (!CanTrigger(group, room) ||
            !_context.Rooms.SaveData.HasRoomFlag(group, room, _record.RoomFlag))
        {
            throw new InvalidOperationException(
                $"Room {group:x}:{room:x2} cannot trigger interactiondcSubid01Script.");
        }

        _inputEnabled = false;
        _context.Player.BeginCutsceneControl();
        _stage = GraveyardGateEventEventStage.Running;
        _runner.Start(_database.Commands);
    }

    public void UpdateFrame()
    {
        if (_stage != GraveyardGateEventEventStage.Running)
            return;
        _runner.AdvanceFrame();
        UpdateScreenShake();
    }

    public void Cancel()
    {
        _runner.Clear();
        if (!_inputEnabled)
            _context.Player.EndCutsceneControl();
        _context.RoomCamera.Offset = Vector2.Zero;
        _stage = GraveyardGateEventEventStage.Inactive;
        _shakeCounter = 0;
        _inputEnabled = true;
    }

    RoomEventContext ICutsceneCommandHost.Context => _context;
    bool ICutsceneCommandHost.HasActorBinding(CutsceneActorId actor) => false;

    void ICutsceneCommandHost.SetInputEnabled(bool enabled)
    {
        if (!enabled)
            throw Unsupported("disable input from the command stream");
        _context.Player.EndCutsceneControl();
        _inputEnabled = true;
    }

    void ICutsceneCommandHost.SetMenuEnabled(bool enabled) =>
        throw Unsupported($"set menu enabled={enabled}");

    void ICutsceneCommandHost.SetDisabledObjects(int value) =>
        throw Unsupported($"set disabled objects ${value:x2}");

    bool ICutsceneCommandHost.GateOpen(string gate) =>
        throw Unsupported($"read gate '{gate}'");

    bool ICutsceneCommandHost.MemoryEquals(string binding, int value) =>
        throw Unsupported($"read '{binding}'=${value:x2}");

    void ICutsceneCommandHost.ShowText(int textId, string message) =>
        throw Unsupported($"show TX_{textId:x4}");

    void ICutsceneCommandHost.SetActorAnimation(
        string actor, int animation, string encodedAnimation) =>
        throw Unsupported($"set actor '{actor}' animation ${animation:x2}");

    void ICutsceneCommandHost.SetActorMovementAnimation(
        string actor, int angle, string encodedAnimation) =>
        throw Unsupported($"set actor '{actor}' movement angle ${angle:x2}");

    void ICutsceneCommandHost.SetActorCollisionRadii(
        string actor, int radiusY, int radiusX) =>
        throw Unsupported($"set actor '{actor}' collision radii");

    void ICutsceneCommandHost.SetActorButtonSensitive(string actor) =>
        throw Unsupported($"make actor '{actor}' A-button sensitive");

    void ICutsceneCommandHost.MoveActorAtSpeed(string actor, int speed, int angle) =>
        throw Unsupported($"move actor '{actor}'");

    void ICutsceneCommandHost.SetActorZ(string actor, int zFixed) =>
        throw Unsupported($"set actor '{actor}' Z");

    void ICutsceneCommandHost.SetActorVisible(string actor, bool visible) =>
        throw Unsupported($"set actor '{actor}' visible={visible}");

    void ICutsceneCommandHost.WriteMemory(string binding, int value) =>
        throw Unsupported($"write '{binding}'=${value:x2}");

    void ICutsceneCommandHost.SetMusic(int music)
    {
        if (music == OracleSoundEngine.SndCtrlStopMusic)
            _context.Sound.PlaySound(music);
        else if (music == 0xff)
            _context.Sound.PlayRoomMusic(_record.Group, _record.Room);
        else
            throw Unsupported($"set music ${music:x2}");
    }

    void ICutsceneCommandHost.OrRoomFlag(int flag) =>
        throw Unsupported($"OR room flag ${flag:x2}");

    void ICutsceneCommandHost.RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "RemoveGateTiles1":
                RemoveGateTiles1();
                return;
            case "RemoveGateTiles2":
                RemoveGateTiles2();
                return;
            default:
                throw Unsupported($"run native handler '{handler}'");
        }
    }

    void ICutsceneCommandHost.ScriptEnded()
    {
        _context.RoomCamera.Offset = Vector2.Zero;
        _shakeCounter = 0;
        _stage = GraveyardGateEventEventStage.Completed;
    }

    private void RemoveGateTiles1()
    {
        BeginShake();
        foreach (int position in _record.Phase1Ordinary)
            SetOrdinaryTile(position);
        foreach (InterleavedTile tile in
            _record.Phase1Interleaved)
        {
            _context.Rooms.CurrentRoom.SetInterleavedMetatile(
                PackedCenter(tile.Position), tile.Tile1, tile.Tile2, tile.Type,
                _context.AnimationTick());
        }
        SpawnPuffs(_record.Phase1Puffs);
    }

    private void RemoveGateTiles2()
    {
        BeginShake();
        foreach (int position in _record.Phase2Ordinary)
            SetOrdinaryTile(position);
        SpawnPuffs(_record.Phase2Puffs);
    }

    private void SetOrdinaryTile(int position) =>
        _context.Rooms.CurrentRoom.SetPositionTileAndCollision(
            PackedCenter(position), _record.ClearTile, null,
            _context.AnimationTick());

    private void SpawnPuffs(System.Collections.Generic.IReadOnlyList<Vector2> positions)
    {
        foreach (Vector2 position in positions)
        {
            PuzzlePuffEffect puff = _context.Entities.Spawn<PuzzlePuffEffect>(
                new PuzzlePuffSpawn(position, OracleSoundEngine.SndPoof));
            // The controller precedes its newly allocated puff slots, so each
            // puff receives state 0 in this same original interaction pass.
            puff.UpdateFrame();
        }
    }

    private void BeginShake() => _shakeCounter = _record.ShakeFrames;

    private void UpdateScreenShake()
    {
        if (_shakeCounter <= 0)
        {
            _context.RoomCamera.Offset = Vector2.Zero;
            return;
        }

        int[] amounts = [-2, -1, 1, 2];
        int y = amounts[_context.Entities.NextRandomValue() & 3];
        int x = amounts[_context.Entities.NextRandomValue() & 3];
        _context.RoomCamera.Offset = new Vector2(x, y);
        _shakeCounter--;
        if (_shakeCounter == 0)
            _context.RoomCamera.Offset = Vector2.Zero;
    }

    private static Vector2 PackedCenter(int packed) => new(
        (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
        (packed >> 4) * OracleRoomData.MetatileSize + 8);

    private static InvalidOperationException Unsupported(string operation) =>
        new($"Room 0:5c graveyard gate cannot {operation}.");
}
