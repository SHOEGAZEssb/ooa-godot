using Godot;

namespace oracleofages;

// Declared $ccae/$ccad publications isolate INTERAC$14's permission gate.
// The actual cube movement, sensor and flame owners are separate regressions.
internal sealed class DeclaredColoredCubeState : RoomEntityAdapter<Node2D>, IColoredCubePuzzleStateSource
{
    internal DeclaredColoredCubeState(OracleRuntimeState runtimeState) : base(new Node2D(), _ => { })
        => ColoredCubePuzzleState = new(runtimeState,0x2c);
    public ColoredCubePuzzleState ColoredCubePuzzleState { get; }
}
