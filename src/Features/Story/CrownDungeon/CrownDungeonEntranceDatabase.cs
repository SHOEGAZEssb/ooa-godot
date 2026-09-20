using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace oracleofages;

internal sealed class CrownDungeonEntranceDatabase
{
    internal IReadOnlyList<CutsceneCommand> Commands { get; } =
        CutsceneCommandCatalog.Load("res://assets/oracle/cutscenes/crown_dungeon_commands.tsv");
    internal IReadOnlyList<CrownDungeonEntranceFrame> Frames { get; }

    internal CrownDungeonEntranceDatabase()
    {
        var frames = new List<CrownDungeonEntranceFrame>();
        foreach (GeneratedTableRow row in GeneratedTable.Load(
            "res://assets/oracle/cutscenes/crown_dungeon_frames.tsv",
            new GeneratedTableSchema("Crown Dungeon entrance frames", GeneratedTableKeySemantics.Ordered,
                ["phase", "x", "y", "width", "height", "tiles-and-attributes", "source"], headerRequired: true)).Rows)
        {
            if (row.UnsignedDecimal(0) != frames.Count)
                throw row.Invalid(0, "ordered Crown Dungeon phases 0-3");
            byte[] pairs = row.RequiredString(5).Split(',').Select(value =>
                byte.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte parsed)
                    ? parsed : throw row.Invalid(5, "hexadecimal tile/attribute bytes")).ToArray();
            int width = row.Decimal(3, 1, 6);
            if (pairs.Length != width * row.Decimal(4, 1, 4) * 2)
                throw row.Invalid(5, "complete tile/attribute rectangle");
            frames.Add(new(new Vector2I(row.UnsignedDecimal(1), row.UnsignedDecimal(2)), width, pairs));
        }
        if (frames.Count != 4 || Commands.Count != 14)
            throw new InvalidOperationException("miscPuzzles_subid11: incomplete Crown Dungeon entrance data.");
        Frames = frames;
    }
}

internal readonly record struct CrownDungeonEntranceFrame(Vector2I TopLeft, int Width, byte[] Pairs);
