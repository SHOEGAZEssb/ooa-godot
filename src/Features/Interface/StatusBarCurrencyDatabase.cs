
namespace Oracles;

// bank2.s:loadCommonGraphics_body and updateStatusBar_body@updateHeartDisplay.
internal sealed class StatusBarCurrencyDatabase
{
    internal int RupeeSelector { get; }
    internal int OreSelector { get; }
    internal int SelectorAddress { get; }
    internal int KeyTile { get; }
    private readonly int _dungeonMask, _noKeyMask, _oreMask, _replacementTile;
    private readonly byte[] _keyGraphics, _oreGraphics;
    private readonly string _source;

    internal StatusBarCurrencyDatabase(string root)
    {
        var row = GeneratedTable.Load(root + "hud/currency.tsv",
            new("native HUD currency", GeneratedTableKeySemantics.Ordered,
                ["rupee-selector", "ore-selector", "selector-address", "dungeon-mask", "no-key-mask", "ore-mask",
                 "key-tile", "replacement-tile", "key-graphics", "ore-graphics", "source"], headerRequired: true)).SingleRow();
        RupeeSelector = row.HexByte(0); OreSelector = row.HexByteOrSentinel(1, "-", -1);
        SelectorAddress = row.HexWordOrSentinel(2, "-", -1);
        if (SelectorAddress != -1 && SelectorAddress is < 0xc000 or > 0xdfff)
            throw row.Invalid(2, "a native WRAM selector address");
        _dungeonMask = row.HexByte(3); _noKeyMask = row.HexByte(4); _oreMask = row.HexByte(5);
        KeyTile = row.HexByte(6); _replacementTile = row.HexByteOrSentinel(7, "-", -1);
        _keyGraphics = ReadGraphics(8); _oreGraphics = ReadGraphics(9); _source = row.RequiredString(10);
        if ((SelectorAddress < 0) != (OreSelector < 0) || (_replacementTile < 0) != (_keyGraphics.Length == 0) ||
            _keyGraphics.Length != _oreGraphics.Length)
            throw row.Invalid(2, "matching native currency selector and graphics capabilities");

        byte[] ReadGraphics(int column)
        {
            string path = row.RequiredString(column);
            if (path == "-") return [];
            byte[] bytes = OracleAssetCache.ReadBytes(root + path);
            if (bytes.Length != 16) throw row.Invalid(column, "one native 2bpp HUD tile");
            return bytes;
        }
    }

    internal int SelectMoney(byte flags) => (flags & _dungeonMask) == 0 && (flags & _oreMask) != 0 ? OreSelector : RupeeSelector;
    internal bool ShowsKeys(byte flags) => (flags & _dungeonMask) != 0 && (flags & _noKeyMask) == 0;
    internal int MoneyValue(InventoryState inventory, int selector) => selector == RupeeSelector ? inventory.Rupees :
        selector == OreSelector && OreSelector >= 0 ? inventory.OreChunks :
        throw new NotSupportedException($"{_source}: unsupported HUD money selector ${selector:x2}.");

    internal bool TryGetReplacementShade(byte loadedFlags, byte tile, byte attributes, int x, int y, out int shade)
    {
        byte[] graphics = (loadedFlags & _dungeonMask) != 0 ? _keyGraphics :
            (loadedFlags & _oreMask) != 0 ? _oreGraphics : [];
        if (tile != _replacementTile || (attributes & 8) != 0 || graphics.Length == 0) { shade = 0; return false; }
        int bit = 7 - x;
        shade = (graphics[y * 2] >> bit & 1) | (graphics[y * 2 + 1] >> bit & 1) << 1;
        return true;
    }
}
