using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class RoomOfRitesRitualDatabase
{
    private readonly Lookup<int, DungeonObjectRecord> _placements = new();
    private readonly Dictionary<int, (Vector2 Position, DungeonInteractionVisual Visual, int SaveRoom, int SaveAdd)> _flames = [];
    internal Color[,] Palette { get; } = new Color[2, 4];
    internal Color[,] AngryPalette { get; } = new Color[1, 4];
    internal RoomOfRitesRitualDatabase()
    {
        const string root = AgesGameContent.AssetRoot + "objects/";
        var colors = OracleGraphicsData.ReadBytes(root + "ritual_palette_ac.bin", 24);
        for (int i = 0; i < 8; i++) Palette[i / 4, i % 4] = i % 4 == 0 ? Colors.Transparent :
            new(colors[i * 3] / 31f, colors[i * 3 + 1] / 31f, colors[i * 3 + 2] / 31f);
        var angry = OracleGraphicsData.LoadPaletteColors(root + "ritual_palette_af.bin", transparentZero: true);
        for (int i = 0; i < 4; i++) AngryPalette[0, i] = angry[i];
        foreach (var row in GeneratedTable.Load(root + "twinrova_flames.tsv", new("Twinrova flames $a9",
            GeneratedTableKeySemantics.Unique, ["subid", "y", "x", "sprite", "tile-base", "palette", "inverted", "animation", "save-room", "save-add", "source"],
            ["subid"], headerRequired: true)).Rows)
        {
            int subid = row.HexByte(0);
            if (subid > 8) throw row.Invalid(0, "$00..$08 Ages flame subid");
            _flames.Add(subid, (new(row.HexByte(2), row.HexByte(1)), new($"twinrova-flame-{subid:x2}",
                [row.RequiredString(3)], row.UnsignedDecimal(4), row.Decimal(5, 0, 7), row.Boolean01(6), [row.RequiredString(7)]), row.HexByte(8), row.HexByte(9)));
            _ = row.RequiredString(10);
        }
        if (_flames.Count != 9) throw new InvalidOperationException("twinrovaFlame.s requires nine Ages subids.");
        foreach (var row in GeneratedTable.Load(root + "room_of_rites_ritual.tsv", new("Room of Rites ritual placements",
            GeneratedTableKeySemantics.Unique, ["group", "room", "order", "id", "subid", "y", "x", "source"],
            ["group", "room", "order"], headerRequired: true)).Rows)
        {
            int group = row.Decimal(0, 0, 7), room = row.HexByte(1), id = row.HexByte(3), subid = row.HexByte(4);
            var kind = id switch { 0xa9 when subid <= 8 => DungeonObjectKind.TwinrovaFlame,
                0xda when subid == 0 => DungeonObjectKind.ZeldaApproach,
                0xad when subid == 0 => DungeonObjectKind.Zelda,
                _ => throw row.Invalid(3, "$a9:$00..$08 flame or $da:$00 approach trigger") };
            _placements.GetOrAdd(group << 8 | room).Add(new(group, room, row.UnsignedDecimal(2), kind,
                id, subid, row.HexByte(5), row.HexByte(6), DungeonObjectCondition.Always, row.RequiredString(7)));
        }
    }
    internal (Vector2 Position, DungeonInteractionVisual Visual, int SaveRoom, int SaveAdd) Flame(int subid) => _flames[subid];
    internal IReadOnlyList<DungeonObjectRecord> GetRoomRecords(int group, int room) => _placements.ValuesOrEmpty(group << 8 | room);
}
