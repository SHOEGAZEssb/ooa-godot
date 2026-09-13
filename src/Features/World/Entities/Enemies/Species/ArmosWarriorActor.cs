using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>ENEMY_ARMOS_WARRIOR $73's four native enemy slots.</summary>
internal sealed partial class ArmosWarriorActor : EnemyCharacter
{
    private ArmosWarriorEnvironment _world = null!;
    private EnemyTerrainMovement _movement = null!;
    private bool _justHit;
    private bool _dying;
    private int _zFixed;
    private int _speedZ;
    private int _shieldFrame;
    private int _shieldAnimationBase;
    private Vector2I _swordBase;
    private Vector2I _target;
    private bool _swordHitShield;
    private bool _controlsDisabled;
    internal ImportedEnemyDefinition Record { get; private set; }
    internal int SubId => Record.SubId;
    internal int State { get; private set; }
    internal int Substate { get; private set; }
    internal int Counter { get; private set; }
    internal int Angle { get; private set; }
    internal int Speed { get; private set; }
    internal int Turn { get; private set; }
    internal int ShieldHits { get; private set; }
    internal int CollisionMode { get; private set; } = 0x44;
    internal int ZFixed => _zFixed;
    internal bool ControlsDisabled => _controlsDisabled && !IsDead;
    internal bool Dying => _dying;
    internal bool JustHit => _justHit;
    internal bool Initializing => State == 0 && Substate == 0;
    internal ArmosWarriorActor? Body { get; private set; }
    internal ArmosWarriorActor? Shield { get; private set; }
    internal ArmosWarriorActor? Sword { get; private set; }
    internal ArmosWarriorDatabase Data => _world.Data;
    internal void RequestSound(int sound) => _world.Sound(sound);
    internal BossEntryMovement Entry => _world.Entry;
    internal override bool CollisionEnabled => base.CollisionEnabled && Health > 0 && State != 0 && !_dying && SubId != 0;
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset + Vector2.Down * (_zFixed >> 8);

