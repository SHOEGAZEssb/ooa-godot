using Godot;

namespace Oracles;

// Controlled related-object bytes isolate PART$12 from species AI and collision admission.
internal sealed class BurningEnemyValidationTarget : IBurningEnemyTarget
{
    public bool BurnTargetAlive { get; set; } = true;
    public int BurnTargetId { get; set; } = 0x33;
    public Vector2 BurnPosition { get; } = new(88,48);
    public int BurnHealth { get; set; } = 5;
    public int BurnZFixed { get; set; } = -0x12b;
    internal int ReleaseCalls { get; private set; }
    public void ReleaseBurn() => ReleaseCalls++;
}
