using Godot;
using System;

namespace oracleofages;

// PART_WIZZROBE_PROJECTILE $1f: its held pose never advances during flight.
internal sealed partial class WizzrobeProjectile : TransitionOffsetNode2D
{
    private readonly WizzrobeBehaviorProfile _data = EnemyBehaviorTables.Shared.Wizzrobe;
    private readonly OracleRoomData _room;
    private readonly OracleRuntimeState _memory;
    private readonly EnemyAnimationPlayer _animation;
    internal int State { get; private set; }
    internal int Angle { get; }
    internal int ZHigh { get; }
    internal int InvincibilityCounter { get; set; }
    internal bool PendingCollision { get; private set; }
    internal bool Finished { get; private set; }
    internal bool CollisionEnabled { get; private set; } = true;
    internal int AnimationIndex => _animation.AnimationIndex;
    internal Rect2 CollisionBounds => new(Position - Vector2.One * (_data.PartData[2].Value & 15),
        Vector2.One * ((_data.PartData[2].Value & 15) * 2));
    internal WizzrobeProjectile(EnemyProjectileVisualRecord visual, OracleRoomData room, OracleRuntimeState memory,
        Vector2 position, int angle, int zHigh)
    {
        _room = room; _memory = memory; Position = position; Angle = angle; ZHigh = zHigh;
        _animation = new(this, visual.Animations.Length);
        _animation.Load(EnemyVisualSource.LoadComposite(visual.Sprites), visual.Animations, visual.TileBase,
            visual.Palette, sourceGrayscaleInverted: visual.SourceGrayscaleInverted);
        Visible = false;
    }
    internal void PublishCollision() => PendingCollision = true;
    internal void ClearHealthAndCollision() { CollisionEnabled = false; PendingCollision = true; }
    internal void UpdateFrame(Vector2 camera)
    {
        if (Finished) return;
        if (State != 0 && InvincibilityCounter != 0) InvincibilityCounter -= Math.Sign(InvincibilityCounter);
        if (PendingCollision) { Delete(); return; }
        if (State == 0)
        {
            State = 1; Visible = true;
            ZIndex = ObjectDrawPriority.FromVisible(_data.PartState[1].Value);
            _animation.SetAnimation(Angle >> 3);
            QueueRedraw(); return;
        }
        if (!OracleObjectMath.IsInsideOriginalScreenBoundary(Position - camera) || _room.IsSolid(Position))
        { Delete(); return; }
        var velocity = NativeObjectMovement.Velocity(_memory, _data.PartState[0].Value, Angle);
        Position = OracleObjectPosition.FromPixels(Position).Add(velocity.YFixed, velocity.XFixed).PrecisePosition;
        QueueRedraw();
    }
    private void Delete() { Finished = true; Visible = false; }
    public override void _Draw()
    {
        if (Visible && !Finished) DrawTexture(_animation.CurrentTexture,
            _animation.CurrentOffset + Vector2.Down * ZHigh + TransitionDrawOffset);
    }
}
