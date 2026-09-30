using Godot;

namespace oracleofages;

// An occupied native PART slot without behavior; used only to test allocation
// failure while the actual enemy and gameplay owners continue updating.
internal sealed class EnemyAiSlotReservation : IRoomEntity
{
    public Node2D Node { get; } = new();
    public void SetTransitionDrawOffset(Vector2 offset) { }
}
