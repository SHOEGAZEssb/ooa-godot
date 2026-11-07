namespace Oracles;

internal sealed class SeasonsDungeonEssenceDatabase
{
    private const string Root=SeasonsGameContent.AssetRoot;
    internal DungeonEssenceDefinition Essence {get;}
    internal IReadOnlyList<CutsceneCommand> MakuScript {get;}=CutsceneCommandCatalog.Load(Root+"cutscenes/root_dungeon_maku_commands.tsv");
    private readonly Dictionary<string,int[]> _tables=new(StringComparer.Ordinal);
    internal int[] Table(string key)=>_tables.TryGetValue(key,out var value)?value:throw new KeyNotFoundException($"Seasons remote Maku: missing {key}.");
    private readonly Dictionary<string,DungeonInteractionVisual> _visuals=new(StringComparer.Ordinal);
    internal DungeonInteractionVisual Visual(string key)=>_visuals.TryGetValue(key,out var value)?value:
        throw new KeyNotFoundException($"Seasons INTERAC$7f: missing {key} visual.");
    internal SeasonsDungeonEssenceDatabase()
    {
        var row=GeneratedTable.Load(Root+"objects/root_dungeon_essence.tsv",new("Seasons D1 Essence",GeneratedTableKeySemantics.Ordered,
            ["index","text-id","text-base64","destination-group","destination-room","destination-position","destination-transition","source"],headerRequired:true)).SingleRow();
        _=row.RequiredString(7);
        if(row.UnsignedDecimal(0)!=0||row.HexWord(1)!=0x000e)throw row.Invalid(0,"Seasons first Essence/TX_000e");
        Essence=new(row.UnsignedDecimal(0),row.Base64Utf8(2),new(4,0x13,-1,0,0,row.Decimal(3,0,7),row.HexByte(4),row.HexByte(5),0,row.HexByte(6)));
        foreach(var visual in GeneratedTable.Load(Root+"objects/root_dungeon_essence_visuals.tsv",new("Seasons Essence visuals",GeneratedTableKeySemantics.Unique,
            ["key","sprites","tile-base","palette","source-grayscale-inverted","animations-base64"],["key"],headerRequired:true)).Rows)
            _visuals.Add(visual.RequiredString(0),new(Root,visual.RequiredString(0),visual.SplitRequired(1,','),visual.UnsignedDecimal(2),
                visual.UnsignedDecimal(3),visual.Boolean01(4),visual.EncodedAnimations(5)));
        foreach(var group in GeneratedTable.Load(Root+"objects/root_dungeon_maku_tables.tsv",new("Seasons remote Maku native tables",GeneratedTableKeySemantics.Unique,
            ["profile","index","value","source"],["profile","index"],headerRequired:true)).Rows.GroupBy(row=>row.RequiredString(0)))
            _tables.Add(group.Key,group.Select((row,index)=>{
                _=row.RequiredString(3);if(row.UnsignedDecimal(1)!=index)throw row.Invalid(1,"ordered native table index");return row.Decimal(2,0,255);
            }).ToArray());
        foreach(var (key,count) in new[]{("leaf-delays",4),("leaf-positions",14),("leaf-motion",73),("maku-map-text",13)})
            if(!_tables.TryGetValue(key,out var values)||values.Length!=count)
                throw new InvalidOperationException($"{Root}objects/root_dungeon_maku_tables.tsv: {key} requires {count} ordered source bytes.");
        if(_tables.Count!=4||Table("leaf-motion")[72]!=0xff)
            throw new InvalidOperationException($"{Root}objects/root_dungeon_maku_tables.tsv: unsupported source table or motion terminator.");
    }
}
