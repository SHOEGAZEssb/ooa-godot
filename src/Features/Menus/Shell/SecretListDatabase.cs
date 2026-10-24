using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class SecretListDatabase
{
    private readonly List<SecretListEntry>[] _entries = [new(), new()];
    private readonly Dictionary<int, string> _names = new();
    private readonly LinkedGameNpcDatabase _secrets = new();
    private readonly byte[] _glyphs = new SecretEntryDatabase().Glyphs;
    internal SecretListDatabase()
    {
        foreach (var row in GeneratedTable.Load("res://assets/oracle/menu/secret_list_entries.tsv", new(
            "Farore's secret list", GeneratedTableKeySemantics.Ordered,
            ["linked", "index", "name-mode", "flag", "secret-index", "source"], ["linked", "index"], headerRequired: true)).Rows)
        {
            var entries = _entries[row.Decimal(0, 0, 1)];
            if (row.Decimal(1) != entries.Count) throw row.Invalid(1, "native contiguous secret list order");
            entries.Add(new(row.HexByte(2), row.HexByte(3), row.HexByte(4)));
        }
        foreach (var row in GeneratedTable.Load("res://assets/oracle/menu/secret_list_names.tsv", new(
            "Secret list names", GeneratedTableKeySemantics.Unique,
            ["index", "text-base64", "source"], ["index"], headerRequired: true)).Rows)
            _names.Add(row.HexByte(0), row.Base64Utf8(1));
        if (_entries[0].Count != 10 || _entries[1].Count != 11 || _names.Count != 25)
            throw new InvalidOperationException("bank2.s: incomplete clean-US secret list.");
    }
    internal IReadOnlyList<SecretListEntry> Entries(OracleSaveData save) => _entries[save.IsLinkedGame ? 1 : 0];
    internal string Name(SecretListEntry entry, OracleSaveData save) => save.HasGlobalFlag(entry.Flag)
        ? _names[entry.NameMode & 0x3f] + _names[2] : _names[1];
    internal byte[] Text(SecretListEntry entry, OracleSaveData save)
    {
        var text = new byte[24];
        if (!save.HasGlobalFlag(entry.Flag)) return text;
        int mode = entry.NameMode >> 6;
        if (mode < 2) return _secrets.GenerateGameTransferSecretText(save);
        byte[] values = mode == 2 ? _secrets.GenerateRingSecretValues(save) : _secrets.GenerateSecretValues(entry.SecretIndex, save);
        if (mode == 3)
        { for (int i = 0; i < 5; i++) text[i] = _glyphs[values[i]]; return text; }
        int offset = 0, value = 0;
        foreach (var row in GeneratedTable.Load("res://assets/oracle/objects/linked_secret_ring_spacing.tsv", new(
            "Ring secret spacing", GeneratedTableKeySemantics.Ordered, ["index", "value", "source"], headerRequired: true)).Rows)
        {
            int spacing = row.HexByte(1), count = spacing & 15;
            if (count == 0) break;
            for (int i = 0; i < count; i++) text[offset++] = _glyphs[values[value++]];
            text[offset++] = (byte)(spacing & 0xf0);
        }
        return text;
    }
}

internal readonly record struct SecretListEntry(int NameMode, int Flag, int SecretIndex);
