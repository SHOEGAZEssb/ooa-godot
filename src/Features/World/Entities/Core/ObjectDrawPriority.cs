using Godot;

namespace oracleofages;

internal static class ObjectDrawPriority
{
    internal const int TerrainShadowZIndex = 7;
    // Native visible & 3 buckets, ordered ahead of Link within each queue.
    internal const int FixedHighPriorityZIndex = 12; // objectSetVisible80
    internal const int InFrontOfLinkZIndex = 11; // objectSetVisible81
    internal const int BehindLinkZIndex = 9; // objectSetVisible82
    internal const int FixedLowPriorityZIndex = 8; // objectSetVisible83
    internal const int LinkPriorityYOffset = 0x0b;

    internal static int FromVisible(int visible) => (visible & 3) switch
    {
        0 => FixedHighPriorityZIndex,
        1 => InFrontOfLinkZIndex,
        2 => BehindLinkZIndex,
        _ => FixedLowPriorityZIndex
    };
    // bank0.s: objectSetPriorityRelativeToLink/@getPriority. These are byte
    // comparisons of yh/zh, not comparisons of subpixel or screen positions.
    internal static int RelativeToLink(float objectY, float linkY, int zHigh = 0)
    {
        if (((zHigh - 1) & 0xff) < 0x10)
            return FixedLowPriorityZIndex;
        int threshold = (Mathf.FloorToInt(linkY) + LinkPriorityYOffset) & 0xff;
        return (Mathf.FloorToInt(objectY) & 0xff) > threshold
            ? InFrontOfLinkZIndex : BehindLinkZIndex;
    }
}
