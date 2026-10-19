using Godot;

namespace oracleofages;

// Motion-only ROM fixtures omit the post-object collision scan in both hosts.
internal sealed class ProjectileMotionContactSuppression()
    : RoomEntityAdapter<Node2D>(new Node2D(), static _ => { }), IPlayerRestriction
{
    public bool DisablesSword => false;
    public bool DisablesPlayerContact => true;
}
