using System.Collections.Generic;

namespace oracleofages;

// otherSwordsParent.s state1 tests bit7 before advancing. Its final pose
// lasts one update; a caller freeze retains both the counter and parameter.
internal sealed class BiggoronSwordParentAnimation
{
    private readonly IReadOnlyList<BiggoronSwordParentFrame> _frames;
    internal int Frame { get; private set; }
    internal int Counter { get; private set; }
    internal bool Active { get; private set; }=true;
    internal int Mode { get; }
    internal int Parameter { get; private set; }
    internal BiggoronSwordParentAnimation(BiggoronSwordDatabase data,bool mounted,bool raft)
    {
        Mode=mounted&&!raft?0x27:0x23;
        _frames=data.Frames(Mode); Counter=_frames[0].Duration; Parameter=_frames[0].Parameter;
    }
    internal void Update()
    {
        if(!Active) return;
        if((Parameter&0x80)!=0) { Active=false; return; }
        if(--Counter==0) { Frame++; Counter=_frames[Frame].Duration; Parameter=_frames[Frame].Parameter; }
    }
    // cpRelatedObject1ID leaves H on the parent. The unconditional weapon
    // post pass clears its bit6 tile probe, including during caller freezes.
    internal bool ConsumeTileProbe()
    {
        bool probe=(Parameter&0x40)!=0;
        Parameter&=~0x40;
        return probe;
    }
    internal void Cancel() => Active=false;
}
