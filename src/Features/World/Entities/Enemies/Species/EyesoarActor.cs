using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Native ENEMY_EYESOAR $7b and its four ENEMY_EYESOAR_CHILD $11 slots.</summary>
internal sealed partial class EyesoarActor : EnemyCharacter, ISwitchHookEnemy
{
    private EyesoarEnvironment _world = null!;
    private EnemyTerrainMovement _movement = null!;
    private readonly List<EyesoarActor> _children = new();
    private EyesoarSpawnEffect? _spawnEffect;
    private bool _collision = true;
    private bool _justHit;
    private bool _dying;
    private int _lastCollision;
    private int _zFixed;
    private int _speedZ;
    private int _direction;
    private Vector2I _target;
    internal ImportedEnemyDefinition Record { get; private set; }
    internal bool IsChild => Record.Id == 0x11;
    internal bool IsSpawner => !IsChild && Record.SubId == 0;
    internal EyesoarActor? Parent { get; private set; }
    internal EyesoarActor? Body { get; private set; }
    internal IReadOnlyList<EyesoarActor> Children => _children;
    internal int State { get; private set; }
    internal int Substate { get; private set; }
    internal int Counter { get; private set; }
    internal int FormationCounter { get; private set; }
    internal int Flags { get; private set; }
    internal int ReadyFlags { get; private set; }
    internal int Formation { get; private set; }
    internal int OrbitRotation { get; private set; }
    internal int MysteryCounter { get; private set; }
    internal int Distance { get; private set; }
    internal int TargetDistance { get; private set; }
    internal int Angle { get; private set; }
    internal int Speed { get; private set; }
    internal int CollisionMode { get; private set; }
    internal int ZFixed => _zFixed;
    internal bool JustHit => _justHit;
    internal bool ControlsDisabled => !IsDead && !IsChild && (State == 9 || State == 8 && _world.ShuttersClosed());
    internal bool Initializing => State == 0;
    internal EyesoarDatabase Data => _world.Data;
    internal void RequestSound(int sound) => _world.Sound(sound);
    internal BossEntryMovement Entry => _world.Entry;
    internal override bool CollisionEnabled => base.CollisionEnabled && Health > 0 && _collision && !_dying && !IsSpawner;
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset + Vector2.Down * (_zFixed >> 8);

