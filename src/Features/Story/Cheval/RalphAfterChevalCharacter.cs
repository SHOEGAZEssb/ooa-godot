using Godot;

namespace oracleofages;

/// <summary>
/// INTERAC_RALPH $37:$10's script-owned directional animation wrapper.
/// </summary>
internal sealed partial class RalphAfterChevalCharacter : NpcCharacter
{
    internal void InitializeRalph(
        NpcRecord record,
        RalphAfterChevalEventRecord source)
    {
        Initialize(record);
        if (record.SpriteName != source.Sprite ||
            record.TileBase != source.TileBase ||
            record.Palette != source.Palette ||
            record.DefaultAnimation != source.InitialAnimation ||
            record.UpAnimation != source.Animation0 ||
            record.RightAnimation != source.Animation1 ||
            record.DownAnimation != source.Animation2 ||
            record.LeftAnimation != source.Animation3)
        {
            throw new System.InvalidOperationException(
                "INTERAC_RALPH $37:$10 placed metadata diverges from its native event.");
        }

        SetFacingDirection(Vector2I.Down);
        SetAnimationRate(0.0f);
    }

    internal void SetDirection(int direction)
    {
        SetFacingDirection(direction switch
        {
            0 => Vector2I.Up,
            1 => Vector2I.Right,
            2 => Vector2I.Down,
            3 => Vector2I.Left,
            _ => throw new System.ArgumentOutOfRangeException(nameof(direction))
        });
    }

    internal void AdvanceRalphAnimation(int updates) =>
        AdvanceAnimationUpdates(updates);
}

internal sealed class RalphAfterChevalRoomEntity(
    RalphAfterChevalCharacter ralph)
    : NpcCharacterRoomEntityAdapter(ralph, ralph.SetTransitionDrawOffset),
        IRoomBlocker,
        IOrdinaryNpcEntity
{
    public NpcCharacter Npc => Entity;

    public bool BlocksLink(Vector2 linkCenter) =>
        Entity.BlocksLinkCenter(linkCenter);
}
