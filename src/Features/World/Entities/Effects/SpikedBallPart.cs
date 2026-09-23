using Godot;
using System;

namespace oracleofages;

internal partial class SpikedBallPart : TransitionOffsetNode2D
{
    private readonly BallChainBehaviorProfile _data = EnemyBehaviorTables.Shared.BallChain;
    private readonly BallChainSoldierCharacter _soldier;
    private readonly SpikedBallPart? _head;
    private readonly EnemyAnimationPlayer _animation;
    private int _pendingCollision = -1;
    private int _lastCollision;
    private bool _collisionCleared;
    private readonly SpikedBallDatabase _visual;
    internal int SubId { get; }
    internal int State { get; private set; }
    internal int Angle { get; private set; }
    internal int Radius { get; private set; }
    internal int ExtensionSpeed { get; private set; }
    internal int ZHigh { get; private set; }
    internal int SpeedZ { get; set; }
    internal int InvincibilityCounter { get; set; }
    internal int Health { get; private set; }
    internal bool PendingCollision => _pendingCollision >= 0;
    internal bool Finished { get; private set; }
    internal bool CollisionEnabled => SubId == 0 && State != 0 && !Finished && !_collisionCleared;
    internal Rect2 CollisionBounds => new(Position - new Vector2(_visual.RadiusX, _visual.RadiusY),
        new Vector2(_visual.RadiusX * 2, _visual.RadiusY * 2));

    internal SpikedBallPart(BallChainSoldierCharacter soldier, SpikedBallDatabase visual, int subid, SpikedBallPart? head = null)
    {
        if (subid is < 0 or > 3 || (subid != 0 && head is null))
            throw new ArgumentException("PART_SPIKED_BALL $2a requires head0 or linked chain1..3.");
        _soldier = soldier; _head = head; SubId = subid; _visual = visual;
        _animation = new(this, visual.Animations.Length);
        _animation.Load(OracleGraphicsCache.LoadImage($"res://assets/oracle/gfx/{visual.Sprite}.png"),
            visual.Animations, visual.TileBase, visual.Palette);
        _animation.SetAnimation(0);
        Visible = false;
        ZIndex = NpcCharacter.InFrontOfLinkZIndex;
    }

    internal void PublishCollision(int itemCollision) => _lastCollision = _pendingCollision = itemCollision & 0x7f;
    internal void ClearHealthAndCollision() { Health = 0; _collisionCleared = true; }

    internal void UpdateFrame(Vector2 enemyTarget)
    {
        if (Finished) return;
        bool initialized = State != 0;
        if (!initialized) { Health = _data.PartData[4].Value; _collisionCleared = false; }
        if (State != 0 && InvincibilityCounter != 0) InvincibilityCounter -= Math.Sign(InvincibilityCounter);
        int hit = _pendingCollision;
        _pendingCollision = -1;
        if (!GodotObject.IsInstanceValid(_soldier) || _soldier.IsDead ||
            SubId != 0 && (!GodotObject.IsInstanceValid(_head) || _head!.Finished))
        { Finished = true; Visible = false; return; }
        // PARTSTATUS_DEAD also enters the status handler using retained
        // var2a's low bits, even after its pending bit was cleared.
        int statusCollision = hit >= 0 ? hit : Health == 0 ? _lastCollision : -1;
        if (initialized && statusCollision is >= 1 and <= 9)
        {
            _soldier.ProtectFromBallBlock();
            if (SpeedZ >= 0) SpeedZ = 0;
        }
        if (SubId != 0)
        {
            if (State == 0) { State = 1; _animation.SetAnimation(1); Visible = true; }
            Angle = _head!.Angle;
            int quarter = _head.Radius >> 2;
            Radius = SubId switch { 1 => quarter * 3 + 1, 2 => quarter * 2, _ => quarter };
            UpdatePosition();
            return;
        }
        // Only rotating/alignment states track every parent signal. A live
        // throw keeps state4/5 until the parent withdraws its throw signal.
        if (State != 0 && (State < 4 || _soldier.BallSignal != 2))
            State = _soldier.BallSignal == 0 ? 1 : _soldier.BallSignal == 1 ? 2 : 3;
        switch (State)
        {
            case 0: State = 1; Visible = true; goto case 1;
            case 1: Rotate(_data.SlowRotation); break;
            case 2: Rotate(_data.FastRotation); break;
            case 3:
                // Source BC=origin, hFF8F/E=target, then XOR$10. Retain
                // that order even for coincident positions and byte wraps.
                int target = OracleObjectMovement.Shared.RelativeAngle(enemyTarget, Origin()) ^ 0x10;
                if (((target - 6 - Angle + 1) & _data.AngleMask) >= 3) Rotate(_data.FastRotation);
                else { Angle = (target - 3) & _data.AngleMask; Radius = _data.ReleaseRadius; State = 4; }
                break;
            case 4:
                State = 5;
                Angle = (Angle + 3) & _data.AngleMask;
                Radius = _data.ThrowRadius;
                ExtensionSpeed = _data.ExtensionSpeed;
                break;
            case 5:
                if (hit > 0 && ExtensionSpeed >= 0) ExtensionSpeed = 0;
                int radius = (Radius + (ExtensionSpeed >> 8)) & 0xff;
                if (radius < _data.OrbitRadius) _soldier.FinishBallThrow();
                else
                {
                    Radius = radius;
                    ExtensionSpeed = unchecked((short)(ExtensionSpeed - _data.ExtensionDeceleration));
                }
                break;
            default: throw new NotSupportedException($"spikedBall.s: PART $2a state ${State:x2} is not represented.");
        }
        UpdatePosition();
    }

    private void Rotate(int increment)
    { Angle = (Angle + increment) & _data.AngleMask; Radius = _data.OrbitRadius; }
    private Vector2 Origin() => new(((int)MathF.Floor(_soldier.Position.X) + _data.OriginOffset) & 0xff,
        ((int)MathF.Floor(_soldier.Position.Y) + _data.OriginOffset) & 0xff);
    private void UpdatePosition()
    {
        Vector2 p = Origin() + OracleObjectMovement.Shared.CircleArcOffset(Radius, Angle);
        Position = new((int)p.X & 0xff, (int)p.Y & 0xff);
        ZHigh = _soldier.ZFixed >> 8;
        QueueRedraw();
    }
    public override void _Draw()
    {
        if (Visible && !Finished)
            DrawTexture(_animation.CurrentTexture, _animation.CurrentOffset + Vector2.Down * ZHigh + TransitionDrawOffset);
    }
}
