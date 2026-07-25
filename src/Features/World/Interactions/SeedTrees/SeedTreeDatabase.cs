using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Typed Ages data for ENEMY_SEEDS_ON_TREE ($5a), PART_SEED_ON_TREE ($10),
/// and the sixteen session-local eight-room refill histories.
/// </summary>
internal sealed class SeedTreeDatabase
{
    private readonly Dictionary<(int Group, int Room), List<SeedTreePlacementRecord>>
        _placements = new();
    private readonly SeedTreeTypeRecord[] _types = new SeedTreeTypeRecord[5];
    private readonly SeedTreeRefillLocation[] _refills =
        new SeedTreeRefillLocation[OracleRuntimeState.SeedTreeRefillLocationCount];
    private readonly Dictionary<string, int> _constants = new();

    internal int TreeTopLeftTile => Constant("tree-top-left-tile");
    internal int SeedCount => Constant("seed-count");
    internal int CollisionRadiusY => Constant("collision-radius-y");
    internal int CollisionRadiusX => Constant("collision-radius-x");
    internal int LinkRadius => Constant("link-radius");
    internal int InitialSpeedZ => Constant("initial-speed-z");
    internal int SpeedRaw => Constant("speed-raw");
    internal int Gravity => Constant("gravity");
    internal int CollisionDelay => Constant("collision-delay");
    internal int TreasureParameter => Constant("treasure-parameter");
    internal int CollectionSound => Constant("collection-sound");
    internal int NoSatchelTextId => Constant("no-satchel-text-id");
    internal SeedTreeVisualRecord Visual { get; }
    internal int PlacementCount { get; private set; }

    internal SeedTreeDatabase()
    {
        LoadConstants();
        LoadPlacements();
        LoadRefills();
        LoadTypes();
        Visual = LoadVisual();
        Validate();
    }

    internal IReadOnlyList<SeedTreePlacementRecord> GetRoomRecords(
        int group,
        int room) =>
        _placements.TryGetValue(
            (group, room), out List<SeedTreePlacementRecord>? records)
            ? records
            : Array.Empty<SeedTreePlacementRecord>();

    internal SeedTreeTypeRecord Type(int type)
    {
        if (type is < 0 or >= 5)
            throw new ArgumentOutOfRangeException(nameof(type));
        return _types[type];
    }

    internal bool IsRefilled(OracleRuntimeState runtime, int index)
    {
        ValidateRefillIndex(index);
        int address =
            OracleRuntimeState.SeedTreeRefilledBitsetAddress + (index >> 3);
        return (runtime.ReadWramByte(address) & (1 << (index & 7))) != 0;
    }

    internal void SetRefilled(
        OracleRuntimeState runtime,
        int index,
        bool refilled)
    {
        ValidateRefillIndex(index);
        int address =
            OracleRuntimeState.SeedTreeRefilledBitsetAddress + (index >> 3);
        int mask = 1 << (index & 7);
        byte current = runtime.ReadWramByte(address);
        runtime.SetWramByte(
            address,
            refilled
                ? (byte)(current | mask)
                : (byte)(current & ~mask));
    }

    /// <summary>
    /// Mirrors updateSeedTreeRefillData after getNextActiveRoom. Callers gate
    /// this to outdoor scrolling transitions; ordinary loads and warps do not
    /// advance these histories.
    /// </summary>
    internal void UpdateRefillState(
        OracleRuntimeState runtime,
        int activeGroup,
        int activeRoom)
    {
        if (activeGroup is < 0 or > 7 || activeRoom is < 0 or > 0xff)
            throw new ArgumentOutOfRangeException(nameof(activeGroup));

        for (int index = 0; index < _refills.Length; index++)
        {
            SeedTreeRefillLocation location = _refills[index];
            if (location.Group == activeGroup && location.Room == activeRoom)
            {
                bool full = true;
                for (int slot = 0;
                     slot < OracleRuntimeState.SeedTreeRefillRoomsPerLocation;
                     slot++)
                {
                    if (runtime.ReadSeedTreeRefillRoom(index, slot) == 0)
                    {
                        full = false;
                        break;
                    }
                }
                if (full)
                    SetRefilled(runtime, index, true);
                // The source clears the history on every tree-screen visit,
                // including visits made before all eight rooms were recorded.
                runtime.ClearSeedTreeRefillRooms(index);
                continue;
            }

            if (IsRefilled(runtime, index))
                continue;

            for (int slot = 0;
                 slot < OracleRuntimeState.SeedTreeRefillRoomsPerLocation;
                 slot++)
            {
                byte remembered =
                    runtime.ReadSeedTreeRefillRoom(index, slot);
                if (remembered == activeRoom)
                    break;
                if (remembered != 0)
                    continue;
                // Only the room byte is stored. Room $00 therefore remains
                // indistinguishable from an empty slot, matching WRAM.
                runtime.SetSeedTreeRefillRoom(
                    index, slot, (byte)activeRoom);
                break;
            }
        }
    }

    internal bool TryFindTreeCenter(
        OracleRoomData room,
        out Vector2 center)
    {
        for (int y = 0; y < room.HeightInTiles; y++)
        for (int x = 0; x < room.WidthInTiles; x++)
        {
            Vector2 topLeft = new(
                x * OracleRoomData.MetatileSize,
                y * OracleRoomData.MetatileSize);
            if (room.GetMetatile(topLeft) != TreeTopLeftTile)
                continue;
            center = topLeft + new Vector2(
                OracleRoomData.MetatileSize,
                OracleRoomData.MetatileSize);
            return true;
        }
        center = default;
        return false;
    }

