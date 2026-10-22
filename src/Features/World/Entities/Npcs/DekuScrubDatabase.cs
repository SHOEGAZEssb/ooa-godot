using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Source animations used by INTERAC_DEKU_SCRUB $d6:$00.</summary>
internal sealed class DekuScrubDatabase
{
    private readonly Dictionary<int, string> _animations = new();

    public DekuScrubDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/deku_scrub_animations.tsv",
            new GeneratedTableSchema(
                "Deku Scrub $d6:$00 animations",
                GeneratedTableKeySemantics.Unique,
                ["animation", "encoded-animation"],
                ["animation"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
            _animations.Add(row.HexByte(0), row.RequiredString(1));
        if (_animations.Count != 3 ||
            !_animations.ContainsKey(0) ||
            !_animations.ContainsKey(1) ||
            !_animations.ContainsKey(3))
        {
            throw new InvalidOperationException(
                "Deku Scrub $d6:$00 source animations $00/$01/$03 are incomplete.");
        }
    }

    public string Animation(int id) =>
        _animations.TryGetValue(id, out string? animation)
            ? animation
            : throw new KeyNotFoundException(
                $"Deku Scrub $d6:$00 animation ${id:x2} was not imported.");
}
