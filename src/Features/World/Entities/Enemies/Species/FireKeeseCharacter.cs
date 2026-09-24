using Godot;
using System;

namespace oracleofages;

internal partial class FireKeeseCharacter : EnemyCharacter
{
    private readonly FireKeeseBehaviorProfile _behavior = EnemyBehaviorTables.Shared.FireKeese;
    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private bool _linkHitPending;
    private bool _otherHitPending;
    private int _rotation;
    private int _scanPosition;
    private int _stunCounter;
    internal int StunCounter => _stunCounter;
    internal bool HasPendingHit => _linkHitPending || _otherHitPending;
    internal void ClearSeedStun() => _stunCounter = 0;
    private int _speedZ;
    internal ImportedEnemyDefinition Record { get; private set; }
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int Angle { get; private set; }
    internal int Speed { get; private set; }
    internal int ZFixed { get; private set; }
    internal bool Lit { get; private set; } = true;
    internal int ScanPosition => _scanPosition;
    internal Vector2 TorchPosition { get; private set; }
    internal int DamageQuarters => Lit ? _behavior.LitDamage : _behavior.UnlitDamage;
    internal override Texture2D CurrentDrawTexture => !Lit && !DrawsDamagePalette
        ? Animation.CurrentTextureForPalette(1) : base.CurrentDrawTexture;
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset + new Vector2(0, ZFixed >> 8);

    internal void Initialize(ImportedEnemyDefinition record, OracleRoomData room, Vector2 position, OracleRandom random)
    {
        if (record.SubId != 0 || room.Layout.Length != _behavior.LayoutSize)
            throw new InvalidOperationException($"ENEMY_FIRE_KEESE ${record.Id:x2}:${record.SubId:x2} requires the source subid00 and 176-byte room layout.");
        Record = record;
        _room = room;
        _random = random;
        InitializeEnemy(position, EnemyCharacterConfiguration.FromImported(record), paletteVariants: [1]);
        ConfigureSwordKnockback(room, EnemyKnockbackMotion.ScreenBoundary);
    }

    internal bool UpdateFrame(Vector2 target, int frameCounter = 0)
    {
        if (IsDead) return false;
        if (_linkHitPending)
        {
            _linkHitPending = false;
            AdvanceInvincibilityCounter();
            if (!Lit) return false;
            Lit = false;
            State = 8;
            Speed = _behavior.SeekSpeed;
            RestartAnimation(3);
            return true;
        }
        if (_otherHitPending)
        {
            _otherHitPending = false;
            AdvanceInvincibilityCounter();
            return false;
        }
        if (BeginFrame()) return false;
        if (_stunCounter > 0)
        {
            int z = ZFixed;
            Position = EnemyStunMotion.Update(Position, State, frameCounter, ref _stunCounter, ref z, ref _speedZ);
            ZFixed = z;
            QueueRedraw();
            return false;
        }
        if (State >= 11 && CheckNewTorch()) return false;
        switch (State)
        {
            case 0:
                _random.Next(); // enemyStandardUpdate's var3d initialization.
                var roll = _random.Next();
                Angle = roll.High & _behavior.AngleMask;
                _rotation = (roll.Low & 1) == 0 ? -1 : 1;
                Counter = _behavior.OrbitFrames;
                Speed = _behavior.OrbitSpeed;
                ZFixed = _behavior.InitialZ;
                State = 11;
                RestartAnimation(1);
                return false;
            case 8:
                FindTorch();
                return false;
            case 9:
                bool atTorch = OracleObjectMath.ToPixelPosition(Position) == TorchPosition;
                bool descending = MoveToGround();
                if (!atTorch)
                {
                    Angle = OracleObjectMovement.Shared.RelativeAngle(Position, TorchPosition);
                    Move(Speed, Angle);
                    AdvanceAnimation();
                }
                else if (!descending)
                {
                    State = 10;
                    Counter = _behavior.RelightFrames;
                    RestartAnimation(2);
                }
                return false;
            case 10:
                if (--Counter == 0)
                {
                    Angle = (Angle + 16) & _behavior.AngleMask;
                    State = 13;
                    RestartAnimation(1);
                }
                else if (Counter == _behavior.RelightMidpoint)
                {
                    Lit = true;
                    RestartAnimation(0);
                }
                return false;
            case 11:
                int dy = (OracleObjectPosition.HighByte(target.Y) - OracleObjectPosition.HighByte(Position.Y) + _behavior.CloseRange) & 255;
                int dx = (OracleObjectPosition.HighByte(target.X) - OracleObjectPosition.HighByte(Position.X) + _behavior.CloseRange) & 255;
                if (dy <= _behavior.CloseRange * 2 && dx <= _behavior.CloseRange * 2)
                {
                    State = 12;
                    Counter = _behavior.DiveFrames;
                    Speed = _behavior.DiveSpeed;
                }
                if (--Counter == 0)
                {
                    Counter = _behavior.OrbitFrames;
                    Angle = (Angle + _rotation) & _behavior.AngleMask;
                }
                Move(Speed, Angle);
                if (OracleObjectPosition.HighByte(Position.X) >= _behavior.RoomWidth ||
                    OracleObjectPosition.HighByte(Position.Y) >= _behavior.LayoutSize)
                    Move(_behavior.CorrectionSpeed, OracleObjectMovement.Shared.RelativeAngle(Position,
                        new Vector2(_behavior.CenterX, _behavior.CenterY)));
                break;
            case 12:
                if (--Counter == 0) { State = 13; break; }
                ZFixed = unchecked((short)(ZFixed + EnemyBehaviorTables.Shared.FireKeeseZOffsets[Counter >> 4].Value));
                int toward = OracleObjectMovement.Shared.RelativeAngle(Position, target);
                if ((Counter & 3) == 0 && Angle != toward)
                    Angle = (Angle + (((Angle - toward) & 31) < 16 ? -1 : 1)) & 31;
                BounceAndMove();
                break;
            case 13:
                ZFixed = unchecked((short)(ZFixed - _behavior.RiseStep));
                if (ZFixed >= _behavior.InitialZ) BounceAndMove();
                else
                {
                    State = 11;
                    Speed = _behavior.OrbitSpeed;
                    Counter = _behavior.OrbitFrames;
                }
                break;
        }
        AdvanceAnimation();
        QueueRedraw();
        return false;
    }

