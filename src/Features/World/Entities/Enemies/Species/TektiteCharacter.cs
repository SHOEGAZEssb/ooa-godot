using Godot;
using System;

namespace oracleofages;

/// <summary>ENEMY_TEKTITE $30: tektite.s states $08-$0b, including the
/// separate crouch, ready and launch updates and signed 8.8 jump integration.</summary>
internal partial class TektiteCharacter : EnemyCharacter
{
    private readonly TektiteBehaviorProfile _behavior = EnemyBehaviorTables.Shared.Tektite;
    private OracleRandom _random = null!;
    private OracleRoomData _room = null!;
    private EnemyTerrainMovement _movement = null!;
    private Action<int> _soundRequested = null!;
    private int _minimumWait;
    private int _zFixed;
    private int _speedZ;

    internal ImportedEnemyDefinition Record { get; private set; }
    internal TektiteState State { get; private set; }
    internal int Counter1 { get; private set; }
    internal int Counter2 { get; private set; }
    internal int Angle { get; private set; }
    internal int Gravity { get; private set; }
    internal int ZFixed => _zFixed;
    internal int ZHigh => _zFixed >> 8;
    internal int SpeedZ => _speedZ;
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset + new Vector2(0, ZHigh);

    internal void Initialize(ImportedEnemyDefinition record, OracleRoomData room,
        Vector2 position, OracleRandom random, Action<int> soundRequested)
    {
        Record = record;
        _room = room;
        _random = random;
        _soundRequested = soundRequested;
        _movement = new EnemyTerrainMovement(this, room);
        _minimumWait = (record.SubId & 1) == 0 ? _behavior.EvenSubIdWait : _behavior.OddSubIdWait;
        State = TektiteState.Uninitialized;
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record));
        ConfigureSwordKnockback(room, EnemyKnockbackMotion.Terrain, checksHazards: true);
        ConfigureHazards(room, zPosition: () => ZHigh);
        Visible = false;
    }

    internal void UpdateFrame(Vector2 target)
    {
        if (IsDead || CheckHazards() || BeginFrame()) return;
        switch (State)
        {
            case TektiteState.Uninitialized:
                InitializeState();
                return;
            case TektiteState.Waiting:
                Counter1 = (Counter1 - 1) & 0xff;
                if (Counter1 != 0) { AdvanceAnimation(); return; }
                // This random counter1 write is overwritten on landing, but
                // its global RNG call must still occur before the crouch.
                Counter1 = (_random.Next().Value & _behavior.WaitMask) + _minimumWait;
                Counter2 = _behavior.CrouchFrames;
                State = TektiteState.Crouching;
                RestartAnimation(1);
                return;
            case TektiteState.Crouching:
                if (--Counter2 != 0) return;
                State = TektiteState.Ready;
                RestartAnimation(2);
                return;
            case TektiteState.Ready:
                bool big = (_random.Next().Value & _behavior.BigLeapMask) == 0;
                _speedZ = big ? _behavior.BigLeapSpeedZ : _behavior.SmallLeapSpeedZ;
                Gravity = big ? _behavior.BigLeapGravity : _behavior.SmallLeapGravity;
                Angle = OracleObjectMovement.Shared.RelativeAngle(Position, target);
                State = TektiteState.Leaping;
                ZIndex = 11; // objectSetVisiblec1; ground uses objectSetVisiblec2.
                _soundRequested(_behavior.JumpSound);
                return;
            case TektiteState.Leaping:
                Angle = EnemyAdjacentWallResolver.Shared.BounceAngle(Position, Angle,
                    point => point.X < 0 || point.X >= _room.Width || point.Y < 0 || point.Y >= _room.Height);
                if (!OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, Gravity))
                {
                    _movement.MoveAtAngle(Angle, _behavior.SpeedRaw, allowHoles: true);
                    QueueRedraw();
                    return;
                }
                Counter1 = (_random.Next().Value & _behavior.WaitMask) + _minimumWait;
                State = TektiteState.Waiting;
                RestartAnimation(0);
                ZIndex = 10;
                QueueRedraw();
                return;
        }
    }

    internal ScreenTransitionPresentation PrepareForScreenTransition()
    {
        if (State == TektiteState.Uninitialized) InitializeState();
        return ScreenTransitionPresentation.Visible;
    }

    private void InitializeState()
    {
        // bank0.s:enemyStandardUpdate initializes var3d before enemyCode30.
        _random.Next();
        Counter1 = (_random.Next().Value & _behavior.WaitMask) + 1;
        State = TektiteState.Waiting;
        Visible = true;
        ZIndex = 10;
    }

    internal override bool TakeBurnHit(int damage) => base.TakeBurnHit(Health);
}

internal enum TektiteState
{
    Uninitialized, Waiting = 8, Crouching, Ready, Leaping
}
