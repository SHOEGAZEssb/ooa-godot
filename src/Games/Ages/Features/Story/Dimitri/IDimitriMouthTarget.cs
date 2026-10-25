using Godot;

namespace Oracles;

internal interface IDimitriMouthTarget
{
    int DimitriCollisionMode { get; }
    int DimitriCollisionType { get; }
    bool TrySwallow(Rect2 hitbox);
}
