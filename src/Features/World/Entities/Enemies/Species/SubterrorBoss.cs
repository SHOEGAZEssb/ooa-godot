using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>ENEMY_SUBTERROR $72 native Moonlit Grotto miniboss.</summary>
internal sealed partial class SubterrorBoss : EnemyCharacter
{
    private static readonly int[] UndergroundSpeeds = [0x14, 0x28, 0x3c];
    private static readonly int[] DrillAttackWaits = [120, 90, 60];
    private static readonly int[] AboveGroundDurations = [60, 90, 120, 180];

    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private Action<int> _playSound = null!;
    private Func<bool> _shuttersClosed = null!;
    private Action _disableLink = null!;
    private Action _enableLink = null!;
    private Action _restoreRoomMusic = null!;
    private Action<int, string, Vector2> _showDialogue = null!;
    private Func<bool> _dialogueOpen = null!;
    private Func<long> _animationTick = null!;
    private string _introMessage = string.Empty;
    private SubterrorState _state = SubterrorState.WaitingForDoors;
    private int _substate;
    private int _counter1;
    private int _counter2;
    private int _angle;
    private int _speed;
    private int _zFixed;
    private int _speedZ;
    private int _dirtCounter;
    private bool _dirtEnabled;
    private bool _initialized;
    private bool _introActive = true;
    private bool _acceptedHit;
    private bool _dying;
    private int _deathCounter;
    private Vector2 _linkPosition;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal SubterrorState State => _state;
    internal int Substate => _substate;
    internal int Counter1 => _counter1;
    internal int Counter2 => _counter2;
    internal int Angle => _angle;
    internal int Speed => _speed;
    internal int ZFixed => _zFixed;
    internal int DirtCounter => _dirtCounter;
    internal bool DirtEnabled => _dirtEnabled;
    internal bool IntroActive => _introActive;
    internal bool Defeated => _dying || IsDead;
    internal bool ShovelCollisionEnabled =>
        !_dying && _state == SubterrorState.UndergroundMoving &&
        _substate == 1;
    internal bool DrillingCollisionEnabled =>
        !_dying && _state == SubterrorState.Drilling &&
        _substate == 0 && _counter2 == 0;
    internal bool Vulnerable =>
        !_dying && _state == SubterrorState.AboveGround &&
        _substate != 0;
    internal override bool CollisionEnabled =>
        base.CollisionEnabled && (DrillingCollisionEnabled || Vulnerable);

