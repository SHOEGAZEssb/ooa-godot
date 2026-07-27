using Godot;

namespace oracleofages;

/// <summary>
/// Visible INTERAC_SHOOTING_GALLERY $30:$00 host. Its native handler animates
/// as an NPC every update while the imported script owns talk sensitivity.
/// </summary>
internal sealed partial class ShootingGalleryCharacter : NpcCharacter
{
    internal void InitializeShootingGallery(NpcRecord record)
    {
        Initialize(record);
        SetAnimationRate(1.0f);
    }

    internal void UpdateShootingGallery(Player player)
    {
        AdvanceAnimationUpdates(1);
        PreventPlayerPassing(player);
        UpdateDrawPriority(player.Position);
    }
}

internal sealed class ShootingGalleryNpcRoomEntity(
    ShootingGalleryCharacter keeper)
    : RoomEntityAdapter<ShootingGalleryCharacter>(
        keeper, keeper.SetTransitionDrawOffset),
        IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity
{
    public NpcCharacter Npc => Entity;

    public bool BlocksLink(Vector2 linkCenter) =>
        Entity.BlocksLinkCenter(linkCenter);

    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanTalkTo(player) ? Entity : null;
}
