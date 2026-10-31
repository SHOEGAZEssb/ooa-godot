namespace Oracles;

// Native publication and arrival-copy routines, without graphics or object stepping.
internal sealed class TimeWarpStateRom
{
    private readonly LinkCollisionRom _rom = new(bankedStack:true);
    internal byte PublishSource(byte flags, byte previous)
    {
        _rom[0xcc34] = flags; _rom[0xcc50] = previous;
        _rom.Call(0x7350,bank:3); // func_03_7244@state2@cbb3_01.
        if (_rom[0xd241] != 0xdd || _rom[0xd243] != _rom[0xcc50] || _rom[0xd246] != 120)
            throw new System.InvalidOperationException("Native time-warp source did not publish/copy its palette selector and counter.");
        return _rom[0xcc50];
    }
    internal byte CopyArrival(byte selector)
    {
        _rom[0xcc50] = selector;
        _rom.Call(0x4da5,bank:5); // warpTransition6@createDestinationTimewarpAnimation.
        if (_rom[0xd341] != 0xdd || _rom[0xd342] != 1)
            throw new System.InvalidOperationException("Native time-warp arrival did not allocate INTERAC $dd:$01 after the source slot.");
        return _rom[0xd343];
    }
}
