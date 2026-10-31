namespace Oracles;

internal sealed class ScreenTransitionGraphicsDatabase
{
    public string AssetRoot { get; }

    private readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<ScreenGraphicsUpload>> _uploads = new();
    private readonly System.Collections.Generic.Dictionary<(int Tileset, int State), (int Unique, int Entries)> _tilesets = new();
    private readonly System.Collections.Generic.Dictionary<(int Tileset, int State), int> _palettes = new();

    internal ScreenTransitionGraphicsDatabase(string assetRoot)
    {
        AssetRoot = assetRoot;
        bool seasons = OracleAssetPack.GameFor(assetRoot) == OracleGame.Seasons;
        int tilesets = seasons ? 99 : 103;
        int headers = seasons ? 41 : 21;

        GeneratedTable table = GeneratedTable.Load(
            AssetRoot + "metadata/screen_transition_graphics.tsv",
            new GeneratedTableSchema("screen transition graphics",
                GeneratedTableKeySemantics.Unique,
                ["tileset", "state-modifier", "unique-gfx", "entries", "palette-header", "source"], ["tileset", "state-modifier"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int tileset = row.HexByte(0);
            int state = row.Decimal(1, -1, seasons ? 3 : -1);
            int unique = row.HexByte(2);
            if (tileset >= tilesets || unique >= headers || (seasons && tileset < 0x1b) != (state >= 0))
                throw new System.InvalidOperationException(
                    $"Invalid screen_transition_graphics.tsv tileset ${tileset:x2}, unique header ${unique:x2}.");
            _tilesets.Add((tileset, state), (unique, row.Decimal(3, 1, 16)));
            _palettes.Add((tileset, state), row.HexByte(4));
            _ = row.RequiredString(5);
        }
        if (table.Rows.Count != (seasons ? 180 : 103))
            throw new System.InvalidOperationException($"{AssetRoot}: incomplete native tileset transition records.");
        GeneratedTable uploads = GeneratedTable.Load(
            AssetRoot + "metadata/screen_transition_uploads.tsv",
            new GeneratedTableSchema("screen transition VRAM uploads", GeneratedTableKeySemantics.Unique,
                ["unique-gfx", "order", "vram-address", "tiles", "palette-header", "data", "source"],
                ["unique-gfx", "order"], headerRequired: true));
        foreach (GeneratedTableRow row in uploads.Rows)
        {
            int id = row.HexByte(0);
            if (!_uploads.TryGetValue(id, out var list))
                _uploads.Add(id, list = new());
            int address = row.HexWord(2);
            int count = row.Decimal(3, 0, 256);
            int palette = row.HexByteOrSentinel(4, "-", -1);
            byte[] bytes = count == 0 ? [] : System.Convert.FromHexString(row.RequiredString(5));
            if (id >= headers || row.UnsignedDecimal(1) != list.Count || bytes.Length != count * 16 ||
                (count != 0 && (address < 0x8800 || address + count * 16 > 0x9800)) ||
                (count == 0 && palette < 0))
                throw new System.InvalidOperationException($"Invalid VRAM upload {row.RequiredString(6)}.");
            list.Add(new ScreenGraphicsUpload(address, count, palette, bytes));
        }
        if (_uploads.Count != headers)
            throw new System.InvalidOperationException($"{AssetRoot}: incomplete native unique graphics headers.");
        foreach (var record in _tilesets.Values)
            if (!_uploads.TryGetValue(record.Unique, out var list) || list.Count != record.Entries)
                throw new System.InvalidOperationException($"uniqueGfxHeader{record.Unique:x2} entry count mismatch.");
    }

    internal (int Unique, int Entries) ForTileset(int tileset, int stateModifier = -1) =>
        _tilesets.TryGetValue((tileset, -1), out var record) ||
        _tilesets.TryGetValue((tileset, stateModifier), out record) ? record :
            throw new System.InvalidOperationException($"{AssetRoot}: no native transition graphics for tileset ${tileset:x2}, state ${stateModifier:x2}.");

    internal System.Collections.Generic.IReadOnlyList<ScreenGraphicsUpload> Uploads(int unique) => _uploads[unique & 0x7f];
    internal int PaletteForTileset(int tileset, int stateModifier) =>
        _palettes.TryGetValue((tileset, -1), out int palette) ||
        _palettes.TryGetValue((tileset, stateModifier), out palette) ? palette :
            throw new System.InvalidOperationException($"{AssetRoot}: no native palette for tileset ${tileset:x2}, state ${stateModifier:x2}.");
}

internal sealed record ScreenGraphicsUpload(int Address, int Tiles, int Palette, byte[] Data);
