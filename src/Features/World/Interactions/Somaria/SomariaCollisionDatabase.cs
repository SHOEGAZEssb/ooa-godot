using System;

namespace oracleofages;

internal sealed class SomariaCollisionDatabase
{
    internal static SomariaCollisionDatabase Shared { get; } = new();
    private readonly SomariaCollisionEffects[] _effects = new SomariaCollisionEffects[0x7d];
    private readonly SomariaCollisionEligibility[] _enemies;
    private readonly SomariaCollisionEligibility[] _parts;

    private SomariaCollisionDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/metadata/somaria_collision_effects.tsv",
            new GeneratedTableSchema("Somaria collision effects", GeneratedTableKeySemantics.Unique,
                ["mode", "swing-effect", "block-effect", "source"], ["mode"], headerRequired: true));
        if (table.Rows.Count != _effects.Length)
            throw new InvalidOperationException("objectCollisionTable: expected $7d vanilla Somaria collision modes.");
        for (int mode = EnemyCollisionMode.Mode00; mode < _effects.Length; mode++)
        {
            var row = table.Rows[mode];
            if (row.HexByte(0) != mode)
                throw new InvalidOperationException($"objectCollisionTable: expected ordered mode ${mode:x2}.");
            _effects[mode] = new(row.HexByte(1), row.HexByte(2));
        }
        _enemies = LoadEligibility("enemy", 128);
        _parts = LoadEligibility("part", 0x5a);
    }

    private static SomariaCollisionEligibility[] LoadEligibility(string kind, int count)
    {
        var table = GeneratedTable.Load($"res://assets/oracle/metadata/somaria_{kind}_collisions.tsv",
            new GeneratedTableSchema($"Somaria {kind} eligibility", GeneratedTableKeySemantics.Unique,
                ["id", "swing-enabled", "block-enabled", "source"], ["id"], headerRequired: true));
        if (table.Rows.Count != count)
            throw new InvalidOperationException($"{kind}ActiveCollisions: expected ${count:x2} Somaria masks.");
        var result = new SomariaCollisionEligibility[count];
        for (int id = 0; id < count; id++)
        {
            var row = table.Rows[id];
            if (row.HexByte(0) != id)
                throw new InvalidOperationException($"{kind}ActiveCollisions: expected ordered type ${id:x2}.");
            result[id] = new(row.Decimal(1, 0, 1) != 0, row.Decimal(2, 0, 1) != 0);
        }
        return result;
    }

    internal SomariaCollisionEffects Effects(byte mode) => (mode & 0x7f) < _effects.Length
        ? _effects[mode & 0x7f]
        : throw new NotSupportedException($"Vanilla objectCollisionTable has no mode ${(mode & 0x7f):x2}.");
    internal SomariaCollisionEligibility Enemy(byte collisionType) => _enemies[collisionType & ObjectCollisionFlags.TypeMask];
    internal SomariaCollisionEligibility Part(byte collisionType)
    {
        int id = collisionType & ObjectCollisionFlags.TypeMask;
        if (id >= _parts.Length)
            throw new NotSupportedException($"partActiveCollisions has no mask for type ${id:x2}; Somaria eligibility is undefined.");
        return _parts[id];
    }
}

internal readonly record struct SomariaCollisionEffects(int Swing, int Block);
internal readonly record struct SomariaCollisionEligibility(bool Swing, bool Block);
