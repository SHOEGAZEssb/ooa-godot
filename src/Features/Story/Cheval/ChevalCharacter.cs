using Godot;

namespace oracleofages;

/// <summary>
/// INTERAC_CHEVAL's script-owned animation and collision wrapper.
/// </summary>
internal sealed partial class ChevalCharacter : NpcCharacter
{
    internal void InitializeCheval(
        NpcRecord record,
        ChevalEventRecord cheval)
    {
        Initialize(record);
        if (record.SpriteName != cheval.Sprite ||
            record.TileBase != cheval.TileBase ||
            record.Palette != cheval.Palette ||
            record.DefaultAnimation != cheval.InitialAnimation ||
            record.UpAnimation != cheval.Animation0)
        {
            throw new System.InvalidOperationException(
                "INTERAC_CHEVAL placed NPC metadata diverges from its imported native event.");
        }

        // interactionInitGraphics selects animation $00. Cheval never invokes
        // a facing helper; interactionAnimateAsNpc only advances that animation.
        SetFacingDirection(Vector2I.Up);
        SetAnimationRate(0.0f);
    }

    internal void AdvanceCheval(Player player) => AnimateAsNpcOneUpdate(player);
}

internal sealed class ChevalRoomEntity(
    ChevalCharacter cheval,
    ChevalEventRecord record)
    : NpcCharacterRoomEntityAdapter(cheval, cheval.SetTransitionDrawOffset),
        IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity
{
    public NpcCharacter Npc => Entity;

    public bool BlocksLink(Vector2 linkCenter) =>
        Entity.BlocksLinkCenter(linkCenter);

    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanScriptTalkTo(
            player,
            record.CollisionRadiusY,
            record.CollisionRadiusX,
            NpcCharacter.AButtonPointOffset)
            ? Entity
            : null;
}
