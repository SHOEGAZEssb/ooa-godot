using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Typed native-object, visual, and enemy records needed by Spirit's Grave.
/// Room placement and every OAM frame are generated from the supported Ages
/// disassembly; this class only enforces the runtime contract.
/// </summary>
internal sealed class SpiritsGraveDatabase
{

    private readonly Dictionary<(int Group, int Room), List<ObjectRecord>> _objects = new();
    private readonly Dictionary<(int Id, int SubId), EnemyRecord> _enemies = new();
    private readonly Dictionary<string, VisualRecord> _visuals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _constants = new(StringComparer.Ordinal);
    private readonly Dictionary<int, Color[]> _cubePalettes = new();
    private string _essenceMessage = string.Empty;

    internal SpiritsGraveDatabase()
    {
        LoadObjects();
        LoadEnemies();
        LoadVisuals();
        LoadCubePalettes();
        LoadConstants();
        LoadText();
        ValidateContract();
    }

    internal IReadOnlyList<ObjectRecord> GetRoomRecords(int group, int room) =>
        _objects.TryGetValue((group, room), out List<ObjectRecord>? records)
            ? records
            : Array.Empty<ObjectRecord>();

    internal EnemyRecord Enemy(int id, int subId = 0) =>
        _enemies.TryGetValue((id, subId), out EnemyRecord record)
            ? record
            : throw new KeyNotFoundException(
                $"Spirit's Grave enemy ${id:x2}:${subId:x2} was not imported.");

    internal VisualRecord Visual(string key) =>
        _visuals.TryGetValue(key, out VisualRecord record)
            ? record
            : throw new KeyNotFoundException(
                $"Spirit's Grave visual {key} was not imported.");

    internal int Constant(string key) =>
        _constants.TryGetValue(key, out int value)
            ? value
            : throw new KeyNotFoundException(
                $"Spirit's Grave constant {key} was not imported.");

    internal Vector2 MovingPlatformCollisionRadii(int rawSubId)
    {
        int size = rawSubId & 0x07;
        return new Vector2(
            Constant($"platform-radius-{size}-x"),
            Constant($"platform-radius-{size}-y"));
    }

    internal string EssenceMessage => _essenceMessage;
    internal IReadOnlyDictionary<int, Color[]> CubePalettes => _cubePalettes;

    private void LoadObjects()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/spirits_grave_objects.tsv",
            new GeneratedTableSchema(
                "Spirit's Grave native objects",
                GeneratedTableKeySemantics.Grouped,
                [
                    "group", "room", "order", "kind", "id", "subid", "y", "x",
                    "condition", "source"
                ],
                ["group", "room"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            ObjectRecord record = new ObjectRecord(
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
            if (!_objects.TryGetValue(
                (record.Group, record.Room), out List<ObjectRecord>? records))
            {
                records = new List<ObjectRecord>();
                _objects.Add((record.Group, record.Room), records);
            }
            records.Add(record);
        }
        foreach (List<ObjectRecord> records in _objects.Values)
            records.Sort((left, right) => left.Order.CompareTo(right.Order));
    }

    private void LoadEnemies()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/spirits_grave_enemies.tsv",
            new GeneratedTableSchema(
                "Spirit's Grave enemies",
                GeneratedTableKeySemantics.Unique,
                [
                    "id", "subid", "sprites", "tile-base", "palette",
                    "source-grayscale-inverted", "radius-y", "radius-x",
                    "damage-quarters", "health", "animations-base64"
                ],
                ["id", "subid"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            EnemyRecord record = new EnemyRecord(
                row.HexByte(0),
                row.HexByte(1),
                SplitRequired(row, 2, ','),
                row.UnsignedDecimal(3),
                row.UnsignedDecimal(4),
                row.Boolean01(5),
                row.UnsignedDecimal(6),
                row.UnsignedDecimal(7),
                row.UnsignedDecimal(8),
                row.UnsignedDecimal(9),
                SplitDecoded(row, 10));
            if (!_enemies.TryAdd((record.Id, record.SubId), record))
                throw new InvalidOperationException(
                    $"Duplicate Spirit's Grave enemy ${record.Id:x2}:${record.SubId:x2}.");
        }
    }

    private void LoadVisuals()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/spirits_grave_visuals.tsv",
            new GeneratedTableSchema(
                "Spirit's Grave interaction visuals",
                GeneratedTableKeySemantics.Unique,
                [
                    "key", "sprites", "tile-base", "palette",
                    "source-grayscale-inverted", "animations-base64"
                ],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            VisualRecord record = new VisualRecord(
                row.RequiredString(0),
                SplitRequired(row, 1, ','),
                row.UnsignedDecimal(2),
                row.UnsignedDecimal(3),
                row.Boolean01(4),
                SplitDecoded(row, 5));
            if (!_visuals.TryAdd(record.Key, record))
                throw new InvalidOperationException(
                    $"Duplicate Spirit's Grave visual {record.Key}.");
        }
    }

