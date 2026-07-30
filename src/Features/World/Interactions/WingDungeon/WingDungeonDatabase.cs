using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Typed native interaction records for dungeon $02, imported from the Ages
/// group-$04 object lists and their handler-owned source tables.
/// </summary>
internal sealed class WingDungeonDatabase
{
    private readonly Lookup<(int Group, int Room), ObjectRecord> _objects = new();
    private readonly Dictionary<string, int> _constants =
        new(StringComparer.Ordinal);
    private readonly Dictionary<(ObjectKind Kind, int Color), byte[]> _patterns = new();
    private readonly Dictionary<int, WingDungeonSidePlatformRecord> _platforms = new();
    private readonly List<WingDungeonMinecartRecord> _minecarts = new();
    private readonly Dictionary<int, string> _texts = new();

    internal WingDungeonDatabase()
    {
        LoadObjects();
        LoadConstants();
        LoadPatterns();
        LoadPlatforms();
        LoadMinecarts();
        LoadText();
        ValidateContract();
    }

    internal IReadOnlyList<ObjectRecord> GetRoomRecords(int group, int room) =>
        _objects.ValuesOrEmpty((group, room));

    internal int Constant(string key) =>
        _constants.TryGetValue(key, out int value)
            ? value
            : throw new KeyNotFoundException(
                $"Wing Dungeon constant {key} was not imported.");

    internal IReadOnlyList<byte> Pattern(ObjectKind kind, int color) =>
        _patterns.TryGetValue((kind, color), out byte[]? positions)
            ? positions
            : throw new KeyNotFoundException(
                $"Wing Dungeon {kind} color {color} pattern was not imported.");

    internal (int Off, int On) SwitchTiles(int index) => (
        Constant($"switch-{index}-off"),
        Constant($"switch-{index}-on"));

    internal string EssenceMessage => Text(0x000f);
    internal string SwoopMessage => Text(0x2f00);

    internal WingDungeonSidePlatformRecord SidePlatform(int subId) =>
        _platforms.TryGetValue(subId, out WingDungeonSidePlatformRecord record)
            ? record
            : throw new KeyNotFoundException(
                $"Wing Dungeon side-platform subid ${subId:x2} was not imported.");

