using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Oracles;

internal sealed class NativeObjectStreamDatabase
{
    private static readonly ConcurrentDictionary<string,Lazy<NativeObjectStreamDatabase>> Packs = new(StringComparer.Ordinal);
    internal static NativeObjectStreamDatabase For(string root) => Packs.GetOrAdd(root,
        static pack => new Lazy<NativeObjectStreamDatabase>(() => new(pack))).Value;
    private readonly Dictionary<int,NativeObjectInstruction> _instructions=[];
    private readonly Dictionary<string,int> _labels=new(StringComparer.Ordinal);
    private readonly Dictionary<int,int> _roots=[];
    private readonly Dictionary<(int Group,int Room,int Order),int> _legacyPlacements=[];
    internal int PointerLoadMask { get; }
    private readonly byte _initialLoadFlags;
    private readonly byte _restoreLoadMask;
    private readonly int _rootLoadMask;
    private readonly int _rememberedCompanionLoadMask;
    private readonly int _mapleLoadMask;

    private NativeObjectStreamDatabase(string root)
    {
        var program=GeneratedTable.Load(root+"objects/room_object_streams.tsv",new("native object instructions",
            GeneratedTableKeySemantics.Unique,["address","next","opcode","header","flags","id","subid","y","x","position","var03","target","mask","source"],
            ["address"],headerRequired:true));
        foreach (var row in program.Rows)
        {
            var instruction=new NativeObjectInstruction(row.HexWord(0),row.HexWord(1),row.HexByte(2),row.Boolean01(3),
                row.HexByte(4),row.HexByte(5),row.HexByte(6),row.HexByte(7),row.HexByte(8),row.HexByte(9),row.HexByte(10),
                row.HexWord(11),row.HexByte(12),row.RequiredString(13));
            if (instruction.Address is <0x4000 or >=0x8000 || instruction.Next<=instruction.Address || instruction.Next>0x8000 ||
                instruction.Opcode is not (>=0 and <=10 or 0xfd or 0xfe or 0xff) ||
                instruction.Opcode==9 && instruction.Flags>2 || !_instructions.TryAdd(instruction.Address,instruction))
                throw row.Invalid(0,"one valid native object instruction boundary");
        }
        foreach (var row in GeneratedTable.Load(root+"objects/room_object_labels.tsv",new("native object labels",
            GeneratedTableKeySemantics.Unique,["label","address","source"],["label"],headerRequired:true)).Rows)
        {
            _labels.Add(row.RequiredString(0),row.HexWord(1)); _=row.RequiredString(2);
        }
        foreach (var row in GeneratedTable.Load(root+"objects/room_object_roots.tsv",new("native object roots",
            GeneratedTableKeySemantics.Unique,["group","room","address","source"],["group","room"],headerRequired:true)).Rows)
        {
            int address=row.HexWord(2); _=row.RequiredString(3);
            if (!_instructions.ContainsKey(address)) throw row.Invalid(2,"an imported object instruction");
            _roots.Add(row.Decimal(0,0,7)*256+row.HexByte(1),address);
        }
        if (_roots.Count!=2048) throw new InvalidOperationException($"{root}: expected $0800 native room object roots.");
        foreach (var row in GeneratedTable.Load(root+"objects/legacy_placement_bindings.tsv",new("legacy placement bindings",
            GeneratedTableKeySemantics.Unique,["group","room","order","address","source"],["group","room","order"],headerRequired:true)).Rows)
        {
            int address=row.HexWord(3); _=row.RequiredString(4);
            if (!_instructions.ContainsKey(address)) throw row.Invalid(3,"an imported object instruction");
            _legacyPlacements.Add((row.Decimal(0,0,7),row.HexByte(1),row.UnsignedDecimal(2)),address);
        }
        var policy=GeneratedTable.Load(root+"metadata/object_stream_policy.tsv",new("native object pointer policy",
            GeneratedTableKeySemantics.Ordered,["pointer-load-mask","initial-load-flags","restore-load-mask","root-load-mask","remembered-companion-load-mask","maple-load-mask","source"],headerRequired:true)).SingleRow();
        PointerLoadMask=policy.HexByte(0); _initialLoadFlags=(byte)policy.HexByte(1); _restoreLoadMask=(byte)policy.HexByte(2);
        _rootLoadMask=policy.HexByte(3); _rememberedCompanionLoadMask=policy.HexByte(4); _mapleLoadMask=policy.HexByte(5); _=policy.RequiredString(6);
        if (PointerLoadMask is not (0 or 2)) throw policy.Invalid(0,"native pointer load mask $00 or $02");
        if (PointerLoadMask==0 && (_initialLoadFlags!=0 || _restoreLoadMask!=0) ||
            PointerLoadMask==2 && (_initialLoadFlags!=0xff || _restoreLoadMask!=0x80))
            throw policy.Invalid(1,"source object-load initialization and completion masks");
        if (_rootLoadMask!=(PointerLoadMask==0 ? 0 : 1) || _rememberedCompanionLoadMask!=(PointerLoadMask==0 ? 0 : 4) || _mapleLoadMask!=(PointerLoadMask==0 ? 0 : 8))
            throw policy.Invalid(3,"source root, remembered-companion and Maple load masks");
    }

    internal void InitializeLoadFlags(OracleRuntimeState memory)
    {
        if (PointerLoadMask!=0) memory.SetField("wcc05",_initialLoadFlags);
    }

    internal bool RememberedCompanionAllowed(OracleRuntimeState memory) =>
        _rememberedCompanionLoadMask==0 || (memory.ReadField("wcc05")&_rememberedCompanionLoadMask)!=0;
    internal bool MapleAllowed(OracleRuntimeState memory) =>
        _mapleLoadMask==0 || (memory.ReadField("wcc05")&_mapleLoadMask)!=0;
    internal bool RootAllowed(OracleRuntimeState memory) =>
        _rootLoadMask==0 || (memory.ReadField("wcc05")&_rootLoadMask)!=0;

    internal void CompleteRoomInitialization(OracleRuntimeState memory)
    {
        if (_restoreLoadMask!=0 && memory.ReadField("wScrollMode")==1 &&
            (memory.ReadField("wcc05")&_restoreLoadMask)!=0)
            memory.SetField("wcc05",_initialLoadFlags);
    }

    internal int Root(int group,int room) => _roots.TryGetValue(group*256+room,out int address) ? address :
        throw new NotSupportedException($"Native object root ${group:x2}:${room:x2} is not imported.");
    internal int Entry(string label) => _labels.TryGetValue(label,out int address) ? address :
        throw new NotSupportedException($"Native object entry {label} is not imported.");
    internal int LegacyPlacementAddress(int group,int room,int order) =>
        _legacyPlacements.TryGetValue((group,room,order),out int address) ? address :
        throw new NotSupportedException($"Room ${group:x1}:${room:x2} placement[{order}]: missing native source binding.");
    internal NativeObjectInstruction At(int address,string source) => _instructions.TryGetValue(address,out var instruction) ? instruction :
        throw new NotSupportedException($"{source}: native object address ${address:x4} has no imported instruction.");
}
