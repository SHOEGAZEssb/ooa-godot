using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Typed source records shared by INTERAC_DUNGEON_STUFF $12:$00,
/// INTERAC_STATUE_EYEBALL $e2:$01/$02, and INTERAC_MINIBOSS_PORTAL $7e:$00.
/// </summary>
internal sealed class DungeonEntranceInteractionDatabase
{
    internal enum ObjectKind
    {
        Entry,
        EyeSpawner,
        MinibossPortal
    }

    internal readonly record struct EntryRecord(
        int Dungeon,
        int TextId,
        string Message,
        int SpinnerState);

    internal readonly record struct PlacementRecord(
        int Group,
        int Room,
        int Order,
        ObjectKind Kind,
        int Id,
        int SubId,
        int Y,
        int X,
        int Dungeon,
        string Source);

    internal readonly record struct VisualRecord(
        string Kind,
        int Index,
        string Sprite,
        int TileBase,
        int Palette,
        string Animation,
        int LowY,
        int LowX);

    internal readonly record struct PortalPair(
        int Dungeon,
        int MinibossRoom,
        int EntranceRoom);

    private readonly EntryRecord[] _entries = new EntryRecord[16];
    private readonly VisualRecord[] _eyeVisuals = new VisualRecord[8];
    private readonly Dictionary<int, List<PlacementRecord>> _placements = new();
    private readonly Dictionary<int, PortalPair> _portalPairs = new();
    private readonly Dictionary<string, int> _constants = new();
    private VisualRecord _portalVisual;

    internal int PlacementCount { get; }
    internal int EntryMinimumY => Constant("entry-min-y");
    internal int EntryRadius => Constant("entry-radius");
    internal int EyeStatueTile => Constant("eye-statue-tile");
    internal int EyeInitialYOffset => Constant("eye-initial-y-offset");
    internal int PortalPosition => Constant("portal-position");
    internal int PortalRadius => Constant("portal-radius");
    internal int PortalSpinUpdates => Constant("portal-spin-updates");
    internal int PortalSound => Constant("portal-sound");
    internal int PortalSourceTransition => Constant("portal-source-transition");
    internal int PortalDestinationTransition =>
        Constant("portal-destination-transition");
    internal int PortalDestinationParameter =>
        Constant("portal-destination-parameter");
    internal IReadOnlyList<VisualRecord> EyeVisuals => _eyeVisuals;
    internal VisualRecord PortalVisual => _portalVisual;