    internal void Initialize(ImportedEnemyDefinition record, EyesoarEnvironment world, Vector2 position)
    {
        Record = record; _world = world; _movement = new(this, world.Room);
        CollisionMode = record.Id == 0x11 ? 0x15 : 0x6d;
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record), positionedOam: true);
        Name = $"Eyesoar_{record.Id:x2}_{record.SubId:x2}";
        ZIndex = 10;
        Visible = false;
    }

    internal EyesoarActor CreateChild(int index)
    {
        var actor = new EyesoarActor();
        actor.Initialize(_world.Bosses.Enemy(index == 0 ? 0x7b : 0x11, index == 0 ? 1 : 4 - index), _world, Position.Floor());
        if (index == 0) Body = actor;
        else { actor.Parent = Body!; Body!._children.Add(actor); }
        return actor;
    }

    internal void InitializeState(ICollection<RoomEntitySpawn> spawns)
    {
        if (!Initializing) return;
        _world.Random.Next(); // enemyStandardUpdate: var3d, including the deleted spawner.
        if (IsChild)
        {
            Angle = Data.ChildAngle(Record.SubId);
            SetHighPosition(Parent!.HighPosition() + OracleObjectMovement.Shared.CircleArcOffset(0x18, Angle));
            Counter = 90; Speed = 0x28; State = 8;
            return;
        }
        Counter = 60; _zFixed = -0x200; Speed = 0x14;
        if (!IsSpawner) { State = 8; return; }
        State = 0x15; // Source writes SPEED_80+1 before falling through state1.
        _world.EnableLink(); _world.Sound(OracleSoundEngine.SndCtrlStopMusic);
        if (!_world.EnemySlotsAvailable(5))
            throw new InvalidOperationException("ENEMY_EYESOAR $7b:$00 state0: five free ENEMY slots required; source would retain invalid state $15.");
        for (int i = 0; i < 5; i++) spawns.Add(new EyesoarChildSpawn(this, i));
        Finish();
    }

    internal void UpdateFrame(Player player, int frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (IsDead) return;
        if (Initializing) { InitializeState(spawns); return; }
        if (Health == 0 && !_justHit)
        {
            if (!IsChild) { UpdateDeath(spawns); return; }
            if (Parent!.Health == 0)
            {
                if (_world.InteractionSlotAvailable())
                    spawns.Add(new EnemyDeathPuffSpawn(Position + Vector2.Down * (_zFixed >> 8), EnemyId: 0x11));
                Finish(); return;
            }
            if (_world.InteractionSlotAvailable())
                spawns.Add(new PuzzlePuffSpawn(Position + Vector2.Down * (_zFixed >> 8), OracleSoundEngine.SndPoof));
            State = 12; Counter = 30; Distance = 0; _collision = false; Health = 4; Visible = false;
        }
        if (IsChild) UpdateChild(player, frame, spawns);
        else
        {
            if (_justHit && _lastCollision == 0x9a) { MysteryCounter = 120; Flags |= 1; }
            if (MysteryCounter != 0 && --MysteryCounter == 0) Flags &= ~1;
            UpdateBody(player, frame);
        }
        _justHit = false;
        AdvanceInvincibilityCounter();
        QueueRedraw();
    }

    private void UpdateBody(Player player, int frame)
    {
        switch (State)
        {
            case 3: UpdateHook(); return;
            case 8:
                if (!_world.ShuttersClosed()) return;
                if (--Counter != 0) { Visible = !Visible; return; }
                Counter = 60; FormationCounter = 180; ReadyFlags = 0xff; State = 9; Visible = true; return;
            case 9:
                if (--Counter == 0) { State = 10; Counter = 1; _world.EnableLink(); _world.Sound(OracleSoundEngine.MusBoss); }
                AdvanceAnimation(); return;
            case 10:
                UpdateFormation(frame);
                if (DecCounter() == 0)
                {
                    State = 11;
                    _target = new(ClampTarget(High(player.EnemyContactPosition.X), 0x70), ClampTarget(High(player.EnemyContactPosition.Y), 0x30));
                }
                AdvanceAnimation(); return;
            case 11:
                UpdateFormation(frame);
                var delta = _target - HighPosition();
                if (Math.Abs(delta.X) <= 2 && Math.Abs(delta.Y) <= 2) { State = 10; Counter = 60; }
                else MoveToward(_target);
                AdvanceAnimation(); return;
            case 12:
                if (DecCounter() != 0) { AdvanceAnimation(); return; }
                State = 13; CollisionMode = 0x6d; RestartAnimation(0);
                goto case 13;
            case 13:
                _zFixed -= 0x80;
                if ((_zFixed >> 8) != -2) { AdvanceAnimation(); return; }
                State = 14; Counter = 240; ReadyFlags = 0x0f; Flags &= ~2; ChooseAngle();
                goto case 14;
            case 14:
                if (DecCounter() == 0) { Flags &= ~8; Flags |= 16; }
                if (ReadyFlags == 0xff) { Flags &= ~16; State = 10; Counter = 1; FormationCounter = 8; }
                if ((Counter & 0x3f) == 0) ChooseAngle();
                _movement.MoveAtAngle(Angle, Speed, allowHoles: false); AdvanceAnimation(); return;
            default: throw InvalidState();
        }
    }

    private void UpdateFormation(int frame)
    {
        if ((ReadyFlags & 0xf0) != 0xf0) return;
        if ((frame & 3) == 0) OrbitRotation = (OrbitRotation + 1) & 31;
        if (ReadyFlags == 0xff)
        {
            FormationCounter = (FormationCounter - 1) & 255;
            if (FormationCounter != 0) return;
            FormationCounter = 180; Flags |= 4; ReadyFlags = 0xf0;
            int index = (Formation + 1) & 7;
            Formation = Data.FormationDistance(index) | index;
        }
        else Flags &= ~4;
    }

    private void ChooseAngle()
    {
        int random;
        do { random = _world.Random.Next().Value & 15; } while (random >= 9);
        int quadrant = (High(Position.Y) >= 0x58 ? 1 : 0) | (High(Position.X) >= 0x88 ? 2 : 0);
        Angle = (Data.CenterAngle(quadrant) + random) & 31;
    }

    private void UpdateChild(Player player, int frame, ICollection<RoomEntitySpawn> spawns)
    {
        var parent = Parent!;
        if ((parent.Flags & 2) != 0 && State < 15)
        {
            if (State != 12) { State = 15; Counter = 240; }
            TargetDistance = 0x18;
        }
        switch (State)
        {
            case 8:
                if (DecCounter() != 0 || !_world.InteractionSlotAvailable()) return;
                _spawnEffect = new EyesoarSpawnEffect();
                _spawnEffect.Initialize(Position, _world.SpawnVisual, _world.Sound);
                spawns.Add(new EyesoarSpawnEffectSpawn(_spawnEffect)); State = 9; return;
            case 9:
                if ((_spawnEffect!.Parameter & 128) == 0) return;
                State = 10; Counter = 240; _zFixed = -0x200; Distance = 0x18; Visible = true; return;
            case 10:
                if ((parent.Flags & 4) != 0) { TargetDistance = parent.Formation & 0xf8; State = 11; }
                Orbit(); return;
            case 11:
                if (Distance == TargetDistance) { State = 10; parent.ReadyFlags |= 1 << Record.SubId; }
                else Distance += Math.Sign(TargetDistance - Distance);
                Orbit(); return;
            case 12:
                if ((parent.Flags & 1) == 0 && DecCounter() == 0)
                { State = 13; _collision = true; Visible = true; Orbit(); return; }
                parent.ReadyFlags |= Data.ChildReadyFlags(Record.SubId); return;
            case 13:
                int target = parent.Formation & 0xf8;
                if (Distance == target) State = 10;
                else Distance += Math.Sign(target - Distance);
                Orbit(); return;
            case 14:
                if ((parent.Flags & 16) == 0) State = 11;
                Orbit(); return;
            case 15:
                if ((parent.Flags & 8) == 0)
                { Angle = OrbitAngle(); State = 16; Distance = 0x18; AdvanceAnimation(); return; }
                if ((frame & 15) == 0)
                {
                    int difference = (Angle - OracleObjectMovement.Shared.RelativeAngle(Position, player.EnemyContactPosition)) & 31;
                    if (difference != 0) Angle = (Angle + (difference < 16 ? -1 : 1)) & 31;
                }
                Move();
                Angle = EnemyAdjacentWallResolver.Shared.BounceAngle(Position, Angle,
                    point => point.X < 0 || point.X >= _world.Room.Width || point.Y < 0 || point.Y >= _world.Room.Height);
                AdvanceAnimation(); return;
            case 16:
                var destination = parent.HighPosition() + OracleObjectMovement.Shared.CircleArcOffset(0x18, OrbitAngle());
                destination = new(destination.X & 255, destination.Y & 255);
                if (HighPosition() == destination)
                {
                    if ((parent.Flags & 2) == 0) { State = 14; parent.ReadyFlags |= 0x10 << Record.SubId; }
                    return;
                }
                MoveToward(destination); AdvanceAnimation(); return;
            default: throw InvalidState();
        }
    }

    private void Orbit()
    {
        Angle = OrbitAngle();
        SetHighPosition(Parent!.HighPosition() + OracleObjectMovement.Shared.CircleArcOffset(Distance, Angle));
        AdvanceAnimation();
    }
    private int OrbitAngle() => (Data.ChildAngle(Record.SubId) + Parent!.OrbitRotation) & 31;
    private void UpdateHook()
    {
        switch (Substate)
        {
            case 0:
                Flags |= 0x0a; Formation = (Formation & 7) | 0x18; CollisionMode = 0x4c;
                Counter = 150; _direction = 0; Substate = 1; return;
            case 1: return;
            case 2:
                if (_direction == 0) { _direction = 1; RestartAnimation(1); }
                return;
            case 3:
                if (OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, 0x20))
                { _collision = true; State = 12; }
                return;
            default: throw InvalidState();
        }
    }
    public bool SwitchHookHeld => GodotObject.IsInstanceValid(this) && !IsDead && State == 3 && Substate < 3;
    public Vector2 SwitchHookPosition => Position;
    public void BeginSwitchHook(Vector2 linkPosition) { State = 3; Substate = 0; _collision = false; }
    public void CopySwitchHookPosition(Vector2 position, int zHigh)
    { SetHighPosition((Vector2I)position.Floor()); _zFixed = (zHigh << 8) | (_zFixed & 255); QueueRedraw(); }
    public void SwapSwitchHook() => Substate = 2;
    public void ReleaseSwitchHook() { if (SwitchHookHeld) Substate = 3; }
    internal void ApplyNativeHit(int item, int damage, int invincibility)
    { Health = Math.Max(0, Health - damage); InvincibilityCounter = invincibility; MarkContact(item); }
    internal void MarkContact(int item) { _justHit = true; _lastCollision = item | 128; }
    private void UpdateDeath(ICollection<RoomEntitySpawn> spawns)
    {
        if (!_dying)
        {
            foreach (var child in _children) { child.Health = 0; child._collision = false; }
            _dying = true; _collision = false; Counter = 120;
            _world.DisableLink(); _world.Sound(OracleSoundEngine.SndBossDead);
        }
        if (--Counter != 0) { Visible = !Visible; return; }
        Counter = 1;
        if (!_world.PartSlotAvailable()) return;
        spawns.Add(new BossDeathExplosionSpawn(Position, 0x7b)); Finish();
    }
    private int DecCounter() => Counter = (Counter - 1) & 255;
    private void MoveToward(Vector2I target) { Angle = OracleObjectMovement.Shared.RelativeAngle(Position, target); Move(); }
    private void Move() => Position += OracleObjectMovement.Shared.Delta(Speed, Angle);
    private static int ClampTarget(int value, int length)
    { int delta = (value - 0x40) & 255; return delta < length ? value : delta < 0xc0 ? 0x40 + length : 0x40; }
    private static int High(float value) => Mathf.FloorToInt(value) & 255;
    private Vector2I HighPosition() => new(High(Position.X), High(Position.Y));
    private void SetHighPosition(Vector2I high) => Position = new Vector2(high.X & 255, high.Y & 255) + Position - Position.Floor();
    private InvalidOperationException InvalidState() => new($"ENEMY_EYESOAR ${Record.Id:x2}:${Record.SubId:x2}: invalid state ${State:x2}/${Substate:x2}.");
}

internal sealed record EyesoarEnvironment(OracleRoomData Room, EyesoarDatabase Data, DungeonBossDatabase Bosses,
    DungeonInteractionVisual SpawnVisual, OracleRandom Random, Func<bool> ShuttersClosed, Func<bool> PartSlotAvailable,
    Func<int, bool> EnemySlotsAvailable, Func<bool> InteractionSlotAvailable, Action<int> Sound,
    Action DisableLink, Action EnableLink, BossEntryMovement Entry);
internal sealed record EyesoarChildSpawn(EyesoarActor Spawner, int Index) : RoomEntitySpawn;
