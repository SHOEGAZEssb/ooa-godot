using System.Collections.Generic;

namespace oracleofages;

internal sealed class CompanionWeaponDatabase
{
    internal static CompanionWeaponDatabase Shared { get; } = new();
    private readonly Dictionary<int, CompanionWeapon> _weapons = new();
    private CompanionWeaponDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/cutscenes/companion_weapons.tsv",
            new GeneratedTableSchema("companion weapon attributes", GeneratedTableKeySemantics.Unique,
                ["item", "collision-type", "damage", "source"], ["item"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            _weapons.Add(row.HexByte(0), new(row.HexByte(1), row.UnsignedDecimal(2)));
            _ = row.RequiredString(3);
        }
    }
    internal CompanionWeapon Weapon(int item) => _weapons[item];
}

internal readonly record struct CompanionWeapon(int CollisionType, int Damage);
