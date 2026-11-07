using System.Collections.Concurrent;

namespace Oracles;

internal sealed class LightableTorchDatabase
{
    private static readonly ConcurrentDictionary<string,Lazy<LightableTorchDatabase>> Packs=new(StringComparer.Ordinal);
    internal static LightableTorchDatabase For(string root)=>Packs.GetOrAdd(root,
        static key=>new Lazy<LightableTorchDatabase>(()=>new(key))).Value;
    private readonly Dictionary<string,int> _values=[];
    internal int UnlitTile=>Value("unlit-tile");
    internal int LitTile=>Value("lit-tile");
    internal int TorchRadiusY=>Value("radius-y");
    internal int TorchRadiusX=>Value("radius-x");
    internal int TorchItemCollision=>Value("item-collision");
    internal int LightSound=>Value("light-sound");

    private LightableTorchDatabase(string root)
    {
        var table=GeneratedTable.Load(root+"objects/lightable_torch_constants.tsv",
            new("PART$06 permanent torch parameters",GeneratedTableKeySemantics.Unique,["key","value"],["key"],headerRequired:true));
        foreach(var row in table.Rows)_values.Add(row.RequiredString(0),row.UnsignedDecimal(1));
        if(_values.Count!=6)throw new InvalidOperationException($"{table.Path}: expected six permanent torch parameters.");
    }
    private int Value(string key)=>_values.TryGetValue(key,out int value)?value:
        throw new InvalidOperationException($"PART$06: missing imported parameter {key}.");
}
