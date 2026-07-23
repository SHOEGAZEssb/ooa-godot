using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class GiantGhiniChild : SpiritsGraveEnemyCharacter
{

    private GiantGhiniBoss _owner = null!;
    private ChildState _state;
    private int _counter;
    private int _angle;
    private bool _reportedFinished;
    private bool _spawnPuffPending;
    private bool _slowsLink;
    private const float Z = -4.0f;

    internal ChildState State => _state;
    internal int Counter => _counter;
    internal override bool CollisionEnabled =>
        base.CollisionEnabled && _state is ChildState.SpawnDelay or
            ChildState.Charging or ChildState.Attached;
    internal bool SlowsLink => _slowsLink;
    internal bool DisablesItems => _state == ChildState.Attached;

    internal void Initialize(
        EnemyRecord record,
        GiantGhiniBoss owner,
        int index)
    {
        Vector2[] offsets = { Vector2.Right * 24, Vector2.Up * 24, Vector2.Left * 24 };
        _owner = owner;
        InitializeEnemy(record, owner.Position + offsets[index]);
        if (owner.State is GiantGhiniBossBossState.IntroWait or
            GiantGhiniBossBossState.IntroFlicker)
        {
            _state = ChildState.Waiting;
            Visible = false;
        }
        else
        {
            _state = ChildState.SpawnDelay;
            _counter = 30;
            _spawnPuffPending = true;
        }
    }

    internal void UpdateFrame(
        Player player,
        bool anyButtonJustPressed,
        int frameCounter,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (_owner.Defeated && !IsDead)
        {
            Finish();
            ReportFinished();
            spawns.Add(new EnemyDeathPuffSpawn(Position, EnemyId: Record.Id));
            return;
        }
        if (IsDead)
        {
            ReportFinished();
            return;
        }
        BeginFrame();
        _slowsLink = false;
        if (_spawnPuffPending)
        {
            _spawnPuffPending = false;
            spawns.Add(new PuzzlePuffSpawn(
                Position, OracleSoundEngine.SndPoof));
        }
        switch (_state)
        {
            case ChildState.Waiting:
                if (_owner.State is GiantGhiniBossBossState.Moving or
                    GiantGhiniBossBossState.Charging)
                {
                    _state = ChildState.Charging;
                    _counter = 5;
                    _angle = OracleObjectMath.AngleToward(Position, player.Position);
                    Visible = true;
                }
                break;
            case ChildState.SpawnDelay:
                if (--_counter == 0)
                {
                    _state = ChildState.Charging;
                    _counter = 5;
                    _angle = OracleObjectMath.AngleToward(Position, player.Position);
                }
                break;
            case ChildState.Charging:
                Position += OracleObjectMath.VectorFromAngle32(_angle) * 0.75f;
                if (--_counter == 0)
                {
                    _counter = 5;
                    _angle = NudgeAngle(
                        _angle, OracleObjectMath.AngleToward(Position, player.Position));
                }
                break;
            case ChildState.Attached:
                Position = player.Position;
                _counter--;
                if (_counter == 0)
                {
                    _state = ChildState.Fading;
                    _counter = 60;
                    _owner.ChildDetached();
                    break;
                }
                if (anyButtonJustPressed)
                    _counter = _counter >= 3 ? _counter - 3 : 1;
                if ((_counter & 3) == 0)
                    Visible = !Visible;
                _slowsLink = (frameCounter & 1) != 0;
                break;
            case ChildState.Fading:
                Visible = !Visible;
                if (--_counter == 0)
                {
                    Finish();
                    ReportFinished();
                }
                break;
        }
        AdvanceAnimation();
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawSetTransform(Vector2.Down * Z);
        base._Draw();
        DrawSetTransform(Vector2.Zero);
    }

    internal void HandleLinkContact(Player player)
    {
        if (_state != ChildState.Charging || !OverlapsLink(player.Position))
            return;
        _state = ChildState.Attached;
        _counter = 120;
        _owner.ChildAttached(player);
    }

    internal override bool TakeSwordHit(Vector2 sourcePosition, int damage)
    {
        bool hit = base.TakeSwordHit(sourcePosition, damage);
        if (hit && IsDead)
            ReportFinished();
        return hit;
    }

    private void ReportFinished()
    {
        if (_reportedFinished)
            return;
        _reportedFinished = true;
        _owner.ChildFinished();
    }

    private static int NudgeAngle(int current, int target)
    {
        int clockwise = (target - current) & 0x1f;
        return clockwise == 0 ? current
            : (current + (clockwise < 0x10 ? 1 : -1)) & 0x1f;
    }
}

internal enum ChildState
{
    Waiting,
    SpawnDelay,
    Charging,
    Attached,
    Fading
}
