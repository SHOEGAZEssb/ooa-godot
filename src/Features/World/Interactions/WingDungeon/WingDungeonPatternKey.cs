using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_DUNGEON_EVENTS $21:$01/$05.</summary>
internal sealed partial class WingDungeonPatternKey : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly ObjectRecord _record;
    private readonly OracleRoomData _room;
    private readonly WingDungeonDatabase _data;
    private readonly GroundTreasureGrantRequest _request;

    public Node2D Node => this;
    public bool Finished { get; private set; }

    internal WingDungeonPatternKey(
        ObjectRecord record,
        OracleRoomData room,
        WingDungeonDatabase data,
        GroundTreasureGrantRequest request)
    {
        _record = record;
        _room = room;
        _data = data;
        _request = request;
        Name = $"WingDungeonPatternKey_{record.Room:x2}";
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
        int firstTile = _record.Kind == ObjectKind.FloorPatternKey
            ? _data.Constant("red-toggle-floor")
            : _data.Constant("red-pushable-block");
        for (int color = 0; color < 3; color++)
        {
            IReadOnlyList<byte> positions = _data.Pattern(_record.Kind, color);
            foreach (byte position in positions)
            {
                Vector2 point = new(
                    (position & 0x0f) * 16 + 8,
                    (position >> 4) * 16 + 8);
                if (_room.GetMetatile(point) != firstTile + color)
                    return false;
            }
        }
        return true;
    }
}
