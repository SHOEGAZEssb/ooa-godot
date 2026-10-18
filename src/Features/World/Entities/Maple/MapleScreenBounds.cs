using Godot;

namespace oracleofages;

// mapleKeepInBounds and itemFromMaple.setDroppedItemPosition share these
// limits. They replace coordinate high bytes, retaining fractional bytes;
// wrapped high bytes $f0-$ff select the lower edge.
internal static class MapleScreenBounds
{
    internal static Vector2 Clamp(Vector2 position) =>
        new(ClampWord(position.X, 0x08, 0x98), ClampWord(position.Y, 0x20, 0x78));

    private static float ClampWord(float coordinate, int minimum, int maximum)
    {
        int word = (int)(coordinate * 256) & 0xffff;
        int high = word >> 8;
        if (high >= 0xf0) high = 0;
        high = System.Math.Clamp(high, minimum, maximum);
        return ((high << 8) | (word & 0xff)) / 256f;
    }
}
