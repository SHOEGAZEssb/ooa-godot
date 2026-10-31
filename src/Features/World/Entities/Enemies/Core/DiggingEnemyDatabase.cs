using Godot;
using System;

namespace Oracles;

/// <summary>PART_ITEM_DROP:$0f's ordered enemy selection and Beetle walk table.</summary>
internal sealed class DiggingEnemyDatabase
{
    public string AssetRoot { get; }

    private readonly EnemyCombatSourceDescriptor[] _enemies = new EnemyCombatSourceDescriptor[8];
    internal byte[] BeetleCounters { get; }
    private readonly int _overrideFlags;
    private readonly EnemyCombatSourceDescriptor _override;

    internal DiggingEnemyDatabase(string assetRoot)
    {
        AssetRoot = assetRoot;

        GeneratedTable table = GeneratedTable.Load(
            AssetRoot + "metadata/digging_enemies.tsv",
            new GeneratedTableSchema("digging enemies", GeneratedTableKeySemantics.Unique,
                ["index", "enemy-id", "collision-mode", "source"],
                ["index"], headerRequired: true));
        int index = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            if (index >= 8 || row.UnsignedDecimal(0) != index ||
                row.HexByte(1) != (index < 3 ? 0x10 : 0x51))
                throw row.Invalid(0, "itemDrop_spawnEnemy's eight ordered Rope/Beetle entries");
            _enemies[index++] = new EnemyCombatSourceDescriptor(
                AssetRoot, row.HexByte(1), 0, row.HexByte(2), 0, 0,
                row.RequiredString(3));
        }
        BeetleCounters = OracleAssetCache.ReadBytes(AssetRoot + "metadata/beetle_counters.bin");
        if (index != 8 || BeetleCounters.Length != 8)
            throw new InvalidOperationException("itemDrop_spawnEnemy / beetle_chooseRandomAngleAndCounter1: expected eight table entries.");
        var rule = GeneratedTable.Load(AssetRoot + "metadata/digging_enemy_override.tsv",
            new("native digging override", GeneratedTableKeySemantics.Ordered,
                ["flags", "enemy-id", "collision-mode", "source"], headerRequired: true)).SingleRow();
        _overrideFlags = rule.Decimal(0, -1, 255);
        _override = new(AssetRoot, rule.HexByte(1), 0, rule.HexByte(2), 0, 0, rule.RequiredString(3));
    }

    internal EnemyCombatSourceDescriptor Enemy(int roll, int subId, int tilesetFlags = 0) =>
        (tilesetFlags == _overrideFlags ? _override : _enemies[roll & 7]) with { SubId = subId };
}
