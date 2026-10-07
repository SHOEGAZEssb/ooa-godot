using Godot;
using System;

namespace oracleofages;

// PART_VIRE_PROJECTILE $3a. relatedObj1 reads the live ENEMY page, including
// after reuse; the visual effect does not retain a detached parent health.
internal sealed partial class VireProjectile : TransitionOffsetNode2D
{
    private readonly VireProjectileProfile _data = EnemyBehaviorTables.Shared.VireProjectile;
    private readonly OracleRoomData _room;
    private readonly OracleRuntimeState _memory;
    private readonly Func<int, int> _health;
    private readonly Func<VireProjectileSpawn, bool> _spawn;
    private readonly Func<int, bool> _canAllocate;
    private readonly Func<Vector2, int> _puff;
    private readonly Func<int, int> _parameter;
    private readonly Func<Vector2, bool> _normalPuff;
    internal EnemyAnimationPlayer Animation { get; }
    internal int ParentSlot { get; }
    internal int SubId { get; }
    internal int Var03 { get; }
    internal int State { get; private set; }
    internal int Speed { get; private set; }
    internal int Angle { get; private set; }
    internal int ZHigh { get; private set; }
    internal int Counter1 { get; private set; }
    internal int Counter2 { get; private set; }
    internal int PuffSlot { get; private set; } = -1;
    internal Vector2 Target { get; private set; }
    internal int InvincibilityCounter { get; set; }
    internal int ContactFlags { get; private set; }
    internal bool PendingCollision => (ContactFlags & ObjectCollisionFlags.JustHit) != 0;
    internal bool Finished { get; private set; }
    private bool _healthCleared;
    internal bool CollisionEnabled => State != 0 && !Finished && !_healthCleared;
    internal int Palette => SubId == 2 ? 3 : SubId == 3 ? 2 : _data.PartData[6].Value;
    internal Rect2 CollisionBounds => new(Position.Floor() - new Vector2(_data.PartData[2].Value & 15, _data.PartData[2].Value >> 4),
        new Vector2((_data.PartData[2].Value & 15) * 2, (_data.PartData[2].Value >> 4) * 2));

