using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_PICKAXE_WORKER $57:$03.</summary>
internal sealed class BlackTowerPickaxeWorkerRoomEntity : BlackTowerNpcRoomEntity,
    IFixedRoomEntity, INpcTalkLifecycle
{
    private static readonly int[] AnimationTable = { 0, 1, 0, 1, 0, 1, 1, 1 };
    private static readonly int[] TextTable =
    {
        0x1b01, 0x1b02, 0x1b03, 0x1b04,
        0x1b05, 0x1b01, 0x1b02, 0x1b03
    };
    private readonly PickaxeRecord _strike;
    private readonly BlackTowerWorkerDatabase _data;
    private readonly OracleRandom _random;
    private readonly Action<int> _playSound;

    internal BlackTowerPickaxeWorkerRoomEntity(
        NpcCharacter npc,
        PickaxeRecord strike,
        BlackTowerWorkerDatabase data,
        OracleRandom random,
        Action<int> playSound)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        _strike = strike;
        _data = data;
        _random = random;
        _playSound = playSound;
        int animation = AnimationTable[npc.Record.Var03 & 0x07];
        npc.SetScriptAnimation(data.Visual($"pickaxe-{animation}").Animation);
        npc.SetDialogue(0x1b01, data.Text(0x1b01), canFace: false);
    }

    public NpcCharacter TalkNpc => Entity;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.Active)
            return;
        Entity.AdvanceAnimationUpdates(1);
        Entity.PreventPlayerPassing(frame.Player);
        Entity.UpdateDrawPriority(frame.Player.Position);

        int parameter = Entity.CurrentAnimationParameter;
        if (parameter == 0)
            return;
        if (parameter is not 1 and not 2)
            throw new InvalidOperationException(
                $"Pickaxe worker $57:$03 produced animation parameter ${parameter:x2}.");

        _playSound(_strike.Sound);
        float x = Entity.Position.X +
            (parameter == 1 ? -_strike.OffsetX : _strike.OffsetX);
        Vector2 position = new(x, Entity.Position.Y + _strike.OffsetY);
        int[] angles = { _strike.Angle0, _strike.Angle1 };
        for (int index = _strike.DebrisCount - 1; index >= 0; index--)
        {
            spawns.Add(new Room148DebrisSpawn(
                position, parameter, angles[index],
                Math.Max(0, Entity.ZIndex - 1)));
        }
    }

    public void OnNpcTalkStarted()
    {
        int textId = TextTable[_random.Next().Value & 0x07];
        Entity.SetDialogue(textId, _data.Text(textId), canFace: false);
    }

    public void OnNpcTalkEnded() { }
}
