using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal abstract class BlackTowerNpcRoomEntity(
    NpcCharacter npc,
    Action<Vector2> transitionOffset)
    : RoomEntityAdapter<NpcCharacter>(npc, transitionOffset),
        IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity
{
    public NpcCharacter Npc => Entity;
    public virtual bool BlocksLink(Vector2 linkCenter) =>
        Entity.BlocksLinkCenter(linkCenter);
    public virtual NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanTalkTo(player) ? Entity : null;

    protected static Vector2I DirectionVector(int direction) => direction switch
    {
        0 => Vector2I.Up,
        1 => Vector2I.Right,
        2 => Vector2I.Down,
        3 => Vector2I.Left,
        _ => throw new ArgumentOutOfRangeException(nameof(direction))
    };
}

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
    private readonly Room148PickaxeDatabase.PickaxeRecord _strike;
    private readonly BlackTowerWorkerDatabase _data;
    private readonly OracleRandom _random;
    private readonly Action<int> _playSound;

    internal BlackTowerPickaxeWorkerRoomEntity(
        NpcCharacter npc,
        Room148PickaxeDatabase.PickaxeRecord strike,
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

/// <summary>INTERAC_HARDHAT_WORKER $58:$03 patrol script.</summary>
internal sealed class BlackTowerPatrollingWorkerRoomEntity : BlackTowerNpcRoomEntity,
    IFixedRoomEntity, INpcTalkLifecycle, IPlayerRestriction
{
    private readonly BlackTowerWorkerDatabase _data;
    private readonly BlackTowerWorkerDatabase.PatrolLeg[] _patrol;
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

/// <summary>INTERAC_MALE_VILLAGER $3a:$02 path blocker.</summary>
internal sealed class BlackTowerBlockingVillagerRoomEntity : BlackTowerNpcRoomEntity,
    IFixedRoomEntity, INpcTalkLifecycle, IPlayerRestriction
{
    private enum BlockState { Watching, Moving, Waiting }

    private readonly BlackTowerWorkerDatabase _data;
    private BlockState _state;
    private float _savedX;
    private int _savedLinkY;
    private int _movementUpdates;
    private int _wait;
    private int _talkWait;
    private bool _movingRight = true;
    private bool _talking;

    internal BlackTowerBlockingVillagerRoomEntity(
        NpcCharacter npc,
        BlackTowerWorkerDatabase data)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        _data = data;
        _savedX = npc.Position.X;
        npc.SetCollisionRadii(6, 6);
        npc.SetDialogue(0x1441, npc.Message, canFace: true);
    }

    internal bool MovingRight => _movingRight;
    internal int MovementUpdates => _movementUpdates;
    internal int SavedLinkY => _savedLinkY;
    public NpcCharacter TalkNpc => Entity;
    public bool DisablesSword => _state != BlockState.Watching;
    public bool DisablesMovement => DisablesSword;

    public override NpcCharacter? FindTalkTarget(Player player) =>
        _state == BlockState.Watching && !_talking && _talkWait == 0
            ? base.FindTalkTarget(player)
            : null;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.Active)
            return;

        bool armedThisUpdate = false;
        if (_state == BlockState.Watching)
        {
            Entity.FaceToward(frame.Player.Position);
            Entity.AdvanceAnimationUpdates(1);
            // npcFaceLinkAndAnimate falls through interactionAnimateAsNpc,
            // which separates Link before this state's open-side collision
            // check runs.
            Entity.PreventPlayerPassing(frame.Player);
            if (_talkWait > 0)
            {
                _talkWait--;
                if (_talkWait == 0)
                    Entity.SetScriptAnimation(Entity.Record.DownAnimation);
            }

            Vector2 openPosition = new(
                _savedX + (_movingRight ? 0x11 : -0x11), Entity.Position.Y);
            Vector2 delta = OracleObjectMath.ToPixelPosition(frame.Player.Position) -
                OracleObjectMath.ToPixelPosition(openPosition);
            if (Mathf.Abs(delta.X) < 3 + NpcCharacter.LinkCollisionRadius &&
                Mathf.Abs(delta.Y) < 5 + NpcCharacter.LinkCollisionRadius)
            {
                _savedLinkY = Mathf.FloorToInt(frame.Player.Position.Y);
                _state = BlockState.Moving;
                _movementUpdates = 0;
                // interactionSetScript runs after this update's
                // interactionRunScript call; part 2 starts next update.
                armedThisUpdate = true;
            }
        }

        if (_state == BlockState.Moving && !armedThisUpdate)
        {
            Entity.AdvanceAnimationUpdates(2);
            // The native substate calls interactionAnimateAsNpc before the
            // movement script. This keeps Link outside the worker's current
            // collision box as the worker closes the passage.
            Entity.PreventPlayerPassing(frame.Player);
            float step = _data.Speed100 / 40.0f;
            _savedX += _movingRight ? step : -step;
            _movementUpdates++;
            Entity.SetStatePosition(new Vector2(_savedX, Entity.Position.Y));
            frame.Player.SetScriptedCoordinateHigh(horizontal: false, _savedLinkY);
            if (_movementUpdates >= _data.BlockerDistance)
            {
                _state = BlockState.Waiting;
                _wait = _data.BlockerWait;
            }
        }
        else if (_state == BlockState.Waiting)
        {
            Entity.AdvanceAnimationUpdates(_wait == 1 ? 1 : 2);
            // The final wait update performs this separation before
            // enableinput, so Link cannot regain control inside the blocker.
            Entity.PreventPlayerPassing(frame.Player);
            _wait--;
            if (_wait == 0)
            {
                _movingRight = !_movingRight;
                _state = BlockState.Watching;
            }
        }

        Entity.UpdateDrawPriority(frame.Player.Position);
    }

    public void OnNpcTalkStarted() => _talking = true;

    public void OnNpcTalkEnded()
    {
        _talking = false;
        // A collision can replace part 1 with part 2 while its textbox is
        // open. In that case the old post-text wait/setanimation commands no
        // longer exist.
        _talkWait = _state == BlockState.Watching ? _data.BlockerWait : 0;
    }
}

/// <summary>Invisible INTERAC_DUNGEON_STUFF $12:$00 in room 4:$e7.</summary>
internal sealed class BlackTowerEntranceRoomEntity : RoomEntityAdapter<Node2D>,
    IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly BlackTowerWorkerDatabase _data;
    private readonly Action<int, string> _triggered;
    private readonly bool _whiteoutEntry;
    private bool _initialized;

    internal BlackTowerEntranceRoomEntity(
        Vector2 position,
        BlackTowerWorkerDatabase data,
        bool whiteoutEntry,
        Action<int, string> triggered)
        : base(new Node2D { Name = "BlackTowerEntrance" }, static _ => { })
    {
        Entity.Position = position;
        Entity.Visible = false;
        _data = data;
        _whiteoutEntry = whiteoutEntry;
        _triggered = triggered;
    }

    public bool Finished { get; private set; }
    internal bool Initialized => _initialized;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        if (!_initialized)
        {
            _initialized = true;
            if (!_whiteoutEntry ||
                frame.Player.Position.Y < _data.EntranceMinimumY)
            {
                Finished = true;
                return;
            }
        }

        Vector2 delta = frame.Player.Position - Entity.Position;
        float radius = _data.EntranceRadius + NpcCharacter.LinkCollisionRadius;
        if (Mathf.Abs(delta.X) >= radius || Mathf.Abs(delta.Y) >= radius)
            return;
        Finished = true;
        _triggered(0x020f, _data.Text(0x020f));
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}
