using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_CHILD $35 native movement before its stage talk script.</summary>
internal sealed class FamilyChildRoomEntity : NpcCharacterRoomEntityAdapter,
    IFixedRoomEntity, IRoomBlocker, ITalkTarget,
    IOrdinaryNpcEntity
{
    private readonly FamilyChildMotionDatabase _data;
    private readonly RoomEntityManager _entities;
    private readonly int _mode;
    private readonly int _baseAnimation;
    private Vector2 _precisePosition;
    private int _angle;
    private int _speed;
    private int _animationOffset;
    private int _direction;
    private int _waypointList = -1;
    private int _waypointIndex;
    private int _shySubstate;
    private int _curiousSubstate;
    private int _curiousDelay;
    private int _zFixed;
    private int _speedZ;
    private bool _initialized;
    private readonly int _solidity;

    internal FamilyNpcScriptHost? Script { get; set; }
    public NpcCharacter Npc => Entity;

    internal FamilyChildRoomEntity(NpcCharacter npc, FamilyChildMotionDatabase data,
        RoomEntityManager entities)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        _data = data;
        _entities = entities;
        _mode = npc.Record.Var03;
        if (_mode is < 0 or > 0x15)
            throw new InvalidOperationException(
                $"Unsupported family child var03=${_mode:x2} in " +
                $"{npc.Record.Group}:{npc.Record.Room:x2}.");
        _baseAnimation = data.AnimationBase(npc.Record.SubId);
        _precisePosition = npc.Position;
        _angle = 0x18;
        _solidity = _mode switch
        {
            1 or 2 or 4 or 5 or 7 or 8 or 0x0b or 0x0f or 0x13 => 1,
            3 or 6 or 9 => 2,
            _ => 0
        };
        _speed = _mode switch
        {
            1 or 4 or 7 => ObjectSpeed.Speed180,
            2 or 5 or 8 => ObjectSpeed.Speed200,
            0x0b or 0x0f or 0x13 => ObjectSpeed.Speed80,
            _ => ObjectSpeed.Speed100
        };
        _waypointList = _mode switch
        {
            2 or 5 => 0,
            8 => 1,
            7 => 2,
            0x0b or 0x13 => 3,
            0x0f => 4,
            _ => -1
        };
        _animationOffset = _mode is 0x0c or 0x10 or 0x14 ? 3 : 0;
        npc.SetScriptAnimation(_data.Animation(_baseAnimation + _animationOffset));
        if (_solidity != 0) npc.SetBlocksLink(false);
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.Active) return;
        if (!_initialized)
        {
            _initialized = true;
            Entity.ShowInitializedPresentation();
            UpdateSolidity(frame.Player);
            return; // child.s state $00 has no interactionRunScript call.
        }

        bool runScript = true;
        switch (_mode)
        {
            case 1 or 4:
                UpdateHyperactive();
                break;
            case 2 or 5 or 8:
                UpdateShy(frame.Player);
                break;
            case 3 or 6 or 9:
                UpdateCurious();
                runScript = _curiousSubstate != 1;
                break;
            case 7 or 0x0b or 0x0f or 0x13:
                UpdateWaypoints(changeAnimation: true);
                break;
            case 0x0a or 0x0e or 0x12:
                SpawnMusicNote(frame.Counter, singer: false);
                break;
            case 0x0d or 0x11 or 0x15:
                SpawnMusicNote(frame.Counter, singer: true);
                break;
            case 0 or 0x0c or 0x10 or 0x14:
                break;
            default:
                throw new InvalidOperationException(
                    $"Unimplemented INTERAC_CHILD var03=${_mode:x2} in " +
                    $"{Npc.Record.Group}:{Npc.Record.Room:x2}.");
        }
        if (runScript) Script?.Advance(frame.Player);
        Entity.AdvanceAnimationUpdates(1);
        UpdateSolidity(frame.Player);
    }

    private void Move() => Entity.Position = NativeObjectMovement.ApplySpeed(
        _entities.RuntimeState, ref _precisePosition, _speed, _angle);

    private void UpdateHyperactive()
    {
        Move();
        int x = (int)Entity.Position.X;
        int offset = (x - 0x29) & 0xff;
        if (offset < 0x40) return;
        if ((offset & 0x80) == 0) x -= 2;
        x++;
        Entity.Position = new Vector2(x, Entity.Position.Y);
        _precisePosition = new Vector2(x, _precisePosition.Y);
        _waypointIndex = (_waypointIndex + 1) & 3;
        _angle = _data.HyperactiveAngle(_waypointIndex);
        FlipAnimation();
    }

    private void UpdateShy(Player player)
    {
        if (_shySubstate == 0)
        {
            Vector2 delta = player.Position - Entity.Position;
            if (Math.Abs((int)delta.X) + Math.Abs((int)delta.Y) >= 0x18)
                return;
            _shySubstate = 1;
        }
        UpdateWaypoints(changeAnimation: false);
        if (ReachedWaypoint())
        {
            _shySubstate = 0;
            NextWaypoint();
        }
    }

    private void UpdateWaypoints(bool changeAnimation)
    {
        IReadOnlyList<Vector2I> positions = _data.Waypoints(_waypointList);
        Vector2I target = positions[_waypointIndex];
        _angle = OracleObjectMovement.Shared.RelativeAngle(
            Entity.Position, new Vector2(target.X, target.Y));
        Move();
        if (changeAnimation)
        {
            int direction = (((_angle >> 4) & 1) ^ 1);
            if (direction != _direction)
            {
                _direction = direction;
                Entity.SetScriptAnimation(_data.Animation(
                    _baseAnimation + _animationOffset + _direction));
            }
            if (ReachedWaypoint()) NextWaypoint();
        }
    }

    private bool ReachedWaypoint()
    {
        Vector2I target = _data.Waypoints(_waypointList)[_waypointIndex];
        return Math.Abs(target.X - (int)Entity.Position.X) <= 1 &&
               Math.Abs(target.Y - (int)Entity.Position.Y) <= 1;
    }

    private void NextWaypoint() => _waypointIndex =
        (_waypointIndex + 1) % _data.Waypoints(_waypointList).Count;

    private void UpdateCurious()
    {
        if (_curiousSubstate == 0)
        {
            _speed = ObjectSpeed.Speed100;
            _angle = 0x18;
            BeginJump();
            return;
        }
        if (_curiousSubstate == 1)
        {
            bool landed = OracleObjectMath.UpdateSpeedZ(
                ref _zFixed, ref _speedZ, 0x50);
            Entity.SetScriptDrawOffset(new Vector2(0, _zFixed >> 8));
            if (!landed)
            {
                Move();
                return;
            }
            _curiousSubstate = 2;
            _curiousDelay = 0x78;
            _angle ^= 0x10;
            return;
        }
        _curiousDelay = (_curiousDelay - 1) & 0xff;
        if (_curiousDelay == 0) BeginJump();
    }

    private void BeginJump()
    {
        _curiousSubstate = 1;
        _zFixed = 0;
        _speedZ = unchecked((short)0xfb00);
        _entities.RequestSound(SoundId.SndJump);
    }

    private void SpawnMusicNote(int frameCounter, bool singer)
    {
        if ((frameCounter & 0x1f) != 0) return;
        if (singer)
        {
            bool left = _direction == 0;
            NayruRescueMusicNoteRoomEntity.Spawn(_entities,
                Entity.Position + new Vector2(left ? -4 : 0, -4), left);
            return;
        }
        bool slackerLeft = (Entity.ConsumeAnimationParameter() & 1) == 0;
        NayruRescueMusicNoteRoomEntity.Spawn(_entities,
            Entity.Position + new Vector2(slackerLeft ? -4 : 8, -12), slackerLeft);
    }

    private void FlipAnimation()
    {
        _animationOffset ^= 1;
        Entity.SetScriptAnimation(_data.Animation(_baseAnimation + _animationOffset));
    }

    private void UpdateSolidity(Player player)
    {
        if (_solidity == 0) Entity.PreventPlayerPassing(player);
        else if (_solidity == 1 && !player.PassesNpcs)
        {
            Vector2 delta = player.Position - Entity.Position;
            if (Math.Abs(delta.X) < 12 && Math.Abs(delta.Y) < 12)
                player.ApplyNpcLightPush(Entity.Position);
        }
        Entity.UpdateDrawPriority(player.Position, withTerrainEffects: true);
    }

    public bool BlocksLink(Vector2 center) => Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanTalkTo(player) ? Entity : null;
}
