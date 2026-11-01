using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static Oracles.SeasonsGlobalFlag;

namespace Oracles;

internal sealed class SeasonsTalkNpcDatabase
{
    private const string Root = SeasonsGameContent.AssetRoot;
    private readonly Dictionary<(int Id, int Variant, int State), string> _selectors = [];
    private readonly NpcVisibilityRuleDatabase _visibility = new(Root);
    private readonly Dictionary<(int Id, int Subid), GeneratedTableRow> _visuals = [];
    private readonly Dictionary<(int Id, int Index), string> _animations = [];
    private readonly Dictionary<int, string> _texts = [];
    private readonly Dictionary<string, byte[]> _nativeTables = [];
    private readonly Dictionary<string, IReadOnlyList<CutsceneCommand>> _scripts = CutsceneCommandCatalog.LoadPrograms(Root + "npcs/commands.tsv");

    internal SeasonsTalkNpcDatabase()
    {
        foreach (var row in Table("texts", ["id", "message-base64", "source"]).Rows)
        { _ = row.RequiredString(2); _texts.Add(row.HexWord(0), row.Base64Utf8(1)); }
        foreach (var command in _scripts.Values.SelectMany(commands => commands))
        {
            var text = command switch
            {
                CutsceneShowTextCommand shown => (shown.TextId, shown.Message),
                CutsceneSetTextIdCommand loaded => (loaded.TextId, loaded.Message),
                _ => (0, string.Empty)
            };
            if (text.Item1 != 0 && !_texts.TryAdd(text.Item1, text.Item2) && _texts[text.Item1] != text.Item2)
                throw new InvalidOperationException($"{command.Source}: conflicting Seasons text TX_{text.Item1:x4}.");
        }
        foreach (var row in Table("native_tables", ["name", "bytes", "source"]).Rows)
        { _ = row.RequiredString(2); _nativeTables.Add(row.RequiredString(0), row.RequiredString(1).Split(',').Select(value => Convert.ToByte(value, 16)).ToArray()); }
        foreach (var row in Table("selectors", ["id", "variant", "state", "script", "source"]).Rows)
        {
            string script = row.RequiredString(3); _ = row.RequiredString(4);
            if (!_scripts.ContainsKey(script)) throw row.Invalid(3, "an imported Seasons NPC program");
            _selectors.Add((row.HexByte(0), row.HexByte(1), row.UnsignedDecimal(2)), script);
        }
        foreach (var row in Table("visuals", ["id", "subid", "sprite", "tile-base", "palette", "default-animation", "append-sprites", "source-offset", "source", "source-inverted"]).Rows)
            _visuals.Add((row.HexByte(0), row.HexByte(1)), row);
        foreach (var row in Table("animations", ["id", "index", "frames", "source"]).Rows)
        { _ = row.RequiredString(3); _animations.Add((row.HexByte(0), row.HexByte(1)), row.RequiredString(2)); }
    }
    private static GeneratedTable Table(string name, string[] columns) => GeneratedTable.Load(Root + $"npcs/{name}.tsv",
        new($"Seasons NPC {name}", GeneratedTableKeySemantics.Ordered, columns, headerRequired: true));

    internal static bool UsesMiscNpcHandler(int id) => id is 0x2d or 0x33 or 0x36 or 0x37 or 0x38 or 0x39 or 0x3c or 0x3d;

    internal static bool Supports(int id, int subid) => id switch
    {
        0x2d or 0x37 or 0x3c or 0x3d or 0x36 or 0x38 or 0x39 or 0x80 or 0x8a or 0x8b or 0x42 or 0xc2 => true,
        0x33 => (subid & 0x7f) is 0 or 3,
        0x30 => subid is >= 2 and <= 0x0f or >= 0x14 and <= 0x24,
        0x3e => (subid >> 4) <= 3,
        0xb1 => subid is >= 0x10 and <= 0x16,
        0x40 => subid == 9,
        0xa5 => subid == 8,
        0x4e => subid == 0x0a,
        0x25 => subid is 0 or 1,
        0x26 => subid==1,
        0x27 => subid==2,
        0xe5 => subid is 0 or 1,
        0x8d => subid is >=1 and <=3,
        0x2a => subid < 0x0a || subid == 0x0b,
        0x2e or 0x32 or 0xc8 => true,
        0x3b => (subid & 15) <= 5,
        0xb8 => subid is 0 or 1 or 2 or 4,
        0x9d => subid is 1 or 5,
        _ => false
    };

