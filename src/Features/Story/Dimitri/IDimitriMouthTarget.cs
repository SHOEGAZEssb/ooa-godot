using Godot;

namespace oracleofages;

internal interface IDimitriMouthTarget
{
    int DimitriCollisionMode { get; }
    int DimitriCollisionType { get; }
    bool TrySwallow(Rect2 hitbox);
}
