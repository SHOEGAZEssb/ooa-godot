using System;
using System.Collections.Generic;
using Godot;

namespace oracleofages;

internal sealed class ScreenTransitionPaletteDatabase
{
    private readonly Dictionary<(int Group, int Room, int Direction), (byte[] Source, byte[] Destination)> _routes = new();

    internal ScreenTransitionPaletteDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/screen_transition_palettes.tsv",
            new GeneratedTableSchema("smooth screen palettes", GeneratedTableKeySemantics.Unique,
                ["group", "room", "direction", "source-rgb5", "destination-rgb5", "source"],
                ["group", "room", "direction"], headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            byte[] from = Convert.FromHexString(row.RequiredString(3));
            byte[] to = Convert.FromHexString(row.RequiredString(4));
            if (from.Length != 72 || to.Length != 72 || Array.Exists(from, b => b > 31) || Array.Exists(to, b => b > 31))
                throw new InvalidOperationException($"Invalid RGB5 palette {row.RequiredString(5)}.");
            _routes.Add((row.Decimal(0, 0, 2), row.HexByte(1), row.Decimal(2, 0, 3)), (from, to));
        }
        if (_routes.Count != 21) throw new InvalidOperationException("Expected 21 paletteTransitionData routes.");
    }

    internal bool TryGet(int group, int room, Vector2I direction, OracleSaveData save,
        out (byte[] Source, byte[] Destination) palettes)
    {
        palettes = default;
        // checkSymmetryCityPaletteTransition, GLOBALFLAG_TUNI_NUT_PLACED.
        if (group == 0 && (room is 0x12 or 0x22 or 0x14 or 0x24) && save.HasGlobalFlag(0x29))
            return false;
        int index = direction == Vector2I.Up ? 0 : direction == Vector2I.Right ? 1 : direction == Vector2I.Down ? 2 : 3;
        return _routes.TryGetValue((group, room, index), out palettes);
    }
}
