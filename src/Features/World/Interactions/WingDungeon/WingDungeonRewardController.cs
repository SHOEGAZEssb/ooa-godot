using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>D2 INTERAC_DUNGEON_SCRIPT $20:$00/$01 immediate rewards.</summary>
internal sealed partial class WingDungeonRewardController : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly GroundTreasureGrantRequest _request;

    public Node2D Node => this;
    public bool Finished { get; private set; }

    internal WingDungeonRewardController(
        ObjectRecord record,
        GroundTreasureGrantRequest request)
    {
        _request = request;
        Name = $"WingDungeonReward_{record.Kind}";
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        spawns.Add(new GroundTreasureGrantSpawn(_request));
        Finished = true;
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }
}
