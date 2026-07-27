using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public sealed class TimePortalDatabase
{

    private readonly Dictionary<int, List<PortalRecord>> _byRoom = new();
    public TemporaryPortalVisualRecord TemporaryVisual { get; }
    internal IReadOnlyDictionary<byte, byte> EntryTileReplacements { get; }
    internal IReadOnlyDictionary<byte, byte> ReturnTileReplacements { get; }

    public TimePortalDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/timePortals.tsv",
            new GeneratedTableSchema(
                "time portals",
                GeneratedTableKeySemantics.Grouped,
                [
                    "group", "room", "subid", "y", "x", "sprite",
                    "tile-base", "palette", "loop-start", "animation"
                ],
                ["group", "room"],
                headerRequired: true));
        int count = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            PortalRecord record = new PortalRecord(
                row.Decimal(0, 0, 7),
                row.HexByte(1),
                row.HexByte(2),
                row.HexByte(3),
                row.HexByte(4),
                row.RequiredString(5),
                row.UnsignedDecimal(6),
                row.UnsignedDecimal(7),
                row.UnsignedDecimal(8),
                row.RequiredString(9));
            int key = MakeKey(record.Group, record.Room);
            if (!_byRoom.TryGetValue(key, out List<PortalRecord>? records))
            {
                records = new List<PortalRecord>();
                _byRoom.Add(key, records);
            }
            records.Add(record);
            count++;
        }

        if (count != 21)
            throw new InvalidOperationException($"Expected 21 time-portal spawners, loaded {count}.");

        GeneratedTableRow temporary = GeneratedTable.Load(
            "res://assets/oracle/objects/temporaryTimePortal.tsv",
            new GeneratedTableSchema(
                "temporary time portal",
                GeneratedTableKeySemantics.Ordered,
                [
                    "sprite", "tile-base", "palette", "contact-radius",
                    "animation", "entry-tile-replacements",
                    "return-tile-replacements"
                ],
                headerRequired: true)).SingleRow();
        TemporaryVisual = new TemporaryPortalVisualRecord(
            temporary.RequiredString(0),
            temporary.UnsignedDecimal(1),
            temporary.UnsignedDecimal(2),
            temporary.UnsignedDecimal(3),
            temporary.RequiredString(4));
        EntryTileReplacements = ParseTileReplacements(
            temporary.RequiredString(5), "time-warp entry");
        ReturnTileReplacements = ParseTileReplacements(
            temporary.RequiredString(6), "time-warp return");
        if (TemporaryVisual is not
            {
                SpriteName: "spr_common_sprites",
                TileBase: 0x4a,
                Palette: 1,
                ContactRadius: 9
            } ||
            !TileReplacementsMatch(
                EntryTileReplacements,
                [(0xc5, 0x3a), (0xc8, 0x3a), (0x04, 0x3a)]) ||
            !TileReplacementsMatch(
                ReturnTileReplacements,
                [
                    (0xc0, 0x3a), (0xc3, 0x3a), (0xc5, 0x3a),
                    (0xc8, 0x3a), (0xce, 0x3a), (0xdb, 0x3a),
                    (0xf2, 0x3a), (0xcd, 0x3a), (0x04, 0x3a)
                ]))
        {
            throw new InvalidOperationException(
                "INTERAC_TIMEPORTAL $de visual or time-warp tile contract changed.");
        }
    }

    public IReadOnlyList<PortalRecord> GetRoomPortals(int group, int room) =>
        _byRoom.TryGetValue(MakeKey(group, room), out List<PortalRecord>? records)
            ? records
            : Array.Empty<PortalRecord>();

    internal bool ApplyEntryTileReplacement(
        OracleRoomData room,
        int packedPosition,
        long animationTick) =>
        ApplyTileReplacement(
            room, packedPosition, EntryTileReplacements, animationTick);

    internal bool ApplyReturnTileReplacement(
        OracleRoomData room,
        int packedPosition,
        long animationTick) =>
        ApplyTileReplacement(
            room, packedPosition, ReturnTileReplacements, animationTick);

    private static bool ApplyTileReplacement(
        OracleRoomData room,
        int packedPosition,
        IReadOnlyDictionary<byte, byte> replacements,
        long animationTick)
    {
        Vector2 point = new(
            (packedPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
            (packedPosition >> 4) * OracleRoomData.MetatileSize + 8);
        byte tile = room.GetMetatile(point);
        return replacements.TryGetValue(tile, out byte replacement) &&
            room.ReplaceMetatile(point, tile, replacement, animationTick);
    }

    private static IReadOnlyDictionary<byte, byte> ParseTileReplacements(
        string encoded,
        string label)
    {
        var result = new Dictionary<byte, byte>();
        foreach (string pair in encoded.Split(
            ',', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] fields = pair.Split(':');
            if (fields.Length != 2 ||
                !byte.TryParse(
                    fields[0],
                    System.Globalization.NumberStyles.HexNumber,
                    null,
                    out byte source) ||
                !byte.TryParse(
                    fields[1],
                    System.Globalization.NumberStyles.HexNumber,
                    null,
                    out byte replacement) ||
                !result.TryAdd(source, replacement))
            {
                throw new InvalidOperationException(
                    $"Malformed {label} tile replacement '{pair}'.");
            }
        }
        return result;
    }

    private static bool TileReplacementsMatch(
        IReadOnlyDictionary<byte, byte> actual,
        ReadOnlySpan<(byte Source, byte Replacement)> expected)
    {
        if (actual.Count != expected.Length)
            return false;
        foreach ((byte source, byte replacement) in expected)
        {
            if (!actual.TryGetValue(source, out byte actualReplacement) ||
                actualReplacement != replacement)
            {
                return false;
            }
        }
        return true;
    }

    private static int MakeKey(int group, int room) => (group << 8) | room;
}

public readonly record struct PortalRecord(int Group, int Room, int SubId, int Y, int X, string SpriteName, int TileBase, int Palette, int LoopStart, string Animation);

public readonly record struct TemporaryPortalVisualRecord(
    string SpriteName,
    int TileBase,
    int Palette,
    int ContactRadius,
    string Animation);
