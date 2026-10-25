using Godot;
using System;

namespace Oracles;

internal sealed partial class BlueStalfosCharacter : EnemyCharacter, ITerrainShadowSource
{
    private readonly BlueStalfosProfile _data = BlueStalfosProfile.Shared;
    private BlueStalfosEnvironment _world = null!;
    private readonly byte[] _differences = new byte[8];
    private Vector2 _lastParentPosition;
    private int _differenceIndex;
    private bool _collision, _dying;
    internal ImportedEnemyDefinition Record { get; private set; }
    internal int Slot { get; set; } = -1;
    internal int ParentSlot { get; private set; } = -1;
    internal int State { get; private set; }
    internal int Counter1 { get; set; }
    internal int Counter2 { get; private set; }
    internal int Speed { get; private set; }
    internal int Angle { get; private set; }
    internal int ZFixed { get; private set; }
    internal int CollisionMode { get; private set; } = 0x48;
    internal Vector2 Target { get; private set; }
    internal int ProjectilePatternIndex { get; set; }
    internal int PuffSlot { get; private set; } = -1;
    internal bool CountTransferred { get; private set; }
    internal bool BossExploded { get; private set; }
    internal int NativeVisible { get; private set; }
    internal override bool InitializationPending => State == 0;
    internal override bool CollisionEnabled => _collision && !IsDead && Health != 0;
    int? ITerrainShadowSource.TerrainShadowZHigh => ZFixed >> 8;
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset + Vector2.Down * (ZFixed >> 8);

