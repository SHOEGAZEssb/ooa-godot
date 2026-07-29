using Godot;
using System;

namespace oracleofages;

/// <summary>
/// Native rendering, collision, and direction state for
/// INTERAC_TOILET_HAND $5b:$00 in room 2:3e.
/// </summary>
internal sealed partial class ToiletHandCharacter : NpcCharacter
{
    private ToiletHandEventRecord _metadata;

    internal int Direction { get; private set; }
    internal bool HasVisiblePriority { get; private set; }
    internal int NativeCollisionRadiusY => _metadata.CollisionRadiusY;
    internal int NativeCollisionRadiusX => _metadata.CollisionRadiusX;

    internal void InitializeToiletHand(
        NpcRecord record,
        ToiletHandEventRecord metadata)
    {
        if (record is not
            {
                Group: 2,
                Room: 0x3e,
                Id: 0x5b,
                SubId: 0x00,
                Var03: 0x00
            })
        {
            throw new InvalidOperationException(
                "ToiletHandCharacter requires room 2:3e " +
                "INTERAC_TOILET_HAND $5b:$00 var03=$00.");
        }

        _metadata = metadata;
        Initialize(record);
        SetCollisionRadii(
            metadata.CollisionRadiusY,
            metadata.CollisionRadiusX);
        SetScriptAnimation(metadata.Animation(0));
        SetAnimationRate(0.0f);
        Direction = 0;
        HasVisiblePriority = false;
    }

    internal void SetToiletAnimation(
        int animation,
        string encodedAnimation)
    {
        if (encodedAnimation != _metadata.Animation(animation))
        {
            throw new InvalidOperationException(
                $"Toilet Hand animation ${animation:x2} diverged from " +
                "its imported metadata.");
        }

        Direction = animation;
        SetScriptAnimation(encodedAnimation);
    }

    internal void UpdateVisibleState(Player player, int animationUpdates)
    {
        if (!ScriptVisible)
            return;

        AdvanceAnimationUpdates(animationUpdates);
        PreventPlayerPassing(
            player,
            _metadata.CollisionRadiusY,
            _metadata.CollisionRadiusX);
        UpdateDrawPriority(player.Position);
        HasVisiblePriority = true;
    }
}

internal sealed class ToiletHandRoomEntity(ToiletHandCharacter toiletHand)
    : RoomEntityAdapter<ToiletHandCharacter>(
        toiletHand, toiletHand.SetTransitionDrawOffset),
        IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity
{
    public NpcCharacter Npc => Entity;

    public bool BlocksLink(Vector2 linkCenter) =>
        Entity.ScriptVisible && Entity.BlocksLinkCenter(linkCenter);

    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanScriptTalkTo(
            player,
            Entity.NativeCollisionRadiusY,
            Entity.NativeCollisionRadiusX,
            NpcCharacter.AButtonPointOffset)
            ? Entity
            : null;
}
