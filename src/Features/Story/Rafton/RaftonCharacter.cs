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

    internal void AdvanceDeparture(Player player, int animationUpdates)
    {
        AdvanceAnimationUpdates(animationUpdates);
        UpdateDrawPriority(player.Position);
    }
}

internal sealed class RaftonRoomEntity(RaftonCharacter rafton)
    : NpcCharacterRoomEntityAdapter(rafton, rafton.SetTransitionDrawOffset),
        IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity
{
    public NpcCharacter Npc => Entity;

    public bool BlocksLink(Vector2 linkCenter) =>
        Entity.BlocksLinkCenter(linkCenter);

    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanScriptTalkTo(
            player,
            NpcCharacter.CollisionRadius,
            NpcCharacter.CollisionRadius,
            NpcCharacter.AButtonPointOffset)
            ? Entity
            : null;
}
