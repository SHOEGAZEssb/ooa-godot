using System.Collections.Concurrent;

namespace Oracles;

// collisionEffects.s selects a live target mode and an independent item column.
internal sealed class ObjectCollisionDatabase
{
    private static readonly ConcurrentDictionary<string, Lazy<ObjectCollisionDatabase>> Packs = new(StringComparer.Ordinal);
    internal static ObjectCollisionDatabase For(string assetRoot) => Packs.GetOrAdd(assetRoot,
        static root => new Lazy<ObjectCollisionDatabase>(() => new(root))).Value;

    internal string AssetRoot { get; }
    internal int ModeCount => _effects.Length;
    internal int PartCount => _parts.Length;
    private readonly byte[][] _effects, _enemies, _parts;
    private readonly int[] _enemyModes = new int[0x80];
    private readonly int[] _itemTypes = new int[0x2c];

    private ObjectCollisionDatabase(string assetRoot)
    {
        AssetRoot = assetRoot;
        _effects = LoadRows("effects", "mode", "effects", 32);
        _enemies = LoadRows("enemy_masks", "id", "mask", 4);
        _parts = LoadRows("part_masks", "id", "mask", 4);
        if (_enemies.Length != 0x80)
            throw new InvalidOperationException($"{AssetRoot}: enemyActiveCollisions requires $80 native masks.");
        var table = GeneratedTable.Load(AssetRoot + "metadata/object_collision_enemy_modes.tsv",
            new GeneratedTableSchema("enemyData collision modes", GeneratedTableKeySemantics.Unique,
                ["id", "mode", "source"], ["id"], headerRequired: true));
        if (table.Rows.Count != _enemyModes.Length)
            throw new InvalidOperationException($"{AssetRoot}: enemyData requires $80 native modes.");
        for (int id = 0; id < _enemyModes.Length; id++)
        {
            var row = table.Rows[id];
            if (row.HexByte(0) != id || row.HexByte(1) >= ModeCount)
                throw row.Invalid(0, "ordered enemy IDs with a native collision mode");
            _enemyModes[id] = row.HexByte(1);
            _ = row.RequiredString(2);
        }
        table = GeneratedTable.Load(AssetRoot + "metadata/object_collision_item_types.tsv",
            new GeneratedTableSchema("itemAttributes collision types", GeneratedTableKeySemantics.Unique,
                ["id", "type", "source"], ["id"], headerRequired: true));
        if (table.Rows.Count != _itemTypes.Length)
            throw new InvalidOperationException($"{AssetRoot}: itemAttributes requires $2c native types.");
        for (int id = 0; id < _itemTypes.Length; id++)
        {
            var row = table.Rows[id];
            if (row.HexByte(0) != id || row.HexByte(1) >= 0x20)
                throw row.Invalid(0, "ordered item IDs with a native collision type");
            _itemTypes[id] = row.HexByte(1);
            _ = row.RequiredString(2);
        }
    }

    private byte[][] LoadRows(string name, string key, string value, int width)
    {
        var table = GeneratedTable.Load(AssetRoot + $"metadata/object_collision_{name}.tsv",
            new GeneratedTableSchema("native object collisions", GeneratedTableKeySemantics.Unique,
                [key, value, "source"], [key], headerRequired: true));
        if (table.Rows.Count is < 1 or > 0x80)
            throw new InvalidOperationException($"{AssetRoot}: invalid native collision {name} extent.");
        var result = new byte[table.Rows.Count][];
        for (int index = 0; index < result.Length; index++)
        {
            var row = table.Rows[index];
            if (row.HexByte(0) != index) throw row.Invalid(0, "ordered native collision rows");
            string bytes = row.RequiredString(1);
            try { result[index] = Convert.FromHexString(bytes); }
            catch (FormatException) { throw row.Invalid(1, $"{width} hexadecimal bytes"); }
            if (result[index].Length != width) throw row.Invalid(1, $"{width} hexadecimal bytes");
            _ = row.RequiredString(2);
        }
        return result;
    }

    internal int Effect(int mode, int item)
    {
        CheckItem(item);
        int index = mode & 0x7f;
        if (index >= _effects.Length)
            throw new NotSupportedException($"{AssetRoot}: objectCollisionTable has no mode ${index:x2}, item ${item:x2}.");
        return _effects[index][item];
    }

    internal bool EnemyEnabled(int type, int item) => type >= 0 && Enabled(_enemies, type, item, "enemy");
    internal bool PartEnabled(int type, int item) => Enabled(_parts, type, item, "part");
    private bool Enabled(byte[][] masks, int type, int item, string kind)
    {
        CheckItem(item);
        int index = type & 0x7f;
        if (type < 0 || index >= masks.Length)
            throw new NotSupportedException($"{AssetRoot}: {kind}ActiveCollisions has no type ${index:x2}, item ${item:x2}.");
        return (masks[index][item >> 3] & (1 << (item & 7))) != 0;
    }

    internal int EnemyMode(int id) => id is >= 0 and < 0x80 ? _enemyModes[id] :
        throw new ArgumentOutOfRangeException(nameof(id), $"{AssetRoot}: enemyData has no ID ${id:x2}.");
    internal int ItemType(int id) => id is >= 0 and < 0x2c ? _itemTypes[id] :
        throw new ArgumentOutOfRangeException(nameof(id), $"{AssetRoot}: itemAttributes has no ID ${id:x2}.");
    private static void CheckItem(int item)
    {
        if (item is < 0 or >= 0x20) throw new ArgumentOutOfRangeException(nameof(item), $"Native collision column ${item:x2} is outside $00..$1f.");
    }
}
