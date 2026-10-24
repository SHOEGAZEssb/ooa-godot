using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

internal sealed class BlackTowerEndingDatabase
{
    private const string Root="res://assets/oracle/cutscenes/black_tower_ending_";
    private readonly Dictionary<string,byte[]> _tables=[];
    private readonly Dictionary<int,List<NativeSimulatedInputEntry>> _inputs=[];
    private readonly Dictionary<string,List<(int Id,int SubId,Vector2 Position)>> _placements=[];
    private readonly Dictionary<int,string> _texts=[];
    private readonly Dictionary<(int,int),NpcRecord> _actors=[];
    private readonly Dictionary<(int,int),string> _animations=[];
    private readonly Dictionary<string,IReadOnlyList<CutsceneCommand>> _commands=[];
    private readonly Dictionary<int,(int Group,int Room,string Scene)> _roomSets=[];
    private readonly Dictionary<int,(int Group,int Room,string Scene,int Uncmp,int Palette)> _creditsRooms=[];
    private readonly Dictionary<(int,int),string[]> _extraSprites=[];
    private readonly Dictionary<(int,int),int> _nativeFlags=[];
    private readonly Dictionary<(int,int),IntroOamPart[][]> _nativeFrames=[];
    internal BlackTowerEndingDatabase()
    {
        foreach(var row in GeneratedTable.Load(Root+"tables.tsv",new("Black Tower ending tables",GeneratedTableKeySemantics.Unique,
            ["profile","values","source"],["profile"],headerRequired:true)).Rows)
            _tables.Add(row.RequiredString(0),Array.ConvertAll(row.SplitRequired(1,','),v=>Convert.ToByte(v,16)));
        foreach(var spec in new[]{("blackTowerEscapeCutscene_doorBlockReplacement",9),("blackTowerEscapeCutscene_doorOpenReplacement",9),
            ("explosion-positions",8),("debris-speeds",4),("dog-waits",8),("dog-targets",16),("dog-jumps",14),("dog-z",8),("boy-patterns",27),("companion-jumps",9),("triforce-positions",6),("triforce-glow-offsets",6),("waving-positions",6),("ship-z",8),("wave-intervals",4)})
            if(!_tables.TryGetValue(spec.Item1,out var values)||values.Length!=spec.Item2)
                throw new InvalidOperationException($"endgameCutscenes.s: missing or malformed {spec.Item1}.");
        foreach(var row in GeneratedTable.Load(Root+"inputs.tsv",new("Black Tower native simulated input",GeneratedTableKeySemantics.Ordered,
            ["stream","index","frames","keys","source"],headerRequired:true)).Rows)
        {
            int id=row.Decimal(0);if(!_inputs.TryGetValue(id,out var stream))_inputs.Add(id,stream=[]);
            if(row.Decimal(1)!=stream.Count)throw row.Invalid(1,"contiguous original input order");
            stream.Add(new(row.Decimal(2,0,65535),(byte)row.HexByte(3)));
        }
        if(_inputs.Count!=6||_inputs.Values.Any(s=>s.Count==0||s[^1].Frames!=65535))
            throw new InvalidOperationException("bank10.s / a8.s: missing native simulated-input stream or $ffff sentinel.");
        foreach(var row in GeneratedTable.Load(Root+"placements.tsv",new("Black Tower ordered ending actors",GeneratedTableKeySemantics.Ordered,
            ["scene","index","id","subid","y","x","source"],headerRequired:true)).Rows)
        {
            string name=row.RequiredString(0);if(!_placements.TryGetValue(name,out var actors))_placements.Add(name,actors=[]);
            if(row.Decimal(1)!=actors.Count)throw row.Invalid(1,"contiguous original object order");
            actors.Add((row.HexByte(2),row.HexByte(3),new(row.HexByte(5),row.HexByte(4))));
        }
        foreach(var row in GeneratedTable.Load(Root+"texts.tsv",new("Black Tower ending dialogue",GeneratedTableKeySemantics.Unique,
            ["text-id","message-base64","source"],["text-id"],headerRequired:true)).Rows)_texts.Add(row.HexWord(0),row.Base64Utf8(1));
        foreach(var row in GeneratedTable.Load(Root+"room_sets.tsv",new("Black Tower ending native room/object sets",GeneratedTableKeySemantics.Unique,
            ["index","group","room","scene","source"],["index"],headerRequired:true)).Rows)
            _roomSets.Add(row.HexByte(0),(row.HexByte(1),row.HexByte(2),row.RequiredString(3)));
        if(_roomSets.Count!=6||Enumerable.Range(8,5).Any(index=>!_roomSets.ContainsKey(index))||!_roomSets.ContainsKey(0x11))
            throw new InvalidOperationException("miscCutscenes.s / objectTable2: missing ending room/object sets $08..$0a/$11.");
        foreach(var row in GeneratedTable.Load(Root+"visuals.tsv",new("Black Tower ending actors",GeneratedTableKeySemantics.Unique,
            ["id","subid","sprite","tile-base","palette","animation","extra-sprite","native-flags","source"],["id","subid"],headerRequired:true)).Rows)
        {
            int id=row.HexByte(0),subid=row.HexByte(1);string animation=row.RequiredString(5);
            _actors.Add((id,subid),new(0,0x38,id,subid,0,0,0,0,row.RequiredString(2),row.Decimal(3),row.Decimal(4),0,false,
                animation,animation,animation,animation,"",NpcImplementationClassification.EventOwned));
            if(row.RequiredString(6)!="-")_extraSprites.Add((id,subid),row.SplitRequired(6,','));
            _nativeFlags.Add((id,subid),row.HexByte(7));
        }
        foreach(var row in GeneratedTable.Load(Root+"animations.tsv",new("Black Tower ending animation",GeneratedTableKeySemantics.Unique,
            ["id","index","animation","source"],["id","index"],headerRequired:true)).Rows)
            _animations.Add((row.HexByte(0),row.HexByte(1)),row.RequiredString(2));
        foreach(string script in new[]{"ambi","ambi-farewell","restored-boy","restored-father","restored-rabbit","credits-impa","reunion-impa","reunion-zelda","credits-zelda","credits-dog","listening-boy","listening-rabbit",
            "credits-ambi","credits-statue","credits-worker1-start","credits-worker2-start","credits-worker1-middle","credits-worker2-middle","credits-worker1-end","credits-worker2-end","lead-impa","kidnap-impa","kidnap-boy","kidnap-zelda","prerites-nayru"})
            _commands.Add(script,CutsceneCommandCatalog.Load(Root+script+"_commands.tsv"));
        foreach(var row in GeneratedTable.Load(Root+"linked_tables.tsv",new("Linked lead-in source tables",GeneratedTableKeySemantics.Unique,
            ["profile","values","source"],["profile"],headerRequired:true)).Rows)
            _tables.Add(row.RequiredString(0),Array.ConvertAll(row.SplitRequired(1,','),v=>Convert.ToByte(v,16)));
        foreach(var row in GeneratedTable.Load(Root+"credits_rooms.tsv",new("Normal credits room/object sets",GeneratedTableKeySemantics.Unique,
            ["index","group","room","scene","uncmp","palette","source"],["index"],headerRequired:true)).Rows)
            _creditsRooms.Add(row.Decimal(0,0,3),(row.HexByte(1),row.HexByte(2),row.RequiredString(3),row.HexByte(4),row.HexByte(5)));
        if(_creditsRooms.Count!=4)throw new InvalidOperationException("endgameCutscenes.s: incomplete normal credits room/object table.");
    }
    internal byte[] Table(string name)=>_tables[name];
    internal IReadOnlyList<NativeSimulatedInputEntry> Input(int index)=>_inputs[index];
    internal IReadOnlyList<(int Id,int SubId,Vector2 Position)> Placements(string name)=>_placements[name];
    internal string Text(int id)=>_texts[id];
    internal NpcRecord ActorRecord(int id,int subid)=>_actors[(id,subid)];
    internal IntroOamPart[][] NativeFrames(int id,int subid)
    {
        if(_nativeFrames.TryGetValue((id,subid),out var frames))return frames;
        var record=ActorRecord(id,subid);int flags=_nativeFlags[(id,subid)];
        frames=OracleGraphicsCache.GetAnimationDefinition(record.UpAnimation).Frames.Select(frame=>
            frame.EncodedOam.Length==0?Array.Empty<IntroOamPart>():frame.EncodedOam.Split(';').Select(part=>
            {
                var bytes=Array.ConvertAll(part.Split(','),int.Parse);
                return new IntroOamPart(bytes[0],bytes[1],(bytes[2]+record.TileBase)&255,bytes[3]^flags);
            }).ToArray()).ToArray();
        _nativeFrames.Add((id,subid),frames);return frames;
    }
    internal NpcCharacter Actor(int id,int subid,Vector2 position)
    {
        var actor=NpcCharacter.CreateFromRecord(_actors[(id,subid)] with {X=(int)position.X,Y=(int)position.Y});
        if(_extraSprites.TryGetValue((id,subid),out var extras))foreach(string extra in extras)actor.AppendScriptGraphics(extra);
        actor.SetAnimationRate(0);actor.SetBlocksLink(false);actor.SetDialogue(0,"",false);return actor;
    }
    internal string Animation(int id,int index)=>_animations[(id,index)];
    internal IReadOnlyList<CutsceneCommand> Commands(string script)=>_commands[script];
    internal (int Group,int Room,string Scene) RoomSet(int index)=>_roomSets.TryGetValue(index,out var row)?row:
        throw new NotSupportedException($"miscCutscenes.s: ending room/object set ${index:x2} is not represented.");
    internal (int Group,int Room,string Scene,int Uncmp,int Palette) CreditsRoom(int index)=>_creditsRooms[index];
}
