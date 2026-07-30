using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_DUNGEON_EVENTS $21:$01/$05.</summary>
internal sealed partial class DungeonPatternKeyRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly DungeonObjectRecord _record;
    private readonly OracleRoomData _room;
    private readonly int _firstTile;
    private readonly IReadOnlyList<byte>[] _patterns;
    private readonly GroundTreasureGrantRequest _request;

    public Node2D Node => this;
    public bool Finished { get; private set; }

    internal DungeonPatternKeyRoomEntity(
        DungeonObjectRecord record,
        OracleRoomData room,
        int firstTile,
        IReadOnlyList<byte>[] patterns,
        GroundTreasureGrantRequest request)
    {
        _record = record;
        _room = room;
        _firstTile = firstTile;
        _patterns = patterns;
        _request = request;
        Name = $"DungeonPatternKey_{record.Group}_{record.Room:x2}";
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished || !PatternMatches())
            return;
        spawns.Add(new GroundTreasureGrantSpawn(_request));
        Finished = true;
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

    private bool PatternMatches()
    {
        for (int color = 0; color < 3; color++)
        {
            IReadOnlyList<byte> positions = _patterns[color];
            foreach (byte position in positions)
            {
                Vector2 point = new(
                    (position & 0x0f) * 16 + 8,
                    (position >> 4) * 16 + 8);
                if (_room.GetMetatile(point) != _firstTile + color)
                    return false;
            }
        }
        return true;
    }
}
