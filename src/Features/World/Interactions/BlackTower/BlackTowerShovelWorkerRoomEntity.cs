using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_HARDHAT_WORKER $58:$00.</summary>
internal sealed class BlackTowerShovelWorkerRoomEntity : BlackTowerNpcRoomEntity,
    IFixedRoomEntity, INpcTalkLifecycle
{
    private readonly string _workAnimation;

    internal BlackTowerShovelWorkerRoomEntity(
        NpcCharacter npc,
        BlackTowerWorkerDatabase data)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        _workAnimation = data.Visual("hardhat-work").Animation;
        npc.SetDirectionalAnimations(
            data.Visual("hardhat-0").Animation,
            data.Visual("hardhat-1").Animation,
            data.Visual("hardhat-2").Animation,
            data.Visual("hardhat-3").Animation);
        npc.SetDialogue(0x1000, data.Text(0x1000), canFace: true);
        npc.SetScriptAnimation(_workAnimation);
    }

    public NpcCharacter TalkNpc => Entity;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.Active)
            return;
        Entity.AdvanceAnimationUpdates(1);
        Entity.PreventPlayerPassing(frame.Player);
        Entity.UpdateDrawPriority(frame.Player.Position);
    }

    public void OnNpcTalkStarted() { }
    public void OnNpcTalkEnded() => Entity.SetScriptAnimation(_workAnimation);
}
