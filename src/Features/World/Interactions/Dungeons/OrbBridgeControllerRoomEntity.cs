using System.Collections.Generic;
using System;

namespace oracleofages;

/// <summary>INTERAC_MISCELLANEOUS_2 $dc:$12, cave bridge trigger.</summary>
internal sealed partial class OrbBridgeControllerRoomEntity : DungeonMechanicRoomEntity,
    IFixedRoomEntity, IRoomEntityLifetime, IScreenTransitionPreloadRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly DungeonMechanicDatabaseRecord _record;
    private readonly OracleSaveData _save;
    private readonly OracleRuntimeState _runtime;
    private readonly System.Action<int> _playSound;
    private readonly int _solveSound;
    private readonly RoomSession? _rooms;
    private readonly Func<BridgeSpawnerSpawn,bool> _createPart;
    public bool Finished { get; private set; }

    internal OrbBridgeControllerRoomEntity(DungeonMechanicDatabaseRecord record,
        OracleSaveData save, OracleRuntimeState runtime, System.Action<int> playSound,
        int solveSound,RoomSession? rooms,Func<BridgeSpawnerSpawn,bool> createPart) : base(record, "OrbBridgeController")
    {
        _record = record;
        _save = save;
        _runtime = runtime;
        _playSound = playSound;
        _solveSound = solveSound;
        _rooms = rooms;
        _createPart = createPart;
        Visible = false;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Advance();

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Advance();
        return ScreenTransitionPresentation.Hidden;
    }

    public void UpdateDuringScreenTransition(RoomEntityFrame frame)
    {
        if (_rooms is null) throw new InvalidOperationException("INTERAC$dc:$12 scrolling requires active room identity.");
        Advance();
    }

    private void Advance()
    {
        if (Finished)
            return;
        int group = _rooms?.ActiveGroup ?? _record.Group;
        int room = _rooms?.CurrentRoom.Id ?? _record.Room;
        if (_save.HasRoomFlag(group, room, OracleSaveData.RoomFlag40))
        {
            Finished = true;
            return;
        }
        if (_runtime.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress) == 0)
            return;

        // updateParts has already run: the new PART_BRIDGE_SPAWNER starts
        // next update, independently of further orb hits.
        // getFreePartSlot failure returns before setting flag$40 or playing
        // Solve. This state-zero handler retries on its next eligible update.
        if (!_createPart(new BridgeSpawnerSpawn(_record.PackedPosition, _record.Parameter))) return;
        _save.SetRoomFlag(group, room, OracleSaveData.RoomFlag40);
        _playSound(_solveSound);
        Finished = true;
    }
}