    internal void Initialize(ImportedEnemyDefinition record, ArmosWarriorEnvironment world, Vector2 position, int z = 0)
    {
        Record = record;
        _world = world;
        _movement = new(this, world.Room);
        _zFixed = z;
        _controlsDisabled = record.SubId is 0 or 1;
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record), positionedOam: true);
        Name = $"ArmosWarrior_{record.SubId:x2}";
        Visible = false;
    }

    internal ArmosWarriorActor CreateChild(int subid)
    {
        var child = new ArmosWarriorActor();
        child.Initialize(_world.Bosses.Enemy(0x73, subid), _world, Position, _zFixed);
        switch (subid)
        {
            case 1: Body = child; break;
            case 2: Shield = child; break;
            case 3:
                Sword = child;
                foreach (var actor in new[] { Body!, Shield!, Sword })
                { actor.Body = Body; actor.Shield = Shield; actor.Sword = Sword; }
                break;
            default: throw new InvalidOperationException($"ENEMY_ARMOS_WARRIOR child ${subid:x2} is invalid.");
        }
        return child;
    }

    internal void InitializeState()
    {
        if (!Initializing) return;
        _world.Random.Next(); // enemyStandardUpdate: one var3d draw for each slot.
        if (SubId == 0)
        {
            State = 1;
            _world.EnableLink();
            _world.Sound(OracleSoundEngine.SndCtrlStopMusic);
        }
        else { State = 8; Speed = SubId; }
    }

    internal void UpdateFrame(Player player, int frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (IsDead) return;
        if (Initializing) { InitializeState(); return; }
        // enemyCode73 ignores JUST_HIT and KNOCKBACK statuses, while a
        // just-hit status takes precedence over zero health in the common code.
        if (Health == 0 && !_justHit) UpdateDeath(spawns);
        else if (SubId == 0) SpawnChildren(spawns);
        else if (SubId == 1) UpdateBody(player, spawns);
        else if (SubId == 2) UpdateShield();
        else UpdateSword(player, frame);
        _justHit = false;
        AdvanceInvincibilityCounter(); // bank0:updateEnemies, after the handler.
        QueueRedraw();
    }

    private void SpawnChildren(ICollection<RoomEntitySpawn> spawns)
    {
        if (!_world.EnemySlotsAvailable(3)) return;
        // ecom_setZAboveScreen: high-byte negative(yh+$0c), adjusted by camera.
        int c = (-(High(Position.Y) + 12)) & 255;
        int sum = _world.CameraY() + c;
        int z = sum > 255 ? c : sum;
        if ((z & 128) == 0) z = 128;
        _zFixed = (sbyte)z * 256;
        for (int subid = 1; subid <= 3; subid++) spawns.Add(new ArmosWarriorChildSpawn(this, subid));
        Finish(); // The body receives the counted enabled byte; no kill outcome.
    }

    private void UpdateBody(Player player, ICollection<RoomEntitySpawn> spawns)
    {
        switch (State)
        {
            case 8:
                if (!_world.ShuttersClosed() || !_world.PartSlotAvailable()) return;
                spawns.Add(new BossShadowSpawn(() => Position, () => _zFixed >> 8,
                    () => GodotObject.IsInstanceValid(this) && !IsDead, 1, 8));
                State = 9; Speed = 0x14; CollisionMode = 0x60; Visible = true; ZIndex = 10;
                return;
            case 9: UpdateIntro(spawns); return;
            case 10:
                int toSword = (OracleObjectMovement.Shared.RelativeAngle(Position, Sword!.Position) + 4) & 0x18;
                if (Angle == toSword) { Angle ^= 16; Turn = -Turn; }
                State = 11; Counter = 75; AdvanceAnimation(); return;
            case 11:
                if (--Counter == 0) State = 10;
                MoveBox(); return;
            case 12:
                AdvanceAnimation();
                if (--Counter != 0) MoveBox();
                else { State = 10; Speed = Data.ParentSpeed(Shield!.ShieldHits); }
                return;
            case 13:
                if (--Counter == 0)
                {
                    Counter = 30; State = 14; CollisionMode = 0x44; Speed = 0x50;
                    ShowText(0x2f02); RestartAnimation(1);
                }
                else if ((Counter & 7) == 0)
                {
                    int random = _world.Random.Next().Value;
                    spawns.Add(new RockDebrisSpawn(Position + new Vector2(random & 15, ((random & 0x70) >> 4) - 4), 0x06));
                }
                return;
            case 14:
                AdvanceAnimation();
                if (--Counter != 0) AdvanceAnimation();
                else { State = 15; Angle = OracleObjectMovement.Shared.RelativeAngle(Position, player.EnemyContactPosition); }
                return;
            case 15:
                AdvanceAnimation();
                if (EnemyAdjacentWallResolver.Shared.Probe(Position, Angle,
                    point => _world.Room.IsSolid(point)).Bitset == 0) Move();
                else
                {
                    State = 16; Angle ^= 16; _speedZ = -0x180; Speed = 0x28;
                    _world.Shake(30); _world.Sound(OracleSoundEngine.SndStrongPound);
                }
                return;
            case 16:
                AdvanceAnimation();
                if (!OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, 0x16)) Move();
                else { State = 14; Speed = 0x50; Counter = 60; }
                return;
            default: throw InvalidState();
        }
    }

    private void UpdateIntro(ICollection<RoomEntitySpawn> spawns)
    {
        switch (Substate)
        {
            case 0:
                if (!OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, 0x20)) return;
                Substate = 1; Counter = 26; _world.Shake(26);
                Sword!._zFixed = (_zFixed & ~255) | (Sword._zFixed & 255);
                _world.Sound(OracleSoundEngine.SndStrongPound); return;
            case 1:
                if (--Counter == 0) { Substate = 2; ShowText(0x2f01); }
                return;
            case 2:
                Substate = 3; Counter = 30; _controlsDisabled = false;
                _world.EnableLink(); _world.Sound(OracleSoundEngine.MusMiniboss); RestartAnimation(2); return;
            case 3:
                if (--Counter != 0) return;
                Counter = 70; Angle = 16; Substate = 4;
                Sword!.SetHighPosition(new(High(Sword.Position.X) - 1, High(Sword.Position.Y) - 2));
                RestartAnimation(0); return;
            case 4:
                if (--Counter == 0) { State = 10; Angle = 24; Turn = 8; }
                Move(); AdvanceAnimation(); return;
            default: throw InvalidState();
        }
    }

    private void MoveBox()
    {
        Vector2I high = HighPosition();
        bool corner = true;
        if (high.Y < 0x30) high.Y = 0x31;
        else if (high.Y >= 0x80) high.Y = 0x7f;
        else if (high.X >= 0xc0) high.X = 0xbf;
        else if (high.X < 0x30) high.X = 0x31;
        else corner = false;
        if (corner) { SetHighPosition(high); Angle = (Angle + Turn) & 0x18; }
        else Move();
        AdvanceAnimation();
    }

    private void UpdateShield()
    {
        if (State == 8)
        {
            if (!_world.ShuttersClosed()) return;
            State = 9; CollisionMode = 0x61; ShieldHits = 3; _shieldAnimationBase = 3;
            RestartAnimation(3); Visible = true; ZIndex = 11;
        }
        else
        {
            if (ShieldHits == 0) { Health = 0; return; }
            if (_shieldFrame != Body!.AnimationParameter)
            { _shieldFrame = Body.AnimationParameter; RestartAnimation(_shieldAnimationBase + _shieldFrame); }
        }
        SetHighPosition(Body!.HighPosition() + Data.ShieldOffset(_shieldFrame));
        _zFixed = (Body._zFixed & ~255) | (_zFixed & 255);
    }

    private void UpdateSword(Player player, int frame)
    {
        if (State >= 11) Slash(frame);
        switch (State)
        {
            case 8:
                if (!_world.ShuttersClosed()) return;
                State = 9; CollisionMode = 0x62; Speed = 5;
                HeldSword(); RestartAnimation(9); Visible = true; ZIndex = 12; return;
            case 9:
                if (Body!.Substate == 0) { HeldSword(); return; }
                if (Body.Substate < 3) return;
                if (Body.Substate == 3)
                {
                    if (Counter++ == 0) RestartAnimation(10);
                    else SetHighPosition(HighPosition() + Vector2I.Up);
                    return;
                }
                if (Body.State >= 10) { State = 10; Counter = 1; _swordBase = HighPosition(); return; }
                Slash(frame); AdvanceAnimation(); return;
            case 10:
                if (--Counter != 0) return;
                State = 11; Speed = 0x3c; Counter = 150; _target = (Vector2I)player.EnemyContactPosition.Floor();
                Angle = OracleObjectMovement.Shared.RelativeAngle(Position, _target);
                AdvanceAnimation(); UpdateSwordBox(); return;
            case 11:
                CheckShield();
                if ((frame & 3) == 0) Angle = OracleObjectMovement.Shared.RelativeAngle(Position, _target);
                SetHighPosition(_swordBase);
                Vector2I boundary = Data.SwordBoundaries(Angle);
                if (PastBoundary(boundary.Y, High(Position.Y)) || PastBoundary(boundary.X, High(Position.X)) ||
                    Math.Abs(_target.Y - High(Position.Y)) <= 28 && Math.Abs(_target.X - High(Position.X)) <= 28)
                { State = 12; Counter = 0x70; }
                AdvanceAnimation(); MoveSword(); return;
            case 12:
                CheckShield();
                if (--Counter == 0)
                {
                    if (AnimationParameter != 7) { Counter = 2; AdvanceAnimation(); return; }
                    State = 10; _swordHitShield = false; Counter = 30 + Shield!.ShieldHits * 32;
                    RestartAnimation(10); return;
                }
                Speed = Data.SwordSpeed(Counter); SetHighPosition(_swordBase);
                if (Counter >= 30 || (Counter & 1) == 0) AdvanceAnimation();
                MoveSword(); return;
            default: throw InvalidState();
        }
    }

    private void HeldSword()
    {
        SetHighPosition(Body!.HighPosition() + new Vector2I(-6, -12));
        _zFixed = Body._zFixed;
    }
    private void MoveSword()
    {
        // armosWarrior_sword_updatePosition uses the top-down probe table.
        _movement.MoveUsingAdjacentWalls(Angle, Speed, allowHoles: true, topDown: true);
        _swordBase = HighPosition(); UpdateSwordBox();
    }
    private void UpdateSwordBox()
    {
        var box = Data.SwordBox(AnimationParameter);
        SetHighPosition(_swordBase + box.Offset); SetCollisionRadii(box.RadiusX, box.RadiusY);
    }
    private void CheckShield()
    {
        if (_swordHitShield) return;
        Vector2 delta = (Position.Floor() - Shield!.Position.Floor()).Abs();
        Vector2 radii = (CollisionBounds.Size + Shield.CollisionBounds.Size) / 2;
        if (delta.X > radii.X || delta.Y > radii.Y) return;
        _swordHitShield = true;
        Shield.InvincibilityCounter = 24; Shield.ShieldHits--; Shield._shieldAnimationBase += 2;
        Body!.Counter = 60; Body.State = 12; Body.Speed = 0x78; Body.InvincibilityCounter = 24;
        _world.Sound(OracleSoundEngine.SndBossDamage);
    }
    internal void ApplyNativeHit(int damage, int invincibility)
    { Health = Math.Max(0, Health - damage); InvincibilityCounter = invincibility; _justHit = true; }
    internal void MarkContact() => _justHit = true;
    private void UpdateDeath(ICollection<RoomEntitySpawn> spawns)
    {
        if (SubId == 2)
        {
            Sword!.Health = 0; Body!.State = 13; Body.Counter = 90; Body.InvincibilityCounter = 96;
            Finish(); return;
        }
        if (SubId != 1) { Finish(); return; }
        if (!_dying)
        {
            _dying = true; Counter = 120; _world.DisableLink();
            _world.Sound(OracleSoundEngine.SndBossDead);
        }
        if (--Counter != 0) { Visible = (Counter & 1) != 0; return; }
        Counter = 1;
        if (!_world.PartSlotAvailable()) return;
        spawns.Add(new BossDeathExplosionSpawn(Position, 0x73));
        _world.RestoreMusic(); Finish();
    }
    private void ShowText(int id)
    {
        var message = Data.Message(id);
        string text = message.Position == 0 ? message.Text : $"\\pos({message.Position})" + message.Text;
        _world.ShowDialogue(id, text, Position);
    }
    private void Slash(int frame) { if ((frame & 15) == 0) _world.Sound(OracleSoundEngine.SndSwordSlash); }
    private void Move() => Position += OracleObjectMovement.Shared.Delta(Speed, Angle);
    private static int High(float value) => Mathf.FloorToInt(value) & 255;
    private Vector2I HighPosition() => new(High(Position.X), High(Position.Y));
    private void SetHighPosition(Vector2I high) => Position = new Vector2(high.X & 255, high.Y & 255) + Position - Position.Floor();
    private static bool PastBoundary(int boundary, int position) => (boundary & 1) != 0 ? position <= boundary : position > boundary;
    private InvalidOperationException InvalidState() => new($"ENEMY_ARMOS_WARRIOR $73:${SubId:x2}: unsupported state ${State:x2}/${Substate:x2}.");
}

internal sealed record ArmosWarriorEnvironment(OracleRoomData Room, ArmosWarriorDatabase Data, DungeonBossDatabase Bosses,
    OracleRandom Random, Func<bool> ShuttersClosed, Func<bool> PartSlotAvailable, Func<int, bool> EnemySlotsAvailable,
    Func<int> CameraY, Action<int> Sound, Action<int> Shake, Action DisableLink, Action EnableLink,
    Action RestoreMusic, Action<int, string, Vector2> ShowDialogue, BossEntryMovement Entry);

internal sealed record ArmosWarriorChildSpawn(ArmosWarriorActor Spawner, int SubId) : RoomEntitySpawn;
