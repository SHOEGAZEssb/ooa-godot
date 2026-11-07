
namespace Oracles;

internal sealed class DungeonSwitchDatabase
{
    public string AssetRoot { get; }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Lazy<DungeonSwitchDatabase>> Packs = new(StringComparer.Ordinal);
    internal static DungeonSwitchDatabase For(string assetRoot) => Packs.GetOrAdd(assetRoot,
        static root => new Lazy<DungeonSwitchDatabase>(() => new DungeonSwitchDatabase(root))).Value;

    private readonly int[] _lockouts = new int[32];
    private readonly Dictionary<string,int> _parameters=[];
    private readonly (int Off,int On)[] _tiles=new (int,int)[24];
    internal int SwitchOffTile=>Parameter("off-tile");
    internal int SwitchOnTile=>Parameter("on-tile");
    internal int OverworldSwitchOnTile=>Parameter("overworld-on-tile");
    internal int SwitchRadiusY=>Parameter("radius-y");
    internal int SwitchRadiusX=>Parameter("radius-x");
    internal int SwitchCollisionZ=>Parameter("collision-z");
    internal int SwitchSound=>Parameter("sound");
    internal (int Off,int On) SwitchTiles(int index)=>index is >=0 and <24?_tiles[index]:
        throw new NotSupportedException($"INTERAC$78 tile replacement index${index:x2} is not imported.");
    private int Parameter(string key)=>_parameters.TryGetValue(key,out int value)?value:
        throw new InvalidOperationException($"PART$05 missing imported parameter {key}.");

    private DungeonSwitchDatabase(string assetRoot)
    {
        AssetRoot = assetRoot;
        var parameters=GeneratedTable.Load(assetRoot+"objects/part_switch_behavior.tsv",
            new("PART$05 parameters",GeneratedTableKeySemantics.Unique,["key","value"],["key"],headerRequired:true));
        foreach(var row in parameters.Rows)_parameters.Add(row.RequiredString(0),row.Decimal(1));
        if(_parameters.Count!=7)throw new InvalidOperationException($"{parameters.Path}: expected seven PART$05 parameters.");
        var tiles=GeneratedTable.Load(assetRoot+"objects/switch_tile_toggler.tsv",
            new("INTERAC$78 tile replacements",GeneratedTableKeySemantics.Unique,["index","off","on"],["index"],headerRequired:true));
        if(tiles.Rows.Count!=24)throw new InvalidOperationException($"{tiles.Path}: expected24 native switch tile pairs.");
        foreach(var row in tiles.Rows)_tiles[row.Decimal(0,0,23)]=(row.HexByte(1),row.HexByte(2));

        var table = GeneratedTable.Load(AssetRoot + "objects/part_switch_collisions.tsv",
            new GeneratedTableSchema("PART_SWITCH collision responses", GeneratedTableKeySemantics.Unique,
                ["item", "enabled", "effect", "lockout", "source"], ["item"], headerRequired: true));
        if (table.Rows.Count != 32) throw new InvalidOperationException("Incomplete PART_SWITCH collision row $03.");
        for (int index = 0; index < table.Rows.Count; index++)
        {
            var row = table.Rows[index];
            if (row.HexByte(0) != index) throw row.Invalid(0, "ordered collision indices $00-$1f");
            int enabled = row.Decimal(1, 0, 1), effect = row.HexByte(2), lockout = row.HexByte(3);
            if (enabled != 0 && (effect is not (0x20 or 0x26) || lockout != (effect == 0x26 ? 28 : 0)))
                throw row.Invalid(2, "PART_SWITCH effects $20/$26 and their source lockouts");
            _lockouts[index] = enabled != 0 ? lockout : -1;
        }
    }

    internal int HitLockout(int itemCollision) => itemCollision is >= 0 and < 32 ? _lockouts[itemCollision] : -1;
}
