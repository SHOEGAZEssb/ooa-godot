using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

internal sealed class FinalDungeonEntryDatabase
{
    private readonly Lookup<int, DungeonObjectRecord> _placements = new();
    internal byte CompletionFlag { get; }
    internal int WaitUpdates { get; }
    internal int RevealDelay { get; }
    internal int FadeDelay { get; }
    internal Vector2 Center { get; }
    internal DungeonInteractionVisual Decoration { get; }
    internal FinalDungeonEntryDatabase()
    {
        const string root = "res://assets/oracle/objects/final_dungeon_";
        var row = GeneratedTable.Load(root + "entry.tsv", new("final dungeon entry",
            GeneratedTableKeySemantics.Unique,
            ["flag", "wait-updates", "reveal-delay", "fade-delay", "y", "x", "source"], ["flag"], headerRequired: true)).Rows.Single();
        CompletionFlag = (byte)row.HexByte(0); WaitUpdates = row.Decimal(1, 1, 255);
        RevealDelay = row.Decimal(2, 1, 255); FadeDelay = row.Decimal(3, 1, 255);
        Center = new(row.HexByte(5), row.HexByte(4)); _ = row.RequiredString(6);
        foreach (var placed in GeneratedTable.Load(root + "entry_placements.tsv", new("final dungeon entry placements",
            GeneratedTableKeySemantics.Unique,
            ["group", "room", "order", "id", "subid", "y", "x", "source"], ["group", "room", "order"], headerRequired: true)).Rows)
        {
            int id = placed.HexByte(3), subid = placed.HexByte(4);
            var kind = (id, subid) switch
            {
                (0xb5, 0) => DungeonObjectKind.FinalDungeonEnergy,
                (0x80, 0) => DungeonObjectKind.Decoration,
                (0x90, 0x1e) => DungeonObjectKind.EyePuzzleExit,
                _ => throw placed.Invalid(3, "finalDungeonEnergy $b5:$00, decoration $80:$00, or miscPuzzles $90:$1e")
            };
            int group = placed.Decimal(0, 0, 7), room = placed.HexByte(1);
            _placements.GetOrAdd((group << 8) | room).Add(new(group, room, placed.UnsignedDecimal(2), kind,
                id, subid, placed.HexByte(5), placed.HexByte(6), DungeonObjectCondition.Always, placed.RequiredString(7)));
        }
        var visual = GeneratedTable.Load(root + "decoration.tsv", new("decoration $80:$00",
            GeneratedTableKeySemantics.Unique,
            ["sprite", "tile-base", "palette", "source-grayscale-inverted", "animation", "source"], ["sprite"], headerRequired: true)).Rows.Single();
        Decoration = new("decoration-00", [visual.RequiredString(0)], visual.UnsignedDecimal(1),
            visual.Decimal(2, 0, 7), visual.Boolean01(3), [visual.RequiredString(4)]);
        _ = visual.RequiredString(5);
    }
    internal IReadOnlyList<DungeonObjectRecord> GetRoomRecords(int group, int room) => _placements.ValuesOrEmpty((group << 8) | room);
}
