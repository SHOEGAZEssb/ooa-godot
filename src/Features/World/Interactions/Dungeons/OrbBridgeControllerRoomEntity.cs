using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_MISCELLANEOUS_2 $dc:$12, cave bridge trigger.</summary>
internal sealed partial class OrbBridgeControllerRoomEntity : DungeonMechanicRoomEntity,
    IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly DungeonMechanicDatabaseRecord _record;
    private readonly OracleSaveData _save;
    private readonly OracleRuntimeState _runtime;
    private readonly System.Action<int> _playSound;
    private readonly int _solveSound;
    public bool Finished { get; private set; }

    internal OrbBridgeControllerRoomEntity(DungeonMechanicDatabaseRecord record,
        OracleSaveData save, OracleRuntimeState runtime, System.Action<int> playSound,
        int solveSound) : base(record, "OrbBridgeController")
    {
        _record = record;
        _save = save;
        _runtime = runtime;
        _playSound = playSound;
        _solveSound = solveSound;
        // tileReplacement_group2Map9e clears this before parsing the objects,
        // even when ROOMFLAG_40 is set. The following orb starts switched off.
        runtime.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress, 0);
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        if (_save.HasRoomFlag(_record.Group, _record.Room, 0x40))
        {
            Finished = true;
            return;
        }
        if (_runtime.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress) == 0)
            return;

        // updateParts has already run: the new PART_BRIDGE_SPAWNER starts
        // next update, independently of further orb hits.
        spawns.Add(new BridgeSpawnerSpawn(_record.PackedPosition, _record.Parameter));
        _save.SetRoomFlag(_record.Group, _record.Room, 0x40);
        _playSound(_solveSound);
        Finished = true;
    }
}