    private void LoadConstants()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/spirits_grave_constants.tsv",
            new GeneratedTableSchema(
                "Spirit's Grave constants",
                GeneratedTableKeySemantics.Unique,
                ["key", "value"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            if (!_constants.TryAdd(row.RequiredString(0), row.UnsignedDecimal(1)))
                throw new InvalidOperationException(
                    $"Duplicate Spirit's Grave constant at {row.Path}:{row.LineNumber}.");
        }
    }

    private void LoadCubePalettes()
    {
        Color[,] palettes = OracleGraphicsData.LoadPalette(
            "res://assets/oracle/objects/spirits_grave_cube_palettes.bin", 2, 6);
        for (int palette = 6; palette <= 7; palette++)
        {
            var colors = new Color[4];
            for (int shade = 0; shade < colors.Length; shade++)
                colors[shade] = palettes[palette, shade];
            _cubePalettes.Add(palette, colors);
        }
    }

    private void LoadText()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/spirits_grave_text.tsv",
            new GeneratedTableSchema(
                "Spirit's Grave text",
                GeneratedTableKeySemantics.Unique,
                ["text-id", "message-base64"],
                ["text-id"],
                headerRequired: true));
        if (table.Rows.Count != 1 || table.Rows[0].HexWord(0) != 0x000e)
            throw new InvalidOperationException("Spirit's Grave must import TX_000e.");
        _essenceMessage = table.Rows[0].Base64Utf8(1);
    }

    private void ValidateContract()
    {
        int objectCount = 0;
        foreach (List<ObjectRecord> records in _objects.Values)
            objectCount += records.Count;
        if (objectCount != 17 || _enemies.Count != 7 || _visuals.Count != 10 ||
            _constants.Count != 25 ||
            Enemy(0x0a) is not { Health: 3, DamageQuarters: 2, Animations.Length: 4 } ||
            Enemy(0x70) is not { Health: 12, DamageQuarters: 1, Sprites.Length: 2 } ||
            Enemy(0x78) is not { Health: 8, Sprites.Length: 3 } ||
            Visual("colored-cube").Animations.Length != 30 ||
            Visual("energy-bead").Animations.Length != 8 ||
            Visual("moblin-boomerang").Animations.Length != 1 ||
            Visual("pumpkin-projectile").Animations.Length != 1 ||
            _cubePalettes.Count != 2 ||
            string.IsNullOrWhiteSpace(_essenceMessage) ||
            Constant("cube-push-frames") != 20 ||
            Constant("move-block-sound") != 0x7f ||
            Constant("pumpkin-body-palette") != 1 ||
            Constant("pumpkin-ghost-palette") != 5 ||
            MovingPlatformCollisionRadii(0x09) != new Vector2(8, 16) ||
            MovingPlatformCollisionRadii(0x05) != new Vector2(16, 16))
        {
            throw new InvalidOperationException(
                "Imported Spirit's Grave native-object contract is incomplete.");
        }
    }

    private static ObjectKind ParseKind(GeneratedTableRow row, int column) =>
        row.RequiredString(column) switch
        {
            "bracelet-reward" => ObjectKind.BraceletReward,
            "essence" => ObjectKind.Essence,
            "boss-reward" => ObjectKind.BossReward,
            "pumpkin-head" => ObjectKind.PumpkinHead,
            "moving-platform" => ObjectKind.MovingPlatform,
            "spawn-moving-platform" => ObjectKind.SpawnMovingPlatform,
            "miniboss-reward" => ObjectKind.MinibossReward,
            "giant-ghini" => ObjectKind.GiantGhini,
            "torch-stairs" => ObjectKind.TorchStairs,
            "enemy-small-key" => ObjectKind.EnemySmallKey,
            "colored-cube" => ObjectKind.ColoredCube,
            "cube-flame" => ObjectKind.CubeFlame,
            "cube-light-sensor" => ObjectKind.CubeLightSensor,
            "cube-trigger-sensor" => ObjectKind.CubeTriggerSensor,
            _ => throw row.Invalid(column, "a supported Spirit's Grave object kind")
        };

    private static SpiritsGraveDatabaseCondition ParseCondition(GeneratedTableRow row, int column) =>
        row.RequiredString(column) switch
        {
            "always" => SpiritsGraveDatabaseCondition.Always,
            "item-clear" => SpiritsGraveDatabaseCondition.ItemClear,
            "flag80-clear" => SpiritsGraveDatabaseCondition.Flag80Clear,
            _ => throw row.Invalid(column, "always, item-clear, or flag80-clear")
        };

    private static string[] SplitRequired(
        GeneratedTableRow row, int column, char separator)
    {
        string[] values = row.RequiredString(column).Split(
            separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (values.Length == 0)
            throw row.Invalid(column, "one or more values");
        return values;
    }

    private static string[] SplitDecoded(GeneratedTableRow row, int column)
    {
        string value = row.Base64Utf8(column);
        string[] values = value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (values.Length == 0)
            throw row.Invalid(column, "one or more encoded animations");
        return values;
    }
}
