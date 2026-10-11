using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

internal sealed class SeaFairyDatabase
{
    private readonly Dictionary<(string, int), NpcRecord> _visuals = [];
    private readonly Dictionary<string, int> _offsets = [];
    internal int SourceOffset(string role) => _offsets[role];
    internal IReadOnlyList<CutsceneCommand> Commands { get; } = CutsceneCommandCatalog.Load("res://assets/oracle/cutscenes/sea_fairy_commands.tsv");
    internal IReadOnlyList<CutsceneCommand> ZoraCommands(int subid) => CutsceneCommandCatalog.Load($"res://assets/oracle/cutscenes/clean_seas_zora_{subid:x2}.tsv");
    internal string Thanks { get; }
    internal (int Group, int Room)[] Rooms { get; }
    internal (int Acceleration, int SpeedZ, int Counter)[] PowderMotion { get; }
    internal int[] Timing { get; }

    internal SeaFairyDatabase()
    {
        var visuals = GeneratedTable.Load("res://assets/oracle/objects/sea_fairy_visuals.tsv",
            new GeneratedTableSchema("sea fairy visuals", GeneratedTableKeySemantics.Unique,
                ["role", "id", "subid", "sprite", "tile-base", "palette", "animation-index", "animation", "source-offset"],
                ["role", "animation-index"], headerRequired: true));
        foreach (var row in visuals.Rows)
        {
            string animation = row.RequiredString(7);
            _offsets[row.RequiredString(0)] = row.UnsignedDecimal(8);
            _visuals.Add((row.RequiredString(0), row.UnsignedDecimal(6)), new NpcRecord(3, 0xef,
                row.HexByte(1), row.HexByte(2), 0, 0, 0, 0, row.RequiredString(3), row.UnsignedDecimal(4),
                row.UnsignedDecimal(5), row.UnsignedDecimal(6), false, animation, animation, animation, animation,
                "", NpcImplementationClassification.EventOwned));
        }
        var texts = GeneratedTable.Load("res://assets/oracle/cutscenes/sea_fairy_text.tsv",
            new GeneratedTableSchema("sea fairy thanks", GeneratedTableKeySemantics.Unique,
                ["text-id", "utf8-base64", "source"], ["text-id"], headerRequired: true));
        if (texts.Rows.Count != 1 || texts.Rows[0].HexWord(0) != 0x4109 || _visuals.Count != 9)
            throw new InvalidOperationException("greatFairy.s: incomplete $d5:$01 visuals or TX_4109.");
        Thanks = texts.Rows[0].Base64Utf8(1);
        var native = GeneratedTable.Load("res://assets/oracle/cutscenes/sea_fairy_native.tsv",
            new GeneratedTableSchema("sea fairy native tables", GeneratedTableKeySemantics.Unique,
                ["kind", "values", "source"], ["kind"], headerRequired: true));
        var rows = native.Rows.ToDictionary(r => r.RequiredString(0), r => r.RequiredString(1));
        if (rows.Count != 3) throw new InvalidOperationException("CUTSCENE_CLEAN_SEAS $1e: incomplete native tables.");
        Rooms = rows["rooms"].Split(',').Select(v => v.Split(':')).Select(v => (Convert.ToInt32(v[0], 16), Convert.ToInt32(v[1], 16))).ToArray();
        Timing = rows["timing"].Split(',').Select(int.Parse).ToArray();
        PowderMotion = rows["powder-motion"].Split(',').Select(v => v.Split(':').Select(int.Parse).ToArray())
            .Select(v => (v[0], v[1], v[2])).ToArray();
        if (Rooms.Length != 5 || Timing.Length != 7 || PowderMotion.Length != 4)
            throw new InvalidOperationException("CUTSCENE_CLEAN_SEAS $1e: malformed native tables.");
    }

    internal NpcRecord Visual(string role, Vector2 position, int animation = -1)
    {
        NpcRecord record = animation >= 0 ? _visuals[(role, animation)] : _visuals.First(v => v.Key.Item1 == role).Value;
        return record with { X = (int)position.X, Y = (int)position.Y };
    }
}
