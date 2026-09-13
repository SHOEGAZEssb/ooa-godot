using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_DUNGEON_EVENTS $21:$01/$05/$10.</summary>
internal sealed partial class DungeonPatternKeyRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly DungeonObjectRecord _record;
    private readonly OracleRoomData _room;
    private readonly int _firstTile;
    private readonly IReadOnlyList<byte>[] _patterns;
    private readonly GroundTreasureGrantRequest _request;
    private readonly OracleSaveData? _saveData;

    public Node2D Node => this;
    public bool Finished { get; private set; }

    internal DungeonPatternKeyRoomEntity(
        DungeonObjectRecord record,
        OracleRoomData room,
        int firstTile,
        IReadOnlyList<byte>[] patterns,
        GroundTreasureGrantRequest request,
        OracleSaveData? saveData)
    {
        _record = record;
        _room = room;
        _firstTile = firstTile;
        _patterns = patterns;
        _request = request;
        _saveData = saveData;
        Name = $"DungeonPatternKey_{record.Group}_{record.Room:x2}";
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (_saveData?.HasRoomFlag(_record.Group, _record.Room, OracleSaveData.RoomFlagItem) == true)
            Finished = true;
        if (Finished || !DungeonTilePattern.Matches(_room, _firstTile, _patterns))
            return;
        spawns.Add(new GroundTreasureGrantSpawn(_request));
        Finished = true;
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

}
