using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;
internal sealed class NayruAudienceTalkState(string actor, int resetAnimation, int resetDelay, bool hopping)
{
    public string Actor { get; } = actor;
    public int ResetAnimation { get; } = resetAnimation;
    public int ResetDelay { get; } = resetDelay;
    public bool Hopping { get; } = hopping;
    public bool WaitingForText { get; set; } = true;
    public int Counter { get; set; }

    public int ZFixed;
    public int SpeedZ = hopping ? -0xc0 : 0;
}
