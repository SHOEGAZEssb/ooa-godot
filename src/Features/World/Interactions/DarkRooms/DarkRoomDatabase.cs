using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Source-ordered PART_DARK_ROOM_HANDLER $08 placements and the
/// INTERAC_MISCELLANEOUS_2 $dc:$00 Graveyard Key consumer which shares their
/// room-local torch count.
/// </summary>
internal sealed class DarkRoomDatabase
{

    private readonly Dictionary<int, List<DarkRoomDatabaseRecord>> _recordsByRoom = new();
    private readonly Dictionary<string, int> _constants = new();

    internal int RecordCount { get; }
    internal int UnlitTile => Constant("unlit-tile");
    internal int LitTile => Constant("lit-tile");
    internal int TorchCollisionMode => Constant("torch-collision-mode");
    internal int TorchRadiusY => Constant("torch-radius-y");
    internal int TorchRadiusX => Constant("torch-radius-x");
    internal int FullDarkParameter => Constant("full-dark-parameter");
    internal int PartialDarkParameter => Constant("partial-dark-parameter");
    internal int FadeSpeed => Constant("fade-speed");
    internal int LightSound => Constant("light-sound");
    internal int RewardSpawnMode => Constant("reward-spawn-mode");
    internal int RewardGrabMode => Constant("reward-grab-mode");
    internal int SpawnDelay => Constant("spawn-delay");
    internal int BounceCount => Constant("bounce-count");
    internal int Gravity => Constant("gravity");
    internal int BounceSpeed => Constant("bounce-speed");
    internal int SpawnSound => Constant("spawn-sound");
    internal int LandingSound => Constant("landing-sound");
    internal int AboveScreenMargin => Constant("above-screen-margin");
    internal int AboveScreenFallback => Constant("above-screen-fallback");

