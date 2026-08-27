using Godot;

namespace oracleofages;

/// <summary>
/// Shared shape of the source-ordered native-object streams imported for
/// individual dungeons. Dungeon databases retain placement ownership while
/// globally dispatched interaction handlers consume this common identity.
/// </summary>
internal static class DungeonObjectData
{
    internal static DungeonObjectCondition ParseCondition(
        GeneratedTableRow row,
        int column) =>
        row.RequiredString(column) switch
        {
            "always" => DungeonObjectCondition.Always,
            "item-clear" => DungeonObjectCondition.ItemClear,
            "flag80-clear" => DungeonObjectCondition.Flag80Clear,
            _ => throw row.Invalid(
                column,
                "always, item-clear, or flag80-clear")
        };
}

internal readonly record struct DungeonObjectRecord(
    int Group,
    int Room,
    int Order,
    DungeonObjectKind Kind,
    int Id,
    int SubId,
    int Y,
    int X,
    DungeonObjectCondition Predicate,
    string Source)
{
    internal Vector2 Position => new(X, Y);
}

internal enum DungeonObjectCondition
{
    Always,
    ItemClear,
    Flag80Clear
}

internal enum DungeonObjectKind
{
    BraceletReward,
    RupeeReward,
    FeatherReward,
    Essence,
    BossReward,
    PumpkinHead,
    MovingPlatform,
    SpawnMovingPlatform,
    MinibossReward,
    GiantGhini,
    TorchStairs,
    EnemySmallKey,
    ColoredCube,
    CubeFlame,
    CubeLightSensor,
    CubeTriggerSensor,
    FloorPatternKey,
    ToggleFloor,
    SwitchTileToggler,
    MinecartGate,
    CubeSwitchSensor,
    RedFloorTrigger,
    FloorSwitchBit,
    FloorColorChanger,
    CubeColorSource,
    ColoredBlockKey,
    RedFlameTrigger,
    CircularSidePlatform,
    HeadThwomp,
    Swoop,
    Subterror
}
