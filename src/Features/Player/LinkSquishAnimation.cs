using System.Collections.Generic;

namespace oracleofages;

// LINK_STATE_SQUISHED's animation and visible-update countdown. Link owns
// request consumption, item cancellation, collision state and respawn.
internal sealed class LinkSquishAnimation(bool vertical)
{
    private readonly IReadOnlyList<LinkSquishFrame> _frames = LinkSquishDatabase.Shared.Frames(vertical);
    internal int State { get; private set; }
    internal int Frame { get; private set; }
    internal int AnimationCounter { get; private set; }
    internal int FlickerCounter { get; private set; }
    internal bool Visible { get; private set; } = true;
    internal bool Finished { get; private set; }
    internal LinkSquishFrame Current => _frames[Frame];
    internal void Advance(int frameCounter)
    {
        if (Finished) return;
        if (State == 0)
        {
            State = 1;
            AnimationCounter = Current.Duration;
        }
        if (State == 1)
        {
            Animate();
            if (Current.Parameter != 0xff) return;
            State = 2;
            FlickerCounter = LinkSquishDatabase.Shared.FlickerCount;
        }
        // The terminal update reaches BOTH specialObjectAnimate calls.
        Animate();
        Visible = (frameCounter & 1) == 0;
        if (Visible && --FlickerCounter == 0) Finished = true;
    }
    private void Animate()
    {
        if (--AnimationCounter != 0) return;
        Frame = Current.Next;
        AnimationCounter = Current.Duration;
    }
}
