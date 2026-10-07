using Godot;

namespace oracleofages;

// An occupied native object slot without behavior; other gameplay owners
// continue updating. A supplied actor can expose a declared live parent field.
internal sealed class EnemyAiSlotReservation : IRoomEntity
{
    public Node2D Node { get; }
    internal EnemyAiSlotReservation(Node2D? node = null) => Node = node ?? new Node2D();
    public void SetTransitionDrawOffset(Vector2 offset) { }
}