    public DarkRoomDatabase()
    {
        GeneratedTable interactions = GeneratedTable.Load(
            "res://assets/oracle/objects/dark_room_interactions.tsv",
            new GeneratedTableSchema(
                "dark-room interactions",
                GeneratedTableKeySemantics.Grouped,
                [
                    "group", "room", "order", "kind", "id", "subid", "y", "x",
                    "parameter", "required-count", "treasure-object", "source"
                ],
                ["group", "room"],
                headerRequired: true));
        int count = 0;
        foreach (GeneratedTableRow row in interactions.Rows)
        {
            DarkRoomDatabaseObjectKind kind = row.RequiredString(3) switch
            {
                "handler" => DarkRoomDatabaseObjectKind.Handler,
                "reward" => DarkRoomDatabaseObjectKind.Reward,
                _ => throw row.Invalid(3, "one of handler, reward")
            };
            DarkRoomDatabaseRecord record = new DarkRoomDatabaseRecord(
                row.Decimal(0, 0, 7),
                row.HexByte(1),
                row.UnsignedDecimal(2),
                kind,
                row.HexByte(4),
                row.HexByte(5),
                row.HexByteOrSentinel(6, "-", -1),
                row.HexByteOrSentinel(7, "-", -1),
                row.HexByte(8),
                row.UnsignedDecimal(9),
                row.String(10),
                row.RequiredString(11));
            bool valid = kind switch
            {
                DarkRoomDatabaseObjectKind.Handler => record is
                {
                    Id: 0x08, SubId: 0x00, Y: -1, X: -1,
                    RequiredCount: 0, TreasureObject: "-"
                },
                DarkRoomDatabaseObjectKind.Reward => record is
                {
                    Id: 0xdc, SubId: 0x00, Y: >= 0, X: >= 0,
                    RequiredCount: > 0
                } && record.TreasureObject != "-",
                _ => false
            };
            if (!valid)
                throw new InvalidOperationException(
                    $"Invalid dark-room interaction at {row.Path}:{row.LineNumber}.");

            int key = MakeKey(record.Group, record.Room);
            if (!_recordsByRoom.TryGetValue(key, out List<DarkRoomDatabaseRecord>? records))
            {
                records = new List<DarkRoomDatabaseRecord>();
                _recordsByRoom.Add(key, records);
            }
            if (records.Count > 0 && records[^1].Order >= record.Order)
            {
                throw new InvalidOperationException(
                    $"Room {record.Group:x1}:{record.Room:x2} dark-room object order " +
                    $"did not increase at source object {record.Order}.");
            }
            records.Add(record);
            count++;
        }
        RecordCount = count;

        GeneratedTable constants = GeneratedTable.Load(
            "res://assets/oracle/objects/dark_room_constants.tsv",
            new GeneratedTableSchema(
                "dark-room constants",
                GeneratedTableKeySemantics.Unique,
                ["key", "value"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in constants.Rows)
            _constants.Add(row.RequiredString(0), row.Decimal(1));

        IReadOnlyList<DarkRoomDatabaseRecord> roomA8 = GetRoomRecords(5, 0xa8);
        IReadOnlyList<DarkRoomDatabaseRecord> roomEd = GetRoomRecords(5, 0xed);
        if (RecordCount != 3 || _constants.Count != 19 ||
            roomA8.Count != 1 || roomA8[0] != new DarkRoomDatabaseRecord(
                5, 0xa8, 0, DarkRoomDatabaseObjectKind.Handler, 0x08, 0x00,
                -1, -1, 0x00, 0, "-", "darkRoomHandler.s:partCode08") ||
            roomEd.Count != 2 || roomEd[0] != new DarkRoomDatabaseRecord(
                5, 0xed, 0, DarkRoomDatabaseObjectKind.Reward, 0xdc, 0x00,
                0x48, 0x78, 0x00, 2, "TREASURE_OBJECT_GRAVEYARD_KEY_00",
                "miscellaneous2.s:interactiondc_subid00") ||
            roomEd[1] != new DarkRoomDatabaseRecord(
                5, 0xed, 1, DarkRoomDatabaseObjectKind.Handler, 0x08, 0x00,
                -1, -1, 0x50, 0, "-", "darkRoomHandler.s:partCode08") ||
            UnlitTile != 0x08 || LitTile != 0x09 ||
            TorchCollisionMode != 0x82 || TorchRadiusY != 4 || TorchRadiusX != 4 ||
            FullDarkParameter != 0xf0 || PartialDarkParameter != 0xf7 ||
            FadeSpeed != 1 || LightSound != 0x72 ||
            RewardSpawnMode != 2 || RewardGrabMode != 1 || SpawnDelay != 40 ||
            BounceCount != 2 || Gravity != 0x10 || BounceSpeed != -0xaa ||
            SpawnSound != 0x4d || LandingSound != 0x77 ||
            AboveScreenMargin != 8 || AboveScreenFallback != -0x80)
        {
            throw new InvalidOperationException(
                "Imported PART_DARK_ROOM_HANDLER $08 / torch / Graveyard Key contract is incomplete.");
        }
    }

    internal IReadOnlyList<DarkRoomDatabaseRecord> GetRoomRecords(int group, int room) =>
        _recordsByRoom.TryGetValue(MakeKey(group, room), out List<DarkRoomDatabaseRecord>? records)
            ? records
            : Array.Empty<DarkRoomDatabaseRecord>();

    private int Constant(string key) => _constants.TryGetValue(key, out int value)
        ? value
        : throw new KeyNotFoundException(
            $"Dark-room constant '{key}' was not imported.");

    private static int MakeKey(int group, int room) => (group << 8) | room;
}

internal readonly record struct DarkRoomDatabaseRecord(int Group, int Room, int Order, DarkRoomDatabaseObjectKind Kind, int Id, int SubId, int Y, int X, int Parameter, int RequiredCount, string TreasureObject, string Source);

internal enum DarkRoomDatabaseObjectKind
{
    Handler,
    Reward
}
