using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Typed native interaction records for dungeon $02, imported from the Ages
/// group-$04 object lists and their handler-owned source tables.
/// </summary>
internal sealed class WingDungeonDatabase
{
    private readonly Lookup<(int Group, int Room), DungeonObjectRecord> _objects = new();
    private readonly Dictionary<(DungeonObjectKind Kind, int Color), byte[]> _patterns = new();
    private readonly List<MinecartStaticRecord> _minecarts = new();
    private readonly Dictionary<int, string> _texts = new();

    internal WingDungeonDatabase()
    {
        LoadObjects();
        LoadPatterns();
        LoadMinecarts();
        LoadText();
        ValidateContract();
    }

    internal IReadOnlyList<DungeonObjectRecord> GetRoomRecords(int group, int room) =>
        _objects.ValuesOrEmpty((group, room));

    internal IReadOnlyList<byte> Pattern(DungeonObjectKind kind, int color) =>
        _patterns.TryGetValue((kind, color), out byte[]? positions)
            ? positions
            : Array.Empty<byte>();

    internal string EssenceMessage => Text(0x000f);
    internal string SwoopMessage => Text(0x2f00);

    internal IReadOnlyList<MinecartStaticRecord> Minecarts => _minecarts;

    private void LoadObjects()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/wing_dungeon_objects.tsv",
            new GeneratedTableSchema(
                "Wing Dungeon native objects",
                GeneratedTableKeySemantics.Grouped,
                [
                    "group", "room", "order", "kind", "id", "subid", "y", "x",
                    "condition", "source"
                ],
                ["group", "room"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            DungeonObjectRecord record = new(
                row.Decimal(0, 0, 7),
                row.HexByte(1),
                row.UnsignedDecimal(2),
                ParseKind(row, 3),
                row.HexByte(4),
                row.HexByte(5),
                row.HexByte(6),
                row.HexByte(7),
                DungeonObjectData.ParseCondition(row, 8),
                row.RequiredString(9));
            _objects.Add((record.Group, record.Room), record);
        }
        _objects.SortValues(
            static (left, right) => left.Order.CompareTo(right.Order));
    }

    private void LoadPatterns()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/wing_dungeon_patterns.tsv",
            new GeneratedTableSchema(
                "Wing Dungeon tile patterns",
                GeneratedTableKeySemantics.Unique,
                ["kind", "color", "positions"],
                ["kind", "color"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            DungeonObjectKind kind = row.RequiredString(0) switch
            {
                "floor-pattern-key" => DungeonObjectKind.FloorPatternKey,
                "colored-block-key" => DungeonObjectKind.ColoredBlockKey,
                _ => throw row.Invalid(0, "a Wing Dungeon pattern kind")
            };
            int color = row.RequiredString(1) switch
            {
                "red" => 0,
                "yellow" => 1,
                "blue" => 2,
                _ => throw row.Invalid(1, "red, yellow, or blue")
            };
            string[] cells = row.RequiredString(2).Split(
                ',', StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);
            var positions = new byte[cells.Length];
            for (int index = 0; index < cells.Length; index++)
            {
                if (!byte.TryParse(
                        cells[index],
                        System.Globalization.NumberStyles.AllowHexSpecifier,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out positions[index]))
                {
                    throw row.Invalid(2, "comma-separated hexadecimal positions");
                }
            }
            if (!_patterns.TryAdd((kind, color), positions))
                throw row.Invalid(0, "a unique Wing Dungeon pattern");
        }
    }

    private void LoadText()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/wing_dungeon_text.tsv",
            new GeneratedTableSchema(
                "Wing Dungeon text",
                GeneratedTableKeySemantics.Unique,
                ["text-id", "message-base64"],
                ["text-id"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int textId = row.HexWord(0);
            if (!_texts.TryAdd(textId, row.Base64Utf8(1)))
                throw row.Invalid(0, "a unique Wing Dungeon text id");
        }
    }

