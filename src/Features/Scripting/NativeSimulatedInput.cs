using System;
using System.Collections.Generic;

namespace Oracles;

// bank0.s:setSimulatedInputAddress/getSimulatedInput. The live union stays in
// WRAM; this owner retains only the immutable program and its cursor.
internal sealed class NativeSimulatedInput(OracleRuntimeState runtime,Action clearPegasus)
{
    private IReadOnlyList<NativeSimulatedInputEntry>? _program;
    private int _index;
    internal bool Enabled=>runtime.ReadWramByte(0xcbc3)!=0;
    internal void Install(IReadOnlyList<NativeSimulatedInputEntry> program)
    {
        _program=program;_index=0;Counter(0);runtime.SetWramByte(0xcbc3,1);clearPegasus();
    }
    internal void SetEnabled(int value)=>runtime.SetWramByte(0xcbc3,(byte)value);
    private int CounterValue=>runtime.ReadWramByte(0xcbc4)|(runtime.ReadWramByte(0xcbc5)<<8);
    private void Counter(int value){runtime.SetWramByte(0xcbc4,(byte)value);runtime.SetWramByte(0xcbc5,(byte)(value>>8));}
    internal int Sample(int paletteMode)
    {
        // The source returns A=palette mode without touching its counter.
        if(paletteMode!=0)return paletteMode;
        if((runtime.ReadWramByte(0xcbc3)&128)==0)
        {
            int count=Math.Max(0,CounterValue-1);Counter(count);
            if(count==0)
            {
                if(_program is null||_index>=_program.Count)throw new NotSupportedException("bank0.s:getSimulatedInput ran beyond its imported native stream.");
                var entry=_program[_index++];Counter(entry.Frames);
                if((entry.Frames&0x8000)!=0)runtime.SetWramByte(0xcbc3,255);
                else runtime.SetWramByte(0xcbc9,entry.Keys);
            }
        }
        return runtime.ReadWramByte(0xcbc9);
    }
}

internal readonly record struct NativeSimulatedInputEntry(int Frames,byte Keys);