    internal NpcCharacter Create(NativeObjectInstruction source, OracleRoomData room, Color[,] palettes, NpcCharacter? presenter=null)
    {
        int graphicsSubid = source.Id == 0x3e ? source.SubId >> 4 : source.Id == 0x3b ? source.SubId & 15 : source.SubId & 0x7f;
        int last = _visuals.Keys.Where(key => key.Id == source.Id).Max(key => key.Subid);
        bool faces = UsesMiscNpcHandler(source.Id) ? (source.SubId & 0x80) == 0 :
            source.Id is 0x25 or 0x30 or 0x42 or 0x3b or 0x9d or 0xb1 or 0xb8;
        var npc = SeasonsNpcPresentation.CreateActor(_visuals[(source.Id, Math.Min(graphicsSubid, last))], source.Id, source.SubId,
            new(source.X, source.Y), index => Animation(source.Id, index), palettes, room.Group, room.Id, faces,presenter,
            UsesMiscNpcHandler(source.Id) ? NpcImplementationClassification.OrdinaryGeneric : NpcImplementationClassification.SpecializedNative, source.Var03);
        npc.SetScriptVisible(false);
        npc.SetScriptButtonSensitive(false);
        return npc;
    }
    internal string Animation(int id, int index) => _animations.TryGetValue((id, index), out var animation) ? animation :
        throw new NotSupportedException($"data/seasons/interactionAnimations.s: NPC ${id:x2} has no animation ${index:x2}.");
    internal string Text(int id) => _texts[id];
    internal byte[] NativeTable(string name) => _nativeTables[name];
    internal DungeonInteractionVisual BubbleVisual
    {
        get
        {
            var row=Table("bubble_visual",["sprite","tile-base","palette","inverted","animations-base64","source"]).SingleRow();
            _=row.RequiredString(5);
            return new(Root,"npc-bubble",[row.RequiredString(0)],row.UnsignedDecimal(1),row.UnsignedDecimal(2),row.Boolean01(3),row.Base64Utf8(4).Split('\n'));
        }
    }

    internal (bool Visible, IReadOnlyList<CutsceneCommand> Script) Select(NpcRecord npc, OracleSaveData save, OracleRuntimeState memory)
    {
        int id=npc.Id, subid=npc.SubId&0x7f, variant=0, state=0;
        if(id is 0x2d or 0x3c or 0x3d or 0x80 || id==0x37 && subid!=6)
            state=SeasonsNpcStoryState.HoronStage(save,memory);
        else if(id==0x37)state=11;
        else if(id==0x3e)
        {
            variant=npc.SubId>>4;
            state=variant==3?SeasonsNpcStoryState.SunkenStage(save):SeasonsNpcStoryState.HoronStage(save,memory);
        }
        else if(id is 0x2e or 0x36 or 0x39 or 0x8a or 0x8b)state=SeasonsNpcStoryState.SunkenStage(save);
        else if(id is 0x8d or 0x42 or 0xe5 or 0x33 or 0x32)state=subid;
        else if(id is 0x30 or 0xb1 or 0xb8)variant=npc.SubId;
        else if(id==0x2a && subid==0x0b)variant=0x0b;
        else if(id==0x3b){variant=subid&15;state=save.ReadField("wTradeItem")>=5&&save.HasTreasure(TreasureId.TradeItem)?1:0;}
        else if(id==0x9d)
            state=subid==5?11:memory.ReadField("wIsLinkedGame")!=0&&save.HasGlobalFlag(ZeldaKidnappedSeen)?12:
                BitOperations.Log2((uint)(save.HasTreasure(TreasureId.Essence)?save.ReadField("wEssencesObtained"):TreasureId.Essence));
        return (_visibility.ShouldShow(npc,save,memory),_scripts[_selectors[(id,variant,state)]]);
    }
}
