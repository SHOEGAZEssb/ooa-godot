namespace Oracles;

public readonly record struct RoomObjectRecord(
    int Group,
    int Room,
    int Order,
    RoomObjectKind Kind,
    int Id,
    int SubId,
    int Flags,
    int Count,
    int Y,
    int X,
    int PackedPosition,
    int ConditionMask)
{
    public int Var03 { get; init; }
    internal int NativeAddress { get; init; }
    internal string? SourceOverride { get; init; }
    public string Source => SourceOverride ??
        $"room {Group:x1}:{Room:x2} object[{Order}] {Kind} ${Id:x2}:${SubId:x2}";
}

public enum RoomObjectKind
{
    RandomEnemy,
    FixedEnemy,
    ParameterEnemy,
    ReservingPart,
    ParameterPart,
    ItemDrop
}
