using System;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CompareParentAnimationPublicationRom(Player player, Func<int, byte> native, string context)
    {
        var memory = player.Inventory.RuntimeState;
        int best = 0, page = 0xd2;
        for (int candidate = 0xd2; candidate < 0xd6; candidate++)
        {
            int claim = native((candidate << 8) | 0x3f);
            FailIf(memory.ReadWramByte((candidate << 8) | 0x3f) != claim,
                $"{context}: parent${candidate:x2} claim ${memory.ReadWramByte((candidate << 8) | 0x3f):x2}, native ${claim:x2}.");
            if (claim < best) continue;
            page = candidate; best = claim;
        }
        if (native(0xd03f) >= best) page = 0xd0;
        int actual = SomariaPrivate<int>(player, "_publishedParentPage");
        FailIf(actual != page || page != 0xd0 && SomariaPrivate<int>(player, "_publishedParentId") != native((page << 8) | 1),
            $"{context}: graphics publication page${actual:x2}, native page${page:x2}/ITEM${native((page << 8) | 1):x2}.");
    }
}
