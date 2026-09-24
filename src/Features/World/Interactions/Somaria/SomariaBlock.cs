using Godot;
using System;

namespace oracleofages;

// ITEM18 state machine. The item owner supplies eligible updates and native
// shared signals; this actor does not poll input or own room/save state.
internal sealed partial class SomariaBlock : TransitionOffsetNode2D
{
    private readonly OracleRoomData _room;
    private readonly SomariaPlacementDatabase _geometry;
    private readonly SomariaLifecycleDatabase _data;
    private readonly SomariaBlockEnvironment _world;
    private readonly SomariaBlockPlacement _placement;
    private readonly SomariaBlockVisual _visual;
    private Vector2 _precisePosition;
    private OracleRuntimeState? _movementMemory;
    internal void BindMovementMemory(OracleRuntimeState memory) => _movementMemory = memory;
    private int _direction, _speed;
    private SomariaThrowMotion? _throw;
    private int _throwAngle;
    private readonly BraceletWeightDatabase _weights = new();
    internal int State { get; private set; }
    internal int Substate { get; private set; }
    internal int Counter { get; private set; }
    internal int ZHigh { get; private set; }
    internal int Health { get; private set; }
    internal int DamageToApply { get; set; }
    internal int Damage { get; }
    internal int ContactFlags { get; private set; }
    internal int KnockbackAngle { get; private set; }
    internal void QueueEnemyDamage(int damage, int contactFlags, Vector2 origin)
    {
        // applyDamageToLink (also used for ITEMs) assigns damageToApply;
        // multiple contacts overwrite damage but OR their var3e flags.
        DamageToApply = unchecked((sbyte)damage);
        ContactFlags |= contactFlags;
        KnockbackAngle = OracleObjectMovement.Shared.RelativeAngle(origin.Floor(), Position.Floor());
    }
    internal int Flags { get; set; }
    internal int Collision { get; private set; }
    internal Vector2I Radius { get; private set; }
    internal bool CollisionEnabled => !Finished && (Collision & 0x80) != 0;
    internal Rect2 CollisionBounds => new(Position - (Vector2)Radius, (Vector2)Radius * 2);
    internal int PackedPosition => _placement.PackedPosition;
    internal bool Finished { get; private set; }
    internal bool IsHeld => !Finished && State == 2 && Substate < 2;
    internal void ClearPhysicalItem()
    {
        Flags |= 0x30;
        Visible = false;
    }

    internal void BeginPickup()
    {
        if (Finished || State != 3) throw new InvalidOperationException("ITEM$18 pickup requires a solid block.");
        State = 2; Substate = 0;
    }

    internal void CopyHeldPosition(Vector2 link, int linkZ, int frame, int direction)
    {
        if (!IsHeld) throw new InvalidOperationException("ITEM$18 held-position copy requires state2/substate0-1.");
        Vector2I offset = _weights.LiftOffset(0, frame, direction);
        CopyHeldOffset(link, linkZ, offset);
    }

    internal void CopyHeldOffset(Vector2 link, int linkZ, Vector2I offset)
    {
        if (!IsHeld) throw new InvalidOperationException("ITEM$18 held-position copy requires state2/substate0-1.");
        Position = new((byte)((int)Mathf.Floor(link.X)+offset.X), (byte)(int)Mathf.Floor(link.Y));
        _precisePosition = Position; ZHigh = (sbyte)(byte)(linkZ+offset.Y);
        QueueRedraw();
    }

    internal void Release(int angle, bool dropped = false)
    {
        // dropLinkHeldItem checks state2, but does not gate on substate.
        if (!IsHeld || Substate != 1 && !dropped) throw new InvalidOperationException("ITEM$18 ordinary release requires initialized pickup.");
        if (angle is not (>= 0 and < 32) && angle != 0xff) throw new ArgumentOutOfRangeException(nameof(angle));
        Substate = dropped ? 3 : 2; _throwAngle = dropped ? 0xff : angle;
    }

    internal SomariaBlock(OracleRoomData room, Vector2 position, int z,
        SomariaPlacementDatabase geometry, SomariaGraphicsDatabase graphics,
        SomariaLifecycleDatabase data, SomariaBlockEnvironment world)
    {
        _room = room; _geometry = geometry; _data = data; _world = world;
        _placement = new(room, geometry); _visual = new(this, graphics);
        Position = position.Floor(); ZHigh = (sbyte)(byte)z;
        Health = graphics.Block(0).Health; Collision = graphics.Block(0).InitialCollision;
        Damage = unchecked((sbyte)graphics.Block(0).Damage);
        Radius = graphics.Block(0).InitialRadius;
        Visible = false;
    }

    // Called only after interactableTiles' push geometry/counter/destination
    // checks; the signal persists until successful block placement clears it.
    internal void RequestPush(int direction)
    {
        if (direction is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(direction));
        Flags |= 1; _direction = direction;
    }

