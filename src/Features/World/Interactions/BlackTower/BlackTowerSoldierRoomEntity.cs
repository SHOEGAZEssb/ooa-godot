using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_SOLDIER $40:$0c.</summary>
internal sealed class BlackTowerSoldierRoomEntity : BlackTowerNpcRoomEntity,
    IFixedRoomEntity, INpcTalkLifecycle
{
    private static readonly int[] TextTable = { 0x590d, 0x590e, 0x590f, 0x590d };
    private readonly BlackTowerWorkerDatabase _data;
    private readonly OracleRandom _random;

    internal BlackTowerSoldierRoomEntity(
        NpcCharacter npc,
        BlackTowerWorkerDatabase data,
        OracleRandom random)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        _data = data;
        _random = random;
        npc.SetDirectionalAnimations(
            data.Visual("soldier-0").Animation,
            data.Visual("soldier-1").Animation,
            data.Visual("soldier-2").Animation,
            data.Visual("soldier-3").Animation);
        npc.SetDialogue(0x590d, data.Text(0x590d), canFace: true);
    }

    public NpcCharacter TalkNpc => Entity;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.Active)
            return;
        Entity.FaceToward(frame.Player.Position);
        Entity.AdvanceAnimationUpdates(1);
        Entity.PreventPlayerPassing(frame.Player);
        Entity.UpdateDrawPriority(frame.Player.Position);
    }

    public void OnNpcTalkStarted()
    {
        int textId = TextTable[_random.Next().Value & 0x03];
        Entity.SetDialogue(textId, _data.Text(textId), canFace: true);
    }

    public void OnNpcTalkEnded() { }
}
