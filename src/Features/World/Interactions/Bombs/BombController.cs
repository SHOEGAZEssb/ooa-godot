using Godot;
using System;

namespace oracleofages;

/// <summary>
/// ITEM_BOMB ($03) parent. It first reuses a touching unexploded Bomb, then
/// allocates one child subject to the normal/Bomber's Ring object cap, and
/// shares Bracelet's exact lift/carry/throw Link state boundaries.
/// </summary>
public sealed class BombController
{
    private readonly InventoryState _inventory;
    private readonly RoomEntityManager _entities;
    private readonly RoomSession _rooms;
    private readonly Action<int> _playSound;
    private readonly Func<bool> _isUnderwater;
    private readonly BombRecord _record;
    private readonly LinkItemDatabase _linkItems;

    private BombParentState _state;
    private BombEffect? _bomb;
    private int _counter;

    internal BombParentState State => _state;
    internal BombEffect? Bomb => _bomb;
    internal int Counter => _counter;
    internal bool Active => _state != BombParentState.Idle;

    public BombController(
        InventoryState inventory,
        RoomEntityManager entities,
        RoomSession rooms,
        Action<int> playSound,
        Func<bool> isUnderwater,
        BombDatabase? database = null)
    {
        _inventory = inventory;
        _entities = entities;
        _rooms = rooms;
        _playSound = playSound;
        _isUnderwater = isUnderwater;
        _record = (database ?? new BombDatabase()).Data;
        _linkItems = LinkItemDatabase.Shared;
    }

    public bool TryUse(Player player)
    {
        if (_state != BombParentState.Idle ||
            player.IsCarryingObject ||
            !player.IsGroundedForFloorButton ||
            _isUnderwater())
        {
            return false;
        }

        if (!_entities.TryPickupBomb(player, out BombEffect? bomb))
        {
            if (_inventory.Bombs == 0 ||
                _entities.ActiveBombCount >=
                    RingEffects.BombObjectLimit(_inventory))
            {
                return false;
            }
            bomb = _entities.Spawn<BombEffect>(new BombSpawn(
                player,
                _record,
                _rooms.ActiveGroup,
                OnHeldExplosion));
            if (!_inventory.TryConsumeBomb())
            {
                bomb.Discard();
                throw new InvalidOperationException(
                    "ITEM_BOMB child allocation succeeded without a packed-BCD Bomb to consume.");
            }
        }
        else
        {
            bomb!.BeginHeld(player, OnHeldExplosion);
        }

        _bomb = bomb ?? throw new InvalidOperationException(
            "Bomb pickup/allocation returned no ITEM_BOMB actor.");
        _counter = 0;
        _state = BombParentState.Lifting;
        player.SetBraceletActionPose(BraceletActionPose.PullStrain);
        player.SetBraceletLiftCollisionsDisabled(true);
        _bomb.SetHeldOffset(player, GetLiftOffset(player, 0));
        _playSound(_record.PickupSound);
        return true;
    }

    /// <summary>
    /// Advances one Bomb parent update. True means Link movement is disabled
    /// by the lift or throw animation; held movement remains available.
    /// </summary>
    public bool Update(
        Player player,
        Vector2 movementInput,
        bool itemButtonJustPressed)
    {
        switch (_state)
        {
            case BombParentState.Idle:
                return false;

            case BombParentState.Lifting:
                UpdateLift(player);
                return _state == BombParentState.Lifting;

            case BombParentState.Holding:
                UpdateHeldPosition(player);
                if (!itemButtonJustPressed)
                    return false;
                Throw(player, movementInput);
                return true;

            case BombParentState.Throwing:
                _counter++;
                if (_counter >= _record.ThrowFrames)
                {
                    player.ClearBraceletActionPose();
                    _counter = 0;
                    _state = BombParentState.Idle;
                    _bomb = null;
                }
                return true;

            default:
                throw new InvalidOperationException(
                    $"Unsupported Bomb parent state {_state}.");
        }
    }

