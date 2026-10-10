using Godot;
using System;

namespace oracleofages;

// ENEMY$7d owns body state; $cfd0..$cfd7 are views of the shared WRAM union,
// not a detached save-state copy. Subid$02 retains its separate collision page.
internal sealed partial class OctogonCharacter : EnemyCharacter
{
    private readonly OctogonBehaviorProfile _data = OctogonBehaviorProfile.Shared;
    private OracleRandom _random = null!;
    private OracleRuntimeState _memory = null!;
    private OctogonRoomEnvironment _world = null!;
    private bool _underwaterPalette;
    private bool _deathStarted;
    internal ImportedEnemyDefinition Record { get; private set; }
    internal int Slot { get; set; } = -1;
    internal int RelatedSlot { get; private set; } = -1;
    internal int State { get; private set; }
    internal int Depth { get; private set; }
    internal int Direction { get; private set; }
    internal int Angle { get; private set; }
    internal int Speed { get; private set; }
    internal int Counter1 { get; private set; }
    internal int Counter2 { get; private set; }
    internal int TargetIndex { get; private set; }
    internal Vector2 Target { get; private set; }
    internal Vector2 Origin { get; private set; }
    internal int SwimCounter { get; private set; }
    internal int AttackCounter { get; private set; }
    internal int EntryHealth { get; private set; }
    internal int ZFixed { get; private set; }
    // getFreeEnemySlot leaves the cleared collision byte inactive until
    // enemyStandardUpdate loads properties on the slot's first dispatch.
    internal int CollisionType { get; private set; }
    internal int RadiusY { get; private set; }
    internal int RadiusX { get; private set; }
    internal bool ShellForm => Record.SubId == 2;
    internal bool SurfaceRoom => Record.SubId == 0;
    internal override bool InitializationPending => State == 0;
    internal override bool CollisionEnabled => !IsDead && (CollisionType & 0x80) != 0;
    public override Rect2 CollisionBounds => new(Position.Floor()-new Vector2(RadiusX,RadiusY),new(RadiusX*2,RadiusY*2));
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset+Vector2.Down*(ZFixed >> 8);
    internal override Texture2D CurrentDrawTexture => _underwaterPalette && !DrawsDamagePalette
        ? Animation.CurrentTextureForPalette(6) : base.CurrentDrawTexture;

