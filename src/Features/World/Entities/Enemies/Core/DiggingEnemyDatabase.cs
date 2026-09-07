using Godot;
using System;

namespace oracleofages;

/// <summary>PART_ITEM_DROP:$0f's ordered enemy selection and Beetle walk table.</summary>
internal sealed class DiggingEnemyDatabase
{
    private readonly EnemyCombatSourceDescriptor[] _enemies = new EnemyCombatSourceDescriptor[8];
    internal byte[] BeetleCounters { get; }

    internal DiggingEnemyDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/digging_enemies.tsv",
            new GeneratedTableSchema("digging enemies", GeneratedTableKeySemantics.Unique,
                ["index", "enemy-id", "collision-mode", "shield-l1", "shield-l2", "shield-l3", "source"],
                ["index"], headerRequired: true));
        int index = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            if (index >= 8 || row.UnsignedDecimal(0) != index ||
                row.HexByte(1) != (index < 3 ? 0x10 : 0x51))
                throw row.Invalid(0, "itemDrop_spawnEnemy's eight ordered Rope/Beetle entries");
            _enemies[index++] = new EnemyCombatSourceDescriptor(
                row.HexByte(1), 0, row.HexByte(2), 0, 0,
                row.HexByte(1) == 0x10 ? EnemyHandlerKind.Rope : EnemyHandlerKind.Beetle,
                row.RequiredString(6), row.HexByte(3), row.HexByte(4), row.HexByte(5),
                "data/ages/objectCollisionTable.s");
        }
        BeetleCounters = FileAccess.GetFileAsBytes("res://assets/oracle/metadata/beetle_counters.bin");
        if (index != 8 || BeetleCounters.Length != 8)
            throw new InvalidOperationException("itemDrop_spawnEnemy / beetle_chooseRandomAngleAndCounter1: expected eight table entries.");
    }

    internal EnemyCombatSourceDescriptor Enemy(int roll, int subId) =>
        _enemies[roll & 7] with { SubId = subId };
}