    internal void Interrupt(Player player, bool discard)
    {
        if (_state == BombParentState.Idle)
            return;

        player.SetBraceletLiftCollisionsDisabled(false);
        player.ClearBraceletActionPose();
        player.EndCarriedObjectPose();
        if (_bomb is not null)
        {
            if (discard)
            {
                _bomb.Discard();
            }
            else if (_bomb.State == BombState.Exploding)
            {
                _bomb.ReleaseExploding(player, GetHeldOffset(player));
            }
            else if (_bomb.State == BombState.Held)
            {
                _bomb.Throw(
                    player,
                    GetHeldOffset(player),
                    Vector2I.Zero,
                    speedZ: 0,
                    speedRaw: 0);
            }
        }
        _bomb = null;
        _counter = 0;
        _state = BombParentState.Idle;
    }

    private void UpdateLift(Player player)
    {
        if (_bomb is null || _bomb.Finished)
        {
            Interrupt(player, discard: false);
            return;
        }

        _counter++;
        int middleBoundary =
            _record.LiftLowFrames + _record.LiftMidFrames;
        int finishedBoundary =
            middleBoundary + _record.LiftHighFrames;
        if (_counter <= _record.LiftLowFrames)
        {
            player.SetBraceletActionPose(
                BraceletActionPose.PullStrain);
            _bomb.SetHeldOffset(player, GetLiftOffset(player, 0));
            return;
        }
        if (_counter <= middleBoundary)
        {
            player.SetBraceletActionPose(BraceletActionPose.Pull);
            _bomb.SetHeldOffset(player, GetLiftOffset(player, 1));
            return;
        }

        _bomb.SetHeldOffset(player, GetLiftOffset(player, 2));
        if (_counter < finishedBoundary)
            return;

        player.ClearBraceletActionPose();
        player.SetBraceletLiftCollisionsDisabled(false);
        player.BeginCarriedObjectPose();
        _counter = 0;
        _state = BombParentState.Holding;
        UpdateHeldPosition(player);
    }

    private void UpdateHeldPosition(Player player) =>
        _bomb?.SetHeldOffset(player, GetHeldOffset(player));

    private void Throw(Player player, Vector2 movementInput)
    {
        if (_bomb is null)
        {
            Interrupt(player, discard: false);
            return;
        }

        Vector2I direction =
            player.SelectCarriedObjectReleaseDirection(movementInput);
        bool dropped = direction == Vector2I.Zero;
        _bomb.Throw(
            player,
            GetHeldOffset(player),
            direction,
            dropped ? 0 : _record.InitialSpeedZ,
            dropped
                ? 0
                : RingEffects.UsesStrongThrow(_inventory)
                    ? _record.TossSpeedRaw
                    : _record.SpeedRaw);
        player.EndCarriedObjectPose();
        player.SetBraceletActionPose(BraceletActionPose.Throw);
        _playSound(_record.ThrowSound);
        _counter = 0;
        _state = BombParentState.Throwing;
    }

    private void OnHeldExplosion(BombEffect bomb)
    {
        if (_bomb != bomb || _state is not
                (BombParentState.Lifting or BombParentState.Holding))
        {
            return;
        }
        Player? player = bomb.HeldPlayer;
        if (player is null)
            return;
        bomb.ReleaseExploding(player, GetHeldOffset(player));
        player.SetBraceletLiftCollisionsDisabled(false);
        player.ClearBraceletActionPose();
        player.EndCarriedObjectPose();
        _bomb = null;
        _counter = 0;
        _state = BombParentState.Idle;
    }

    private Vector2I GetLiftOffset(Player player, int frame) =>
        _linkItems.BraceletLiftOffset(
            frame,
            DirectionIndex(player.FacingVector));

    private Vector2I GetHeldOffset(Player player)
    {
        int frame = player.CarriedObjectAnimationFrame == 0 ? 2 : 3;
        return GetLiftOffset(player, frame);
    }

    private static int DirectionIndex(Vector2I direction) =>
        direction == Vector2I.Up ? 0
        : direction == Vector2I.Right ? 1
        : direction == Vector2I.Down ? 2
        : direction == Vector2I.Left ? 3
        : throw new ArgumentOutOfRangeException(nameof(direction));
}

internal enum BombParentState
{
    Idle,
    Lifting,
    Holding,
    Throwing
}
