using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class ChestContentsDatabase
{
    private readonly Dictionary<(int Subid, bool Linked), string> _objects = new();

    internal ChestContentsDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/chest_contents_overrides.tsv",
            new GeneratedTableSchema("miscellaneous2.s chest overrides", GeneratedTableKeySemantics.Unique,
                ["subid", "linked", "treasure-object", "source"], ["subid", "linked"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            int subid = row.HexByte(0);
            if (subid is not (0x15 or 0x16)) throw row.Invalid(0, "INTERAC$dc subid$15/$16");
            _ = row.RequiredString(3);
            _objects.Add((subid, row.Boolean01(1)), row.RequiredString(2));
        }
        if (_objects.Count != 4) throw new InvalidOperationException("INTERAC$dc:$15/$16 requires four linked/unlinked chest selections.");
    }

    internal string Object(int subid, bool linked) => _objects[(subid, linked)];
}
