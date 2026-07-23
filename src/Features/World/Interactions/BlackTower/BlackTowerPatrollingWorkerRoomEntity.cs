using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_HARDHAT_WORKER $58:$03 patrol script.</summary>
internal sealed class BlackTowerPatrollingWorkerRoomEntity : BlackTowerNpcRoomEntity,
    IFixedRoomEntity, INpcTalkLifecycle, IPlayerRestriction
{
    private readonly BlackTowerWorkerDatabase _data;
    private readonly PatrolLeg[] _patrol;
    private Vector2 _precisePosition;
    private int _leg;
    private int _counter;
    private int _wait;
    private int _postTalkWait;
    private int _direction;
    private bool _talking;

    internal BlackTowerPatrollingWorkerRoomEntity(
        NpcCharacter npc,
        BlackTowerWorkerDatabase data,
        OracleRandom random)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        _data = data;
        _patrol = data.Patrol(npc.Record.Var03);
        _precisePosition = npc.Position;
        npc.SetDirectionalAnimations(
            data.Visual("hardhat-0").Animation,
            data.Visual("hardhat-1").Animation,
            data.Visual("hardhat-2").Animation,
            data.Visual("hardhat-3").Animation);
        int textIndex = npc.Record.Var03 == 4 ? 4 : random.Next().Value & 0x03;
        int[] texts = { 0x100a, 0x100b, 0x100c, 0x100c, 0x100d };
        int textId = texts[textIndex];
        npc.SetDialogue(textId, data.Text(textId), canFace: true);
        StartLeg(0);
    }

    internal Vector2 PrecisePosition => _precisePosition;
    internal int PatrolLegIndex => _leg;
    internal int PatrolCounter => _counter;
    internal int PatrolWaitCounter => _wait;
    internal int Direction => _direction;
    public NpcCharacter TalkNpc => Entity;
    public bool DisablesSword => _talking || _postTalkWait > 0;
    public bool DisablesMovement => DisablesSword;

    public override NpcCharacter? FindTalkTarget(Player player) =>
        !_talking && _postTalkWait == 0 && _wait == 0 &&
        base.FindTalkTarget(player) is { } npc
            ? npc
            : null;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.Active)
            return;
        Entity.AdvanceAnimationUpdates(1);

        if (_talking)
        {
            FinishNpcUpdate(frame.Player);
            return;
        }
        if (_postTalkWait > 0)
        {
            _postTalkWait--;
            if (_postTalkWait == 0)
                Entity.SetFacingDirection(DirectionVector(_direction));
            FinishNpcUpdate(frame.Player);
            return;
        }
        if (_wait > 0)
        {
            _wait--;
            if (_wait == 0)
                StartLeg((_leg + 1) % _patrol.Length);
            FinishNpcUpdate(frame.Player);
            return;
        }

        _counter--;
        if (_counter == 0)
        {
            _wait = _data.PatrolWait;
            FinishNpcUpdate(frame.Player);
            return;
        }
        _precisePosition += (Vector2)DirectionVector(_direction) *
            (_data.Speed80 / 40.0f);
        Entity.SetStatePosition(OracleObjectMath.ToPixelPosition(_precisePosition));
        FinishNpcUpdate(frame.Player);
    }

    public void OnNpcTalkStarted() => _talking = true;

    public void OnNpcTalkEnded()
    {
        if (!_talking)
            return;
        _talking = false;
        _postTalkWait = _data.TalkWait;
    }

    private void StartLeg(int index)
    {
        _leg = index;
        _direction = _patrol[index].Direction;
        _counter = _patrol[index].Counter;
        Entity.SetFacingDirection(DirectionVector(_direction));
    }

    private void FinishNpcUpdate(Player player)
    {
        Entity.PreventPlayerPassing(player);
        Entity.UpdateDrawPriority(player.Position);
    }
}
