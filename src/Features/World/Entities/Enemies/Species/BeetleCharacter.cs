using Godot;
using System;

namespace oracleofages;

/// <summary>ENEMY_BEETLE $51:$02/$03, including itemDrop_spawnEnemy's entry states.</summary>
internal partial class BeetleCharacter : EnemyCharacter
{
    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private byte[] _counters = null!;
    private Action<int> _playSound = null!;
    private int _zFixed;
    private int _speedZ;
    private bool _collision;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int Angle { get; private set; }
    internal int Speed { get; private set; }
    internal int ZFixed => _zFixed;
    internal int SpeedZ => _speedZ;
    internal override bool CollisionEnabled => base.CollisionEnabled && _collision;
    protected override Vector2 AnimationDrawOffset =>
        base.AnimationDrawOffset + new Vector2(0, _zFixed >> 8);

    internal void Initialize(ImportedEnemyDefinition record, OracleRoomData room,
        Vector2 position, OracleRandom random, byte[] counters, Action<int> playSound)
    {
        if (record.Id != 0x51 || record.SubId is not (2 or 3))
            throw new InvalidOperationException($"beetle.s: unsupported dynamic enemy ${record.Id:x2}:${record.SubId:x2}.");
        Record = record;
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record));
        _random = random;
        _movement = new EnemyTerrainMovement(this, room);
        _counters = counters;
        _playSound = playSound;
        ConfigureSwordKnockback(room, EnemyKnockbackMotion.Terrain, checksHazards: true);
        ConfigureHazards(room, zPosition: () => _zFixed);
        Visible = false;
    }

    internal void UpdateFrame(Vector2 linkPosition, int linkDirection)
    {
        if (IsDead || BeginFrame() || State >= 0x0a && CheckHazards())
            return;
        switch (State)
        {
            case 0:
                State = 8;
                Speed = 0x14; // SPEED_80
                return;
            case 8:
                State = 9;
                Visible = true;
                if (Record.SubId == 2)
                {
                    Counter = 30;
                    Angle = (OracleObjectMovement.Shared.RelativeAngle(
                        OracleObjectMath.ToPixelPosition(Position),
                        OracleObjectMath.ToPixelPosition(linkPosition)) + 4) & 0x18;
                }
                else
                {
                    _speedZ = -0x102;
                    Speed = 0x1e; // SPEED_c0
                    Angle = linkDirection * 8;
                }
                return;
            case 9 when Record.SubId == 2:
                if (--Counter == 0)
                {
                    State = 0x0a;
                    Counter = 1;
                    UpdateWandering();
                    return;
                }
                if (Counter == 22)
                    _collision = true;
                _movement.MoveAtAngle(Angle, Speed, allowHoles: true);
                AdvanceAnimation();
                return;
            case 9:
                if (OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, 0x0e))
                {
                    int bouncedSpeed = -_speedZ >> 1;
                    if (bouncedSpeed > -0x80 || bouncedSpeed == 0)
                    {
                        State = 0x0a;
                        Speed = 0x14;
                        ChooseDirection();
                        return;
                    }
                    _speedZ = bouncedSpeed;
                    _playSound(OracleSoundEngine.SndBombLand);
                }
                if ((_speedZ >> 8) == 0)
                    _collision = true;
                _movement.MoveAtAngle(Angle, Speed, allowHoles: false);
                QueueRedraw();
                return;
            case 0x0a:
                UpdateWandering();
                return;
            default:
                throw new InvalidOperationException($"beetle.s:$51:${Record.SubId:x2} unsupported state ${State:x2}.");
        }
    }

    internal void OnWeaponHit()
    {
        // enemyCode51: only subid $02 resets its walk state on JUST_HIT.
        if (Record.SubId != 2)
            return;
        State = 0x0a;
        Counter = 1;
    }

    private void UpdateWandering()
    {
        Counter = (Counter - 1) & 0xff;
        if (Counter == 0)
            ChooseDirection();
        _movement.MoveAtAngle(Angle, Speed, allowHoles: false);
        AdvanceAnimation();
    }

    private void ChooseDirection()
    {
        OracleRandomResult roll = _random.Next();
        Angle = roll.Low & 0x1c;
        Counter = _counters[roll.High & 7];
    }
}