    internal void Update(int group, int activeLinkTile, int braceletLevel, long tick,
        int linkDirection = 0, bool tossRing = false)
    {
        if (Finished) return;
        switch (State)
        {
            case 0:
                if ((_room.TilesetFlags & 0x20) != 0)
                {
                    Position = new(Position.X, (byte)((int)Position.Y + ZHigh));
                    ZHigh = 0;
                }
                Position = _geometry.Align(Position);
                _precisePosition = Position;
                _visual.SetPose(0); State = 1;
                _world.PlaySound(_data.PhaseSound); Visible = true;
                break;
            case 1:
                if (_placement.CanAppear(group, Position, ZHigh)) PushLinkAway();
                _visual.AdvancePhase();
                if (_visual.Parameter != 0)
                {
                    Collision |= 0x80;
                    CreateBlock(group, tick);
                }
                break;
            case 2:
                if (Substate == 0)
                {
                    Substate = 1; _placement.Remove(tick); _visual.SetPose(2);
                }
                else if (Substate == 1)
                {
                    if (ApplyDamage())
                    {
                        (_world.DropHeld ?? throw new InvalidOperationException("ITEM$18 requires a held-item owner."))(this);
                        Delete();
                    }
                }
                else UpdateThrow(linkDirection, tossRing);
                break;
            case 3:
                if (!_placement.InPlace) { Delete(); break; }
                if (ApplyDamage() || (Flags & 0x20) != 0 || activeLinkTile == PackedPosition ||
                    !_placement.SupportedBelow(Position))
                {
                    _placement.Remove(tick); Delete(); break;
                }
                if ((Flags & 1) == 0) _world.PublishGrabbable(this);
                else { State = 4; Substate = 0; }
                break;
            case 4:
                if (Substate == 0)
                {
                    Substate = 1;
                    bool glove = braceletLevel == 2; // source cp$02, not >=2
                    _speed = glove ? _data.GloveSpeed : _data.NormalSpeed;
                    Counter = glove ? _data.GloveFrames : _data.NormalFrames;
                    _world.PlaySound(_data.MoveSound);
                    _placement.Remove(tick);
                }
                if (ApplyDamage() || (Flags & 0x20) != 0) { Delete(); break; }
                Position = NativeObjectMovement.ApplySpeed(_movementMemory, ref _precisePosition, _speed, _direction*8);
                PushLinkAway(); Counter--;
                Radius = new(4,4);
                if (Counter == 0) CreateBlock(group, tick);
                break;
            default:
                throw new NotSupportedException($"caneOfSomaria.s:itemCode18 state${State:x2} is not implemented.");
        }
        QueueRedraw();
    }

    private void PushLinkAway()
    {
        Radius = new(Radius.X, 7);
        _world.PushLinkAway(this);
    }

    private bool ApplyDamage()
    {
        Health = (byte)(Health + DamageToApply);
        DamageToApply = 0;
        return (Health & 0x80) != 0; // itemUpdateDamageToApply returns bit7, not unsigned carry
    }

    private void CreateBlock(int group, long tick)
    {
        if (!_placement.CanAppear(group, Position, ZHigh)) { Delete(); return; }
        // The hazard test follows alignment; a failed hazard placement puffs
        // at the aligned position, unlike an earlier wall/height rejection.
        Position = _geometry.Align(Position); _precisePosition = Position;
        if (!_placement.TryCreate(group, Position, ZHigh, tick)) { Delete(); return; }
        ZHigh = 0; State = 3; Substate = 0; Radius = new(4,4); Flags &= 0xf0;
        _visual.SetPose(1);
    }

    private void Delete()
    {
        if ((Flags & 0x10) == 0) _world.CreatePuff(Position, ZHigh);
        Finished = true; Visible = false;
    }

    private void UpdateThrow(int linkDirection, bool tossRing)
    {
        if (Position.X >= _room.Width || Position.Y >= _room.Height || Position.X < 0 || Position.Y < 0)
        { Finished = true; Visible = false; return; }
        if (_throw is null)
        {
            _throw = new(_room, _geometry, _movementMemory);
            _throw.Begin(Position, ZHigh, linkDirection, _throwAngle, tossRing);
        }
        _throw.AdvanceLateral();
        Position = _throw.Position; ZHigh = _throw.ZHigh;
        if ((Flags & 0x20) != 0) { Delete(); return; }
        bool landed = _throw.AdvanceVertical((kind, point, z) =>
            (_world.CreateHazardEffect ?? throw new InvalidOperationException("ITEM$18 requires a hazard-effect owner."))(kind, point, z),
            out bool hazardDelete);
        Position = _throw.Position; ZHigh = _throw.ZHigh;
        if (hazardDelete) { Finished = true; Visible = false; }
        else if (landed) Delete();
    }

    public override void _Draw() => DrawTexture(_visual.Texture, SourceOamDrawOffset + _visual.Offset + Vector2.Down*ZHigh);
}
