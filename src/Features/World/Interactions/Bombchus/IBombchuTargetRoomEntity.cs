using Godot;

namespace oracleofages;

internal interface IBombchuTargetRoomEntity
{
    BombchuTarget BombchuTarget { get; }
}

internal readonly record struct BombchuTarget(int Id, bool Visible, Vector2 Position, Rect2 Bounds, int Health);
