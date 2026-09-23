using System.Collections.Generic;

namespace oracleofages;

// parentItemCode_caneOfSomaria state1 checks bit7 BEFORE advancing the
// animation. Its terminal pose remains observable for one full update.
internal sealed class SomariaParentAnimation
{
    private readonly IReadOnlyList<SomariaParentFrame> _frames;
    private int _index, _counter;
    internal bool Active { get; private set; } = true;
    internal int Mode { get; }
    internal int Parameter => _frames[_index].Parameter;
    internal int Graphic => _frames[_index].Graphic;
    internal int Frame => _index;
    internal SomariaParentAnimation(SomariaSwingDatabase data,bool underwater,bool mounted,bool raft)
    {
        Mode=raft?0x22:underwater?0x2d:mounted?0x26:0x22;
        _frames=data.Frames(Mode); _counter=_frames[0].Duration;
    }
    internal void Update()
    {
        if(!Active) return;
        if((Parameter&0x80)!=0) { Active=false; return; }
        if(--_counter==0) { _index++; _counter=_frames[_index].Duration; }
    }
    internal void Cancel() => Active=false;
}
