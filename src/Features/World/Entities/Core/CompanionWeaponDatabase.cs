
namespace Oracles;

internal sealed class CompanionWeaponDatabase
{
    public string AssetRoot { get; }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<CompanionWeaponDatabase>> Packs = new(StringComparer.Ordinal);
    internal static CompanionWeaponDatabase For(string assetRoot) => Packs.GetOrAdd(assetRoot,
        static root => new Lazy<CompanionWeaponDatabase>(() => new CompanionWeaponDatabase(root))).Value;

    private readonly Dictionary<int, CompanionWeapon> _weapons = new();
    private CompanionWeaponDatabase(string assetRoot)
    {
        AssetRoot = assetRoot;

        var table = GeneratedTable.Load(AssetRoot + "metadata/companion_weapons.tsv",
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
