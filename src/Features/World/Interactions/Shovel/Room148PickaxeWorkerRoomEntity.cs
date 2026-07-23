using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Native fixed-update owner for INTERAC_PICKAXE_WORKER $57:$00. The original
/// animation parameter is nonzero only on a strike update and directly owns
/// both SND_CLINK and creation of the two dirt-chip interactions.
/// </summary>
internal sealed class Room148PickaxeWorkerRoomEntity(
    NpcCharacter worker,
    PickaxeRecord record,
    Action<int> playSound)
    : RoomEntityAdapter<NpcCharacter>(worker, worker.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomBlocker, ITalkTarget, INpcTalkLifecycle
{
    private bool _talking;

    public NpcCharacter TalkNpc => Entity;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.Active)
            return;

        // interactionAnimateAsNpc runs before the worker checks animParameter.
        Entity.AdvanceAnimationUpdates(1);
        Entity.PreventPlayerPassing(frame.Player);
        Entity.UpdateDrawPriority(frame.Player.Position);

        int parameter = Entity.CurrentAnimationParameter;
        if (parameter == 0)
            return;
        if (parameter is not 1 and not 2)
            throw new InvalidOperationException(
                $"Pickaxe worker $57:$00 produced unsupported animation parameter ${parameter:x2}.");

        playSound(record.Sound);
        float x = Entity.Position.X +
            (parameter == 1 ? -record.OffsetX : record.OffsetX);
        Vector2 position = new(x, Entity.Position.Y + record.OffsetY);
        int[] angles = { record.Angle0, record.Angle1 };
        for (int index = record.DebrisCount - 1; index >= 0; index--)
        {
            spawns.Add(new Room148DebrisSpawn(
                position,
                parameter,
                angles[index],
                // The child copies the worker's visible priority and
                // decrements its low priority code during $92:$06 state 0.
                Math.Max(0, Entity.ZIndex - 1)));
        }
    }

    public bool BlocksLink(Vector2 linkCenter) =>
        Entity.BlocksLinkCenter(linkCenter);

    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanTalkTo(player) ? Entity : null;

    public void OnNpcTalkStarted()
    {
        if (_talking)
            return;
        _talking = true;
        Entity.SetScriptAnimation(record.TalkAnimation);
    }

    public void OnNpcTalkEnded()
    {
        if (!_talking)
            return;
        _talking = false;
        Entity.SetScriptAnimation(record.WorkAnimation);
        // The script jumps back to animation $02 and then reaches
        // interactionAnimateAsNpc later on this same update.
        Entity.AdvanceAnimationUpdates(1);
    }
}

internal sealed record Room148DebrisSpawn(
    Vector2 Position,
    int Palette,
    int Angle,
    int DrawPriority)
    : RoomEntitySpawn(UpdateThisFrame: true);
