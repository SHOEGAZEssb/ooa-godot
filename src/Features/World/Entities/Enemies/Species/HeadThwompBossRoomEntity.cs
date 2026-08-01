using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class HeadThwompBossRoomEntity
    : CombatEnemyRoomEntityAdapter<HeadThwompBoss>, IFixedRoomEntity,
        IBombCatchRoomEntity
{
    internal HeadThwompBossRoomEntity(HeadThwompBoss boss)
        : base(
            boss,
            boss.SetTransitionDrawOffset,
            EnemyCombatDescriptor.Special(
                EnemyCombatComponent.WithContactDamage(
                    () => boss.IsDead,
                    () => boss.CollisionBounds,
                    boss.TakeSwordHit,
                    boss.TakeBurnHit,
                    boss.OverlapsLink,
                    () => boss.Position,
                    boss.Record.DamageQuarters,
                    () => null),
                countsAsEnemy: true,
                killableEnemyIndex: 0,
                completedOutcome: () =>
                    RoomEnemyOutcome.BossTeardown(
                        killableEnemyIndex: 0)))
    {
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
        => Entity.UpdateFrame(frame.Player, frame.Counter, spawns);

    public override bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns)
    {
        bool hit = base.ApplySwordHit(
            hitbox,
            sourcePosition,
            damage,
            knockbackStrength,
            spawns);
        if (!hit)
            return false;

        // ENEMYCOLLISION_HEAD_THWOMP maps every sword state to
        // COLLISIONEFFECT_$1b. LINKDMG_$1c applies no attacker recoil; the
        // visible and audible response is the midpoint INTERAC_CLINK.
        spawns.Add(new EnemyClinkSpawn(
            CollisionMidpoint(Entity.Position, hitbox.GetCenter())));
        return true;
    }

    public bool TryCatchBomb(BombEffect bomb) =>
        Entity.TryCatchBomb(bomb);
}

/// <summary>
/// PART_HEAD_THWOMP_BOMB_DROPPER $40 paired with PART_ITEM_DROP $01. The
/// invisible helper moves through room Y/X space, copies its position to the
/// Bomb drop after that drop's own update, and releases it on solid floor.
/// </summary>
internal sealed class HeadThwompBombDropRoomEntity
    : RoomEntityAdapter<ItemDropEffect>, IFixedRoomEntity,
        IRoomEntityLifetime, ILinkSwordCollectibleRoomEntity
{
    private const int Gravity = 0x20;
    private const int GroundYOffset = 6;
    private const int LeftXOffset = -4;
    private const int RightXOffset = 3;
    private static readonly int[] Speeds = [0x14, 0x19, 0x1e, 0x23];
    private static readonly int[] InitialVerticalSpeeds =
        [-0x300, -0x320, -0x340, -0x360];

    private readonly OracleRoomData _room;
    private readonly OracleRandom _random;
    private readonly Action<Vector2, HazardType> _enteredHazard;
    private OracleObjectPosition _dropperPosition;
    private int _speed;
    private int _angle;
    private int _verticalSpeedFixed;
    private bool _initialized;
    private bool _released;

    internal HeadThwompBombDropRoomEntity(
        ItemDropEffect drop,
        OracleRoomData room,
        OracleRandom random,
        Action<Vector2, HazardType> enteredHazard)
        : base(drop, drop.SetTransitionDrawOffset)
    {
        _room = room;
        _random = random;
        _enteredHazard = enteredHazard;
        _dropperPosition =
            OracleObjectMovement.Shared.PositionFromPixels(drop.Position);
    }

    public bool Finished => Entity.Finished;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        // getFreePartSlot allocates the item drop before its helper, so the
        // ascending part update loop advances PART_ITEM_DROP first.
        Entity.UpdateFrame(frame.Player, frame.Counter);
        HazardType pending = Entity.TakePendingHazardEffect();
        if (pending != HazardType.None)
            _enteredHazard(Entity.Position, pending);
        if (Entity.Finished || _released)
            return;

        if (!_initialized)
        {
            OracleRandomResult result = _random.Next();
            _speed = Speeds[result.Value & 0x03];
            _verticalSpeedFixed =
                InitialVerticalSpeeds[(result.Value & 0x60) >> 5];
            _angle = (result.High & 0x10) + 0x08;
            _initialized = true;
            return;
        }

        if (_verticalSpeedFixed >= 0 && IsOnSolidFloor())
        {
            // objectUpdateSpeedZ_sidescroll returns with carry before moving;
            // PART $40 deletes itself without one final position copy.
            _released = true;
            return;
        }

        _dropperPosition = _dropperPosition.Add(
            _verticalSpeedFixed, xFixed: 0);
        _verticalSpeedFixed = unchecked(
            (short)(_verticalSpeedFixed + Gravity));
        _dropperPosition = OracleObjectMovement.Shared.ApplySpeed(
            _dropperPosition, _speed, _angle);
        Entity.CopyHeadThwompBombDropperPosition(_dropperPosition);
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.FinishedHazard is
            HazardType.Water or HazardType.Lava)
        {
            _enteredHazard(Entity.Position, Entity.FinishedHazard);
        }
        else if (Entity.FinishedHazard == HazardType.Hole)
        {
            spawns.Add(new FallingDownHoleSpawn(Entity.Position));
        }
    }

    public bool TryCollectWithSword(Rect2 hitbox) =>
        Entity.TryCollectWithSword(hitbox);

    private bool IsOnSolidFloor()
    {
        int y = unchecked((byte)(
            (_dropperPosition.YFixed >> 8) + GroundYOffset));
        int x = _dropperPosition.XFixed >> 8;
        return IsSolidExceptHole(unchecked((byte)(x + LeftXOffset)), y) ||
            IsSolidExceptHole(unchecked((byte)(x + RightXOffset)), y);
    }

    private bool IsSolidExceptHole(int x, int y)
    {
        var point = new Vector2(x, y);
        return _room.IsSolid(point) &&
            _room.GetTerrainInfo(point).Hazard != HazardType.Hole;
    }
}
