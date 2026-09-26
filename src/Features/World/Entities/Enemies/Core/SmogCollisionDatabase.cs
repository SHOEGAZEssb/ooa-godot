using System;

namespace oracleofages;

internal sealed class SmogCollisionDatabase
{
    private readonly bool[] _enabled = new bool[32];
    private readonly int[] _cloud = new int[32], _large = new int[32];
    internal bool Enabled(int collision) => collision is >= 0 and < 32 && _enabled[collision];
    internal int Effect(int mode, int collision) => mode switch
    {
        EnemyCollisionMode.ProjectileWithRingMod => _cloud[collision], EnemyCollisionMode.Smog => _large[collision],
        _ => throw new NotSupportedException($"ENEMY_SMOG $7c collision mode${mode:x2} is not represented.")
    };
    internal SmogCollisionDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/enemies/smog_collisions.tsv",
            new GeneratedTableSchema("ENEMY_SMOG $7c collisions", GeneratedTableKeySemantics.Unique,
                ["collision", "enabled", "cloud-effect", "large-effect", "source"], ["collision"], headerRequired: true));
        if (table.Rows.Count != 32) throw new InvalidOperationException("Smog requires32 collision entries.");
        for (int i = 0; i < 32; i++)
        {
            var row = table.Rows[i];
            if (row.HexByte(0) != i) throw row.Invalid(0,"ordered collision type");
            _enabled[i] = row.Decimal(1,0,1) != 0;
            _cloud[i] = row.HexByte(2); _large[i] = row.HexByte(3);
        }
    }
}