    protected override Vector2 AnimationDrawOffset =>
        base.AnimationDrawOffset + Vector2.Down * (_zFixed / 256.0f);

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random,
        Action<int> playSound,
        Func<bool> shuttersClosed,
        Action disableLink,
        Action enableLink,
        Action restoreRoomMusic,
        Action<int, string, Vector2> showDialogue,
        Func<bool> dialogueOpen,
        Func<long> animationTick,
        string introMessage)
    {
        Record = record;
        _room = room;
        _random = random;
        _movement = new EnemyTerrainMovement(this, room);
        _playSound = playSound;
        _shuttersClosed = shuttersClosed;
        _disableLink = disableLink;
        _enableLink = enableLink;
        _restoreRoomMusic = restoreRoomMusic;
        _showDialogue = showDialogue;
        _dialogueOpen = dialogueOpen;
        _animationTick = animationTick;
        _introMessage = introMessage;
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record));
        Name = "Subterror";
        ZIndex = 10;
        Visible = false;
    }

    private void InitializeState()
    {
        _speed = 0x3c;
        _angle = 0x10;
        _counter2 = 30;
        _dirtCounter = 7;
        _dirtEnabled = true;
        _playSound(OracleSoundEngine.SndCtrlStopMusic);
    }

    internal void UpdateFrame(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (IsDead)
            return;
        _linkPosition = player.Position;
        BeginFrame();
        if (!_initialized)
        {
            _initialized = true;
            InitializeState();
            return;
        }
        if (_dying)
        {
            Visible = (_deathCounter & 1) != 0;
            if (--_deathCounter == 0)
            {
                Finish();
                spawns.Add(new BossDeathExplosionSpawn(Position, Record.Id));
                _restoreRoomMusic();
            }
            QueueRedraw();
            return;
        }

        if (_dirtEnabled)
        {
            _dirtCounter = DecrementByte(_dirtCounter);
            if (_dirtCounter == 0)
                SpawnDirt(spawns);
        }

        switch (_state)
        {
            case SubterrorState.WaitingForDoors:
                UpdateIntro(spawns);
                break;
            case SubterrorState.Digging:
                AdvanceAnimation();
                if (AnimationParameter == 0)
                    BeginUndergroundMovement(spawns);
                break;
            case SubterrorState.UndergroundMoving:
                UpdateUnderground(player, spawns);
                break;
            case SubterrorState.Drilling:
                UpdateDrilling(spawns);
                break;
            case SubterrorState.AboveGround:
                UpdateAboveGround(spawns);
                break;
        }
        QueueRedraw();
    }

    internal bool TryApplyShovelHit(Rect2 hitbox, Vector2 sourcePosition)
    {
        _ = sourcePosition;
        if (!ShovelCollisionEnabled || !hitbox.Intersects(CollisionBounds))
            return false;

        _state = SubterrorState.AboveGround;
        _substate = 0;
        _dirtEnabled = false;
        _counter1 = 1;
        _zFixed = -0x100;
        _speedZ = -0x100;
        _speed = 0x28;
        _angle = OracleObjectMovement.Shared.RelativeAngle(
            Position, _linkPosition) ^ 0x10;
        SetCollisionRadii(6, 6);
        Visible = true;
        SetAnimation(5);
        return true;
    }

    internal override bool TakeSwordHit(Vector2 sourcePosition, int damage)
    {
        if (!Vulnerable || _dying || !base.TakeSwordHit(sourcePosition, damage))
            return false;
        _acceptedHit = true;
        _playSound(OracleSoundEngine.SndBossDamage);
        if (IsDead)
            BeginDeath();
        return true;
    }

    internal override bool TakeBurnHit(int damage)
    {
        if (!Vulnerable || _dying || !base.TakeBurnHit(damage))
            return false;
        _acceptedHit = true;
        if (IsDead)
            BeginDeath();
        return true;
    }

    private void UpdateIntro(ICollection<RoomEntitySpawn> spawns)
    {
        _disableLink();
        switch (_substate)
        {
            case 0:
                if (!_shuttersClosed())
                    return;
                _counter2 = DecrementCounter2(_counter2);
                if (_counter2 != 0)
                    return;
                Position += OracleObjectMovement.Shared.Delta(_speed, _angle);
                if (OracleObjectPosition.HighByte(Position.Y) < 0x58)
                    return;

                _playSound(OracleSoundEngine.SndDig);
                Visible = true;
                _dirtEnabled = false;
                SetAnimation(6);
                SetCurrentTile(0x4c);
                _substate = 1;
                return;

            case 1:
                AdvanceAnimation();
                if (AnimationParameter != 0)
                    return;
                spawns.Add(new RockDebrisSpawn(Position, 0x06));
                _substate = 2;
                _counter1 = 60;
                _zFixed = 0;
                _speedZ = -0x200;
                SetAnimation(5);
                return;

            case 2:
                if (!OracleObjectMath.UpdateSpeedZ(
                        ref _zFixed, ref _speedZ, 0x10))
                {
                    return;
                }
                SetAnimation(2);
                _counter1 = DecrementByte(_counter1);
                if (_counter1 != 0)
                    return;
                _showDialogue(0x2f03, _introMessage, Position);
                _substate = 3;
                return;

            case 3:
                if (_dialogueOpen())
                    return;
                _playSound(OracleSoundEngine.MusMiniboss);
                _enableLink();
                _introActive = false;
                BeginDigging();
                return;
        }
    }

    private void BeginDigging()
    {
        _state = SubterrorState.Digging;
        _substate = 0;
        SetAnimation(4);
    }

    private void BeginUndergroundMovement(ICollection<RoomEntitySpawn> spawns)
    {
        _state = SubterrorState.UndergroundMoving;
        _substate = 0;
        _angle = 0xff;
        Visible = false;
        _counter1 = 60;
        _counter2 = DrillAttackWaits[AngerLevel()];
        _playSound(OracleSoundEngine.SndDig);
        SpawnDirt(spawns);
    }

    private void UpdateUnderground(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (_substate == 0)
        {
            _counter1 = DecrementByte(_counter1);
            if (_counter1 != 0)
                return;
            _substate = 1;
            ResetUndergroundMovement(player.Position, spawns);
            return;
        }
        if (_substate == 2)
        {
            _counter2 = DecrementCounter2(_counter2);
            _counter1 = DecrementByte(_counter1);
            if (_counter1 == 0)
            {
                _substate = 1;
                ResetUndergroundMovement(player.Position, spawns);
            }
            return;
        }

        Position += OracleObjectMovement.Shared.Delta(_speed, _angle);
        if (TouchesWallOrHole())
        {
            _substate = 2;
            _counter1 = 90;
            Visible = false;
            _dirtEnabled = false;
            return;
        }

        _counter1 = DecrementByte(_counter1);
        if (_counter1 == 0)
            ResetUndergroundMovement(player.Position, spawns);
        _counter2 = DecrementCounter2(_counter2);
        if (_counter2 != 0 ||
            OracleObjectMath.ToPixelPosition(Position).DistanceTo(
                OracleObjectMath.ToPixelPosition(player.Position)) >= 0x18)
        {
            return;
        }

        int packed = _room.GetPackedPosition(player.Position);
        Position = new Vector2(
            (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
            (packed >> 4) * OracleRoomData.MetatileSize + 8);
        _state = SubterrorState.Drilling;
        _substate = 0;
        _dirtEnabled = false;
        _counter1 = 60;
        _counter2 = 30;
        SetCollisionRadii(6, 6);
        SetAnimation(6);
    }

    private void ResetUndergroundMovement(
        Vector2 linkPosition,
        ICollection<RoomEntitySpawn> spawns)
    {
        int target = OracleObjectMovement.Shared.RelativeAngle(
            Position, linkPosition);
        _angle = ((_angle ^ 0x10) == target)
            ? (target + 8) & 0x1f
            : target;
        _counter1 = 30;
        _speed = UndergroundSpeeds[AngerLevel()];
        SetCollisionRadii(10, 10);
        SpawnDirt(spawns);
    }

    private void UpdateDrilling(ICollection<RoomEntitySpawn> spawns)
    {
        if (_substate == 0)
        {
            if (_counter2 != 0)
            {
                _counter2 = DecrementByte(_counter2);
                if (_counter2 != 0)
                    return;
                Visible = true;
                _playSound(OracleSoundEngine.SndShock);
            }
            AdvanceAnimation();
            _counter1 = DecrementByte(_counter1);
            if (_counter1 != 0)
                return;
            _counter1 = 60;
            SetAnimation(7);
            _substate = 1;
            return;
        }

        AdvanceAnimation();
        if (AnimationParameter != 0)
            return;
        BeginUndergroundMovement(spawns);
        _dirtEnabled = false;
    }

    private void UpdateAboveGround(ICollection<RoomEntitySpawn> spawns)
    {
        if (_substate == 0)
        {
            _movement.MoveAtAngle(_angle, _speed, allowHoles: true);
            if (!OracleObjectMath.UpdateSpeedZ(
                    ref _zFixed, ref _speedZ, 0x10))
            {
                return;
            }

            _acceptedHit = false;
            _counter1 = DecrementByte(_counter1);
            if (_counter1 == 0)
            {
                _speedZ = -0x80;
                return;
            }
            _counter1 = 180;
            _substate = 1;
            return;
        }

        if (_substate == 1)
        {
            if (!_acceptedHit)
            {
                AdvanceAnimation();
                _counter1 = DecrementByte(_counter1);
                if (_counter1 != 0)
                    return;
            }
            _acceptedHit = false;
            _substate = 2;
            _angle = _random.Next().Value & 0x1c;
            _speed = 0x14;
            _counter1 = AboveGroundDurations[_random.Next().Value & 0x03];
            SetAnimationFromAngle();
            return;
        }

        AdvanceAnimation();
        if (AnimationParameter != 0)
            _playSound(OracleSoundEngine.SndLand);
        MoveAndBounce();
        _counter1 = DecrementByte(_counter1);
        if (_counter1 == 0)
            BeginDigging();
    }

    private void SpawnDirt(ICollection<RoomEntitySpawn> spawns)
    {
        _dirtCounter = 7;
        _dirtEnabled = true;
        spawns.Add(new SubterrorDirtSpawn(Position));
        SetCurrentTile(0xef);
    }

    private void SetCurrentTile(int tile) =>
        _room.SetPositionTileAndCollision(
            Position,
            checked((byte)tile),
            collision: null,
            _animationTick());

    private bool TouchesWallOrHole() =>
        EnemyAdjacentWallResolver.Shared.Probe(
            Position,
            _angle,
            IsWallOrHole).Bitset != 0;

    private bool IsWallOrHole(Vector2I point) =>
        point.X < 0 || point.X >= _room.Width ||
        point.Y < 0 || point.Y >= _room.Height ||
        _room.IsSolidForEnemyMovement(point, holesAreWalls: true);

    private void MoveAndBounce()
    {
        Position += OracleObjectMovement.Shared.Delta(_speed, _angle);
        EnemyAdjacentWallProbe walls =
            EnemyAdjacentWallResolver.Shared.Probe(
                Position,
                _angle,
                IsWallOrHole);
        if (walls.Bitset == 0)
            return;
        _angle = EnemyAdjacentWallResolver.Shared.BounceAngle(_angle, walls);
        SetAnimationFromAngle();
    }

    private void SetAnimationFromAngle()
    {
        int direction = ((_angle << 1) & 0xff) >> 4;
        SetAnimation(direction & 0x03);
    }

    private int AngerLevel() =>
        Health >= 0x0a ? 0 : Health >= 0x06 ? 1 : 2;

    private void BeginDeath()
    {
        Revive(1);
        _dying = true;
        _deathCounter = 120;
        _dirtEnabled = false;
        _disableLink();
        _playSound(OracleSoundEngine.SndBossDead);
    }

    private static int DecrementByte(int value) => (value - 1) & 0xff;
    private static int DecrementCounter2(int value) =>
        value == 0 ? 0 : value - 1;
}

internal enum SubterrorState
{
    WaitingForDoors = 8,
    Digging = 9,
    UndergroundMoving = 10,
    Drilling = 11,
    AboveGround = 12
}
