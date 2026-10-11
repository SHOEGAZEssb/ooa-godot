using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

internal sealed class PalaceSoldierDatabase
{
    internal IReadOnlyList<CutsceneCommand> Commands { get; } = CutsceneCommandCatalog.Load(
        "res://assets/oracle/npcs/palace_friendly_soldier_commands.tsv");
    internal string[] Animations { get; } = GeneratedTable.Load(
        "res://assets/oracle/npcs/palace_soldier_animations.tsv",
        new GeneratedTableSchema("palace soldier animations", GeneratedTableKeySemantics.Unique,
            ["index", "animation"], ["index"], headerRequired: true))
        .Rows.Select(row => row.RequiredString(1)).ToArray();
    private readonly Dictionary<int, PalaceSoldierRecord> _records = new();

    internal PalaceSoldierDatabase()
    {
        foreach (var row in GeneratedTable.Load("res://assets/oracle/npcs/palace_friendly_soldiers.tsv",
            new GeneratedTableSchema("friendly palace soldiers", GeneratedTableKeySemantics.Unique,
                ["index", "mode", "palette", "text-id", "utf8-base64", "source"], ["index"], headerRequired: true)).Rows)
        {
            _records.Add(row.Decimal(0, 0, 15), new(row.Decimal(1, 0, 3), row.Decimal(2, 1, 2),
                row.HexWord(3), row.Base64Utf8(4)));
            _ = row.RequiredString(5);
        }
        if (_records.Count != 16 || Animations.Length != 4)
            throw new InvalidOperationException("soldier.s:$40:$0d tables are incomplete.");
    }
    internal PalaceSoldierRecord Require(int index) => _records.TryGetValue(index, out var record)
        ? record : throw new InvalidOperationException($"soldier.s:$40:$0d: unknown var03 ${index:x2}.");
}

internal sealed record PalaceSoldierRecord(int Mode, int Palette, int TextId, string Message);