    private void LoadMinecarts()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/wing_dungeon_minecarts.tsv",
            new GeneratedTableSchema(
                "Wing Dungeon static minecarts",
                GeneratedTableKeySemantics.Unique,
                ["slot", "room", "y", "x", "source"],
                ["slot"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            MinecartStaticRecord record = new(
                row.UnsignedDecimal(0),
                row.HexByte(1),
                row.HexByte(2),
                row.HexByte(3),
                row.RequiredString(4));
            _minecarts.Add(record);
        }
        _minecarts.Sort(
            static (left, right) => left.Slot.CompareTo(right.Slot));
    }

    private void ValidateContract()
    {
        int count = 0;
        foreach (IReadOnlyList<DungeonObjectRecord> records in _objects.Values)
            count += records.Count;
        if (count != 40 ||
            _patterns.Count != 5 ||
            _minecarts.Count != 3 ||
            Pattern(DungeonObjectKind.FloorPatternKey, 0).Count != 0 ||
            Pattern(DungeonObjectKind.FloorPatternKey, 1) is not [0x67, 0x77] ||
            Pattern(DungeonObjectKind.FloorPatternKey, 2) is not [0x68, 0x78] ||
            Pattern(DungeonObjectKind.ColoredBlockKey, 2).Count != 4 ||
            _minecarts[0] is not { Slot: 0, Room: 0x33, Y: 0x38, X: 0xc8 } ||
            _minecarts[2] is not { Slot: 2, Room: 0x40, Y: 0x58, X: 0xa8 } ||
            _texts.Count != 2 ||
            string.IsNullOrWhiteSpace(EssenceMessage) ||
            string.IsNullOrWhiteSpace(SwoopMessage))
        {
            throw new InvalidOperationException(
                "Imported Wing Dungeon native-object contract is incomplete.");
        }
    }

    private string Text(int id) =>
        _texts.TryGetValue(id, out string? message)
            ? message
            : throw new KeyNotFoundException(
                $"Wing Dungeon text TX_{id:x4} was not imported.");

    private static DungeonObjectKind ParseKind(GeneratedTableRow row, int column) =>
        row.RequiredString(column) switch
        {
            "rupee-reward" => DungeonObjectKind.RupeeReward,
            "feather-reward" => DungeonObjectKind.FeatherReward,
            "floor-pattern-key" => DungeonObjectKind.FloorPatternKey,
            "toggle-floor" => DungeonObjectKind.ToggleFloor,
            "colored-cube" => DungeonObjectKind.ColoredCube,
            "switch-tile-toggler" => DungeonObjectKind.SwitchTileToggler,
            "minecart-gate" => DungeonObjectKind.MinecartGate,
            "cube-light-sensor" => DungeonObjectKind.CubeLightSensor,
            "cube-switch-sensor" => DungeonObjectKind.CubeSwitchSensor,
            "enemy-chest" => DungeonObjectKind.EnemyChest,
            "red-floor-trigger" => DungeonObjectKind.RedFloorTrigger,
            "miniboss-reward" => DungeonObjectKind.MinibossReward,
            "boss-reward" => DungeonObjectKind.BossReward,
            "essence" => DungeonObjectKind.Essence,
            "enemy-small-key" => DungeonObjectKind.EnemySmallKey,
            "floor-switch-bit" => DungeonObjectKind.FloorSwitchBit,
            "floor-color-changer" => DungeonObjectKind.FloorColorChanger,
            "cube-color-source" => DungeonObjectKind.CubeColorSource,
            "colored-block-key" => DungeonObjectKind.ColoredBlockKey,
            "cube-flame" => DungeonObjectKind.CubeFlame,
            "red-flame-trigger" => DungeonObjectKind.RedFlameTrigger,
            "side-platform" => DungeonObjectKind.SidePlatform,
            "circular-side-platform" => DungeonObjectKind.CircularSidePlatform,
            "head-thwomp" => DungeonObjectKind.HeadThwomp,
            "swoop" => DungeonObjectKind.Swoop,
            _ => throw row.Invalid(column, "a supported Wing Dungeon object kind")
        };

}
