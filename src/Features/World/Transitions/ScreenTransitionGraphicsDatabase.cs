namespace oracleofages;

internal sealed class ScreenTransitionGraphicsDatabase
{
    private readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<ScreenGraphicsUpload>> _uploads = new();
    private readonly (int Unique, int Entries)[] _tilesets = new (int, int)[103];

    internal ScreenTransitionGraphicsDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/screen_transition_graphics.tsv",
            new GeneratedTableSchema("screen transition graphics",
                GeneratedTableKeySemantics.Unique,
                ["tileset", "unique-gfx", "entries", "source"], ["tileset"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int tileset = row.HexByte(0);
            int unique = row.HexByte(1);
            if (tileset >= 103 || unique >= 0x15)
                throw new System.InvalidOperationException(
                    $"Invalid screen_transition_graphics.tsv tileset ${tileset:x2}, unique header ${unique:x2}.");
            _tilesets[tileset] = (unique, row.Decimal(2, 1, 16));
            _ = row.RequiredString(3);
        }
        if (table.Rows.Count != 103)
            throw new System.InvalidOperationException("Expected $67 clean-US tileset transition records.");
        GeneratedTable uploads = GeneratedTable.Load(
            "res://assets/oracle/metadata/screen_transition_uploads.tsv",
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
            if (row.UnsignedDecimal(1) != list.Count || bytes.Length != count * 16 ||
                (count != 0 && (address < 0x8800 || address + count * 16 > 0x9800)) ||
                (count == 0 && palette < 0))
                throw new System.InvalidOperationException($"Invalid VRAM upload {row.RequiredString(6)}.");
            list.Add(new ScreenGraphicsUpload(address, count, palette, bytes));
        }
        foreach (var record in _tilesets)
            if (!_uploads.TryGetValue(record.Unique, out var list) || list.Count != record.Entries)
                throw new System.InvalidOperationException($"uniqueGfxHeader{record.Unique:x2} entry count mismatch.");
    }

    internal (int Unique, int Entries) ForTileset(int tileset) =>
        tileset is >= 0 and < 103 ? _tilesets[tileset] :
            throw new System.InvalidOperationException($"No clean-US transition graphics for tileset ${tileset:x2}.");

    internal System.Collections.Generic.IReadOnlyList<ScreenGraphicsUpload> Uploads(int unique) => _uploads[unique & 0x7f];
}

internal sealed record ScreenGraphicsUpload(int Address, int Tiles, int Palette, byte[] Data);
