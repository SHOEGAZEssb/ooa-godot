using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

internal sealed class ZeldaRescueDatabase
{
    private readonly Dictionary<string, IReadOnlyList<CutsceneCommand>> _commands = new();
    private readonly Dictionary<string, Dictionary<int, GeneratedTableRow>> _animations = new();

    internal ZeldaRescueDatabase()
    {
        foreach (string actor in new[] { "Impa", "Bird", "Zelda" })
        {
            string path = "res://assets/oracle/cutscenes/zelda_rescue_" + actor.ToLowerInvariant();
            _commands.Add(actor, CutsceneCommandCatalog.Load(path + "_commands.tsv"));
            _animations.Add(actor, GeneratedTable.Load(path + "_animations.tsv",
                new GeneratedTableSchema("Zelda rescue animations", GeneratedTableKeySemantics.Unique,
                    ["index", "sprite", "tile-base", "palette", "animation", "default-animation", "source"], ["index"], headerRequired: true)).Rows.ToDictionary(row => row.UnsignedDecimal(0)));
            int[] indices = actor switch { "Impa" => [0, 1, 2, 3, 9], "Bird" => [0, 2], _ => [0, 1, 2, 3] };
            if (!_animations[actor].Keys.SequenceEqual(indices)) throw new InvalidOperationException("Room $0:$65 executed animation table is incomplete or reordered.");
            foreach (GeneratedTableRow row in _animations[actor].Values)
            {
                _ = row.RequiredString(4);
                _ = row.RequiredString(6);
            }
        }
    }

    internal IReadOnlyList<CutsceneCommand> Commands(string actor) => _commands[actor];
    internal string Animation(string actor, int index) => _animations[actor][index].RequiredString(4);
    internal NpcRecord ZeldaRecord()
    {
        GeneratedTableRow row = _animations["Zelda"][0];
        return new(0, 0x65, 0xad, 6, 0x8c, 0x50, 0, 0, row.RequiredString(1),
            row.UnsignedDecimal(2), row.UnsignedDecimal(3), row.UnsignedDecimal(5), false,
            Animation("Zelda", 0), Animation("Zelda", 1), Animation("Zelda", 2), Animation("Zelda", 3),
            string.Empty, NpcImplementationClassification.EventOwned);
    }
}
