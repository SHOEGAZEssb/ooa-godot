using Godot;
using System;

namespace Oracles;

// enemyCode07 and its ENEMY$05 hands share native slot references and phase
// signals. Hits advance var35 through the hands/parts, never through boss HP.
internal sealed partial class RamrockCharacter : EnemyCharacter, ITerrainShadowSource
{
    private readonly RamrockProfile _data = RamrockProfile.Shared;
    private RamrockEnvironment _world = null!;
    private EnemyTerrainMovement _movement = null!;
    private int _speedZ, _collisionSignal = -1, _stun, _beep;
    private bool _collision, _dying;
    internal ImportedEnemyDefinition Record { get; private set; }
    internal RamrockEnvironment World => _world;
    internal int Slot { get; set; } = -1;
    internal int ParentSlot { get; private set; } = -1;
    internal int State { get; private set; }
    internal int Substate { get; private set; }
    internal int Subid { get; set; }
    internal int Counter1 { get; private set; }
    internal int Counter2 { get; private set; }
    internal int Angle { get; private set; }
    internal int Speed { get; private set; }
    internal int ZFixed { get; private set; }
    internal int HitCount { get; set; }
    internal int ShieldSignal { get; set; }
    internal int GloveSignal { get; set; }
    internal int AttackParameter { get; set; }
    internal int NativeVisible { get; private set; }
    internal bool HitPending => _collisionSignal >= 0;
    internal bool BossExploded { get; private set; }
    internal int CollisionMode => Record.Id == AgesEnemyId.Ramrock ? 0x0e : 0x0c;
    internal override bool InitializationPending => State == 0;
    internal override bool CollisionEnabled => _collision && !IsDead;
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset + Vector2.Down * (ZFixed >> 8);
    int? ITerrainShadowSource.TerrainShadowZHigh => ZFixed >> 8;
    internal void Initialize(ImportedEnemyDefinition record, Vector2 position, RamrockEnvironment world, int parentSlot = -1)
    {
        Record = record; Subid = record.SubId; ParentSlot = parentSlot; _world = world;
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record), positionedOam: true);
        _movement = new(this, world.Room); Visible = false;
    }
    internal void InitializeNative()
    {
        if (State != 0) return;
        _world.Random.Next(); State = 8; _collision = true;
        if (Record.Id == AgesEnemyId.Ramrock)
        {
            Speed = 0x28; SetAnimation(4); _world.InitializeRoom();
            _world.Shadow(this); Show(0x81); return;
        }
        InitializeHand();
    }
    private void Show(int value)
    { NativeVisible = value; Visible = (value & 0x80) != 0; ZIndex = ObjectDrawPriority.FromVisible(value); }
    private void CopyHigh(Vector2 point) => Position = point.Floor() + Position - Position.Floor();
    private void Move() => Position = ApplyMovementSpeed(OracleObjectPosition.FromPixels(Position), Speed, Angle).PrecisePosition;
    private bool UpdateZ(int acceleration)
    { int z = ZFixed; bool landed = OracleObjectMath.UpdateSpeedZ(ref z, ref _speedZ, acceleration); ZFixed = z; return landed; }
    private bool Dec1() { if (Counter1 != 0) Counter1--; return Counter1 == 0; }
    private bool Dec2() { if (Counter2 != 0) Counter2--; return Counter2 == 0; }
    private void Nudge(Vector2 target)
    {
        int toward = OracleObjectMovement.Shared.RelativeAngle(Position, target), delta = (Angle - toward) & 31;
        if (delta != 0) Angle = (Angle + (delta < 16 ? -1 : 1)) & 31;
    }
    private bool Horizontal()
    {
        if (_movement.MoveAtAngle(Angle, Speed, allowHoles: true)) return true;
        Angle ^= 16; return false;
    }
    internal void PublishCollision(int item, int invincibility = 0)
    { _collisionSignal = item; if (invincibility != 0) InvincibilityCounter = invincibility; }
    internal void ApplyPhaseHit()
    {
        if (InvincibilityCounter != 0) return;
        InvincibilityCounter = 60; HitCount = (HitCount + 1) & 255; _world.Sound(SoundId.SndBossDamage);
    }
    internal void UpdateFrame(Vector2 link, int frame)
    {
        if (IsDead) return;
        try
        {
            if (_stun != 0 && !HitPending && Health != 0)
            {
                int z = ZFixed;
                Position = EnemyStunMotion.Update(Position, State, frame, ref _stun, ref z, ref _speedZ);
                ZFixed = z;
            }
            if (State == 0) { InitializeNative(); return; }
            if (Record.Id == AgesEnemyId.RamrockArms) { UpdateHand(link.Floor()); return; }
            switch (State)
            {
                case 8: UpdateIntro(); return;
                case 9:
                    AdvanceAnimation();
                    if (HitCount < 3) { Horizontal(); return; }
                    HitCount = 0; _speedZ = 0; State = 10; Substate = 0;
                    Subid++; Counter2 = 30; SetAnimation(4); return;
                case 10: UpdateBomb(link.Floor()); StopTouchingBombs(); return;
                case 11: UpdateSeed(link.Floor()); return;
                case 12: UpdateGlove(link.Floor()); return;
                default: throw new NotSupportedException($"ramrock.s: ENEMY$07 state${State:x2}.");
            }
        }
        finally { _collisionSignal = -1; AdvanceInvincibilityCounter(); QueueRedraw(); }
    }
    private void UpdateIntro()
    {
        switch (Substate)
        {
            case 0:
                _world.Memory.SetWramByte(AgesWramAddress.wDisabledObjects, 1);
                _world.Memory.SetWramByte(AgesWramAddress.wMenuDisabled, 1);
                if (_world.EntryActive()) return; _stun = 60; Substate++; return;
            case 1:
                if (_stun != 0) return;
                _speedZ = -0x80; UpdateZ(0); if ((ZFixed >> 8) != -7) return;
                SpawnHand(1); SpawnHand(0); Substate++; return;
            case 2: if (Subid != 2) return; Counter1 = 2; Substate++; _world.Palette(0x84); return;
            case 3:
                if (!Dec1()) return; Substate++;
                _world.Sound(SoundId.SndSwordObtained);
                // playSound leaves H=$c0; the following source "ld l,Enemy.subid /
                // inc (hl)" increments $c082, not this boss's subid. The clean
                // US boss stays at$02 until substate5 explicitly writes$03.
                int volumeAlias = AgesWramAddress.wChannelVolumes + 5;
                _world.Memory.SetWramByte(volumeAlias, unchecked((byte)(_world.Memory.ReadWramByte(volumeAlias) + 1)));
                _world.Palette(0x83); return;
            case 4:
                AdvanceAnimation(); if (AnimationParameter == 0) return;
                Substate++; Angle = 0; SetAnimation(0); goto case 5;
            case 5:
                AdvanceAnimation(); Move(); if ((int)Position.Y >= 0x41) return;
                State++; Angle = 8; Subid = 3; _world.BeginBattle(); return;
            default: throw new NotSupportedException($"ramrock.s: intro substate${Substate:x2}.");
        }
    }
    private void SpawnHand(int subid)
    {
        if (_world.SpawnHand(this, subid) is null)
            throw new NotSupportedException($"ramrock.s: unchecked ENEMY$05:${subid:x2} allocation failed; native slot-alias writes are not represented.");
    }
    private void StopTouchingBombs()
    {
        foreach (var bomb in _world.Bombs())
            if (RoomEntityManager.ObjectCollisionXYOverlaps(CollisionBounds, new(bomb.Position.Floor() - new Vector2(4,4), new(8,8))))
                bomb.StopLateralMovement();
    }
    private void BombChase() { Counter1 = 4; Counter2 = 50; Substate = 3; }
    private void UpdateBomb(Vector2 link)
    {
        switch (Substate)
        {
            case 0:
                UpdateZ(0x10); if (Subid != 6 || !Dec2()) return;
                SpawnHand(2); SpawnHand(3); Substate++; return;
            case 1:
                if (Subid != 7) return;
                AdvanceAnimation(); if (AnimationParameter == 0) return;
                _speedZ = -0x80; Subid = 8; SetAnimation(1); Substate++; return;
            case 2:
                UpdateZ(0); if ((ZFixed >> 8) != -7) return;
                _world.Sound(SoundId.SndSwordObtained); BombChase(); return;
            case 3:
                _beep = (_beep - 1) & 255;
                if (_beep == 255) { _beep = 30; _world.Sound(SoundId.SndBeam1); }
                if (HitCount >= 3) { State++; Substate = 0; HitCount = 0; Subid = 10; SetAnimation(4); return; }
                AdvanceAnimation(); _movement.MoveAtAngle(Angle, Speed, true);
                if (Dec2()) { Substate++; SetAnimation(2); return; }
                if (Dec1()) { Counter1 = 4; Nudge(link); } return;
            case 4:
                AdvanceAnimation(); Subid = 8;
                if (AnimationParameter == 1) { Subid = 9; _world.Sound(SoundId.SndStrongPound); return; }
                if ((AnimationParameter & 0x80) != 0) { SetAnimation(1); BombChase(); } return;
            default: throw new NotSupportedException($"ramrock.s: bomb substate${Substate:x2}.");
        }
    }
    private bool MoveHome()
    {
        Vector2 home = new(0x78, 0x48);
        if (Position.Floor() == home) return true;
        Angle = OracleObjectMovement.Shared.RelativeAngle(Position, home); Move(); return false;
    }
    private void SeedResume() { Substate = 2; Counter2 = 120; SetAnimation(0); }
    private void UpdateSeed(Vector2 link)
    {
        // Clean US excludes collisions during setup substates0/1.
        if (Substate >= 2)
        {
            bool seed = _collisionSignal is >= ItemCollisionType.MysterySeed and <= ItemCollisionType.GaleSeed;
            if (seed && InvincibilityCounter == 0)
            {
                InvincibilityCounter = 60;
                if (HitCount == 3) { Subid = 16; State++; Substate = 0; HitCount = 0; return; }
                HitCount++; _world.Sound(SoundId.SndBossDamage);
            }
            else if (_collisionSignal >= 0) { Subid = 13; ShieldSignal = 16; }
            else if (ShieldSignal != 0 && --ShieldSignal == 0) Subid = 12;
        }
        switch (Substate)
        {
            case 0:
                if (!MoveHome()) return;
                Subid++; Counter2 = 2; SpawnHand(4); SpawnHand(5); Substate++; return;
            case 1:
                if (Subid != 12) return;
                if (Counter2 != 0)
                {
                    if (Dec2()) { Angle = 8; _world.Palette(0x83); _world.Sound(SoundId.SndSwordObtained); }
                    return;
                }
                AdvanceAnimation(); if (AnimationParameter != 0) { Subid = 12; SeedResume(); } return;
            case 2:
                AdvanceAnimation(); Horizontal();
                if ((_world.Random.Next().Value & 1) == 0 || !Dec2() || Subid != 12) return;
                if ((_world.Random.Next().Value & 3) == 0) { Subid = 14; Angle = 24; Substate++; }
                else { Subid = 15; Substate = 6; Counter1 = 60; SpawnPart(0x4f, 0, new(0,16)); }
                SetAnimation(0); return;
            case 3:
                if (Subid != 14) { SeedResume(); return; }
                if (Horizontal()) return;
                Substate++; Counter1 = 180; Counter2 = 30; SpawnPart(0x34, 14, new(0,4)); return;
            case 4:
                if (Subid != 14) { SeedResume(); return; }
                if (!Dec2()) return;
                if (!Dec1()) { if ((Counter1 & 7) == 0) _world.Sound(SoundId.SndSwordBeam); Horizontal(); return; }
                Substate++; Counter1 = 90; Subid = 12; goto case 5;
            case 5: if (Dec1()) SeedResume(); return;
            case 6: if (Subid != 15 || Dec1()) { Subid = 12; SeedResume(); } return;
            default: throw new NotSupportedException($"ramrock.s: seed substate${Substate:x2}.");
        }
    }
    private void SpawnPart(int id, int subid, Vector2 offset)
    {
        if (!_world.SpawnPart(new(id, subid, Position.Floor() + offset, ZFixed, Slot)))
            throw new NotSupportedException($"ramrock.s: unchecked PART${id:x2} allocation failed; source field writes are not represented.");
    }
    private void GloveResume()
    { _speedZ = -0x80; Subid = 17; Substate = 3; Angle = 8; Counter2 = 120; }
    private void UpdateGlove(Vector2 link)
    {
        switch (Substate)
        {
            case 0:
                if (!MoveHome()) return;
                Substate++; SpawnPart(0x35, 1, new(-32,24)); SpawnPart(0x35, 0, new(32,24)); SetAnimation(4); return;
            case 1:
                UpdateZ(0x10); if (GloveSignal != 3) return;
                Substate++; Counter2 = 2; _world.Palette(0x84); return;
            case 2:
                if (!Dec2()) { _world.Sound(SoundId.SndSwordObtained); _world.Palette(0x83); }
                AdvanceAnimation(); if (AnimationParameter != 0) return;
                SetAnimation(3); GloveResume(); return;
            case 3:
                AdvanceAnimation(); if ((ZFixed >> 8) != -7) UpdateZ(0);
                int y = (int)Position.Y;
                if (y < (int)link.Y && (int)link.Y - y >= 64)
                { if ((int)link.Y - y > 64 && y < 0x50) GivenVertical(16); }
                else if (y >= 0x41) GivenVertical(0);
                if (Dec2() && Math.Abs((int)link.X - (int)Position.X) + Math.Abs((int)link.Y - (int)Position.Y) < 0x50)
                {
                    Subid = 18; AttackParameter = (_world.Random.Next().Value & 1) << 4;
                    if ((_world.Random.Next().Value & 15) == 0) AttackParameter |= 32;
                    Substate = 4; return;
                }
                if ((int)Position.X > 0xc0) { CopyHigh(new(0xc0, Position.Y)); Angle ^= 16; }
                else if ((int)Position.X <= 0x28) { CopyHigh(new(0x29, Position.Y)); Angle ^= 16; }
                Move(); return;
            case 4:
                if (HitCount == 3) { Health = 0; UpdateDeath(); return; }
                AdvanceAnimation(); if (GloveSignal == 3) GloveResume(); return;
            default: throw new NotSupportedException($"ramrock.s: glove substate${Substate:x2}.");
        }
    }
    private void GivenVertical(int angle) => Position = ApplyMovementSpeed(OracleObjectPosition.FromPixels(Position), 0x14, angle).PrecisePosition;
    private void UpdateDeath()
    {
        if (!_dying) { _dying = true; _collision = false; Counter1 = 120; _world.DisableLinkCollisionsAndMenu(); _world.Sound(SoundId.SndBossDead); }
        if (!Dec1()) { Show(NativeVisible ^ 0x80); return; }
        Counter1 = 1; if (!_world.Explosion(this)) return;
        BossExploded = true; Finish();
    }
}
