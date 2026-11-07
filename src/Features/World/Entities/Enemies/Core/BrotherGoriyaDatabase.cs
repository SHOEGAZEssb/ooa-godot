namespace Oracles;

internal sealed class BrotherGoriyaDatabase
{
    internal EnemyProjectileVisualRecord Boomerang {get;}
    internal string IntroText {get;}
    internal BrotherGoriyaDatabase(string root)
    {
        var row=GeneratedTable.Load(root+"effects/brother_goriya_boomerang.tsv",
            new("Seasons PART$38",GeneratedTableKeySemantics.Ordered,
                ["sprites","tile-base","palette","source-grayscale-inverted","animations-base64"],headerRequired:true)).SingleRow();
        Boomerang=new(root,row.SplitRequired(0,','),row.UnsignedDecimal(1),row.UnsignedDecimal(2),row.Boolean01(3),row.EncodedAnimations(4));
        var text=GeneratedTable.Load(root+"objects/brother_goriya_text.tsv",
            new("Brother Goriya dialogue",GeneratedTableKeySemantics.Unique,["id","message-base64","source"],["id"],headerRequired:true)).SingleRow();
        if(text.HexWord(0)!=0x2f00)throw text.Invalid(0,"Brother Goriya TX_2f00");
        IntroText=text.Base64Utf8(1);_=text.RequiredString(2);
    }
}
