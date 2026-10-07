using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class VireDialogueDatabase
{
    private readonly Dictionary<int,string> _texts = new();
    internal VireDialogueDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/vire_text.tsv",
            new GeneratedTableSchema("Vire dialogue",GeneratedTableKeySemantics.Unique,
                ["id","message-base64","source"],["id"],headerRequired:true));
        foreach (var row in table.Rows) _texts.Add(row.HexWord(0),System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(row.RequiredString(1))));
        if (_texts.Count != 7) throw new InvalidOperationException("Vire requires TX_2f12..TX_2f18 including linked variants.");
    }
    internal string Text(int id) => _texts.TryGetValue(id,out var text) ? text :
        throw new NotSupportedException($"vire.s: unknown text TX_{id:x4}.");
}
