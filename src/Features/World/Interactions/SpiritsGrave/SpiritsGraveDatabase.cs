using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Spirit's Grave-owned native placements, constants, and text. Globally
/// dispatched dungeon interactions and enemy species are owned by their
/// shared databases instead.
/// </summary>
internal sealed class SpiritsGraveDatabase
{

    private readonly Lookup<(int Group, int Room), DungeonObjectRecord> _objects = new();
    private readonly Dictionary<string, int> _constants = new(StringComparer.Ordinal);
    private string _essenceMessage = string.Empty;

    internal SpiritsGraveDatabase()
    {
        LoadObjects();
        LoadConstants();
        LoadText();
        ValidateContract();
    }

    internal IReadOnlyList<DungeonObjectRecord> GetRoomRecords(int group, int room) =>
        _objects.ValuesOrEmpty((group, room));

    internal int Constant(string key) =>
        _constants.TryGetValue(key, out int value)
            ? value
            : throw new KeyNotFoundException(
                $"Spirit's Grave constant {key} was not imported.");

    internal string EssenceMessage => _essenceMessage;
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
            DungeonObjectRecord record = new DungeonObjectRecord(
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
        foreach (IReadOnlyList<DungeonObjectRecord> records in _objects.Values)
            objectCount += records.Count;
        if (objectCount != 17 ||
            _constants.Count != 5 ||
            string.IsNullOrWhiteSpace(_essenceMessage) ||
            Constant("moving-platform-spawn-wait") != 30 ||
            Constant("torch-count") != 2 ||
            Constant("torch-tile") != 0x45)
        {
            throw new InvalidOperationException(
                "Imported Spirit's Grave native-object contract is incomplete.");
        }
    }

    private static DungeonObjectKind ParseKind(GeneratedTableRow row, int column) =>
        row.RequiredString(column) switch
        {
            "bracelet-reward" => DungeonObjectKind.BraceletReward,
            "essence" => DungeonObjectKind.Essence,
            "boss-reward" => DungeonObjectKind.BossReward,
            "pumpkin-head" => DungeonObjectKind.PumpkinHead,
            "moving-platform" => DungeonObjectKind.MovingPlatform,
            "spawn-moving-platform" => DungeonObjectKind.SpawnMovingPlatform,
            "miniboss-reward" => DungeonObjectKind.MinibossReward,
            "giant-ghini" => DungeonObjectKind.GiantGhini,
            "torch-stairs" => DungeonObjectKind.TorchStairs,
            "enemy-small-key" => DungeonObjectKind.EnemySmallKey,
            "colored-cube" => DungeonObjectKind.ColoredCube,
            "cube-flame" => DungeonObjectKind.CubeFlame,
            "cube-light-sensor" => DungeonObjectKind.CubeLightSensor,
            "cube-trigger-sensor" => DungeonObjectKind.CubeTriggerSensor,
            _ => throw row.Invalid(column, "a supported Spirit's Grave object kind")
        };

}
