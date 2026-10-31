using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// Ages $67:$03 preset and $71 scripts are not dependencies of the common D1 body.
internal sealed class AgesDimitriStoryDatabase
{
    private readonly Dictionary<int,string> _texts=new();
    private readonly (int Group,int Room) _presetRoom;
    internal Vector2 PresetPosition {get;}
    internal AgesDimitriStoryDatabase()
    {
        const string root=AgesGameContent.AssetRoot+"cutscenes/";
        GeneratedTable placements=GeneratedTable.Load(root+"dimitri_rooms.tsv",
            new("Ages Dimitri preset",GeneratedTableKeySemantics.Ordered,
                ["group","room","role","x","y","source"],headerRequired:true));
        if(placements.Rows.Count!=1)throw new InvalidOperationException($"{placements.Path}: expected the native $67:$03 preset.");
        var placement=placements.Rows[0];
        if(placement.RequiredString(2)!="preset")throw placement.Invalid(2,"native Ages preset role");
        _presetRoom=(placement.HexByte(0),placement.HexByte(1));
        PresetPosition=new(placement.HexByte(3),placement.HexByte(4));
        _=placement.RequiredString(5);
        foreach(var text in GeneratedTable.Load(root+"dimitri_texts.tsv",
            new("Ages Dimitri scripts",GeneratedTableKeySemantics.Ordered,
                ["text-id","text-base64","source"],headerRequired:true)).Rows)
        {
            _=text.RequiredString(2);
            _texts.Add(text.HexWord(0),text.Base64Utf8(1));
        }
    }
    internal bool ShouldSpawnPreset(int group,int room,OracleSaveData save)=>
        _presetRoom==(group,room) && (save.ReadField("wEssencesObtained")&4)!=0 && (save.ReadField("wDimitriState")&0x40)==0;
    internal string Text(int id)=>_texts.TryGetValue(id,out string? text)?text:
        throw new InvalidOperationException($"Ages Dimitri script has no imported TX_{id:x4}.");
}