    internal IReadOnlyList<WingDungeonMinecartRecord> Minecarts => _minecarts;

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
            ObjectRecord record = new(
                row.Decimal(0, 0, 7),
                row.HexByte(1),
                row.UnsignedDecimal(2),
                ParseKind(row, 3),
                row.HexByte(4),
                row.HexByte(5),
                row.HexByte(6),
                row.HexByte(7),
                ParseCondition(row, 8),
                row.RequiredString(9));
            _objects.Add((record.Group, record.Room), record);
        }
        _objects.SortValues(
            static (left, right) => left.Order.CompareTo(right.Order));
    }

    private void LoadConstants()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/wing_dungeon_constants.tsv",
            new GeneratedTableSchema(
                "Wing Dungeon constants",
                GeneratedTableKeySemantics.Unique,
                ["key", "value"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            if (!_constants.TryAdd(
                    row.RequiredString(0), row.UnsignedDecimal(1)))
            {
                throw new InvalidOperationException(
                    $"Duplicate Wing Dungeon constant at {row.Path}:{row.LineNumber}.");
            }
        }
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
            ObjectKind kind = row.RequiredString(0) switch
            {
                "floor-pattern-key" => ObjectKind.FloorPatternKey,
                "colored-block-key" => ObjectKind.ColoredBlockKey,
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

    private void LoadPlatforms()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/wing_dungeon_side_platforms.tsv",
            new GeneratedTableSchema(
                "Wing Dungeon side platforms",
                GeneratedTableKeySemantics.Unique,
                ["subid", "speed", "direction", "radius-y", "radius-x", "commands"],
                ["subid"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            string[] encoded = row.RequiredString(5).Split(
                ',', StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);
            var commands = new WingDungeonPlatformCommand[encoded.Length];
            for (int index = 0; index < encoded.Length; index++)
            {
                string[] parts = encoded[index].Split(
                    ':', StringSplitOptions.TrimEntries);
                if (parts.Length != 2 ||
                    !byte.TryParse(
                        parts[1],
                        System.Globalization.NumberStyles.AllowHexSpecifier,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out byte endpoint))
                {
                    throw row.Invalid(5, "direction:hex-endpoint commands");
                }
                WingDungeonPlatformDirection direction = parts[0] switch
                {
                    "up" => WingDungeonPlatformDirection.Up,
                    "right" => WingDungeonPlatformDirection.Right,
                    "down" => WingDungeonPlatformDirection.Down,
                    "left" => WingDungeonPlatformDirection.Left,
                    _ => throw row.Invalid(5, "up, right, down, or left")
                };
                commands[index] = new(direction, endpoint);
            }
            WingDungeonSidePlatformRecord record = new(
                row.HexByte(0),
                row.UnsignedDecimal(1),
                row.UnsignedDecimal(2),
                row.UnsignedDecimal(3),
                row.UnsignedDecimal(4),
                commands);
            if (!_platforms.TryAdd(record.SubId, record))
                throw row.Invalid(0, "a unique side-platform subid");
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
            WingDungeonMinecartRecord record = new(
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
        foreach (IReadOnlyList<ObjectRecord> records in _objects.Values)
            count += records.Count;
        if (count != 40 ||
            _constants.Count != 73 ||
            _patterns.Count != 5 ||
            _platforms.Count != 4 ||
            _minecarts.Count != 3 ||
            Constant("red-toggle-floor") != 0xad ||
            Constant("blue-toggle-floor") != 0xaf ||
            Constant("enemy-chest-wait") != 30 ||
            Pattern(ObjectKind.FloorPatternKey, 0).Count != 2 ||
            Pattern(ObjectKind.ColoredBlockKey, 2).Count != 4 ||
            SwitchTiles(0x13) != (0x5c, 0x5a) ||
            SidePlatform(0x06) is not
                { Speed: 20, RadiusY: 9, RadiusX: 7 } ||
            SidePlatform(0x07).Commands[1] is not
                { Direction: WingDungeonPlatformDirection.Right, Endpoint: 0xa8 } ||
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

    private static ObjectKind ParseKind(GeneratedTableRow row, int column) =>
        row.RequiredString(column) switch
        {
            "rupee-reward" => ObjectKind.RupeeReward,
            "feather-reward" => ObjectKind.FeatherReward,
            "floor-pattern-key" => ObjectKind.FloorPatternKey,
            "toggle-floor" => ObjectKind.ToggleFloor,
            "colored-cube" => ObjectKind.ColoredCube,
            "switch-tile-toggler" => ObjectKind.SwitchTileToggler,
            "minecart-gate" => ObjectKind.MinecartGate,
            "cube-light-sensor" => ObjectKind.CubeLightSensor,
            "cube-switch-sensor" => ObjectKind.CubeSwitchSensor,
            "enemy-chest" => ObjectKind.EnemyChest,
            "red-floor-trigger" => ObjectKind.RedFloorTrigger,
            "miniboss-reward" => ObjectKind.MinibossReward,
            "boss-reward" => ObjectKind.BossReward,
            "essence" => ObjectKind.Essence,
            "enemy-small-key" => ObjectKind.EnemySmallKey,
            "floor-switch-bit" => ObjectKind.FloorSwitchBit,
            "floor-color-changer" => ObjectKind.FloorColorChanger,
            "cube-color-source" => ObjectKind.CubeColorSource,
            "colored-block-key" => ObjectKind.ColoredBlockKey,
            "cube-flame" => ObjectKind.CubeFlame,
            "red-flame-trigger" => ObjectKind.RedFlameTrigger,
            "side-platform" => ObjectKind.SidePlatform,
            "circular-side-platform" => ObjectKind.CircularSidePlatform,
            "head-thwomp" => ObjectKind.HeadThwomp,
            "swoop" => ObjectKind.Swoop,
            _ => throw row.Invalid(column, "a supported Wing Dungeon object kind")
        };

    private static SpiritsGraveDatabaseCondition ParseCondition(
        GeneratedTableRow row,
        int column) =>
        row.RequiredString(column) switch
        {
            "always" => SpiritsGraveDatabaseCondition.Always,
            "item-clear" => SpiritsGraveDatabaseCondition.ItemClear,
            "flag80-clear" => SpiritsGraveDatabaseCondition.Flag80Clear,
            _ => throw row.Invalid(column, "always, item-clear, or flag80-clear")
        };
}

internal readonly record struct WingDungeonSidePlatformRecord(
    int SubId,
    int Speed,
    int Direction,
    int RadiusY,
    int RadiusX,
    WingDungeonPlatformCommand[] Commands);

internal readonly record struct WingDungeonPlatformCommand(
    WingDungeonPlatformDirection Direction,
    int Endpoint);

internal enum WingDungeonPlatformDirection
{
    Up,
    Right,
    Down,
    Left
}

internal readonly record struct WingDungeonMinecartRecord(
    int Slot,
    int Room,
    int Y,
    int X,
    string Source);
