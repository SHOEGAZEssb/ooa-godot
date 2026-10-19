using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class GiantGhiniBoss : EnemyCharacter
{
    internal override bool InitializationPending => !_initialized;


    private OracleRandom _random = null!;
    private OracleRoomData _room = null!;
    private Func<Vector2,Vector2> _worldToScreen = null!;
    private GiantGhiniBossBossState _state;
    private int _counter = 120;
    private int _angle;
    private int _targetPacked;
    private int _speed = 0x1e;
    private int _nudgeCounter = 2;
    private int _floatCounter = 16;
    private float _z = -8;
    private float _zSpeed = 0.25f;
    private bool _childRequestsCharge;
    private int _childrenAlive;
    private int _childRespawnCounter;
    private int _targetScreenY;
    private Action<int> _playSound = null!;
    private Func<bool> _shuttersClosed = null!;
    private Action _disableLinkCollisionsAndMenu = null!;
    private Action _restoreRoomMusic = null!;
    private bool _initialized;
    private bool _introDoorsClosed;
    private bool _dying;
    private int _deathCounter;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal GiantGhiniBossBossState State => _state;
    internal int ChildrenAlive => _childrenAlive;
    internal bool Defeated => _dying || IsDead;
    internal bool DrawEnabled =>
        CollisionEnabled || _state == GiantGhiniBossBossState.IntroFlicker || _dying;
    protected override int SwordInvincibilityFrames => 0x20;
    internal override bool CollisionEnabled =>
        base.CollisionEnabled && !_dying &&
        (_state is GiantGhiniBossBossState.Moving or GiantGhiniBossBossState.Charging);

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random,
        Action<int> playSound,
        Func<bool> shuttersClosed,
        Action disableLinkCollisionsAndMenu,
        Action restoreRoomMusic,
        Func<Vector2,Vector2> worldToScreen)
    {
        Record = record;
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record));
        _room = room;
        _random = random;
        _playSound = playSound;
        _shuttersClosed = shuttersClosed;
        _disableLinkCollisionsAndMenu = disableLinkCollisionsAndMenu;
        _restoreRoomMusic = restoreRoomMusic;
        _worldToScreen = worldToScreen;
        Visible = true;
    }

    internal void UpdateFrame(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (IsDead)
            return;
        BeginFrame();
        if (!_initialized)
        {
            _random.Next(); // enemyStandardUpdate's common var3d initialization.
            _initialized = true;
            _counter = 120;
            _playSound(SoundId.SndCtrlStopMusic);
            SpawnChildren(spawns);
            return;
        }
        if (_dying)
        {
            Visible = (_deathCounter & 1) != 0;
            if (--_deathCounter == 0)
            {
                if (!TrySpawnNativePart(new BossDeathExplosionSpawn(Position, Record.Id), spawns))
                { _deathCounter = 1; return; }
                Finish();
                _restoreRoomMusic();
            }
            QueueRedraw();
            return;
        }
        UpdateFloat();
        switch (_state)
        {
            case GiantGhiniBossBossState.IntroWait:
                if (!_introDoorsClosed)
                {
                    if (!_shuttersClosed())
                        return;
                    _introDoorsClosed = true;
                    _counter = 120;
                    return;
                }
                if (--_counter != 0)
                    return;
                _counter = 60;
                _state = GiantGhiniBossBossState.IntroFlicker;
                spawns.Add(new BossShadowSpawn(
                    () => Position,
                    () => Mathf.FloorToInt(_z),
                    () => !IsDead,
                    Size: 1,
                    YOffset: 12));
                return;

            case GiantGhiniBossBossState.IntroFlicker:
                Visible = !Visible;
                if (--_counter != 0)
                    return;
                Visible = true;
                _state = GiantGhiniBossBossState.Moving;
                _playSound(SoundId.MusMiniboss);
                ChooseTargetAngle(player.Position);
                _nudgeCounter=2;
                _childRequestsCharge=false;
                SetAnimation(0);
                SetChildRespawnTimer();
                return;

            case GiantGhiniBossBossState.Moving:
                if (_childRequestsCharge)
                {
                    BeginCharge(player.Position);
                    QueueRedraw();
                    return;
                }
                AdvanceAnimation();
                Move(_angle, _speed);
                if (--_nudgeCounter == 0)
                {
                    _nudgeCounter = 2;
                    // The target helper can reverse the live angle and replace
                    // this counter. Read that updated angle for the nudge.
                    int targetAngle=TargetAngle(player.Position);
                    _angle=OracleObjectMath.NudgeAngleTowards(_angle,targetAngle);
                }
                if (_childRespawnCounter > 0)
                    _childRespawnCounter--;
                if (_childRespawnCounter == 0)
                {
                    SetChildRespawnTimer();
                    if (_childrenAlive == 0 && (_random.Next().Value & 3) != 0)
                        SpawnChildren(spawns);
                }
                break;

            case GiantGhiniBossBossState.Charging:
                AdvanceAnimation();
                _counter = Math.Max(0,_counter-1); // ecom_decCounter2 saturates at zero.
                if ((_counter & 3) == 0)
                    _speed = Math.Min(0x78, _speed + 0x05);
                Move(_angle, _speed);
                if (_childRequestsCharge)
                    UpdateChargeTarget(player.Position);
                Vector2 target = PackedPositionCenter(_targetPacked);
                _angle = OracleObjectMovement.Shared.RelativeAngle(
                    Position, target);
                if (_room.GetPackedPosition(Position) == _targetPacked)
                {
                    _state = GiantGhiniBossBossState.Moving;
                    _speed = 0x1e;
                    _childRequestsCharge = false;
                    SetAnimation(0);
                    ChooseTargetAngle(player.Position);
                    _nudgeCounter=2;
                    SetChildRespawnTimer();
                }
                break;
        }
        QueueRedraw();
    }

    internal void ChildAttached(Player player)
    {
        _childRequestsCharge = true;
    }

    internal void ChildDetached() => _childRequestsCharge = false;
    internal void ChildFinished() => _childrenAlive = Math.Max(0, _childrenAlive - 1);

    internal override bool TakeSwordHit(Vector2 sourcePosition, int damage)
    {
        if (_dying || !base.TakeSwordHit(sourcePosition, damage))
            return false;
        _playSound(SoundId.SndBossDamage);
        if (IsDead)
            BeginDeath();
        return true;
    }

    internal override bool TakeBurnHit(int damage)
    {
        if (_dying || !base.TakeBurnHit(damage))
            return false;
        if (IsDead)
            BeginDeath();
        return true;
    }

    public override void _Draw()
    {
        if (!DrawEnabled)
            return;
        DrawSetTransform(Vector2.Down * _z);
        DrawCurrentAnimation();
        DrawSetTransform(Vector2.Zero);
    }

    private void SpawnChildren(ICollection<RoomEntitySpawn> spawns)
    {
        // giantGhini_spawnChildren allocates subids 3, 2, 1 in that order:
        // right, up, then left around the parent.
        for (int index = 0; index < 3; index++)
        {
            if (!TrySpawnNativeEnemy(new GiantGhiniChildSpawn(this, index), spawns)) break;
            _childrenAlive++;
        }
    }

    private void BeginDeath()
    {
        Revive(1);
        _dying = true;
        _deathCounter = 120;
        _disableLinkCollisionsAndMenu();
        _playSound(SoundId.SndBossDead);
    }

    private void BeginCharge(Vector2 linkPosition)
    {
        _state = GiantGhiniBossBossState.Charging;
        _counter = 150;
        _speed = 0x05;
        SetAnimation(1);
        UpdateChargeTarget(linkPosition);
    }

    private void UpdateChargeTarget(Vector2 linkPosition)
    {
        _targetPacked = _room.GetPackedPosition(linkPosition);
        _angle = OracleObjectMovement.Shared.RelativeAngle(
            Position, linkPosition);
    }

    private void ChooseTargetAngle(Vector2 linkPosition) =>
        _angle = TargetAngle(linkPosition);

    private int TargetAngle(Vector2 linkPosition)
    {
        Vector2 camera=-_worldToScreen(Vector2.Zero);
        int cameraX=OracleObjectPosition.HighByte(camera.X);
        int cameraY=OracleObjectPosition.HighByte(camera.Y);
        int relativeY=(OracleObjectPosition.HighByte(linkPosition.Y)-cameraY)&0xff;
        int targetScreenY = relativeY < 72.0f ? 104 : 40;
        if (_targetScreenY != targetScreenY)
        {
            _targetScreenY = targetScreenY;
            _angle = OracleObjectMovement.Shared.RelativeAngle(
                Position, linkPosition) ^ ObjectAngle.HalfTurn;
            _nudgeCounter = 10;
        }
        return OracleObjectMovement.Shared.RelativeAngle(
            Position, new Vector2((cameraX+80)&0xff,(cameraY+targetScreenY)&0xff));
    }

    private void Move(int angle, int speed) =>
        Position=ApplyMovementSpeed(OracleObjectPosition.FromPixels(Position),speed,angle).PrecisePosition;

    private void UpdateFloat()
    {
        _z += _zSpeed;
        if (--_floatCounter != 0)
            return;
        _floatCounter = 16;
        _zSpeed = -_zSpeed;
    }

    private void SetChildRespawnTimer() =>
        _childRespawnCounter = (_random.Next().Value & 3) * 60;

    private static Vector2 PackedPositionCenter(int packed) => new(
        (packed & 0x0f) * 16 + 8,
        (packed >> 4) * 16 + 8);
}

internal enum GiantGhiniBossBossState
{
    IntroWait,
    IntroFlicker,
    Moving,
    Charging
}
