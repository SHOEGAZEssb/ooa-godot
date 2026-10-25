using Godot;
using System;

namespace Oracles;

internal sealed partial class BlueStalfosProjectile : TransitionOffsetNode2D
{
    private readonly BlueStalfosProfile _data = BlueStalfosProfile.Shared;
    private readonly BlueStalfosProjectileDatabase _parts = BlueStalfosProjectileDatabase.Shared;
    private readonly OracleRoomData _room;
    private readonly OracleRuntimeState _memory;
    private readonly Func<int, BlueStalfosCharacter?> _resolve;
    private readonly Func<bool> _canSplit;
    private readonly Action<BlueStalfosProjectile, int> _spawnSmall;
    private readonly Action<int> _sound;
    private int _collisionSignal = -1;
    private int _lastCollision;
    private bool _healthCleared;
    private bool _collision;
    internal int ParentSlot { get; }
    internal int SubId { get; }
    internal int State { get; private set; }
    internal int Counter1 { get; private set; }
    internal int Counter2 { get; private set; }
    internal int Invincibility { get; private set; }
    internal int Angle { get; private set; }
    internal int Speed { get; private set; }
    internal int ZFixed { get; }
    internal bool NonReflectable { get; private set; }
    internal int CollisionMode { get; private set; } = 0x77;
    internal int DamageQuarters { get; private set; }
    internal int Radius { get; private set; }
    internal bool Finished { get; private set; }
    internal bool CollisionEnabled => _collision && !Finished && _collisionSignal == -1 && Invincibility == 0;
    internal EnemyAnimationPlayer Animation { get; }
    internal Rect2 CollisionBounds => new(Position.Floor() - Vector2.One * Radius, Vector2.One * Radius * 2);
    internal BlueStalfosProjectile(BlueStalfosProjectileSpawn spawn, OracleRoomData room, OracleRuntimeState memory,
        Func<int, BlueStalfosCharacter?> resolve, Func<bool> canSplit,
        Action<BlueStalfosProjectile, int> spawnSmall, Action<int> sound)
    {
        Position = spawn.Position.Floor(); ZFixed = (spawn.Z >> 8) << 8;
        Angle = spawn.Angle; ParentSlot = spawn.ParentSlot; SubId = spawn.SubId;
        _room = room; _memory = memory; _resolve = resolve; _canSplit = canSplit; _spawnSmall = spawnSmall; _sound = sound;
        Radius = _parts.Properties[2] & 15; DamageQuarters = (256 - _parts.Properties[3]) % 256 / 2;
        var visual = _parts.Visual;
        Animation = new(this, visual.Animations.Length);
        Animation.Load(EnemyVisualSource.LoadComposite(visual.Sprites), visual.Animations, visual.TileBase, visual.Palette,
            sourceGrayscaleInverted: visual.SourceGrayscaleInverted, positionedOam: true);
        Visible = false; ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex;
    }
    internal void PublishCollision(int item) { _collisionSignal = item; _lastCollision = item; }
    internal void PublishShieldCollision()
    { PublishCollision(3); Invincibility = -28; _sound(SoundId.SndClink2); }
    internal void ClearHealthAndCollision() { _healthCleared = true; _collision = false; }
    internal void Delete() { Finished = true; Visible = false; _collision = false; }
    internal void UpdateFrame(Vector2 target, int frame)
    {
        if (Finished) return;
        try { UpdateCore(target, frame); }
        finally { if (Invincibility != 0) Invincibility += Invincibility > 0 ? -1 : 1; }
    }
    private void UpdateCore(Vector2 target, int frame)
    {
        if (Finished) return;
        int signal = _collisionSignal >= 0 ? _collisionSignal : _healthCleared && State != 0 ? _lastCollision : -1;
        _collisionSignal = -1;
        if (State == 0) _healthCleared = false;
        if (signal != -1 && SubId == 0 && !NonReflectable)
        {
            if (signal == ItemCollisionType.Link)
            {
                (_resolve(ParentSlot) ?? throw new NotSupportedException("PART$3d baby hit references a deleted/reused parent.")).ProjectileHitLink();
                Delete(); return;
            }
            if (signal is >= ItemCollisionType.L1Sword and <= ItemCollisionType.SwordSpin && State < 4)
            { State = 4; Speed = _data.PartState[3]; _sound(SoundId.SndUnknown3); }
        }
        if (SubId != 0)
        {
            if (State == 0)
            {
                State = 1; _collision = true; CollisionMode = 4;
                Speed = _data.PartState[10]; DamageQuarters = 2; Radius = _data.PartState[12];
                Animation.SetAnimation(4); Visible = true; return;
            }
            if (!InBounds()) { Delete(); return; } MoveAndAnimate(); return;
        }
        var state = _data.PartState;
        switch (State)
        {
            case 0:
                State = 1; Counter1 = state[0]; CopyHigh(new(Position.X, (byte)((int)Position.Y - state[1])));
                Speed = state[2];
                var parent = _resolve(ParentSlot) ?? throw new NotSupportedException("PART$3d initialization references a deleted/reused parent.");
                parent.ProjectilePatternIndex = (parent.ProjectilePatternIndex + 1) & 7;
                NonReflectable = (_data.PartPattern & (1 << parent.ProjectilePatternIndex)) != 0;
                if (NonReflectable) { Speed = state[3]; CollisionMode = 4; }
                _sound(SoundId.SndBlueStalfosCharge); Visible = true; return;
            case 1:
                if (Counter1 != 0) Counter1--;
                if (Counter1 != 0) { Animation.Advance(); return; }
                Counter1 = state[0]; Counter2++;
                if (Counter2 < state[4]) { Animation.SetAnimation(Counter2); return; }
                Counter2 = state[5]; Counter1 = 0; State = 2; _collision = true;
                Angle = OracleObjectMovement.Shared.RelativeAngle(Position, target);
                Animation.SetAnimation(NonReflectable ? 3 : 2); _sound(SoundId.SndBeam1); Animation.Advance(); return;
            case 2:
                Counter2 = (Counter2 - 1) & 255; if (Counter2 == 0) State = 3;
                if (CheckExplode(target, frame)) return; MoveAndAnimate(); return;
            case 3:
                if (CheckExplode(target, frame)) return;
                if (!InBounds()) { Delete(); return; } MoveAndAnimate(); return;
            case 4:
                State = 5; Angle = OracleObjectMovement.Shared.RelativeAngle(Position, target) ^ 16; Animation.Advance(); return;
            case 5:
                var boss = _resolve(ParentSlot);
                if (boss is not null &&
                    RoomEntityManager.ObjectCollisionXYOverlaps(CollisionBounds, boss.CollisionBounds))
                { boss.ReflectedProjectileHit(); Delete(); return; }
                if (!InBounds()) { Delete(); return; } MoveAndAnimate(); return;
            case 6:
                if (!_canSplit()) return;
                for (int index = state[8]; index > 0; index--) _spawnSmall(this, _data.PartAngles[index]);
                _sound(SoundId.SndBeam); Delete(); return;
            default: throw new NotSupportedException($"blueStalfosProjectile.s: state${State:x2}.");
        }
    }
    private bool CheckExplode(Vector2 target, int frame)
    {
        if ((frame & _data.PartState[6]) != 0) return false;
        if (Counter1 != 0) Counter1--;
        if (Counter1 != 0 || Math.Abs((int)target.X - (int)Position.X) + Math.Abs((int)target.Y - (int)Position.Y) >= _data.PartState[7]) return false;
        Counter1 = 255;
        if (!NonReflectable) return false;
        _collision = false; State = 6; return true;
    }
    private bool InBounds()
    {
        int rounded = (Angle & 7) == 0 ? Angle : (Angle & 0x18) + 4;
        int y = ((int)Position.Y + unchecked((sbyte)_data.PartBoundaryOffsets[rounded / 2])) & 255;
        int x = ((int)Position.X + unchecked((sbyte)_data.PartBoundaryOffsets[rounded / 2 + 1])) & 255;
        return x < _room.Width && y < _room.Height && _room.GetTerrainInfo(new(x, y)).Collision != 255;
    }
    private void MoveAndAnimate()
    {
        var velocity = NativeObjectMovement.Velocity(_memory, Speed, Angle);
        Position = OracleObjectPosition.FromPixels(Position).Add(velocity.YFixed, velocity.XFixed).PrecisePosition;
        Animation.Advance(); QueueRedraw();
    }
    private void CopyHigh(Vector2 position) => Position = position.Floor() + Position - Position.Floor();
    public override void _Draw()
    {
        if (Visible && !Finished) DrawTexture(Animation.CurrentTexture,
            Animation.CurrentOffset + Vector2.Down * (ZFixed >> 8) + TransitionDrawOffset);
    }
}
