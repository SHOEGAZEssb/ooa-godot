using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom29eOrbBridge()
    {
        CompareOrbBridgeRom();
        CompareOrbBridgeAllocationRom();
        CompareOrbBridgeScrollRom();
        GD.Print("Compared native room$2:$9e orb contact, bridge allocation/retry, shared tile queue, counters, text/scroll gates, completion, cancellation, repeat and reconstruction.");
    }
}