    internal void Initialize(ImportedEnemyDefinition record,Vector2 position,OracleRandom random,
        OracleRuntimeState memory,OctogonRoomEnvironment world,int parent = -1)
    {
        if (record.Id != EnemyId.Octogon || record.SubId is < 0 or > 2)
            throw new NotSupportedException($"octogon.s: ENEMY${record.Id:x2}:${record.SubId:x2}.");
        Record = record; _random = random; _memory = memory; _world = world; RelatedSlot = parent;
        // octogon.s:@subid1_1 selects palette$06 for the underwater body.
        // Load that draw variant as well as the ordinary and damage frames.
        // enemy7dOamDataPointers includes signed cells outside a fixed 32x32
        // canvas. Retain their full bounds and original draw offsets.
        InitializeEnemy(position.Floor(),EnemyCharacterConfiguration.FromImported(record),
            paletteOverrides:_data.Palettes,positionedOam:true,paletteVariants:[6]);
        RadiusX = record.RadiusX; RadiusY = record.RadiusY; Visible = false;
    }
    internal void InitializeNative(bool scrolling,Vector2 link,Vector2 target)
    {
        if (State != 0 || IsDead) return;
        // enemyStandardUpdate repeats these properties/RNG on state-zero
        // retries. Do not move the boss-room side effects after allocation.
        // loadGraphics.s writes the ENEMY ID into collisionType, with the
        // imported properties' enable bit; mode$4e is a separate byte.
        _random.Next(); Health = Record.Health; CollisionType = EnemyId.Octogon|0x80; RestartAnimation(0);
        if (ShellForm) { Speed = 0x67; State = 8; SaveAndOffset(); return; }
        if (Read(0) == 0) { Write(0,1); _world.SetActiveMusic(0xff); _world.InitializeBossRoom(scrolling); }
        int child = _world.CreateShell(this);
        if (child < 0) { SaveAndOffset(); return; }
        RelatedSlot = child; State = 8; Speed = _data.Speed;
        SwimCounter = _data.SwimAnimationWait; AttackCounter = _data.InitialWait; Show(3);
        TargetIndex = Read(6); CopyHigh(new(Read(5),Read(4))); Health = EntryHealth = Read(3);
        Direction = Read(2); Depth = Read(1);
        if (Depth is < 0 or > 1) throw new NotSupportedException($"octogon.s: persisted depth${Depth:x2} in $cfd1.");
        if (SurfaceRoom)
        {
            FixSurfacePosition(link);
            if (Depth == 0)
            {
                Counter2 = _data.InitialWait;
                if (TargetIndex == 0xff) ChooseTarget();
                else if (!LoadTarget()) { Direction = _data.Targets[TargetIndex*3-1]; SetDirectionAnimation(); }
            }
            else { Direction = 0x12; SetDirectionAnimation(); }
        }
        else if (Depth == 0)
        {
            CollisionType &= 0x7f; Related()?.DeleteNative(); Visible = false;
        }
        else
        {
            Write(7,1); _underwaterPalette = true;
            Counter1 = _data.WaterWait; Counter2 = _data.UnderwaterPhaseWait; Target = target.Floor();
            Angle = Aim(target); Direction = ((Angle+4)&0x18)>>1; SetDirectionAnimation();
            _world.Interaction(Position.Floor(),0x91,1,Slot);
        }
        SaveAndOffset();
    }
    internal void UpdateFrame(RoomEntityFrame frame)
    {
        if (IsDead) return;
        Vector2 target = (frame.ScentSeedTarget ?? frame.Player.Position).Floor();
        if (State == 0) { InitializeNative(false,frame.Player.Position,target); return; }
        bool hit = NativeHitPending;
        bool recoil = HasActiveKnockback;
        int sharedInvincibility = InvincibilityCounter;
        BeginFrame(continueDuringHitAndKnockback:true);
        if (hit || recoil)
        {
            // @justHit copies before bank0's per-enemy post-update decrement.
            // A later shell slot subsequently decrements its own copied byte.
            if (Related() is { } related) related.InvincibilityCounter = sharedInvincibility;
            if (!ShellForm)
            {
                if (((EntryHealth-Health)&255) >= _data.PhaseDamage) Counter2 = 1;
                if (Health == 0) { _world.MarkBothBossRooms(); _world.SetActiveMusic(SoundId.MusBoss); return; }
            }
        }
        else if (Health == 0)
        {
            if (ShellForm) { DeleteNative(); return; }
            if (CollisionType != 0) KillShell();
            BossDeath(); return;
        }
        if (ShellForm) { UpdateShell(); return; }
        CopyHigh(Origin);
        if (SurfaceRoom) { if (Depth == 0) Surface(target,frame.Player.Position,frame.Counter); else SurfaceSubmerged(target); }
        else { if (Depth == 0) UnderwaterHidden(); else Underwater(target,frame.Counter); }
        SaveAndOffset();
    }
    internal bool Damage(Vector2 origin,int damage)
    {
        if (!CollisionEnabled || NativeHitPending || InvincibilityCounter != 0) return false;
        Health = Math.Max(0,Health-Math.Max(0,damage));
        if (Health == 0) CollisionType &= 0x7f;
        ApplySwordNoKnockback(origin,EnemyKnockbackStrength.Normal); DeferNativeHitStatus(); return true;
    }
    internal bool Deflect()
    {
        if (!AcceptArmoredSwordHit(20)) return false;
        DeferNativeHitStatus(); return true;
    }
    internal void PublishCollision() => DeferNativeHitStatus();
    internal void SeedCollision(Vector2 origin)
    { KnockbackCounter = 0; KnockbackAngle = Aim(origin)^16; DeferNativeHitStatus(); }
    private void UpdateShell()
    {
        var parent = Related() ?? throw new NotSupportedException("octogon.s: live shell lost its related ENEMY page.");
        int direction = parent.Direction >= 0x10 ? 8 : parent.Direction;
        int index = direction & 0x0c;
        RadiusY = _data.Shell[index]; RadiusX = _data.Shell[index+1];
        CopyHigh(parent.Position.Floor()+new Vector2(unchecked((sbyte)_data.Shell[index+3]),unchecked((sbyte)_data.Shell[index+2])));
        ZFixed = unchecked((short)((parent.ZFixed&0xff00)|(ZFixed&255)));
        // The source pops the dispatch return and skips SaveAndOffset.
    }
    private void Surface(Vector2 target,Vector2 link,int frame)
    {
        switch (State)
        {
            case 8:
                if (_world.ShutterSignal() != 0) return;
                State = 9;
                if (_world.ActiveMusic() != 0) { _world.SetActiveMusic(0); _world.Sound(SoundId.MusBoss); }
                return;
            case 9:
                DecAttack();
                if ((frame&3) == 0 && DecPhase()) { State = 0x0d; CollisionType &= 0x7f; Depth = 1; Counter1 = _data.SubmergeWait; AttackCounter = _data.WaterWait; Direction = 0x15; SetDirectionAnimation(); return; }
                if (((byte)(link.Y-Position.Floor().Y+_data.AlignmentRadius) < _data.AlignmentRadius*2+1 ||
                    (byte)(link.X-Position.Floor().X+_data.AlignmentRadius) < _data.AlignmentRadius*2+1) && AttackCounter == 0)
                {
                    AttackCounter = _data.InitialWait;
                    if (((Direction&12)*2) == ((Aim(target)+0x14)&0x18)) { State = 11; Counter1 = _data.FireWindup; return; }
                }
                Swim(); return;
            case 10: PauseTarget(); return;
            case 11: case 15: Turn(_data.TurnWait); return;
            case 12: Turn(_data.TurnPause); return;
            case 13:
                if (!Dec()) return; Counter1 = _data.FireWindup; State++; Direction += 2; SetDirectionAnimation(); return;
            case 14:
                if (!Dec()) return; Counter1 = _data.FireRecovery; State++; Direction++; SetDirectionAnimation(); Fire(0x18); return;
            case 16: Turn(_data.SwimAnimationWait); return;
            case 17: if (Dec()) State = 9; return;
            default: throw Unsupported();
        }
    }
    private void SurfaceSubmerged(Vector2 target)
    {
        switch (State)
        {
            case 8:
                AdvanceAnimation();
                if (AnimationParameter == 0) { Animation.SetParameter(1); _world.Sound(SoundId.SndLinkSwim); }
                if (!DecAttack()) { MoveTarget(); return; }
                AttackCounter = _data.WaterWait;
                if (_random.Next().Value >= _data.SurfaceFireChance) { MoveTarget(); return; }
                State = 10; Counter1 = _data.AttackWait; SubmergedAnimation(); return;
            case 9: PauseTarget(); AdvanceAnimation(); return;
            case 10:
                if (Dec()) { Counter1 = _data.FireWindup; State++; SubmergedAnimation(); }
                else if ((Counter1&7) == 0) { Direction ^= 1; SetDirectionAnimation(); }
                return;
            case 11:
                if (Dec()) { Counter1 = _data.AttackWait; State++; DepthCharge(); SubmergedAnimation(); }
                else if (Counter1 == 6) { Direction = 0x14; SetDirectionAnimation(); }
                return;
            case 12:
                if (Dec()) { AttackCounter = _data.WaterWait; State = 8; } AdvanceAnimation(); return;
            case 13:
                if (Dec()) { Counter1 = _data.TargetWait; State = 8; SubmergedAnimation(); } return;
            default: throw Unsupported();
        }
    }
    private void Underwater(Vector2 target,int frame)
    {
        switch (State)
        {
            case 8:
                if (DecAttack())
                {
                    AttackCounter = _data.WaterWait;
                    if ((_random.Next().Value&1) != 0 && ((Aim(target)+4)&0x18) == ((Angle+4)&0x18))
                    { State = 11; Counter1 = _data.FireWindup; Direction = (Direction&12)+2; SetDirectionAnimation(); return; }
                }
                if ((frame&3) == 0 && DecPhase())
                { State = 9; Depth = 0; Counter1 = _data.TargetWait; AttackCounter = _data.WaterWait; Direction = 0x10; SetDirectionAnimation(); return; }
                if (Dec()) { Counter1 = _data.AttackWait; State = 9; return; }
                Swim(); return;
            case 9:
                if (!Dec()) return;
                Counter1 = _data.SwimAnimationWait; State = 10; Target = target.Floor(); Angle = Aim(target);
                int difference = (Angle-(Direction&12)*2)&31;
                if (((difference-4)&255) < 24) { Direction = (Direction+((difference&16) == 0 ? 4 : 12))&12; SetDirectionAnimation(); }
                return;
            case 10:
                if (!Dec()) return; Counter1 = _data.WaterWait; State = 8; Direction = ((Angle+4)&0x18)>>1; SetDirectionAnimation(); return;
            case 11:
                if (!Dec()) return; Counter1 = _data.AttackWait; State = 12; Direction++; SetDirectionAnimation(); Fire(0x55); Splash(); return;
            case 12:
                if (!Dec()) return; State = 8; Direction &= 12; SetDirectionAnimation(); return;
            default: throw Unsupported();
        }
    }
    private void UnderwaterHidden()
    {
        switch (State)
        {
            case 8: if (Dec()) { Counter1 = _data.InitialWait; DepthCharge(); } return;
            case 9:
                if (!Dec()) return; State = 10; Direction = 0x11; SetDirectionAnimation(); _world.Sound(SoundId.SndEnemyJump);
                _world.Part(new BossShadowSpawn(() => Position,() => ZFixed>>8,() => !IsDead,2,8)); return;
            case 10:
                ZFixed = unchecked((short)(ZFixed-_data.RiseZStep));
                int high = (ZFixed>>8)&255;
                if (high >= 0xd0) return;
                if (high != 0xc0) { Visible = !Visible; return; }
                ZFixed &= 255; State = 8; CollisionType &= 0x7f; Counter1 = _data.AttackWait; Visible = false; KillShell(); return;
            default: throw Unsupported();
        }
    }
    private void Turn(int wait)
    { if (!Dec()) return; Counter1 = wait; State++; Direction = (Direction+4)&12; SetDirectionAnimation(); }
    private void Swim()
    {
        MoveTarget(); SwimCounter = (SwimCounter-1)&255;
        if (SwimCounter != 0) return;
        SwimCounter = _data.SwimAnimationWait; Direction ^= 1; SetDirectionAnimation();
        if (SurfaceRoom) Splash(); else _world.Sound(SoundId.SndLinkSwim);
    }
    private void MoveTarget()
    {
        if ((byte)(Position.Floor().X-Target.X+1) < 3 && (byte)(Position.Floor().Y-Target.Y+1) < 3)
        { CopyHigh(Target); State++; Counter1 = _data.TargetWait; return; }
        Angle = Aim(Target);
        var velocity = NativeObjectMovement.Velocity(_memory,Speed,Angle);
        Position = OracleObjectPosition.FromPixels(Position).Add(velocity.YFixed,velocity.XFixed).PrecisePosition;
    }
    private void PauseTarget()
    { if (!Dec()) return; State--; TargetIndex = (TargetIndex+1)&255; if ((TargetIndex&7) == 0) ChooseTarget(); else LoadTarget(); }
    private void ChooseTarget() { TargetIndex = _random.Next().Value&0x18; LoadTarget(); }
    private bool LoadTarget()
    {
        int index = (TargetIndex*3)&255;
        if (index+2 >= _data.Targets.Count) throw new NotSupportedException($"octogon.s: target index${TargetIndex:x2} reads beyond the target table.");
        Target = new(_data.Targets[index+1],_data.Targets[index]);
        if (Depth != 0 || (_data.Targets[index+2]&128) != 0) return false;
        Direction = _data.Targets[index+2]; SetDirectionAnimation(); return true;
    }
    private void FixSurfacePosition(Vector2 link)
    {
        if (Read(7) == 0) return;
        Write(7,0);
        var own = Closest(Position); var player = Closest(link);
        int index = own.Index;
        // Original compares A, not E: the center-region helper returns X in A.
        if (player.A == index) index = _data.Compensation[index];
        int row = index*4;
        CopyHigh(new(_data.SurfacePositions[row+1],_data.SurfacePositions[row])); TargetIndex = _data.SurfacePositions[row+2];
        if ((TargetIndex&128) != 0) ChooseTarget(); else LoadTarget();
    }
    private (int Index,int A) Closest(Vector2 point)
    {
        int y = (byte)OracleObjectPosition.HighByte(point.Y),x = (byte)OracleObjectPosition.HighByte(point.X);
        int index = (y < _data.State[17] ? 0 : y < _data.State[18] ? 3 : 6)+(x < _data.State[19] ? 0 : x < _data.State[20] ? 1 : 2);
        if (index != 4) return (index,index);
        return ((y < _data.State[21] ? 0 : 6)+(x < _data.State[22] ? 0 : 2),x);
    }
    private void Fire(int id)
    {
        int index = (Direction&12)>>1;
        Vector2 position = Position.Floor()+new Vector2(unchecked((sbyte)_data.ProjectileOffsets[index+1]),unchecked((sbyte)_data.ProjectileOffsets[index]));
        RoomEntitySpawn shot = id == 0x18 ? new OctorokRockSpawn(position,(Direction&12)*2) :
            new OctogonPartSpawn(position,id,0,Slot,(Direction&12)*2,ZFixed>>8);
        if (_world.Part(shot)) _world.Sound(SoundId.SndStrike);
    }
    private void DepthCharge() => _world.Part(new OctogonPartSpawn(Position.Floor(),0x48,0,Slot,Angle,ZFixed>>8));
    private void Splash() { _world.Sound(SoundId.SndSwordSpin); _world.Interaction(Position.Floor(),0x8e,Direction&12,-1); }
    private void SubmergedAnimation() { Direction = 0x12; SetDirectionAnimation(); }
    // ecom_decCounter1 wraps $00->$ff, including state8 after bubble recovery.
    private bool Dec() { Counter1 = (Counter1-1)&255; return Counter1 == 0; }
    private bool DecAttack() { if (AttackCounter != 0) AttackCounter--; return AttackCounter == 0; }
    private bool DecPhase() { Counter2 = (Counter2-1)&255; return Counter2 == 0; }
    private int Aim(Vector2 target) => OracleObjectMovement.Shared.RelativeAngle(Position.Floor(),target.Floor());
    private int Read(int offset) => _memory.ReadWramByte(0xcfd0+offset);
    private void Write(int offset,int value) => _memory.SetWramByte(0xcfd0+offset,(byte)value);
    private void CopyHigh(Vector2 point) => Position = new((byte)OracleObjectPosition.HighByte(point.X)+Position.X-Position.Floor().X,
        (byte)OracleObjectPosition.HighByte(point.Y)+Position.Y-Position.Floor().Y);
    private void SaveAndOffset()
    {
        Origin = Position.Floor(); int index = ((Direction >= 16 ? 8 : Direction)&12)>>1;
        CopyHigh(Origin+new Vector2(unchecked((sbyte)_data.BodyOffsets[index+1]),unchecked((sbyte)_data.BodyOffsets[index])));
        Write(1,Depth); Write(2,Direction); Write(3,Health); Write(4,(int)Origin.Y); Write(5,(int)Origin.X); Write(6,TargetIndex);
    }
    private void SetDirectionAnimation() => RestartAnimation(Direction);
    private void Show(int priority) { Visible = true; ZIndex = ObjectDrawPriority.FromVisible(priority); }
    private OctogonCharacter? Related() => _world.Resolve(RelatedSlot);
    private void KillShell() { if (Related() is { } shell) { shell.Health = 0; shell.CollisionType &= 0x7f; } }
    private void DeleteNative() { CollisionType = 0; Finish(); }
    private void BossDeath()
    {
        if (!_deathStarted) { _deathStarted = true; CollisionType = 0; Counter1 = _data.DeathWait; _world.DisableLinkCollisionsAndMenu(); _world.Sound(SoundId.SndBossDead); }
        if (!Dec()) { Visible = !Visible; return; }
        Counter1 = 1;
        if (!_world.Part(new BossDeathExplosionSpawn(Position.Floor(),EnemyId.Octogon,ZFixed>>8))) return;
        _world.RestoreMusic(); DeleteNative();
    }
    private NotSupportedException Unsupported() => new($"octogon.s: subid${Record.SubId:x2} depth${Depth:x2} state${State:x2}.");
    internal override bool TakeSwordHit(Vector2 source,int damage) => Damage(source,damage);
    internal override bool TakeBurnHit(int damage) => Damage(Position,damage);
}