    private void LoadConstants()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/seed_tree_constants.tsv",
            new GeneratedTableSchema(
                "seed-tree constants",
                GeneratedTableKeySemantics.Unique,
                ["key", "value"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
            _constants.Add(row.RequiredString(0), row.Decimal(1));
    }

    private void LoadPlacements()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/seed_trees.tsv",
            new GeneratedTableSchema(
                "seed-tree placements",
                GeneratedTableKeySemantics.Grouped,
                [
                    "group", "room", "order", "id", "subid", "seed-type",
                    "refill-index", "source"
                ],
                ["group", "room"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            var record = new SeedTreePlacementRecord(
                row.Decimal(0, 0, 7),
                row.HexByte(1),
                row.UnsignedDecimal(2),
                row.HexByte(3),
                row.HexByte(4),
                row.UnsignedDecimal(5),
                row.UnsignedDecimal(6),
                row.RequiredString(7));
            if (!_placements.TryGetValue(
                (record.Group, record.Room),
                out List<SeedTreePlacementRecord>? records))
            {
                records = new List<SeedTreePlacementRecord>();
                _placements.Add((record.Group, record.Room), records);
            }
            records.Add(record);
            PlacementCount++;
        }
        foreach (List<SeedTreePlacementRecord> records in _placements.Values)
            records.Sort((left, right) => left.Order.CompareTo(right.Order));
    }

    private void LoadRefills()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/seed_tree_refills.tsv",
            new GeneratedTableSchema(
                "seed-tree refill locations",
                GeneratedTableKeySemantics.Unique,
                ["index", "group", "room"],
                ["index"],
                headerRequired: true));
        int count = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            int index = row.UnsignedDecimal(0);
            if (index >= _refills.Length)
                throw new InvalidOperationException(
                    $"Invalid seed-tree refill index {index}.");
            _refills[index] = new SeedTreeRefillLocation(
                index, row.UnsignedDecimal(1), row.HexByte(2));
            count++;
        }
        if (count != _refills.Length)
            throw new InvalidOperationException(
                $"Expected {_refills.Length} seed-tree refill locations, loaded {count}.");
    }

    private void LoadTypes()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/seed_tree_types.tsv",
            new GeneratedTableSchema(
                "seed-tree types",
                GeneratedTableKeySemantics.Unique,
                [
                    "type", "treasure-id", "tile-base", "palette",
                    "intro-text-id", "intro-message-base64"
                ],
                ["type"],
                headerRequired: true));
        int count = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            int type = row.UnsignedDecimal(0);
            if (type >= _types.Length)
                throw new InvalidOperationException(
                    $"Invalid seed-tree type {type}.");
            _types[type] = new SeedTreeTypeRecord(
                type,
                row.HexByte(1),
                row.UnsignedDecimal(2),
                row.UnsignedDecimal(3),
                row.HexWord(4),
                row.Base64Utf8(5));
            count++;
        }
        if (count != _types.Length)
            throw new InvalidOperationException(
                $"Expected {_types.Length} seed-tree types, loaded {count}.");
    }

    private static SeedTreeVisualRecord LoadVisual()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/seed_tree_visual.tsv",
            new GeneratedTableSchema(
                "seed-tree visual",
                GeneratedTableKeySemantics.Unique,
                ["sprite", "animation", "no-satchel-message-base64"],
                ["sprite"],
                headerRequired: true));
        if (table.Rows.Count != 1)
            throw new InvalidOperationException(
                $"Expected one seed-tree visual, loaded {table.Rows.Count}.");
        GeneratedTableRow row = table.Rows[0];
        return new SeedTreeVisualRecord(
            row.RequiredString(0),
            row.RequiredString(1),
            row.Base64Utf8(2));
    }

    private int Constant(string name) =>
        _constants.TryGetValue(name, out int value)
            ? value
            : throw new KeyNotFoundException(
                $"Seed-tree constant '{name}' was not imported.");

    private static void ValidateRefillIndex(int index)
    {
        if (index is < 0 or
            >= OracleRuntimeState.SeedTreeRefillLocationCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    private void Validate()
    {
        if (PlacementCount != 10 ||
            TreeTopLeftTile != 0x6e ||
            SeedCount != 3 ||
            InitialSpeedZ != -0x140 ||
            SpeedRaw != 0x28 ||
            Gravity != 0x20 ||
            CollisionDelay != 2 ||
            TreasureParameter != 0x06 ||
            CollectionSound != OracleSoundEngine.SndGetSeed ||
            NoSatchelTextId != 0x0035 ||
            !_placements.TryGetValue(
                (0, 0x78), out List<SeedTreePlacementRecord>? canonical) ||
            canonical.Count != 1 ||
            canonical[0] is not
                { Id: 0x5a, SubId: 0x06, SeedType: 0, RefillIndex: 6 })
        {
            throw new InvalidOperationException(
                "Imported seed-tree data does not match the traced Ages records.");
        }
    }
}

internal readonly record struct SeedTreePlacementRecord(
    int Group,
    int Room,
    int Order,
    int Id,
    int SubId,
    int SeedType,
    int RefillIndex,
    string Source);

internal readonly record struct SeedTreeTypeRecord(
    int Type,
    int TreasureId,
    int TileBase,
    int Palette,
    int IntroTextId,
    string IntroMessage);

internal readonly record struct SeedTreeRefillLocation(
    int Index,
    int Group,
    int Room);

internal readonly record struct SeedTreeVisualRecord(
    string Sprite,
    string Animation,
    string NoSatchelMessage);
