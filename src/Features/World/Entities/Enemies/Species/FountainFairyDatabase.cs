using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class FountainFairyDatabase
{
    private readonly Dictionary<int, string> _texts = new();
    internal string HeartSprite { get; }
    internal int HeartTileBase { get; }
    internal int HeartPalette { get; }
    internal bool HeartInverted { get; }
    internal string HeartAnimation { get; }

    internal FountainFairyDatabase()
    {
        GeneratedTable texts = GeneratedTable.Load(
            "res://assets/oracle/objects/great_fairy_text.tsv",
            new GeneratedTableSchema("ENEMY_GREAT_FAIRY $38 dialogue",
                GeneratedTableKeySemantics.Unique,
                ["text-id", "text-base64", "source"], ["text-id"], headerRequired: true));
        foreach (GeneratedTableRow row in texts.Rows)
        {
            int id = row.HexWord(0);
            if (id is not (0x4100 or 0x4105))
                throw row.Invalid(0, "TX_4100 or TX_4105");
            _texts.Add(id, row.Base64Utf8(1));
            _ = row.RequiredString(2);
        }
        if (_texts.Count != 2)
            throw new InvalidOperationException("ENEMY_GREAT_FAIRY $38 requires TX_4100/TX_4105.");
        GeneratedTableRow heart = GeneratedTable.Load(
            "res://assets/oracle/effects/great_fairy_heart.tsv",
            new GeneratedTableSchema("PART_GREAT_FAIRY_HEART $30",
                GeneratedTableKeySemantics.Ordered,
                ["sprite", "tile-base", "palette", "inverted", "animation", "source"],
                headerRequired: true)).SingleRow();
        HeartSprite = heart.RequiredString(0);
        HeartTileBase = heart.UnsignedDecimal(1);
        HeartPalette = heart.UnsignedDecimal(2);
        HeartInverted = heart.Boolean01(3);
        HeartAnimation = heart.RequiredString(4);
        _ = heart.RequiredString(5);
    }

    internal string Text(int id) => _texts[id];
}
