using Godot;

namespace oracleofages;

internal sealed class CompanionUpdateGate : RoomEntityAdapter<Node2D>, IPlayerRestriction
{
    internal CompanionUpdateGate() : base(new Node2D(), static _ => { }) { }
    internal bool Active { get; set; }
    public bool DisablesCompanion => Active;
    public bool DisablesSword => false;
}
