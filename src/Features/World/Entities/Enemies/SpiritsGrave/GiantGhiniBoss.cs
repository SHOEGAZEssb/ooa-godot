using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class GiantGhiniBoss : EnemyCharacter
{

    private OracleRandom _random = null!;
    private OracleRoomData _room = null!;
    private GiantGhiniBossBossState _state;
    private int _counter = 120;
    private int _angle;
    private int _targetPacked;
    private float _speed = 0.75f;
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
    internal int Counter => _counter;
    internal int ChildrenAlive => _childrenAlive;
    internal bool Defeated => _dying || IsDead;
    internal bool DrawEnabled =>
        CollisionEnabled || _state == GiantGhiniBossBossState.IntroFlicker || _dying;
    internal float Z => _z;
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
        Action restoreRoomMusic)
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
            _initialized = true;
            _counter = 120;
            _playSound(OracleSoundEngine.SndCtrlStopMusic);
            SpawnChildren(spawns);
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
                _playSound(OracleSoundEngine.MusMiniboss);
                ChooseTargetAngle(player.Position);
                SetAnimation(0);
                SetChildRespawnTimer();
                return;

            case GiantGhiniBossBossState.Moving:
                Move(_angle, _speed);
                if (--_nudgeCounter == 0)
                {
                    _nudgeCounter = 2;
                    _angle = NudgeAngle(_angle, TargetAngle(player.Position));
                }
                if (_childRequestsCharge)
                {
                    BeginCharge(player.Position);
                    break;
                }
                if (_childRespawnCounter > 0)
                    _childRespawnCounter--;
                if (_childRespawnCounter == 0 && _childrenAlive == 0)
                {
                    SetChildRespawnTimer();
                    if ((_random.Next().Value & 3) != 0)
                        SpawnChildren(spawns);
                }
                break;

            case GiantGhiniBossBossState.Charging:
                _counter = (_counter - 1) & 0xff;
                if ((_counter & 3) == 0)
                    _speed = Math.Min(3.0f, _speed + 0.125f);
                Move(_angle, _speed);
                if (_childRequestsCharge)
                    UpdateChargeTarget(player.Position);
                Vector2 target = PackedPositionCenter(_targetPacked);
                _angle = OracleObjectMath.AngleToward(Position, target);
                if (_room.GetPackedPosition(Position) == _targetPacked)
                {
                    _state = GiantGhiniBossBossState.Moving;
                    _speed = 0.75f;
                    _childRequestsCharge = false;
                    SetAnimation(0);
                    ChooseTargetAngle(player.Position);
                }
                break;
        }
        AdvanceAnimation();
        QueueRedraw();
    }

    internal void ChildAttached(Player player)
    {
        _childRequestsCharge = true;
        if (_state == GiantGhiniBossBossState.Moving)
            BeginCharge(player.Position);
    }

    internal void ChildDetached() => _childRequestsCharge = false;
    internal void ChildFinished() => _childrenAlive = Math.Max(0, _childrenAlive - 1);

    internal override bool TakeSwordHit(Vector2 sourcePosition, int damage)
    {
        if (_dying || !base.TakeSwordHit(sourcePosition, damage))
            return false;
        _playSound(OracleSoundEngine.SndBossDamage);
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
        _childrenAlive = 3;
        // giantGhini_spawnChildren allocates subids 3, 2, 1 in that order:
        // right, up, then left around the parent.
        for (int index = 0; index < 3; index++)
            spawns.Add(new GiantGhiniChildSpawn(this, index));
    }

    private void BeginDeath()
    {
        Revive(1);
        _dying = true;
        _deathCounter = 120;
        _disableLinkCollisionsAndMenu();
        _playSound(OracleSoundEngine.SndBossDead);
    }

    private void BeginCharge(Vector2 linkPosition)
    {
        _state = GiantGhiniBossBossState.Charging;
        _counter = 150;
        _speed = 0.125f;
        SetAnimation(1);
        UpdateChargeTarget(linkPosition);
    }

    private void UpdateChargeTarget(Vector2 linkPosition)
    {
        _targetPacked = _room.GetPackedPosition(linkPosition);
        _angle = OracleObjectMath.AngleToward(Position, linkPosition);
    }

    private void ChooseTargetAngle(Vector2 linkPosition) =>
        _angle = TargetAngle(linkPosition);

    private int TargetAngle(Vector2 linkPosition)
    {
        Vector2 camera = CameraOrigin(linkPosition);
        float relativeY = linkPosition.Y - camera.Y;
        int targetScreenY = relativeY < 72.0f ? 104 : 40;
        if (_targetScreenY != targetScreenY)
        {
            _targetScreenY = targetScreenY;
            _angle = OracleObjectMath.AngleToward(Position, linkPosition) ^ 0x10;
            _nudgeCounter = 10;
        }
        return OracleObjectMath.AngleToward(
            Position, camera + new Vector2(80, targetScreenY));
    }

    private void Move(int angle, float speed) =>
        Position += OracleObjectMath.VectorFromAngle32(angle) * speed;

    private Vector2 CameraOrigin(Vector2 linkPosition) => new(
        Mathf.Clamp(
            linkPosition.X - OracleRoomData.ViewportWidth / 2.0f,
            0.0f,
            Math.Max(0.0f, _room.Width - OracleRoomData.ViewportWidth)),
        Mathf.Clamp(
            linkPosition.Y - OracleRoomData.ViewportHeight / 2.0f,
            0.0f,
            Math.Max(0.0f, _room.Height - OracleRoomData.ViewportHeight)));

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

    private static int NudgeAngle(int current, int target)
    {
        int clockwise = (target - current) & 0x1f;
        if (clockwise == 0)
            return current;
        return (current + (clockwise < 0x10 ? 1 : -1)) & 0x1f;
    }

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
