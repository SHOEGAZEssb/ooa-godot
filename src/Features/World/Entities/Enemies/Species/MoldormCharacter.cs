using Godot;
using System;

namespace oracleofages;

/// <summary>
/// ENEMY_MOLDORM $4f:$01 head. Its two linked tails have their own ENEMY slots.
/// </summary>
internal partial class MoldormCharacter : EnemyCharacter
{
    private readonly MoldormBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.Moldorm;
    private OracleRoomData _room = null!;
    private OracleRandom _random = null!;
    private Vector2 _preciseHeadPosition;
    internal int State { get; private set; }
    private int _turnCounter;
    private int _angle;
    private int _angularSpeed;
    private bool _justHit;
    internal MoldormTailCharacter? Tail1 { get; set; }
    internal MoldormTailCharacter? Tail2 { get; set; }
    internal Action<MoldormCharacter>? KillRelatedParts { get; set; }
    internal int NativeSlot { get; private set; } = -1;
    internal int Tail1Slot { get; set; } = -1;
    internal int Tail2Slot { get; set; } = -1;
    private Func<int, IRoomEntity?>? _resolveSlot;

    internal void BindEnemySlot(int slot, Func<int, IRoomEntity?> resolve)
    {
        NativeSlot = slot; _resolveSlot = resolve;
    }

    private void CopyTailInvincibility(int slot, MoldormTailCharacter? standaloneTail)
    {
        var target = slot >= 0 ? _resolveSlot?.Invoke(slot)?.Node : standaloneTail;
        if (target is EnemyCharacter enemy) enemy.InvincibilityCounter = InvincibilityCounter;
        else if (target is not null)
            throw new NotSupportedException($"Moldorm $4f JUST_HIT writes invincibility to reused ENEMY slot ${slot:x2} ({target.GetType().Name}), which has no enemy damage owner.");
    }

    internal ImportedEnemyDefinition Record { get; private set; }
    internal bool Initialized => State >= 8;
    internal int TurnCounter => _turnCounter;
    internal int Angle => _angle;
    internal int AngularSpeed => _angularSpeed;
    internal int SpeedRaw => _behavior.SpeedRaw;
    internal Vector2 Tail1Position => Tail1?.Position ?? Position;
    internal Vector2 Tail2Position => Tail2?.Position ?? Position;

    internal void Initialize(
        ImportedEnemyDefinition record,
        OracleRoomData room,
        Vector2 position,
        OracleRandom random)
    {
        if (record.Animations.Length < 10)
        {
            throw new ArgumentOutOfRangeException(
                nameof(record), record.Animations.Length,
                "ENEMY_MOLDORM requires head animations 0-7 and tail animations 8-9.");
        }
        if (_behavior.RequiredEnemySlots != 3 ||
            _behavior.TailDelayFrames != 8)
        {
            throw new InvalidOperationException(
                "The imported Moldorm multipart contract is not three slots with eight-update tails.");
        }

        Record = record;
        _room = room;
        _random = random;
        _preciseHeadPosition = position;
        State = 0;
        _turnCounter = 0;
        _angle = 0;
        _angularSpeed = _behavior.InitialAngularSpeed;

        EnemyCharacterConfiguration configuration =
            EnemyCharacterConfiguration.FromImported(record);
        InitializeEnemy(position, configuration);
        Visible = false;
        ConfigureSwordKnockback(
            room,
            EnemyKnockbackMotion.Terrain,
            checksHazards: true,
            precisePosition: () => _preciseHeadPosition,
            setPrecisePosition: SetPreciseHeadPosition,
            knockbackHoleAnimation: true);
        ConfigureHazards(room, animateWhileFallingInHole: false);
    }

    internal void UpdateFrame()
    {
        if (IsDead)
            return;
        if (ContinueHazard() || CheckHazards())
        {
            _justHit = false;
            return;
        }

        if (State == 0)
        {
            PrepareForScreenTransition();
            return;
        }

        if (_justHit)
        {
            _justHit = false;
            // updateEnemies advances invincibility AFTER enemyCode4f.
            // Later tails receive this byte before their own post-update tick.
            CopyTailInvincibility(Tail1Slot, Tail1);
            CopyTailInvincibility(Tail2Slot, Tail2);
            AdvanceInvincibilityCounter();
            return;
        }

        if (BeginFrame())
            return;

        if (State == 8)
        {
            State = 9;
            _turnCounter = _behavior.TurnCounterFrames;
            _angle = _random.Next().Value & _behavior.AngleMask;
            _angularSpeed = _behavior.InitialAngularSpeed;
            UpdateHeadAnimation();
            return;
        }

        _turnCounter--;
        if (_turnCounter == 0)
        {
            _turnCounter = _behavior.TurnCounterFrames;
            _angle = (_angle + _angularSpeed) & _behavior.AngleMask;
            UpdateHeadAnimation();
            if ((_random.Next().Value & _behavior.ReverseRollMask) == 0)
                _angularSpeed = -_angularSpeed;
        }

        int bouncedAngle = EnemyAdjacentWallResolver.Shared.BounceAngle(
            Position, _angle, IsWallOrHole);
        if (bouncedAngle != _angle)
        {
            _angle = bouncedAngle;
            UpdateHeadAnimation();
        }

        Position = OracleObjectMovement.Shared.ApplySpeed(
            ref _preciseHeadPosition,
            _behavior.SpeedRaw,
            _angle);
        QueueRedraw();
    }

    internal ScreenTransitionPresentation PrepareForScreenTransition()
    {
        if (State != 0)
            return ScreenTransitionPresentation.Visible;
        _random.Next(); // enemyStandardUpdate var3d; main state8 waits for the next ENEMY update.
        State = 8;
        RestartAnimation(0);
        Visible = true;
        QueueRedraw();
        return ScreenTransitionPresentation.Visible;
    }

    internal bool TakeSwitchHookHit(Vector2 linkPosition, int damage)
    {
        if (!TakeSwordHit(linkPosition, damage)) return false;
        ApplySwordKnockback(linkPosition, EnemyKnockbackStrength.Low);
        return true;
    }

    internal override bool TakeSwordHit(Vector2 sourcePosition, int damage)
    {
        if (!base.TakeSwordHit(sourcePosition, damage)) return false;
        _justHit = true;
        return true;
    }

    internal override bool TryApplyShieldBump(Rect2 hitbox, Vector2 sourcePosition, EnemyKnockbackStrength strength)
    {
        if (!base.TryApplyShieldBump(hitbox, sourcePosition, strength)) return false;
        _justHit = true;
        return true;
    }

    protected override void CompleteKnockbackDeath()
    {
        KillRelatedParts?.Invoke(this); // Clean-US moldorm.s intentionally targets PART slots, not its tails.
        base.CompleteKnockbackDeath();
    }

    private void UpdateHeadAnimation() =>
        SetAnimation(
            ((_angle + _behavior.AnimationAngleOffset) &
                _behavior.AnimationAngleMask) >> 2);

    private bool IsWallOrHole(Vector2I point) =>
        point.X < 0 || point.X >= _room.Width ||
        point.Y < 0 || point.Y >= _room.Height ||
        _room.IsSolid(point) ||
        _room.GetTerrainInfo(point).Hazard == HazardType.Hole;

    private void SetPreciseHeadPosition(Vector2 position)
    {
        _preciseHeadPosition = position;
        Position = OracleObjectMath.ToPixelPosition(position);
        QueueRedraw();
    }

}
