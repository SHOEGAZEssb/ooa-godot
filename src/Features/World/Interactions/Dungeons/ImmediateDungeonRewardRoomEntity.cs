using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_DUNGEON_SCRIPT $20:$00/$01 immediate rewards.</summary>
internal sealed partial class ImmediateDungeonRewardRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly GroundTreasureGrantRequest _request;

    public Node2D Node => this;
    public bool Finished { get; private set; }

    internal ImmediateDungeonRewardRoomEntity(
        DungeonObjectRecord record,
        GroundTreasureGrantRequest request)
    {
        _request = request;
        Name =
            $"ImmediateDungeonReward_{record.Group}_{record.Room:x2}_{record.Kind}";
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