    public DungeonEntranceInteractionDatabase()
    {
        GeneratedTable entries = GeneratedTable.Load(
            "res://assets/oracle/objects/dungeon_entry_data.tsv",
            new GeneratedTableSchema(
                "dungeon entry data",
                GeneratedTableKeySemantics.Unique,
                ["dungeon", "text-id", "utf8-base64", "spinner-state"],
                ["dungeon"],
                headerRequired: true));
        int entryCount = 0;
        foreach (GeneratedTableRow row in entries.Rows)
        {
            int dungeon = row.Decimal(0, 0, 15);
            var record = new EntryRecord(
                dungeon, row.HexWord(1), row.Base64Utf8(2), row.HexByte(3));
            _entries[dungeon] = record;
            entryCount++;
        }

        GeneratedTable placements = GeneratedTable.Load(
            "res://assets/oracle/objects/dungeon_shared_placements.tsv",
            new GeneratedTableSchema(
                "shared dungeon interaction placements",
                GeneratedTableKeySemantics.Grouped,
                [
                    "group", "room", "order", "kind", "id", "subid",
                    "y", "x", "dungeon", "source"
                ],
                ["group", "room"],
                headerRequired: true));
        int placementCount = 0;
        foreach (GeneratedTableRow row in placements.Rows)
        {
            ObjectKind kind = row.RequiredString(3) switch
            {
                "entry" => ObjectKind.Entry,
                "eye-spawner" => ObjectKind.EyeSpawner,
                "miniboss-portal" => ObjectKind.MinibossPortal,
                _ => throw row.Invalid(
                    3, "one of entry, eye-spawner, miniboss-portal")
            };
            var record = new PlacementRecord(
                row.Decimal(0, 0, 7), row.HexByte(1), row.UnsignedDecimal(2),
                kind, row.HexByte(4), row.HexByte(5),
                row.HexByteOrSentinel(6, "--", -1),
                row.HexByteOrSentinel(7, "--", -1),
                row.Decimal(8, 0, 15), row.RequiredString(9));
            if (record.Kind == ObjectKind.Entry &&
                    (record.Id != 0x12 || record.SubId != 0x00 ||
                     record.Y < 0 || record.X < 0) ||
                record.Kind == ObjectKind.EyeSpawner &&
                    (record.Id != 0xe2 || record.SubId != 0x01 ||
                     record.Y >= 0 || record.X >= 0) ||
                record.Kind == ObjectKind.MinibossPortal &&
                    (record.Id != 0x7e || record.SubId != 0x00))
            {
                throw new InvalidOperationException(
                    $"Malformed shared dungeon interaction at {record.Source}.");
            }
            int key = MakeKey(record.Group, record.Room);
            if (!_placements.TryGetValue(key, out List<PlacementRecord>? roomRecords))
            {
                roomRecords = new List<PlacementRecord>();
                _placements.Add(key, roomRecords);
            }
            if (roomRecords.Count > 0 && roomRecords[^1].Order >= record.Order)
            {
                throw new InvalidOperationException(
                    $"Shared dungeon interaction order did not increase in " +
                    $"room {record.Group:x1}:{record.Room:x2}.");
            }
            roomRecords.Add(record);
            placementCount++;
        }
        PlacementCount = placementCount;

        GeneratedTable visuals = GeneratedTable.Load(
            "res://assets/oracle/objects/dungeon_shared_visuals.tsv",
            new GeneratedTableSchema(
                "shared dungeon interaction visuals",
                GeneratedTableKeySemantics.Unique,
                [
                    "kind", "index", "sprite", "tile-base", "palette",
                    "animation", "low-y", "low-x"
                ],
                ["kind", "index"],
                headerRequired: true));
        int eyeVisualCount = 0;
        int portalVisualCount = 0;
        foreach (GeneratedTableRow row in visuals.Rows)
        {
            var visual = new VisualRecord(
                row.RequiredString(0), row.UnsignedDecimal(1),
                row.RequiredString(2), row.UnsignedDecimal(3),
                row.UnsignedDecimal(4), row.RequiredString(5),
                row.Decimal(6), row.Decimal(7));
            if (visual.Kind == "eye" && visual.Index is >= 0 and < 8)
            {
                _eyeVisuals[visual.Index] = visual;
                eyeVisualCount++;
            }
            else if (visual.Kind == "portal" && visual.Index == 0)
            {
                _portalVisual = visual;
                portalVisualCount++;
            }
            else
            {
                throw row.Invalid(0, "eye indices 0-7 or portal index 0");
            }
        }

        GeneratedTable pairs = GeneratedTable.Load(
            "res://assets/oracle/objects/miniboss_portal_pairs.tsv",
            new GeneratedTableSchema(
                "miniboss portal pairs",
                GeneratedTableKeySemantics.Unique,
                ["dungeon", "miniboss-room", "entrance-room"],
                ["dungeon"],
                headerRequired: true));
        foreach (GeneratedTableRow row in pairs.Rows)
        {
            var pair = new PortalPair(
                row.Decimal(0, 0, 8), row.HexByte(1), row.HexByte(2));
            _portalPairs.Add(pair.Dungeon, pair);
        }

        GeneratedTable constants = GeneratedTable.Load(
            "res://assets/oracle/objects/dungeon_shared_constants.tsv",
            new GeneratedTableSchema(
                "shared dungeon interaction constants",
                GeneratedTableKeySemantics.Unique,
                ["key", "value"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in constants.Rows)
            _constants.Add(row.RequiredString(0), row.Decimal(1));

        IReadOnlyList<PlacementRecord> room24 = GetRoomRecords(4, 0x24);
        if (entryCount != 16 || PlacementCount != 42 || eyeVisualCount != 8 ||
            portalVisualCount != 1 || _portalPairs.Count != 9 ||
            _constants.Count != 11 || room24.Count != 3 ||
            room24[0] is not { Order: 0, Kind: ObjectKind.Entry, Dungeon: 1 } ||
            room24[1] is not { Order: 1, Kind: ObjectKind.EyeSpawner } ||
            room24[2] is not { Order: 2, Kind: ObjectKind.MinibossPortal } ||
            Entry(1).TextId != 0x0201 || EntryMinimumY != 0x78 ||
            EntryRadius != 8 || EyeStatueTile != 0xee ||
            EyeInitialYOffset != -2 || PortalPosition != 0x57 ||
            PortalRadius != 3 || PortalSpinUpdates != 0x30 ||
            PortalSound != OracleSoundEngine.SndTeleport ||
            PortalSourceTransition != 2 || PortalDestinationTransition != 0 ||
            PortalDestinationParameter != 0 ||
            PortalPairFor(1) is not { MinibossRoom: 0x18, EntranceRoom: 0x24 })
        {
            throw new InvalidOperationException(
                "Imported shared dungeon-entry interaction contract is incomplete.");
        }
    }

    internal EntryRecord Entry(int dungeon) => dungeon is >= 0 and < 16
        ? _entries[dungeon]
        : throw new ArgumentOutOfRangeException(nameof(dungeon));

    internal PortalPair PortalPairFor(int dungeon) =>
        _portalPairs.TryGetValue(dungeon, out PortalPair pair)
            ? pair
            : throw new KeyNotFoundException(
                $"Dungeon ${dungeon:x2} has no miniboss portal pair.");

    internal IReadOnlyList<PlacementRecord> GetRoomRecords(int group, int room) =>
        _placements.TryGetValue(MakeKey(group, room), out List<PlacementRecord>? records)
            ? records
            : Array.Empty<PlacementRecord>();

    private int Constant(string key) => _constants.TryGetValue(key, out int value)
        ? value
        : throw new KeyNotFoundException(
            $"Shared dungeon interaction constant '{key}' was not imported.");

    private static int MakeKey(int group, int room) => (group << 8) | room;
}
