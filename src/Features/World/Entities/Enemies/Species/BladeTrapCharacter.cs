using Godot;
using System;

namespace oracleofages;

/// <summary>ENEMY_BLADE_TRAP $0e:$01, the blue center-limited trap.</summary>
internal partial class BladeTrapCharacter : EnemyCharacter
{
    private readonly System.Collections.Generic.IReadOnlyList<EnemyBehaviorValue> _behavior =
        EnemyBehaviorTables.Shared.BladeTrap;
    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private EnemyTerrainMovement _movement = null!;
    private Action<int> _sound = null!;
    internal ImportedEnemyDefinition Record { get; private set; }
    internal BladeTrapState State { get; private set; }
    internal int Angle { get; private set; }
    internal int SpeedRaw { get; private set; }
    internal int Counter { get; private set; }

    internal void Initialize(ImportedEnemyDefinition record, OracleRoomData room,
        Vector2 position, OracleRandom random, Action<int> sound)
    {
        if (record is not { Id: 0x0e, SubId: 1 })
            throw new ArgumentException("Blade trap requires imported $0e:$01.", nameof(record));
        Record = record;
        _room = room;
        _random = random;
        _sound = sound;
        _movement = new EnemyTerrainMovement(this, room);
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record));
        Visible = false;
    }

    internal void UpdateFrame(Vector2 target)
    {
        if (BeginFrame()) return;
        // enemyCode0e animates before its dispatch, including the waiting and
        // cooldown states. It never calls the common hazard handler.
        AdvanceAnimation();
        switch (State)
        {
            case BladeTrapState.Uninitialized:
                _random.Next(); // enemyStandardUpdate initializes var3d.
                SpeedRaw = _behavior[0].Value;
                State = BladeTrapState.Initializing;
                Visible = true;
                return;
            case BladeTrapState.Initializing:
                State = BladeTrapState.Waiting;
                goto case BladeTrapState.Waiting;
            case BladeTrapState.Waiting:
                if (!CheckAligned(target) || !HasUnobstructedTarget(target)) return;
                var walls = EnemyAdjacentWallResolver.Shared.ProbeTopDown(Position, Angle,
                    point => _room.IsSolidForEnemyMovement(point, holesAreWalls: true));
                if (walls.Bitset != 0) return;
                SpeedRaw = _behavior[2].Value;
                State = BladeTrapState.Charging;
                _sound(OracleSoundEngine.SndUnknown5);
                return;
            case BladeTrapState.Charging:
                bool moved = Move();
                int coordinate = Mathf.FloorToInt((Angle & 8) == 0 ? Position.Y : Position.X);
                int center = _behavior[(Angle & 8) == 0 ? 5 : 4].Value;
                int radius = _behavior[6].Value;
                if (moved && ((coordinate - center + radius) & 0xff) >= radius * 2 + 1) return;
                Angle ^= 0x10;
                SpeedRaw = _behavior[3].Value;
                State = BladeTrapState.Retracting;
                _sound(OracleSoundEngine.SndClink);
                return;
            case BladeTrapState.Retracting:
                if (Move()) return;
                State = BladeTrapState.Cooldown;
                Counter = _behavior[7].Value;
                return;
            case BladeTrapState.Cooldown:
                if (--Counter == 0) State = BladeTrapState.Waiting;
                return;
        }
    }

    internal void PrepareForScreenTransition()
    {
        // bank0._updateEnemiesIfStateIsZero runs enemyStandardUpdate and
        // enemyCode0e's animation/state0 once even while scrolling or text
        // freezes state8+. This includes the ordered var3d RNG draw.
        if (State == BladeTrapState.Uninitialized)
            UpdateFrame(Vector2.Zero);
    }

    private bool Move() => _movement.MoveUsingAdjacentWalls(Angle, SpeedRaw,
        allowHoles: false, topDown: true);

    private bool CheckAligned(Vector2 target)
    {
        int x = Mathf.FloorToInt(Position.X), y = Mathf.FloorToInt(Position.Y);
        int tx = Mathf.FloorToInt(target.X), ty = Mathf.FloorToInt(target.Y);
        int radius = _behavior[1].Value;
        // bladeTrap_checkLinkAligned prefers a vertical attack when both
        // axes qualify, and compares high bytes with unsigned wrapping.
        if (((tx - x + radius) & 0xff) < radius * 2 + 1)
            Angle = (ty & 0xff) < (y & 0xff) ? 0 : 0x10;
        else if (((ty - y + radius) & 0xff) < radius * 2 + 1)
            Angle = (tx & 0xff) < (x & 0xff) ? 0x18 : 8;
        else return false;
        return true;
    }

    private bool HasUnobstructedTarget(Vector2 target)
    {
        Vector2I position = new(Mathf.FloorToInt(Position.X), Mathf.FloorToInt(Position.Y));
        int dx = Mathf.FloorToInt(target.X) - position.X;
        int dy = Mathf.FloorToInt(target.Y) - position.Y;
        if (((dx + 4) & 0xff) < 9 && ((dy + 4) & 0xff) < 9) return false;
        int distance = (Angle & 8) == 0 ? dy : dx;
        int count = Math.Max(1, (Math.Abs(distance) >> 4) & 0x0f);
        Vector2I offset = (Vector2I)OracleObjectMath.CardinalVector(Angle) * 16;
        for (int i = 0; i < count; i++)
        {
            position += offset;
            position = new(position.X & 0xff, position.Y & 0xff);
            // The source reads wRoomCollisions directly, not the quadrant
            // under Link or the trap. Even a partly solid tile blocks sight.
            if (_room.GetTerrainInfo(position).Collision != 0) return false;
        }
        return true;
    }

    internal override bool TakeSwordHit(Vector2 _, int __) => AcceptArmoredSwordHit(28);
    internal bool TakeSwitchHookHit() => AcceptArmoredSwordHit(20);
    internal override bool TakeBurnHit(int _) => false;
}

internal enum BladeTrapState
{
    Uninitialized = 0,
    Initializing = 8,
    Waiting = 9,
    Charging = 10,
    Retracting = 11,
    Cooldown = 12
}
