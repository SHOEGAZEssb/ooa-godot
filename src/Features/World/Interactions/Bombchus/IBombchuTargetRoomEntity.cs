using Godot;

namespace Oracles;

internal interface IBombchuTargetRoomEntity
{
    BombchuTarget BombchuTarget { get; }
}

internal readonly record struct BombchuTarget(int Id, bool Visible, Vector2 Position, Rect2 Bounds, int Health);
