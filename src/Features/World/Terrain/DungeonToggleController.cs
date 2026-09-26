using System;

namespace oracleofages;

internal sealed class DungeonToggleController : IPlayerRestriction
{
    private readonly RoomSession _rooms;
    private readonly OracleRuntimeState _runtime;
    private readonly DungeonToggleTileDatabase _data = new();
    private readonly RoomEntityManager _entities;
    private readonly Action<int> _sound;
    private readonly Func<long> _tick;
    internal int State { get; private set; } = -1;
    internal int Counter { get; private set; }
    internal bool Active => State >= 0;
    internal bool Frozen => State > 0;
    public bool DisablesSword => false;
    public bool FreezesPlayerUpdates => Frozen;
    public bool DisablesMenus => Active;
    public bool DisablesPlayerContact => Frozen;
    public bool DisablesCompanion => Frozen;

    internal DungeonToggleController(RoomSession rooms, OracleRuntimeState runtime, RoomEntityManager entities,
        Action<int> sound, Func<long> tick)
    {
        _rooms = rooms; _runtime = runtime; _entities = entities; _sound = sound; _tick = tick;
        rooms.RoomChanged += (_, _) => Reset();
        Reset();
    }

    private byte Current => _runtime.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress);
    // cutscene00 samples this after the last loading object's update. An
    // entrance's state0 may have reset the byte after RoomChanged fired.
    internal void CompleteRoomInitialization() => Reset();
    private void Reset()
    {
        State = -1; Counter = 0;
        _runtime.SetWramByte(WramAddress.wLastToggleBlocksState, Current);
    }

    internal void CheckAfterObjects()
    {
        if (Active || !_data.Supports(_rooms.CurrentDungeonIndex)) return;
        if (((Current ^ _runtime.ReadWramByte(WramAddress.wLastToggleBlocksState)) & 1) != 0)
            State = 0;
    }

    internal void AdvanceBeforeObjects()
    {
        if (State < 0) return;
        if (State == 0) { Counter = _data.Delay; State = 1; return; }
        if (State == 1)
        {
            _sound(SoundId.SndDoorClose);
            _data.Upload(_rooms.CurrentRoom, 2, _tick());
            State = 2;
            return;
        }
        if (--Counter != 0) return;
        _data.Complete(_rooms, _entities.TryCreateRockDebris, _tick());
        _data.Upload(_rooms.CurrentRoom, Current == 0 ? 0 : 1, _tick());
        Reset();
    }
}
