using Godot;
using System;

namespace oracleofages;

// ENEMY_VIRE $75, both native forms. State, health and children follow their
// ENEMY pages; projectiles and puffs resolve live pointers through room owners.
internal sealed partial class VireCharacter : EnemyCharacter
{
    private readonly VireBehaviorProfile _data = EnemyBehaviorTables.Shared.Vire;
    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private OracleRuntimeState _memory = null!;
    private VireRoomEnvironment _world = null!;
    private EnemyTerrainMovement _movement = null!;
    private bool _collision;
    private bool _bossDeathStarted;
    internal ImportedEnemyDefinition Record { get; private set; }
    internal int State { get; private set; }
    internal int Substate { get; private set; }
    internal int Counter1 { get; set; }
    internal int Counter2 { get; private set; }
    internal int Angle { get; private set; }
    internal int Speed { get; private set; }
    internal int Direction { get; private set; }
    internal int ZFixed { get; private set; }
    internal int OrbitOffset { get; private set; }
    internal int FireAnimationCounter { get; private set; }
    internal int PreviousHealth { get; private set; }
    internal int ChildrenAlive { get; private set; }
    internal int HealthText { get; private set; }
    internal int FairySpawned { get; private set; }
    internal Vector2 BatTarget { get; private set; }
    internal int ParentSlot { get; private set; } = -1;
    internal int Slot { get; set; } = -1;
    internal int Var03 { get; private set; }
    internal int PuffSlot { get; private set; } = -1;
    internal bool MainForm => Record.SubId == 0;
    internal bool FreezesLink => MainForm && State == 8 && Substate != 0;
    internal override bool InitializationPending => State == 0;
    internal override bool CollisionEnabled => _collision && !IsDead && Health != 0;
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset + Vector2.Down * (ZFixed >> 8);
    internal void Initialize(ImportedEnemyDefinition record, OracleRoomData room, Vector2 position,
        OracleRandom random, OracleRuntimeState memory, VireRoomEnvironment world,
        int parentSlot = -1, int batIndex = 0, int zHigh = 0)
    {
        if (record.Id != EnemyId.Vire || record.SubId is < 0 or > 1)
            throw new NotSupportedException($"vire.s: ENEMY${record.Id:x2} subid${record.SubId:x2}.");
        Record = record; _room = room; _random = random; _memory = memory; _world = world;
        ParentSlot = parentSlot; Var03 = batIndex;
        if (!MainForm) OrbitOffset = _data.BatOffsets[batIndex].Value;
        ZFixed = unchecked((short)(zHigh << 8));
        InitializeEnemy(position.Floor(),EnemyCharacterConfiguration.FromImported(record));
        _movement = new(this,room); Visible = false;
    }
    internal void InitializeNative(bool scrolling)
    {
        if (State != 0) return;
        _random.Next(); // enemyStandardUpdate initializes var3d first.
        State = 8; Speed = _data.InitialSpeed;
        if (MainForm) { ZFixed = -4 * 256; _world.InitializeBossRoom(scrolling); }
    }
    internal bool Damage(Vector2 origin,int damage)
    {
        if (!CollisionEnabled || InvincibilityCounter != 0 || NativeHitPending) return false;
        // ENEMYDMG_0c publishes JUST_HIT before NO_HEALTH. The species owns
        // the zero-HP split; ordinary EnemyCharacter death cannot run here.
        Health = Math.Max(0,Health - Math.Max(0,damage));
        // collisionEffects.s:applyDamageToEnemyOrPart clears collisionType
        // bit7 on lethal damage. Keep it cleared when @subid0Dead restores
        // HP$01 for the split, hidden wait and farewell.
        if (Health == 0) _collision = false;
        ApplySwordNoKnockback(origin,EnemyKnockbackStrength.Normal);
        DeferNativeHitStatus();
        return true;
    }
    internal void PublishCollision() => DeferNativeHitStatus();
    internal void SeedCollision(Vector2 origin)
    {
        KnockbackCounter = 0;
        KnockbackAngle = OracleObjectMovement.Shared.RelativeAngle(Position.Floor(),origin.Floor()) ^ 16;
        DeferNativeHitStatus();
    }
    internal void UpdateFrame(RoomEntityFrame frame)
    {
        if (IsDead) return;
        if (State == 0) { InitializeNative(false); return; }
        bool hit = NativeHitPending || HasActiveKnockback;
        BeginFrame(continueDuringHitAndKnockback:true);
        if (hit)
        {
            if (!MainForm) { if (Health == 0) return; }
            else if (Health != PreviousHealth)
            {
                PreviousHealth = Health;
                if (Health != 0) { State = 14; Substate = 0; }
                return;
            }
        }
        else if (Health == 0)
        {
            if (!MainForm)
            {
                _world.Puff(Position.Floor(),ZFixed >> 8,0);
                var parent = Parent();
                parent.ChildrenAlive = (parent.ChildrenAlive - 1) & 255;
                if (parent.ChildrenAlive == 0) parent.CopyHighPosition(Position,ZFixed >> 8);
                Finish(); return;
            }
            if (State == 15) { BossDeath(); return; }
            State = 15; Substate = 0; Counter1 = _data.SplitWait;
            Health = 1; Direction = 0; RestartAnimation(0);
        }
        if (State < 8) return; // native common stub states.
        Vector2 target = (frame.ScentSeedTarget ?? frame.Player.Position).Floor();
        if (!MainForm) { UpdateBat(target); return; }
        if (Direction != 0 && ++FireAnimationCounter >= _data.FireAnimation)
        { Direction = 0; FireAnimationCounter = 0; RestartAnimation(0); }
        switch (State)
        {
            case 8: Intro(target,frame.Player.IsDying); return;
            case 9:
                Dec(); if (Counter1 == 0) State = _data.Behaviors[(Health >= _data.HighHealth ? 0 : Health >= _data.MediumHealth ? 4 : 8) + (_random.Next().Value & 7)].Value;
                return;
            case 10: Charge(target); return;
            case 11: case 13: CircleAttack(target,frame.Player.NativeItemUseActive,frame.Counter); return;
            case 12: Creep(); return;
            case 14: Retreat(target); return;
            case 15: SplitAndLeave(); return;
            default: throw new NotSupportedException($"vire.s: main state${State:x2}.");
        }
    }
    private void Intro(Vector2 target,bool dying)
    {
        switch (Substate)
        {
            case 0:
                if ((byte)((int)target.Y - 0x38) >= 0x41 || (byte)((int)target.X - 0x50) >= 0x51 || dying) return;
                int puff = _world.Puff(Position.Floor(),ZFixed >> 8,2);
                if (puff < 0) return;
                PuffSlot = puff; Substate = 1; _world.DisableLinkCollisionsAndMenu(); return;
            case 1:
                if ((_world.PuffParameter(PuffSlot) & 0x80) == 0) return;
                Substate = 2; Counter1 = _data.IntroWait; ShowNative(); return;
            case 2:
                Dec(); if (Counter1 != 0) { AdvanceAnimation(); return; }
                Substate = 3; _world.ShowText(_world.Linked() ? 0x2f13 : 0x2f12,Position); return;
            case 3:
                if (_world.Puff(Position.Floor(),ZFixed >> 8,0) < 0) return;
                State = 9; Substate = 0; Counter1 = _data.HiddenWait; PreviousHealth = Health;
                Visible = false; _world.EnableLinkCollisionsAndMenu(); _world.BeginBattle(); return;
            default: throw UnsupportedSubstate();
        }
    }
    private void Charge(Vector2 target)
    {
        if (Substate == 0) { SpawnOutside(); Counter1 = _data.RetreatWait; Speed = _data.ApproachSpeed; return; }
        if (Substate == 1)
        {
            Dec(); if (Counter1 != 0) { MoveAnimate(); return; }
            Substate = 2; Speed = _data.ChargeSpeed;
            Angle = (Aim(target) + (_random.Next().Value & 3) - 2) & 31;
        }
        if (Substate != 2) throw UnsupportedSubstate();
        if (!OnScreen()) { LeftScreen(); return; }
        Dec(); if ((Counter1 & 31) == 0) Fire((_random.Next().Value & 1) + 1);
        MoveAnimate();
    }
    private void CircleAttack(Vector2 target,bool usingItem,int frame)
    {
        if (Substate == 0)
        {
            SpawnOutside(); Counter1 = _data.CircleWait;
            OrbitOffset = (_random.Next().Value & 8) != 0 ? 8 : 0xf8; return;
        }
        if (Substate == 1)
        {
            if ((frame & 3) == 0)
            {
                Dec();
                if (Counter1 == 0) { BeginCharge(target); return; }
                if ((Counter1 & 31) == 0) Fire(1);
            }
            if (Near(target,_data.FleeRadius)) { BeginCharge(target); return; }
            Circle(); return;
        }
        if (State == 11)
        {
            if (Substate == 2)
            {
                if (Near(target,_data.FleeRadius) || usingItem) { Substate = 3; Angle = Aim(target) ^ 16; AdvanceAnimation(); return; }
                MoveOffscreen(); return;
            }
            if (Substate != 3) throw UnsupportedSubstate();
            if (!OnScreen()) { LeftScreen(); return; }
            Dec(); if ((Counter1 & 31) == 0) Fire(1); MoveAnimate(); return;
        }
        switch (Substate)
        {
            case 2:
                if (!usingItem) { MoveOffscreen(); return; }
                Substate = 3; Counter1 = _data.FireWait; Speed = _data.FleeSpeed; Angle = Aim(target) ^ 16; AdvanceAnimation(); return;
            case 3:
                Dec(); if (Counter1 != 0) { MoveAnimate(); return; }
                Substate = 4; Counter1 = _data.FireWait;
                _world.Projectile(new(Position.Floor(),0,Slot,ZHigh:ZFixed >> 8));
                _world.Sound(SoundId.SndSplash); AdvanceAnimation(); return;
            case 4:
                Dec(); if (Counter1 == 0) { Substate = 5; Speed = _data.EscapeSpeed; Angle = Aim(target) ^ 16; }
                AdvanceAnimation(); return;
            case 5: MoveOffscreen(); return;
            default: throw UnsupportedSubstate();
        }
    }
    private void BeginCharge(Vector2 target)
    { Substate = 2; Speed = _data.ChargeSpeed; Angle = Aim(target); AdvanceAnimation(); }
    private void Creep()
    {
        switch (Substate)
        {
            case 0: SpawnOutside(); Counter1 = _data.CreepWait; Speed = _data.ApproachSpeed; return;
            case 1:
                Dec(); if (Counter1 != 0) { MoveAnimate(); return; }
                Substate = 2; Counter1 = _data.FireWait; Fire(3); AdvanceAnimation(); return;
            case 2:
                Dec(); if (Counter1 == 0) { Substate = 3; Angle ^= 16; Speed = _data.ChargeSpeed; }
                AdvanceAnimation(); return;
            case 3: MoveOffscreen(); return;
            default: throw UnsupportedSubstate();
        }
    }
    private void Retreat(Vector2 target)
    {
        switch (Substate)
        {
            case 0:
                Substate = 1; Counter1 = _data.RetreatWait; Speed = _data.FleeSpeed; Angle = Aim(target) ^ 16;
                Direction = FireAnimationCounter = 0; RestartAnimation(0); return;
            case 1:
                Dec(); if (Counter1 != 0) { AdvanceAnimation(); return; }
                Substate = 2;
                if (Health >= _data.HighHealth) return;
                int text = Health >= _data.MediumHealth ? 1 : 2;
                if (text == HealthText) return;
                HealthText = text; _world.ShowText(0x2f13 + text,Position); return;
            case 2: MoveOffscreen(); return;
            default: throw UnsupportedSubstate();
        }
    }
    private void SplitAndLeave()
    {
        switch (Substate)
        {
            case 0:
                Dec(); if (Counter1 == 0) { Substate = 1; _world.ShowText(0x2f16,Position); } return;
            case 1:
                if (!_world.CanAllocateEnemies(_data.Children)) { AdvanceAnimation(); return; }
                Substate = 2; ChildrenAlive = _data.Children; Visible = false;
                _world.Puff(Position.Floor(),ZFixed >> 8,0);
                for (int i = 0; i < _data.Children; i++)
                    if (!_world.Bat(this,i)) throw new NotSupportedException("vire_stateF: unchecked bat allocation exceeded the native ENEMY pool.");
                return;
            case 2:
                if (ChildrenAlive != 0) { if (Counter2 != 0) Counter2--; return; }
                Substate = 3; Counter1 = _data.ReappearWait; _world.Sound(SoundId.SndCtrlStopMusic); return;
            case 3:
                Dec(); if (Counter1 != 0) { Visible = !Visible; return; }
                Substate = 4; Counter1 = _data.FarewellWait; ShowNative(); return;
            case 4:
                Dec(); if (Counter1 != 0) { AdvanceAnimation(); return; }
                Substate = 5; Angle = 6; Speed = _data.FleeSpeed;
                _world.ShowText(_world.Linked() ? 0x2f18 : 0x2f17,Position); return;
            case 5:
                if (_world.Linked()) { Health = 0; return; }
                if (FairySpawned == 0)
                {
                    FairySpawned = 1; // Allocation failure does not retry.
                    _world.Part(new ItemDropSpawn(ItemDropDatabase.Fairy,Position.Floor(),Angle:Angle,ZHigh:ZFixed >> 8));
                }
                AdvanceAnimation(); ZFixed = unchecked((short)(ZFixed - _data.DepartureZStep));
                if (OnScreen()) { Move(Speed,Angle); return; }
                _world.RestoreMusic(); Finish(); return;
            default: throw UnsupportedSubstate();
        }
    }
    private void BossDeath()
    {
        if (!_bossDeathStarted)
        {
            _bossDeathStarted = true; _collision = false; Counter1 = _data.DeathWait;
            _world.DisableLinkCollisionsAndMenu(); _world.Sound(SoundId.SndBossDead);
        }
        Dec(); if (Counter1 != 0) { Visible = !Visible; return; }
        Counter1 = 1;
        if (!_world.Part(new BossDeathExplosionSpawn(Position.Floor(),EnemyId.Vire,ZFixed >> 8))) return;
        _world.RestoreMusic(); Finish();
    }
    private VireCharacter Parent() => _world.Parent(ParentSlot) ??
        throw new NotSupportedException($"vire.s: bat relatedObj1 ENEMY$d{ParentSlot:x1} was deleted/reused without a Vire union owner.");
    private void UpdateBat(Vector2 target)
    {
        switch (State)
        {
            case 8:
                State = 9; Angle = OrbitOffset & 31; Counter1 = _data.BatRiseWait; Health = 1; RestartAnimation(2); ShowNative(); return;
            case 9:
                Dec(); if (Counter1 == 0) { Follow(); return; }
                Rise(); MoveAnimate(); return;
            case 10:
                Dec(); ZFixed = (_data.BatZ[(Counter1 & 0x1c) >> 2].Value << 8) | (ZFixed & 255);
                ZFixed = unchecked((short)ZFixed);
                if (Parent().Counter2 == 0) { State = 11; Counter1 = _data.BatChargeWait; return; }
                if (Near(target,_data.BatAvoidRadius)) _movement.MoveAtAngle(Aim(target) ^ 16,_data.ChargeSpeed,allowHoles:true,nativeSpeed:Speed);
                Angle = (Aim(target) + OrbitOffset) & 31; MoveBatBoundary(); AdvanceAnimation(); return;
            case 11:
                Dec(); if (Counter1 == 0) { State = 12; Speed = _data.ChargeSpeed; BatTarget = target; } return;
            case 12:
                if (Near(BatTarget,_data.BatArrivalRadius) && ((ZFixed >> 8) & 255) >= _data.BatArrivalZ)
                { State = 13; Counter1 = _data.BatRecoverWait; AdvanceAnimation(); return; }
                if (((ZFixed >> 8) & 255) < _data.BatMinimumZ) ZFixed = unchecked((short)(ZFixed + 256));
                Angle = Aim(BatTarget); MoveAnimate(); return;
            case 13:
                Dec(); if (Counter1 == 0) { Follow(); return; }
                Rise(); MoveBatBoundary(); AdvanceAnimation(); return;
            default: throw new NotSupportedException($"vire.s: bat state${State:x2}.");
        }
    }
    private void Follow()
    {
        State = 10; Speed = _data.BatFollowSpeed; _collision = true;
        Counter1 = _random.Next().Value; Parent().Counter2 = _data.BatChaseWait; AdvanceAnimation();
    }
    private void Rise()
    { if (((ZFixed >> 8) & 255) >= 0xf0) ZFixed = unchecked((short)(ZFixed - _data.BatRiseZStep)); }
    private void MoveBatBoundary()
    {
        var walls = EnemyAdjacentWallResolver.Shared.Probe(Position,Angle,p =>
            (byte)p.X >= _room.Width || (byte)p.Y >= _room.Height || _room.GetTerrainInfo(new((byte)p.X,(byte)p.Y)).Collision == 0xff);
        if (walls.Bitset == 0) Move(Speed,Angle);
    }
    private void SpawnOutside()
    {
        int i = (_random.Next().Value & 7) * 3;
        var camera = _world.Camera();
        CopyHighPosition(new((byte)(OracleObjectPosition.HighByte(camera.X) + _data.Spawns[i + 1].Value),
            (byte)(OracleObjectPosition.HighByte(camera.Y) + _data.Spawns[i].Value)),ZFixed >> 8);
        Angle = _data.Spawns[i + 2].Value; _collision = true; Substate++; ShowNative();
    }
    private void Circle()
    {
        var camera = _world.Camera();
        Vector2 center = new((byte)(OracleObjectPosition.HighByte(camera.X) + _data.CenterX),
            (byte)(OracleObjectPosition.HighByte(camera.Y) + _data.CenterY));
        int angle = Aim(center), y = Math.Abs((int)Position.Floor().Y - (int)center.Y), x = Math.Abs((int)Position.Floor().X - (int)center.X);
        bool radial = y >= _data.RingAxisLimit || x >= _data.RingAxisLimit;
        int difference = (y + x - _data.RingRadius) & 255;
        if (radial || difference >= _data.RingWidth)
        {
            Angle = radial || (difference & 128) == 0 ? angle : angle ^ 16;
            Speed = _data.RadialSpeed; Move(Speed,Angle);
        }
        Angle = (angle + OrbitOffset) & 31; Speed = _data.CircleSpeed; MoveAnimate();
    }
    private void Fire(int subid)
    {
        if (!_world.Projectile(new(Position.Floor(),subid,Slot,ZHigh:ZFixed >> 8))) return;
        _world.Sound(SoundId.SndSplash); Direction = 1; RestartAnimation(1);
    }
    private void MoveOffscreen() { if (!OnScreen()) LeftScreen(); else MoveAnimate(); }
    private void LeftScreen() { State = 9; Substate = 0; Counter1 = _data.HiddenWait; _collision = false; Visible = false; }
    private bool OnScreen() => (byte)OracleObjectPosition.HighByte(Position.Y) < _data.BoundsY && (byte)OracleObjectPosition.HighByte(Position.X) < _data.BoundsX;
    private int Aim(Vector2 target) => OracleObjectMovement.Shared.RelativeAngle(Position.Floor(),target.Floor());
    private bool Near(Vector2 target,int radius) => (byte)(OracleObjectPosition.HighByte(Position.X) - target.X + radius) < radius * 2 + 1 &&
        (byte)(OracleObjectPosition.HighByte(Position.Y) - target.Y + radius) < radius * 2 + 1;
    private void Dec() => Counter1 = (Counter1 - 1) & 255;
    private void Move(int speed,int angle)
    {
        var velocity = NativeObjectMovement.Velocity(_memory,speed,angle);
        Position = OracleObjectPosition.FromPixels(Position).Add(velocity.YFixed,velocity.XFixed).PrecisePosition;
    }
    private void MoveAnimate() { Move(Speed,Angle); AdvanceAnimation(); }
    private void CopyHighPosition(Vector2 position,int z)
    {
        Position = new(position.Floor().X + Position.X - Position.Floor().X,position.Floor().Y + Position.Y - Position.Floor().Y);
        ZFixed = unchecked((short)((z << 8) | (ZFixed & 255)));
    }
    private void ShowNative() { Visible = true; ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex; }
    private NotSupportedException UnsupportedSubstate() => new($"vire.s: main state${State:x2} substate${Substate:x2}.");
    internal override bool TakeSwordHit(Vector2 origin,int damage) => Damage(origin,damage);
    internal override bool TakeBurnHit(int damage) => Damage(Position,damage);
}
