using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class MermaidsCaveEntranceDatabase
{
    private readonly Dictionary<(int Group, int Room), MermaidsCaveEntranceRecord> _entrances = new();
    internal IReadOnlyList<CutsceneCommand> Commands { get; } =
        CutsceneCommandCatalog.Load("res://assets/oracle/cutscenes/mermaids_cave_commands.tsv");

    internal MermaidsCaveEntranceDatabase()
    {
        foreach (GeneratedTableRow row in GeneratedTable.Load(
            "res://assets/oracle/cutscenes/mermaids_cave_entrances.tsv",
            new GeneratedTableSchema("Mermaid's Cave entrances", GeneratedTableKeySemantics.Unique,
                ["group", "room", "id", "subid", "x", "y", "open-tile", "source"],
                ["group", "room"], headerRequired: true)).Rows)
        {
            if (row.HexByte(2) != 0x90 || row.HexByte(3) != 0x12)
                throw row.Invalid(2, "INTERAC_MISC_PUZZLES $90:$12");
            int group = row.Decimal(0, 0, 7);
            int room = row.HexByte(1);
            var record = new MermaidsCaveEntranceRecord(group, room,
                new Vector2(row.HexByte(4), row.HexByte(5)), (byte)row.HexByte(6), row.RequiredString(7));
            if (!_entrances.TryAdd((group, room), record))
                throw row.Invalid(1, "unique Mermaid's Cave entrance room");
        }
        if (_entrances.Count != 2 || !_entrances.ContainsKey((1, 0x0e)) ||
            !_entrances.ContainsKey((3, 0x0f)) || Commands.Count != 11)
            throw new InvalidOperationException("miscPuzzles_subid12: incomplete Mermaid's Cave entrance data.");
    }

    internal bool TryGet(int group, int room, out MermaidsCaveEntranceRecord entrance) =>
        _entrances.TryGetValue((group, room), out entrance);
}

internal readonly record struct MermaidsCaveEntranceRecord(
    int Group, int Room, Vector2 Position, byte OpenTile, string Source);
