using System;

namespace Oracles;

// bank2.s:drawTreasureExtraTiles is shared by inventory and the status bar.
internal static class TreasureExtraTiles
{
    internal static int Write(OracleGame game, DisplayRecord display, InventoryState inventory,
        bool equipped, Span<TreasureExtraTile> tiles)
    {
        int flags = equipped ? 0x80 : 7;
        switch (display.ExtraMode)
        {
            case 0:
                int level = inventory.LevelForInventoryDisplay(display.TreasureId);
                if (level <= 0) return 0;
                tiles[0] = new(0x1a, flags, 0, 0);
                tiles[1] = new(0x10 + (level & 15), flags, 8, 0);
                return 2;
            case 1:
            case 4:
                int amount = display.ExtraMode == 4 ? inventory.Slates : inventory.TreasureQuantityDisplayValue(display.TreasureId);
                tiles[0] = new(display.ExtraMode == 4 ? 0x1b : 0x10 + ((amount >> 4) & 15), flags, 0, 0);
                tiles[1] = new(0x10 + (amount & 15), flags, 8, 0);
                return 2;
            case 2 when game == OracleGame.Ages:
                flags = equipped ? 0x80 : 0x84;
                tiles[0] = new(0x1c, flags, -8, -8);
                tiles[1] = new(0x1e, flags, 0, -8);
                tiles[2] = new(0x1d, flags, -8, 0);
                tiles[3] = new(0x1f, flags, 0, 0);
                return 4;
            case 2 when game == OracleGame.Seasons:
                int count = 0;
                for (int season = 0; season < 4; season++)
                {
                    if ((inventory.ObtainedSeasons & (1 << season)) == 0) continue;
                    tiles[count++] = new(0x1c + season, flags,
                        season >= 2 ? 8 : 0, season is 1 or 2 ? -8 : 0);
                }
                return count;
            case 3 when game == OracleGame.Seasons && (inventory.MagnetGlovePolarity & 1) != 0:
                tiles[0] = new(0x0a, flags | 0x80, 0, 0);
                return 1;
            case 3:
            case 0xff:
                return 0;
            default:
                throw new NotSupportedException($"bank2.s:drawTreasureExtraTiles: unsupported {game} mode ${display.ExtraMode:x2}.");
        }
    }
}

internal readonly record struct TreasureExtraTile(int Tile, int Attributes, int X, int Y);