    internal void Initialize(ImportedEnemyDefinition record, Vector2 position, BlueStalfosEnvironment world,
        int parentSlot = -1, int z = 0)
    {
        if (record.Id != AgesEnemyId.BlueStalfos || record.SubId is < 0 or > 3)
            throw new NotSupportedException($"blueStalfos.s: ENEMY${record.Id:x2}:${record.SubId:x2}.");
        Record = record; _world = world; ParentSlot = parentSlot; ZFixed = z;
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record), positionedOam: true);
        Visible = false;
        ConfigureSwordKnockback(world.Room, EnemyKnockbackMotion.Terrain, checksHazards: false);
    }
    private void Show(int visible)
    {
        NativeVisible = visible; Visible = (visible & 0x80) != 0; ZIndex = ObjectDrawPriority.FromVisible(visible);
    }
    private void SetInvisible() { NativeVisible &= 0x7f; Visible = false; }
    private void Flicker() { NativeVisible ^= 0x80; Visible = (NativeVisible & 0x80) != 0; }
    internal void InitializeNative()
    {
        if (State != 0) return;
        _world.Random.Next(); _collision = true;
        if (Record.SubId < 2) Show(0x82);
        if (Record.SubId != 0) { State = 8; Speed = Record.SubId; return; }
        State = 1; ZFixed = (-1 << 8) | (ZFixed & 0xff); _world.InitializeRoom();
    }
    internal void UpdateFrame(Vector2 target, int frame)
    {
        if (IsDead) return;
        try
        {
            if (State == 0) { InitializeNative(); return; }
            bool hit = NativeHitPending, recoil = HasActiveKnockback;
            if (!hit && recoil) { BeginFrame(advanceInvincibility: false); return; }
            if (!hit && Health == 0) { UpdateDeath(); return; }
            BeginFrame(advanceInvincibility: false, continueDuringHitAndKnockback: true);
            if (State < 8)
            {
                if (State != 1) return; // Native low-state RET dispatch.
                if (!_world.CanSpawnChildren()) return;
                var main = _world.SpawnChild(this, 1, true) ?? throw new InvalidOperationException("$77 checked main allocation failed.");
                _world.SpawnChild(main, 2, false); _world.SpawnChild(main, 3, false);
                CountTransferred = true; Finish(); return;
            }
            if (Record.SubId == 2) { UpdateSickle(); return; }
            if (Record.SubId == 3) { UpdateAfterimage(); return; }
            if (Record.SubId != 1) throw new InvalidOperationException("$77 spawner reached main state dispatch.");
            UpdateMain(target.Floor());
        }
        finally { AdvanceInvincibilityCounter(); QueueRedraw(); }
    }
    private bool DecCounter1() { Counter1 = (Counter1 - 1) & 255; return Counter1 == 0; }
    private void Move() => Position = ApplyMovementSpeed(OracleObjectPosition.FromPixels(Position), Speed, Angle).PrecisePosition;
    private void CopyHigh(Vector2 position) => Position = position.Floor() + Position - Position.Floor();
    private void UpdateMain(Vector2 target)
    {
        var state = _data.State;
        switch (State)
        {
            case 8:
                if (!_world.Shadow(this)) return;
                State = 9; Speed = state[3]; Angle = state[4]; return;
            case 9:
                Move();
                if ((int)Position.Y != state[5]) { AdvanceAnimation(); return; }
                State = 10; Counter1 = state[6]; Speed = state[7]; _world.BeginBattle();
                PickTarget(target, initial: true); return;
            case 10:
                if (((int)Position.X - (int)Target.X + state[8] & 255) < state[8] * 2 + 1 &&
                    ((int)Position.Y - (int)Target.Y + state[8] & 255) < state[8] * 2 + 1)
                { State = 11; Counter1 = state[9]; }
                else
                {
                    if (Counter1 != 0)
                    {
                        DecCounter1();
                        if ((Counter1 & 3) == 0) Speed = (Speed + state[7]) & 255;
                    }
                    Angle = OracleObjectMovement.Shared.RelativeAngle(Position, Target); Move();
                }
                AdvanceAnimation(); return;
            case 11: if (DecCounter1()) State = 12; AdvanceAnimation(); return;
            case 12:
                if (((int)target.Y - ((int)Position.Y + state[10]) + state[12] & 255) < state[12] * 2 + 1 &&
                    ((int)target.X - ((int)Position.X + state[11]) + state[13] & 255) < state[13] * 2 + 1)
                { BeginSickle(); return; }
                if (!_world.Projectile(this)) return;
                Counter1 = state[14]; State = 14; SetAnimation(2); return;
            case 13:
                AdvanceAnimation();
                if (AnimationParameter == 0xff) FinishAttack(target);
                else if (AnimationParameter == 0) { Animation.SetParameter(8); _world.Sound(SoundId.SndSwordSpin); }
                return;
            case 14:
                if (DecCounter1()) { Counter1 = state[15]; State = 15; SetAnimation(3); }
                else AdvanceAnimation(); return;
            case 15: if (DecCounter1()) FinishAttack(target); return;
            case 16:
                if (DecCounter1()) { Counter1 = state[16]; State = 17; _collision = false; }
                else AdvanceAnimation(); return;
            case 17:
                if (!DecCounter1()) { Flicker(); return; }
                Counter1 = state[18]; State = 18; _collision = true;
                CopyHigh(new(target.X, state[17])); SetAnimation(0); SetInvisible(); return;
            case 18:
                if (!DecCounter1()) { Flicker(); return; }
                Angle = 16; State = 19; Speed = _data.BabySpeeds[_world.Random.Next().Value & 3]; Show(0x82); return;
            case 19:
                if (((int)target.Y - (int)Position.Y & 255) >= 0x18) Move();
                else BeginSickle(); return;
            case 20:
                if (!CreatePuff()) return;
                State = 21; SetNativeCollisionRadii(state[20], state[19]);
                Counter1 = state[21]; Counter2 = 0; Speed = state[22]; SetInvisible();
                _world.Sound(SoundId.SndScentSeed); SetAnimation(4); return;
            case 21:
                if ((_world.PuffParameter(PuffSlot) & 0x80) != 0) return;
                State = 22; CollisionMode = 0x65; ZFixed &= 255; Show(0xc2); return;
            case 22:
                if (DecCounter1())
                {
                    Counter1 = 1; if (!CreatePuff()) return;
                    State = 23; CollisionMode = 0x48; ZFixed = (-1 << 8) | (ZFixed & 255); SetInvisible(); return;
                }
                if (Counter2 != 0) Counter2--;
                if (Counter2 == 0) { Counter2 = state[23]; Angle = _world.Random.Next().Value & 31; }
                Angle = EnemyAdjacentWallResolver.Shared.BounceAngle(Position, Angle,
                    point => _world.Room.IsSolidForEnemyMovement(point, false));
                Move(); AdvanceAnimation(); return;
            case 23:
                if ((_world.PuffParameter(PuffSlot) & 0x80) != 0) return;
                SetNativeCollisionRadii(state[24], state[24]); FinishAttack(target);
                _world.Sound(SoundId.SndScentSeed); Show(0x82); return;
            default: throw new NotSupportedException($"blueStalfos.s main state${State:x2}.");
        }
    }
    private void BeginSickle() { State = 13; SetAnimation(1); }
    private void FinishAttack(Vector2 target)
    {
        State = 10; Counter1 = _data.State[6]; Speed = _data.State[7]; SetAnimation(0); PickTarget(target, initial: false);
    }
    private void PickTarget(Vector2 link, bool initial)
    {
        var random = _world.Random.Next();
        int index = initial ? random.Value & 15 : Quadrant(Position) * 2 + (random.Value & 3);
        int quadrant = _data.Quadrants[index];
        Target = quadrant == Quadrant(link) ? new(link.X, (byte)((int)link.Y - 20)) :
            new((byte)(_data.Targets[quadrant + 1] + (random.Low & 0x30)),
                (byte)(_data.Targets[quadrant] + (random.High & 0x30)));
    }
    private static int Quadrant(Vector2 point) => ((int)point.Y >= 88 ? 4 : 0) | ((int)point.X >= 120 ? 2 : 0);
    private bool CreatePuff()
    {
        int slot = _world.Puff(Position, ZFixed >> 8);
        if (slot < 0) return false; PuffSlot = slot; return true;
    }
    private BlueStalfosCharacter? Parent() => _world.ResolveEnemy(ParentSlot);
    private bool InitializeChild()
    {
        var parent = Parent() ?? throw new NotSupportedException($"blueStalfos.s child initialization has a deleted/reused ENEMY slot${ParentSlot:x2}.");
        State = 9; CollisionMode = 0x66; ZFixed = (parent.ZFixed & 0xff00) | (ZFixed & 255); return true;
    }
    private void UpdateSickle()
    {
        if (State == 8) { InitializeChild(); return; }
        var parent = Parent(); if (parent is null) { Finish(); return; }
        _collision = parent._collision;
        int parameter = parent.AnimationParameter == 0xff ? 12 : parent.AnimationParameter;
        if (parameter + 3 >= _data.SickleBoxes.Count)
            throw new NotSupportedException($"blueStalfos.s sickle animParameter${parameter:x2} reads beyond its table.");
        CopyHigh(new((byte)((int)parent.Position.X + _data.SickleBoxes[parameter + 1]),
            (byte)((int)parent.Position.Y + _data.SickleBoxes[parameter])));
        SetNativeCollisionRadii(_data.SickleBoxes[parameter + 3], _data.SickleBoxes[parameter + 2]);
        if (_data.SickleBoxes[parameter + 3] == 0) _collision = false;
    }
    private void ResetAfterimage(BlueStalfosCharacter parent)
    {
        CopyHigh(parent.Position); _lastParentPosition = parent.Position.Floor(); _differenceIndex = 0;
        Array.Fill(_differences, (byte)0x88);
    }
    private void UpdateAfterimage()
    {
        if (State == 8)
        {
            InitializeChild(); ResetAfterimage(Parent()!); _collision = false; Show(0x83); SetInvisible(); return;
        }
        var parent = Parent(); if (parent is null) { Finish(); return; }
        if (parent.State == 18) { ResetAfterimage(parent); return; }
        if (parent.State >= 20) Show(0x82);
        Vector2 current = parent.Position.Floor();
        _differences[_differenceIndex] = (byte)((((int)current.Y - (int)_lastParentPosition.Y + 8) & 15) << 4 |
            (((int)current.X - (int)_lastParentPosition.X + 8) & 15));
        _lastParentPosition = current; _differenceIndex = (_differenceIndex + 1) & 7;
        int diff = _differences[_differenceIndex];
        if (diff == 0x88) { SetInvisible(); return; }
        CopyHigh(new((byte)((int)Position.X + (diff & 15) - 8), (byte)((int)Position.Y + (diff >> 4) - 8))); Flicker();
    }
    internal void ReflectedProjectileHit() { InvincibilityCounter = _data.PartState[9]; State = 20; }
    internal void ProjectileHitLink() => State = 16;
    internal void PublishDamage(int damage, int invincibility)
    {
        Health = Math.Max(0, Health - Math.Max(0, damage)); DeferNativeHitStatus();
        InvincibilityCounter = invincibility; KnockbackCounter = 0;
        if (Health == 0) _collision = false;
    }
    private void UpdateDeath()
    {
        if (!_dying) { _dying = true; _collision = false; Counter1 = 120; _world.DisableLinkCollisionsAndMenu(); _world.Sound(SoundId.SndBossDead); }
        if (!DecCounter1()) { Flicker(); return; }
        Counter1 = 1; if (!_world.Explosion(this)) return;
        BossExploded = true; _world.RestoreMusic(); Finish();
    }
}
