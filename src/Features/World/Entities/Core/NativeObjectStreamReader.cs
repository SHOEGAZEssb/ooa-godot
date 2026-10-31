using System;
using System.Collections.Generic;

namespace Oracles;

// Source: objectLoading.s. This owns control flow, not object construction.
internal sealed class NativeObjectStreamReader(NativeObjectStreamDatabase data, OracleRuntimeState memory, Func<byte> roomFlags)
{
    private NativeObjectInstruction? _current;
    private bool _skipRun;

    internal void AllocationFailed()
    {
        if (_current?.Opcode is 1 or 2 or 7 or 10) _skipRun=true;
    }

    internal IEnumerable<NativeObjectInstruction> Read(int entry)
    {
        var returns=new Stack<(int Return,int Entry)>();
        var activeEntries=new HashSet<int> {entry};
        int currentEntry=entry, address=entry;
        string source=$"object entry ${entry:x4}";
        while (true)
        {
            var instruction=data.At(address,source); _current=instruction; source=instruction.Source;
            address=instruction.Next;
            switch (instruction.Opcode)
            {
                case 0:
                    int modifier=memory.ReadField("wRoomStateModifier");
                    if (modifier>=8) throw new NotSupportedException($"{source}: objectDataOp0 uses unsupported room-state bit ${modifier:x2}.");
                    if ((instruction.Mask & (1<<modifier))!=0) break;
                    while (true)
                    {
                        var skipped=data.At(address,source);
                        if (skipped.Header && skipped.Opcode is 0 or 0xfe or 0xff) break;
                        if (skipped.Opcode==0xfd) throw new NotSupportedException($"{skipped.Source}: conditional skip reached native garbage data.");
                        address=skipped.Next;
                    }
                    break;
                case 3 or 4 or 5:
                    if (!PointerAllowed()) break;
                    if (instruction.Opcode==4 && (roomFlags() & 0x80)!=0 || instruction.Opcode==5 && (roomFlags() & 0x80)==0) break;
                    if (!activeEntries.Add(instruction.Target)) throw new NotSupportedException($"{source}: recursive native object pointer ${instruction.Target:x4}.");
                    returns.Push((address,currentEntry)); currentEntry=instruction.Target; address=instruction.Target;
                    break;
                case 0xfe:
                    if (returns.Count==0) throw new NotSupportedException($"{source}: obj_EndPointer has no native return pointer.");
                    activeEntries.Remove(currentEntry);
                    (address,currentEntry)=returns.Pop();
                    break;
                case 0xff:
                    if (returns.Count!=0) throw new NotSupportedException($"{source}: obj_End would return through a native object pointer.");
                    yield break;
                case 0xfd:
                    throw new NotSupportedException($"{source}: native garbage object data is unsupported.");
                default:
                    _skipRun=false;
                    yield return instruction;
                    if (!_skipRun) break;
                    while (true)
                    {
                        var skipped=data.At(address,source);
                        if (skipped.Header) break;
                        if (skipped.Opcode==0xfd) throw new NotSupportedException($"{skipped.Source}: allocation skip reached native garbage data.");
                        address=skipped.Next;
                    }
                    break;
            }
        }
    }

    private bool PointerAllowed()
    {
        if (data.PointerLoadMask!=0 && (memory.ReadField("wcc05") & data.PointerLoadMask)==0) return false;
        int gate=memory.ReadField("wcc85");
        if ((gate&0x80)==0) return gate==0;
        memory.SetField("wcc85",0);
        return memory.ReadField("wScreenTransitionDirection")!=(gate&3);
    }
}
