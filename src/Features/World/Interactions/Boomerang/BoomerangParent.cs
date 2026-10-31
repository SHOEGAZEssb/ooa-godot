namespace Oracles;

// Seasons' state2 holds the shared throw animation while controlling its child.
internal sealed class BoomerangParent(string root, OracleRuntimeState memory) : ItemThrowParent(root, memory, TreasureId.Boomerang)
{
    private BoomerangItem? _child;
    internal bool Controlling { get; private set; }

    internal void Begin(int slot, bool mounted, bool raft, BoomerangItem child, bool primary)
    {
        base.Begin(slot, mounted, raft);
        _child = child;
        ParentItemButtons.Set(Memory, slot, primary ? 1 : 2);
        Controlling = child.SubId != 0;
    }

    internal bool UpdateControl(Player player)
    {
        if (!Active) return false;
        if (Controlling && _child is { Finished: false } child && child.RelatedParentSlot == Slot &&
            ParentItemButtons.IsHeld(Memory, Slot, Input.IsActionPressed("attack"), Input.IsActionPressed("item")))
        {
            int angle = player.LinkMovementAngle;
            child.TargetAngle = (Memory.FrameCounter & 1) == 0 && angle < 0x80 ? angle : child.Angle;
            return true;
        }
        Controlling = false;
        Update();
        return false;
    }

    internal override void Clear()
    {
        base.Clear();
        Controlling = false; _child = null;
    }
}
