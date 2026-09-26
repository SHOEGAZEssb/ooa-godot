using Godot;
using System;

namespace oracleofages;

// PART$4a. Its room adapter supplies native eligibility and collision
// flags; this actor owns only the source state/animation/movement handlers.
internal sealed partial class SmogProjectilePart : TransitionOffsetNode2D
{
    private readonly SmogProjectileDatabase _data;
    private readonly OracleRoomData _room;
    private readonly EnemyAnimationPlayer _animation;
    private OracleObjectPosition _position;
    private OracleRuntimeState? _movementMemory;
    private bool _collisionCleared;
    internal int SubId { get; }
    internal int State { get; private set; }
    internal int Angle { get; private set; }
    internal int ContactFlags { get; private set; }
    internal int InvincibilityCounter { get; set; }
    internal bool PendingCollision => (ContactFlags & ObjectCollisionFlags.JustHit) != 0;
    internal int CollisionMode => SubId == 0 ? _data.InitialCollisionMode & 0x7f : 4;
    internal bool CollisionEnabled => State == 1 && !Finished && !_collisionCleared;
    internal bool Finished { get; private set; }
    internal int AnimationIndex => _animation.AnimationIndex;
    internal int AnimationFrame => _animation.FrameIndex;
    internal Rect2 CollisionBounds => new(Position - (Vector2)_data.Radius, (Vector2)_data.Radius * 2);
    internal void PublishCollision(int flags) => ContactFlags |= flags;
    internal void ClearHealthAndCollision() => _collisionCleared = true;
    internal void BindMovementMemory(OracleRuntimeState memory) => _movementMemory = memory;

    internal SmogProjectilePart(SmogProjectileDatabase data, OracleRoomData room, Vector2 position, int subid)
    {
        if (subid is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(subid));
        _data = data; _room = room; SubId = subid;
        _position = OracleObjectMovement.Shared.PositionFromPixels(position);
        Position = _position.PixelPosition;
        _animation = new(this, 4);
        _animation.Load(OracleGraphicsCache.LoadImage($"res://assets/oracle/gfx/{data.Sprite}.png"),
            data.Animations, data.TileBase, subid == 0 ? data.Palette : 5);
        _animation.SetAnimation(0);
        Visible = false;
    }

    internal void UpdateFrame(Vector2 target, Vector2I camera, int roomFlags, int enemyCount)
    {
        if (Finished) return;
        if (State != 0 && InvincibilityCounter != 0) InvincibilityCounter -= Math.Sign(InvincibilityCounter);
        UpdateState(target, camera, roomFlags, enemyCount);
        // updateParts clears bit7 after the native handler, retaining the
        // collision ID which the small projectile's state1 tests next time.
        ContactFlags &= 0x7f;
    }

    private void UpdateState(Vector2 target, Vector2I camera, int roomFlags, int enemyCount)
    {
        if (State == 0)
        {
            State = 1; Visible = true; ZIndex = NpcCharacter.InFrontOfLinkZIndex;
            _collisionCleared = false;
            Angle = OracleObjectMovement.Shared.RelativeAngle(Position, target.Floor());
            _animation.SetAnimation(SubId == 0 ? 0 : 2 + (((Angle + 4) >> 3) & 1));
            // state0 falls through: boundary checks, movement and (small only)
            // animation all execute on the allocation's first eligible update.
        }
        if (State == 1)
        {
            if ((roomFlags & 0x40) != 0 || enemyCount == 1 ||
                (byte)((int)Position.Y - camera.Y + 7) >= 0x8f ||
                (byte)((int)Position.X - camera.X + 7) >= 0xaf) { Delete(); return; }
            var velocity = NativeObjectMovement.Velocity(_movementMemory, SubId == 0 ? 0x1e : 0x28, Angle);
            _position = _position.Add(velocity.YFixed, velocity.XFixed);
            Position = _position.PixelPosition;
            QueueRedraw();
            if (SubId != 0) return;
            var offset = _data.FrontOffset(Angle);
            Vector2 front = new((byte)((int)Position.X + offset.X), (byte)((int)Position.Y + offset.Y));
            if (ContactFlags != 0 || _room.GetTerrainInfo(front).Collision != 0)
            {
                State = 2; _collisionCleared = true;
                _animation.SetAnimation(1);
            }
        }
        _animation.Advance();
        if (_animation.CurrentParameter != 0) Delete();
    }

    private void Delete() { Finished = true; Visible = false; }
    public override void _Draw()
    {
        if (Visible && !Finished) DrawTexture(_animation.CurrentTexture, _animation.CurrentOffset + TransitionDrawOffset);
    }
}
