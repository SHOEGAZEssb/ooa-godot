using Godot;

namespace oracleofages;

// Stationary collision recipient; only the existing mouth-target boundary is
// represented here. The native fixture executes the mask/effect dispatcher.
internal sealed class CompanionMouthProbe(int type, int mode, Vector2 position, bool vulnerable)
    : RoomEntityAdapter<Node2D>(new Node2D { Position = position }, static _ => { }), IDimitriMouthTarget
{
    public int DimitriCollisionType => type;
    public int DimitriCollisionMode => mode;
    internal bool Swallowed { get; private set; }
    public bool TrySwallow(Rect2 hitbox)
    {
        if (Swallowed || !vulnerable || !RoomEntityManager.ObjectCollisionXYOverlaps(hitbox,
                new Rect2(Node.Position - new Vector2(6, 6), new(12, 12)))) return false;
        Swallowed = true;
        return true;
    }
}
