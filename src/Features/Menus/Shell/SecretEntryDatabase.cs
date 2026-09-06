using System;
using System.Linq;

namespace oracleofages;

internal sealed class SecretEntryDatabase
{
    internal byte[] Glyphs { get; }
    internal int[] LowerOffsets { get; }
    private readonly byte[] _keyboard;
    internal char KeyboardGlyph(int row, int column)
    {
        int tile = _keyboard[row * 64 + 3 + column + (column >= 8 ? 1 : 0)];
        return tile == 2 ? ' ' : (char)Glyphs[(tile - 0x80) / 2];
    }
    internal SecretEntryDatabase()
    {
        _keyboard = Godot.FileAccess.GetFileAsBytes("res://assets/oracle/menu/map_secret_entry_middle.bin");
        Glyphs = GeneratedTable.Load("res://assets/oracle/menu/secret_glyphs.tsv",
            new GeneratedTableSchema("Secret-entry glyphs", GeneratedTableKeySemantics.Ordered,
                ["index", "glyph"], ["index"], headerRequired: true)).Rows.Select(row => (byte)row.HexByte(1)).ToArray();
        LowerOffsets = GeneratedTable.Load("res://assets/oracle/menu/secret_lower_offsets.tsv",
            new GeneratedTableSchema("Secret-entry lower options", GeneratedTableKeySemantics.Ordered,
                ["index", "x"], ["index"], headerRequired: true)).Rows.Select(row => row.Decimal(1)).ToArray();
        if (Glyphs.Length != 64 || LowerOffsets.Length != 4 || _keyboard.Length != 320)
            throw new InvalidOperationException("Incomplete bank2.s secret-entry presentation.");
    }
}
