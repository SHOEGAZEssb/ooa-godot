using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>ENEMY_SHADOW_HAG_BUG $42, an uncounted Shadow Hag child.</summary>
internal sealed partial class ShadowHagBug : EnemyCharacter
{
    private ShadowHagBoss _owner = null!;
    private OracleRandom _random = null!;
    private ShadowHagBugState _state;
    private int _counter1;
    private int _counter2;
    private int _angle;
    private int _zFixed;
    private int _speedZ;
    private bool _initialized;
    private bool _reportedFinished;
    private bool _deathPuff;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal ShadowHagBugState State => _state;
    internal int Counter1 => _counter1;
    internal int Counter2 => _counter2;
    internal int Angle => _angle;
    internal int ZFixed => _zFixed;
    internal bool DeathPuff => _deathPuff;
    protected override Vector2 AnimationDrawOffset =>
        base.AnimationDrawOffset + Vector2.Down * (_zFixed / 256.0f);

    internal void Initialize(
        ImportedEnemyDefinition record,
        ShadowHagBoss owner,
        OracleRandom random,
        Vector2 position)
    {
        Record = record;
        _owner = owner;
        _random = random;
        InitializeEnemy(
            position,
            EnemyCharacterConfiguration.FromImported(record));
        Name = "ShadowHagBug";
        ZIndex = 10;
        Visible = false;
    }

    internal void UpdateFrame(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = spawns;
        if (_owner.Defeated && !IsDead)
        {
            _deathPuff = true;
            FinishAndReport();
            return;
        }
        if (IsDead)
        {
            ReportFinished();
            return;
        }
        if (BeginFrame())
            return;
        if (!_initialized)
        {
            _initialized = true;
            _state = ShadowHagBugState.Airborne;
            _speedZ = -0xe0;
            _angle = _random.Next().Value & 0x1f;
            Visible = true;
            QueueRedraw();
            return;
        }

        if (_state == ShadowHagBugState.Airborne)
        {
            if (!OracleObjectMath.UpdateSpeedZ(
                    ref _zFixed, ref _speedZ, 0x12))
            {
                MoveAndAnimate();
                return;
            }
            _state = ShadowHagBugState.Chasing;
            _counter1 = _random.Next().Value;
            _counter2 = 180;
        }

        _counter2 = DecrementByte(_counter2);
        if (_counter2 == 0)
        {
            FinishAndReport();
            return;
        }
        if (_counter2 < 30)
            Visible = !Visible;
        _counter1 = DecrementByte(_counter1);
        if ((_counter1 & 7) == 0)
        {
            OracleRandomResult result = _random.Next();
            Vector2 target = new(
                player.Position.X + (result.Low & 0x0f) - 8,
                player.Position.Y + (result.High & 0x0f) - 8);
            int targetAngle = OracleObjectMovement.Shared.RelativeAngle(
                Position, target);
            _angle = NudgeAngle(_angle, targetAngle);
        }
        MoveAndAnimate();
    }

    internal override bool TakeSwordHit(Vector2 sourcePosition, int damage)
    {
        if (!base.TakeSwordHit(sourcePosition, damage))
            return false;
        if (IsDead)
        {
            _deathPuff = true;
            ReportFinished();
        }
        return true;
    }

    internal override bool TakeBurnHit(int damage)
    {
        if (!base.TakeBurnHit(damage))
            return false;
        if (IsDead)
        {
            _deathPuff = true;
            ReportFinished();
        }
        return true;
    }

    private void MoveAndAnimate()
    {
        Position += OracleObjectMovement.Shared.Delta(0x0f, _angle);
        AdvanceAnimation();
        QueueRedraw();
    }

    private void FinishAndReport()
    {
        Finish();
        ReportFinished();
    }

    private void ReportFinished()
    {
        if (_reportedFinished)
            return;
        _reportedFinished = true;
        _owner.BugFinished();
    }

    private static int NudgeAngle(int current, int target)
    {
        int clockwise = (target - current) & 0x1f;
        return clockwise == 0 ? current
            : (current + (clockwise < 0x10 ? 1 : -1)) & 0x1f;
    }

    private static int DecrementByte(int value) => (value - 1) & 0xff;
}

internal enum ShadowHagBugState
{
    Airborne,
    Chasing
}
