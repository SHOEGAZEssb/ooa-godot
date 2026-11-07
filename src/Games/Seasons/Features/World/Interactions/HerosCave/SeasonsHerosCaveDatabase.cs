
namespace Oracles;

internal sealed class SeasonsHerosCaveDatabase
{
    private const string Root=SeasonsGameContent.AssetRoot;
    private readonly Dictionary<string,int> _constants=new(StringComparer.Ordinal);
    private readonly Dictionary<int,string> _texts=new();
    private readonly GeneratedTableRow _visual;
    internal SeasonsHerosCaveDatabase()
    {
        foreach(var row in Table("constants",["key","value","source"]).Rows){_constants.Add(row.RequiredString(0),row.Decimal(1));_=row.RequiredString(2);}
        foreach(var row in Table("text",["id","message-base64","source"]).Rows){_texts.Add(row.HexWord(0),row.Base64Utf8(1));_=row.RequiredString(2);}
        _visual=Table("sword_visual",["id","subid","sprite","tile-base","palette","default-animation","append-sprites","source-offset","source","source-inverted","animation"]).SingleRow();
        if(_constants.Count!=11 || _texts.Count!=2 || _visual.HexByte(0)!=0xc6 || _visual.HexByte(1)!=0)
            throw new InvalidOperationException("Seasons Hero's Cave: incomplete native first-sword data.");
    }
    private static GeneratedTable Table(string name,string[] columns)=>GeneratedTable.Load(Root+$"heros_cave/{name}.tsv",
        new($"Seasons Hero's Cave {name}",GeneratedTableKeySemantics.Unique,columns,[columns[0]],headerRequired:true));
    internal int Constant(string key)=>_constants.TryGetValue(key,out int value)?value:
        throw new KeyNotFoundException($"Hero's Cave has no native parameter '{key}'.");
    internal string Text(int id)=>_texts.TryGetValue(id,out var value)?value:
        throw new KeyNotFoundException($"Hero's Cave has no native TX_{id:x4}.");
    internal NpcCharacter CreateSword(Vector2 position,Color[,] palettes)
    {
        var actor=SeasonsNpcPresentation.CreateActor(_visual,0xc6,0,position,_=>_visual.RequiredString(10),palettes);
        actor.Hide();
        return actor;
    }
    internal Warp ReturnWarp(int group,int room)=>new(group,room,0,0,Constant("warp-source")&15,
        Constant("warp-group"),Constant("warp-room"),Constant("warp-position"),0,Constant("warp-arrival"),
        DirectFadeOut:true,NativeFadeDelay:(Constant("warp-source")&0x80)!=0?4:1,
        NativeSource:"herosCaveSwordChest.s:@warpDestVariables");
}
