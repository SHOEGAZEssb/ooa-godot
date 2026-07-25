using Godot;

namespace oracleofages;

/// <summary>
/// INTERAC_COMEDIAN's horizontal-only animation selector. The script owns the
/// moustache animation bank while the native state-1 handler owns direction.
/// </summary>
internal sealed partial class ComedianCharacter : NpcCharacter
{
    private ComedianEventRecord _comedian;
    private bool _mustacheEnabled;

    internal bool MustacheEnabled => _mustacheEnabled;

    internal void InitializeComedian(
        NpcRecord npcRecord,
        ComedianEventRecord comedian)
    {
        Initialize(npcRecord);
        _comedian = comedian;
        SetAnimationRate(0.0f);
    }

    internal void SetMustacheEnabled(bool enabled) =>
        _mustacheEnabled = enabled;

    internal void AdvanceInitialUpdate(Player player)
    {
        AdvanceAnimationUpdates(1);
        PreventPlayerPassing(player);
        UpdateDrawPriority(player.Position);
    }

    internal void UpdateComedian(Player player)
    {
        Vector2 link = OracleObjectMath.ToPixelPosition(player.Position);
        Vector2 position = OracleObjectMath.ToPixelPosition(Position);
        int direction = link.X >= position.X ? 1 : 0;
        int animation = (_mustacheEnabled ? 4 : 0) + direction;
        string encodedAnimation = _comedian.Animation(animation);
        if (CurrentScriptAnimationSource != encodedAnimation)
            SetScriptAnimation(encodedAnimation);

        AdvanceAnimationUpdates(1);
        PreventPlayerPassing(player);
        UpdateDrawPriority(player.Position);
    }
}

internal sealed class ComedianRoomEntity(ComedianCharacter comedian)
    : RoomEntityAdapter<ComedianCharacter>(
        comedian, comedian.SetTransitionDrawOffset),
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