    internal VireProjectile(EnemyProjectileVisualRecord visual, OracleRoomData room, OracleRuntimeState memory,
        VireProjectileSpawn input, Func<int, int> health, Func<VireProjectileSpawn, bool> spawn,
        Func<int, bool> canAllocate, Func<Vector2, int> puff, Func<int, int> parameter, Func<Vector2, bool> normalPuff)
    {
        if (input.SubId is < 0 or > 3) throw new NotSupportedException($"vireProjectile.s: PART$3a subid${input.SubId:x2}.");
        _room = room; _memory = memory; _health = health; _spawn = spawn; _canAllocate = canAllocate;
        _puff = puff; _parameter = parameter; _normalPuff = normalPuff;
        SubId = input.SubId; ParentSlot = input.ParentSlot; Var03 = input.Var03;
        Position = input.Position.Floor(); Angle = input.Angle; ZHigh = unchecked((sbyte)(byte)input.ZHigh);
        Animation = new(this, 2);
        Animation.Load(EnemyVisualSource.LoadComposite(visual.Sprites), visual.Animations, visual.TileBase,
            Palette, sourceGrayscaleInverted: visual.SourceGrayscaleInverted);
        Visible = false;
    }
    internal void PublishCollision(int collision) => ContactFlags = ObjectCollisionFlags.JustHit | collision;
    internal void ClearHealthAndCollision() => _healthCleared = true;
    internal void UpdateFrame(Vector2 target)
    {
        if (Finished) return;
        if (State != 0 && InvincibilityCounter != 0) InvincibilityCounter -= Math.Sign(InvincibilityCounter);
        if (State != 0 && (PendingCollision || _healthCleared))
        {
            if ((ContactFlags & ObjectCollisionFlags.TypeMask) >= 4) _normalPuff(Position.Floor());
            Delete(); return;
        }
        if (State == 0) { _healthCleared = false; Initialize(target); ContactFlags &= 0x7f; return; }
        if (SubId == 3)
        {
            if (Counter1 != 0) Counter1--;
            if (Counter1 == 0) { _normalPuff(Position.Floor()); Delete(); return; }
            Counter2 = (Counter2 - 1) & 0xff;
            if (Counter2 == 0)
            {
                Counter2 = _data.HomingTurnInterval;
                int desired = OracleObjectMovement.Shared.RelativeAngle(Position.Floor(), target.Floor());
                int delta = (Angle - desired) & 31;
                if (delta != 0) Angle = (Angle + (delta >= 16 ? 1 : -1)) & 31;
            }
            MoveAndAnimate(); return;
        }
        if (SubId == 2 && State == 1)
        {
            int tolerance = _data.ArrivalRadius;
            if (unchecked((byte)(OracleObjectPosition.HighByte(Position.X) - Target.X + tolerance)) < tolerance * 2 + 1 &&
                unchecked((byte)(OracleObjectPosition.HighByte(Position.Y) - Target.Y + tolerance)) < tolerance * 2 + 1)
            {
                int slot = _puff(Position.Floor());
                if (slot >= 0) { PuffSlot = slot; State = 2; Visible = false; }
                return;
            }
            Angle = OracleObjectMovement.Shared.RelativeAngle(Position.Floor(), Target);
            MoveAndAnimate(); return;
        }
        if (SubId == 2 && State == 2)
        {
            if ((_parameter(PuffSlot) & 0x80) == 0 || !_canAllocate(_data.SplitChildren)) return;
            // Source allocates $18,$13,$0d,$08,$03 in this order; new higher
            // physical slots initialize in this same PART walk.
            for (int i = _data.SplitAngles.Count - 1; i >= 0; i--)
                SpawnSibling(2, _data.SplitAngles[i].Value);
            State = 3; Angle = _data.PrimarySplitAngle; Speed = _data.Speed(0, _health(ParentSlot));
            Animation.SetAnimation(1); Visible = true; ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex;
            return;
        }
        int rounded = (Angle & 7) == 0 ? Angle : (Angle & 0x18) + 4;
        var offset = _data.BoundaryOffsets[rounded >> 2];
        int y = unchecked((byte)(OracleObjectPosition.HighByte(Position.Y) + offset.First));
        int x = unchecked((byte)(OracleObjectPosition.HighByte(Position.X) + offset.Second));
        if (x >= _room.Width || y >= _room.Height || _room.GetTerrainInfo(new(x, y)).Collision == 0xff)
        { Delete(); return; }
        MoveAndAnimate();
    }
    private void Initialize(Vector2 target)
    {
        Animation.SetAnimation(0); // partLoadGraphicsAndProperties precedes state0.
        if (SubId == 2 && Var03 != 0)
        {
            State = 3; Speed = _data.Speed(0, _health(ParentSlot)); Animation.SetAnimation(1);
            Visible = true; ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex; return;
        }
        State = 1;
        var position = OracleObjectPosition.FromPixels(Position);
        Position = position.Add(ZHigh * 256, 0).PrecisePosition;
        ZHigh = 0;
        Visible = true; ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex;
        if (SubId == 2) { Target = target.Floor(); Speed = _data.Speed(2, _health(ParentSlot)); return; }
        Speed = _data.Speed(SubId == 0 ? 0 : 1, _health(ParentSlot));
        if (SubId == 1 && Var03 != 0) return;
        Angle = OracleObjectMovement.Shared.RelativeAngle(Position.Floor(), target.Floor());
        if (SubId == 1) SpawnSibling(1, (Angle - _data.DoubleAngleOffset) & 31);
        if (SubId == 3) { Counter1 = _data.HomingLifetime; Counter2 = _data.HomingTurnInterval; }
    }
    private void SpawnSibling(int subid, int angle)
    {
        if (!_spawn(new(Position.Floor(), subid, ParentSlot, angle, 1, ZHigh)))
            throw new NotSupportedException("vireProjectile.s:@func_6d22 unchecked getFreePartSlot would write outside the PART pool at $e0c0; this overflowing source path is not represented.");
    }
    private void MoveAndAnimate()
    {
        var velocity = NativeObjectMovement.Velocity(_memory, Speed, Angle);
        Position = OracleObjectPosition.FromPixels(Position).Add(velocity.YFixed, velocity.XFixed).PrecisePosition;
        Animation.Advance(); QueueRedraw();
    }
    private void Delete() { Finished = true; Visible = false; }
    public override void _Draw()
    {
        if (Visible && !Finished) DrawTexture(Animation.CurrentTexture,
            Animation.CurrentOffset + Vector2.Down * ZHigh + TransitionDrawOffset);
    }
}

internal sealed record VireProjectileSpawn(Vector2 Position, int SubId, int ParentSlot, int Angle = 0, int Var03 = 0, int ZHigh = 0) : RoomEntitySpawn;
