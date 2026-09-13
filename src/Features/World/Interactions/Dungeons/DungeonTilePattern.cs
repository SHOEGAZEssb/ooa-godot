using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Shared verifyTiles comparison against the live room layout.</summary>
internal static class DungeonTilePattern
{
    internal static bool Matches(OracleRoomData room, int firstTile, IReadOnlyList<byte>[] patterns)
    {
        for (int color = 0; color < patterns.Length; color++)
            foreach (byte position in patterns[color])
                if (room.GetMetatile(new Vector2((position & 15) * 16 + 8, (position >> 4) * 16 + 8)) != firstTile + color)
                    return false;
        return true;
    }
}
