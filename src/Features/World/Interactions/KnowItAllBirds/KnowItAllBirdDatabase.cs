using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class KnowItAllBirdDatabase
{
    private readonly Dictionary<int, KnowItAllBirdRecord> _records = new();
    internal IReadOnlyList<CutsceneCommand> Commands { get; } = CutsceneCommandCatalog.Load(
        "res://assets/oracle/cutscenes/know_it_all_bird_commands.tsv");

    internal KnowItAllBirdDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/know_it_all_birds.tsv",
            new GeneratedTableSchema("knowItAllBird.s $e3", GeneratedTableKeySemantics.Unique,
                ["subid", "animation0", "animation1", "animation2", "animation3",
                 "tutorial-text", "utf8-base64"], ["subid"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            int subid = row.HexByte(0);
            if (subid >= 10 || row.HexWord(5) != 0x320a + subid)
                throw row.Invalid(0, "knowItAllBird $00..$09 and TX_320a+subid");
            _records.Add(subid, new(
                [row.RequiredString(1), row.RequiredString(2), row.RequiredString(3), row.RequiredString(4)],
                row.HexWord(5), row.Base64Utf8(6)));
        }
        if (_records.Count != 10)
            throw new InvalidOperationException("knowItAllBird.s requires all ten $e3 subids.");
    }

    internal KnowItAllBirdRecord Get(int subid) => _records.TryGetValue(subid, out var record)
        ? record : throw new InvalidOperationException($"Unsupported knowItAllBird $e3:${subid:x2}.");
}

internal sealed record KnowItAllBirdRecord(string[] Animations, int TutorialTextId, string Tutorial);
