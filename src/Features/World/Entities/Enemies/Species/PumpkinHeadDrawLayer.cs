using Godot;

namespace oracleofages;

// The native body, ghost and head are separate enemy OAM entries. Keep their
// drawing separate even though the encounter currently has one gameplay owner.
internal sealed partial class PumpkinHeadDrawLayer(PumpkinHeadBoss owner, int part) : Node2D
{
    public override void _Draw() => owner.DrawPart(this, part);
}
