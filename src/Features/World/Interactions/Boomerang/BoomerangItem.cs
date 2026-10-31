using Godot;
using System;

namespace Oracles;

// The dynamic ITEM$06 child outlives the parent throw animation. Returning
// states ignore terrain and keep their collision enabled until the catch.
internal partial class BoomerangItem : TransitionOffsetNode2D
{
    private readonly BoomerangDatabase _data;
    private readonly OracleRoomData _room;
    private readonly EnemyAnimationPlayer _animation;
    private readonly ItemTilePassage _tilePassage;
    private readonly Action<int> _sound;
    private readonly Action<Vector2, int> _clink;
    private readonly Func<int> _damage;
    private Vector2 _precisePosition;
    private OracleRuntimeState? _movementMemory;
    internal void BindMovementMemory(OracleRuntimeState memory) => _movementMemory = memory;
    internal int TargetAngle { get; set; }
    internal int RelatedParentSlot { get; private set; }
    internal int SubId => _data.SubId;
    private readonly Action<Vector2>? _breakTile;
    private bool _collisionPending;

    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int Angle { get; private set; }
    internal int ZHigh { get; private set; }
    internal int Damage { get; private set; }
    internal bool Finished { get; private set; }
    internal bool CollisionEnabled => !Finished && State is >= 1 and <= 3;
    internal int CollisionType => _data.Collision & 0x7f;
    internal Vector2 PrecisePosition => _precisePosition;
    internal Rect2 CollisionBounds => new(Position - (Vector2)_data.Radius, (Vector2)_data.Radius * 2);

    internal BoomerangItem(OracleRoomData room, Vector2 position, int angle, int zHigh, Func<int> damage,
        Action<int> sound, Action<Vector2, int> clink, int subId = 0, int parentSlot = 0, Action<Vector2>? breakTile = null)
    {
        AssetRoot = room.AssetRoot;
        _tilePassage = new ItemTilePassage(AssetRoot);
        if (angle is < 0 or > 0x1f) throw new ArgumentOutOfRangeException(nameof(angle));
        _data = BoomerangDatabase.For(AssetRoot, subId);
        RelatedParentSlot = parentSlot;
        _breakTile = breakTile;
        if (_data.BreakSource >= 0 && _breakTile is null)
            throw new InvalidOperationException("ITEM$06:$01 requires its native tile-breaking owner.");
        _room = room;
        _precisePosition = position;
        Position = OracleObjectMath.ToPixelPosition(position);
        Angle = TargetAngle = angle;
        ZHigh = unchecked((sbyte)(byte)zHigh);
        _damage = damage;
        _sound = sound;
        _clink = clink;
        _animation = new(this, 1);
        // The $84e1 upload maps item tile base$4e to sprite offset zero.
        _animation.Load(OracleGraphicsCache.LoadTwoBitSpriteSheet(AssetRoot + $"metadata/boomerang_gfx_{subId}.bin", 64, 2),
            [_data.Animation], 0, _data.OamFlags & 7, sourceGrayscaleInverted: _data.SourceInverted);
        Visible = false;
    }

    internal void QueueCollision() => _collisionPending = true;

    internal void UpdateFrame(Vector2 linkPosition, Vector2 linkObjectPosition, int linkZHigh)
    {
        if (Finished) return;
        if (State == 0)
        {
            State = 1;
            Counter = _data.OutwardUpdates;
            Damage = _damage();
            _animation.SetAnimation(0);
            Visible = true;
            return;
        }
        if (State == 4)
        {
            if (--Counter == 0) { Finished = true; return; }
            // objectTakePosition retains this item's fractional bytes.
            _precisePosition = new(
                unchecked((byte)(int)linkObjectPosition.X) + _precisePosition.X - Mathf.Floor(_precisePosition.X),
                unchecked((byte)(int)linkObjectPosition.Y) + _precisePosition.Y - Mathf.Floor(_precisePosition.Y));
            Position = OracleObjectMath.ToPixelPosition(_precisePosition);
            ZHigh = unchecked((sbyte)(byte)linkZHigh);
            return;
        }
        if (State == 1)
        {
            _breakTile?.Invoke(Position);
            if (_collisionPending) BeginReturn(linkPosition);
            else if (_room.IsSolid(Position) && !_tilePassage.CanPass(_room, Position, Angle))
            {
                _clink(Position, ZHigh);
                Angle ^= 0x10;
                State = 2;
                RelatedParentSlot = 0;
            }
            else if (Position.X >= _room.Width || Position.Y >= _room.Height)
                BeginReturn(linkPosition);
            else
            {
                Nudge(TargetAngle);
                if (--Counter == 0) BeginReturn(linkPosition);
            }
        }
        else if (State == 2)
        {
            Nudge(OracleObjectMovement.Shared.RelativeAngle(Position, linkPosition));
            if (Within(linkPosition, _data.NearRadius)) State = 3;
            _breakTile?.Invoke(Position);
        }
        else if (State == 3)
        {
            Angle = OracleObjectMovement.Shared.RelativeAngle(Position, linkPosition);
            if (Within(linkPosition, _data.CatchRadius))
            {
                State = 4;
                Counter = _data.CatchUpdates;
                Visible = false;
                return;
            }
            _breakTile?.Invoke(Position);
        }
        Position = NativeObjectMovement.ApplySpeed(_movementMemory, ref _precisePosition, _data.Speed, Angle);
        if (_animation.ConsumeParameter() != 0) _sound(_data.Sound);
        _animation.Advance();
    }

    private void BeginReturn(Vector2 linkPosition)
    {
        int target = OracleObjectMovement.Shared.RelativeAngle(Position, linkPosition);
        // Native subtraction wraps as a byte, not as a five-bit angle.
        if (Position.X > 0xf0 || Position.Y > 0xf0 || unchecked((byte)(target - Angle + 8)) >= 0x11)
            Angle = target;
        State = 2;
        RelatedParentSlot = 0;
    }

    private void Nudge(int target)
    {
        if (Angle != target) Angle = (Angle + (((Angle - target) & ObjectAngle.Mask) < 0x10 ? -1 : 1)) & ObjectAngle.Mask;
    }

    private bool Within(Vector2 link, int radius) =>
        unchecked((byte)((int)Position.Y - (int)link.Y + radius)) < radius * 2 &&
        unchecked((byte)((int)Position.X - (int)link.X + radius)) < radius * 2;

    public override void _Draw() => DrawTexture(_animation.CurrentTexture,
        new Vector2(-16, -16 + ZHigh) + TransitionDrawOffset);
}
