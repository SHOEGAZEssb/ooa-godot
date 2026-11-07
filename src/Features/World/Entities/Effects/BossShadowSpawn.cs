namespace Oracles;

internal sealed record BossShadowSpawn(
    Func<Vector2> ParentPosition,
    Func<int> ParentZ,
    Func<bool> ParentExists,
    int Size,
    int YOffset) : RoomEntitySpawn(UpdateThisFrame: true);
