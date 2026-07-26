using Godot;

namespace oracleofages;

/// <summary>
/// INTERAC_POE's native state-1 animation, facing, collision, and priority
/// wrapper. poeScript owns its movement and disappearance bytes.
/// </summary>
internal sealed partial class PoeCharacter : NpcCharacter
{
    internal bool Disappearing { get; private set; }
    internal bool NoFace { get; private set; }

    internal void InitializePoe(NpcRecord record, PoeEventRecord poe)
    {
        Initialize(record with
        {
            CanFace = true,
            UpAnimation = poe.Animation(0),
            RightAnimation = poe.Animation(1),
            DownAnimation = poe.Animation(2),
            LeftAnimation = poe.Animation(3)
        });
        SetAnimationRate(0.0f);
    }

    internal void SetDisappearing(bool disappearing) =>
        Disappearing = disappearing;

    internal void SetNoFace(bool noFace) => NoFace = noFace;

    internal void UpdatePoe(Player player)
    {
        if (Disappearing)
            return;
        if (NoFace)
            AnimateAndUpdateDrawPriorityOneUpdate(player);
        else
            FaceLinkAndAnimateOneUpdate(player);
    }
}

internal sealed class PoeRoomEntity(PoeCharacter poe)
    : RoomEntityAdapter<PoeCharacter>(poe, poe.SetTransitionDrawOffset),
        IRoomBlocker, ITalkTarget
{
    public bool BlocksLink(Vector2 linkCenter) =>
        !Entity.Disappearing && !Entity.NoFace &&
        Entity.BlocksLinkCenter(linkCenter);

    public NpcCharacter? FindTalkTarget(Player player) =>
        !Entity.Disappearing && !Entity.NoFace &&
        Entity.CanScriptTalkTo(
            player,
            NpcCharacter.CollisionRadius,
            NpcCharacter.CollisionRadius,
            NpcCharacter.AButtonPointOffset)
            ? Entity
            : null;
}
