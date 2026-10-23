using Godot;

namespace oracleofages;

/// <summary>
/// INTERAC_RAFTON's script-owned directional animation and collision wrapper.
/// </summary>
internal sealed partial class RaftonCharacter : NpcCharacter
{
    internal void InitializeRafton(
        NpcRecord record,
        RaftonEventRecord rafton)
    {
        Initialize(record);
        SetDirectionalAnimations(
            rafton.Animation0,
            rafton.Animation1,
            rafton.Animation2,
            rafton.Animation3);
        SetFacingDirection(Vector2I.Down);
        SetAnimationRate(0.0f);
        if (record.DefaultAnimation != rafton.InitialAnimation)
        {
            throw new System.InvalidOperationException(
                $"INTERAC_RAFTON default animation ${record.DefaultAnimation:x2} " +
                $"does not match imported animation ${rafton.InitialAnimation:x2}.");
        }
    }

    internal void FaceLink(Player player)
    {
        Vector2 difference =
            OracleObjectMath.ToPixelPosition(player.Position) -
            OracleObjectMath.ToPixelPosition(Position);
        Vector2I direction = Mathf.Abs(difference.X) > Mathf.Abs(difference.Y)
            ? difference.X >= 0 ? Vector2I.Right : Vector2I.Left
            : difference.Y >= 0 ? Vector2I.Down : Vector2I.Up;
        SetFacingDirection(direction);
    }

    internal void SetDirection(int animation)
    {
        SetFacingDirection(animation switch
        {
            0 => Vector2I.Up,
            1 => Vector2I.Right,
            2 => Vector2I.Down,
            3 => Vector2I.Left,
            _ => throw new System.ArgumentOutOfRangeException(nameof(animation))
        });
    }

    internal void AdvanceAsNpc(Player player) => AnimateAsNpcOneUpdate(player);

    // Behaviour4 only calls interactionAnimateBasedOnSpeed; visiblec2 from
    // state zero survives. Other behaviours use interactionAnimateAsNpc.
    internal void AdvanceDeparture(int animationUpdates) =>
        AdvanceAnimationUpdates(animationUpdates);
}
