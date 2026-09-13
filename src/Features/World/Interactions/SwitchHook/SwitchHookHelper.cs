using Godot;

namespace oracleofages;

/// <summary>The invisible reserved $de item retains camera focus and both XYZ snapshots.</summary>
internal sealed class SwitchHookHelper(Vector2 position)
{
    internal Vector2 Position { get; } = position.Floor();
    internal Vector2 HookPosition { get; set; } = position.Floor();
    internal Vector2 LinkPosition { get; private set; }
    internal int LinkZLow { get; private set; }
    internal bool Initialized { get; private set; }
    internal void Initialize(Player player)
    {
        LinkPosition = player.PrecisePosition;
        LinkZLow = player.SwitchHookZFixed & 0xff;
        Initialized = true;
    }
    internal void Swap()
    {
        (HookPosition, LinkPosition) = (LinkPosition, HookPosition);
        // The hook/helper snapshot begins at Z=$0000; only Link's Z low byte
        // survives the lift until the six-byte swap replaces it with this zero.
        LinkZLow = 0;
    }
}
