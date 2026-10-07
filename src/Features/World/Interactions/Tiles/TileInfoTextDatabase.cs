using System;
using System.Collections.Generic;

namespace oracleofages;

// code/interactableTiles.s:showInfoTextForTile@data. Several tile kinds share
// one suppression bit; the live byte belongs to RoomSession's runtime memory.
internal sealed class TileInfoTextDatabase
{
    private readonly Dictionary<int,TileInfoTextRecord> _texts = new();

    internal TileInfoTextDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/metadata/tile_info_texts.tsv",
            new GeneratedTableSchema("showInfoTextForTile",GeneratedTableKeySemantics.Unique,
                ["index","mask","text-id","message-utf8-base64","source"],["text-id"],headerRequired:true));
        foreach (var row in table.Rows)
        {
            int index = row.Decimal(0,0,9); byte mask = (byte)row.HexByte(1); int text = row.HexWord(2);
            string message = row.Base64Utf8(3),source = row.RequiredString(4);
            if (index != _texts.Count || mask == 0 || string.IsNullOrWhiteSpace(message) ||
                !source.Contains("showInfoTextForTile@data",StringComparison.Ordinal))
                throw row.Invalid(0,"ordered nonempty showInfoTextForTile source row");
            _texts.Add(text,new(mask,message));
        }
        if (_texts.Count != 10) throw new InvalidOperationException("showInfoTextForTile@data requires10 source rows.");
    }

    internal TileInfoTextRecord Get(int textId) => _texts.TryGetValue(textId,out var text) ? text :
        throw new NotSupportedException($"code/interactableTiles.s:showInfoTextForTile has no TX_{textId:x4}.");
}

internal readonly record struct TileInfoTextRecord(byte Mask,string Message);