    private void Move(int speed, int angle)
    {
        var velocity = MovementVelocity(speed, angle);
        Position = OracleObjectPosition.FromPixels(Position).Add(velocity.YFixed, velocity.XFixed).PrecisePosition;
    }
    private void BounceAndMove()
    {
        Angle = EnemyAdjacentWallResolver.Shared.BounceAngle(Position, Angle,
            point => point.X < 0 || point.Y < 0 || point.X >= _room.Width || point.Y >= _room.Height);
        Move(Speed, Angle);
    }
    private bool MoveToGround()
    {
        if ((ZFixed >> 8) >= _behavior.GroundHigh) return false;
        ZFixed += _behavior.GroundStep;
        return true;
    }
    private void FindTorch()
    {
        int current = (OracleObjectPosition.HighByte(Position.Y) & 0xf0) | (OracleObjectPosition.HighByte(Position.X) >> 4);
        int distance = 255;
        int closest = -1;
        for (int tile = 0; tile < _behavior.LayoutSize; tile++)
        {
            if (_room.Layout[tile] != _behavior.TorchTile) continue;
            int candidate = Math.Abs((tile >> 4) - (current >> 4)) + Math.Abs((tile & 15) - (current & 15));
            if (candidate > distance) continue; // Equal distance selects the later source tile.
            distance = candidate;
            closest = tile;
        }
        if (closest < 0) { State = 13; return; }
        TorchPosition = new Vector2((closest & 15) * 16 + 8, (closest >> 4) * 16 + 8);
        State = 9;
        Speed = _behavior.SeekSpeed;
        RestartAnimation(3);
    }
    private bool CheckNewTorch()
    {
        if (Lit) return false;
        for (int i = 0; i < _behavior.ScanTiles; i++)
        {
            if (_room.Layout[_scanPosition++] != _behavior.TorchTile) continue;
            _scanPosition = 0;
            State = 8;
            Speed = _behavior.SeekSpeed;
            return true;
        }
        if (_scanPosition == _behavior.LayoutSize) _scanPosition = 0;
        return false;
    }
    internal void NotifyLinkCollision() { _linkHitPending = true; _otherHitPending = false; }
    internal void NotifyOtherCollision() { _otherHitPending = true; _linkHitPending = false; }
    internal bool BeginPegasusHit()
    {
        if (!CollisionEnabled || InvincibilityCounter != 0) return false;
        _stunCounter = 240;
        InvincibilityCounter = -16;
        return true;
    }
    internal override bool TakeSwordHit(Vector2 origin, int damage)
    {
        if (!base.TakeSwordHit(origin, damage)) return false;
        NotifyOtherCollision();
        return true;
    }
}
