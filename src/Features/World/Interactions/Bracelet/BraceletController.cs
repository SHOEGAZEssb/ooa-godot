using Godot;
using System;

namespace oracleofages;

/// <summary>
/// ITEM_BRACELET ($16) parent and its lifted-metatile child. The controller
/// retains the original grab, pull, lift, carry, throw, and impact boundaries
/// while room/entity systems own the tile mutation, drops, combat, and effects.
/// </summary>
public sealed class BraceletController
{

    private static readonly Vector2I[] WallOffsets =
    [
        new(0, -5), new(7, 0), new(0, 7), new(-8, 0)
    ];

    // updateGrabbedObjectPosition's ITEM_BRACELET weight-0 rows. Each entry
    // is X followed by the rendered Z offset relative to Link.
    private static readonly Vector2I[,] LiftedObjectOffsets =
    {
        {
            new(0, -8), new(7, 0), new(0, 6), new(-8, 0)
        },
        {
            new(0, -6), new(3, -8), new(0, 4), new(-4, -8)
        },
        {
            new(0, -13), new(0, -14), new(0, -13), new(0, -14)
        },
        {
            new(0, -13), new(0, -13), new(0, -13), new(0, -13)
        }
    };

    private readonly Node _worldRoot;
    private readonly RoomSession _rooms;
    private readonly BreakableTileDatabase _breakables;
    private readonly RoomView _roomView;
    private readonly RoomEntityManager _entities;
    private readonly CombatController _combat;
    private readonly OracleSaveData _saveData;
    private readonly Action<int> _playSound;
    private readonly Func<long> _animationTick;
    private readonly Func<Vector2, Vector2I, bool> _hasFullWall;
    private readonly BraceletDatabaseRecord _record;

    private BraceletState _state;
    private bool _primaryButton;
    private int _counter;
    private int _targetPackedPosition = -1;
    private byte _targetTile;
    private BreakableTileRecord _targetRecord;
    private BraceletLiftedObject? _object;
    private int _breakEffect;

    internal BraceletState State => _state;
    internal int Counter => _counter;
    internal BraceletLiftedObject? LiftedObject => _object;
    internal bool HoldingTile => _state == BraceletState.Holding;

    public BraceletController(
        Node worldRoot,
        RoomSession rooms,
        BreakableTileDatabase breakables,
        RoomView roomView,
        RoomEntityManager entities,
        CombatController combat,
        OracleSaveData saveData,
        Action<int> playSound,
        Func<long> animationTick,
        Func<Vector2, Vector2I, bool> hasFullWall)
    {
        _worldRoot = worldRoot;
        _rooms = rooms;
        _breakables = breakables;
        _roomView = roomView;
        _entities = entities;
        _combat = combat;
        _saveData = saveData;
        _playSound = playSound;
        _animationTick = animationTick;
        _hasFullWall = hasFullWall;
        _record = new BraceletDatabase().Data;
    }

    /// <summary>
    /// Creates the held-input Bracelet parent for the equipped button. A press
    /// is consumed even when Link is not yet touching a wall; state 0 keeps
    /// looking until that same button is released.
    /// </summary>
    public bool TryUse(Player player, bool primaryButton)
    {
        if (_state != BraceletState.Idle)
            return false;

        bool wasCarrying = player.IsCarryingObject;
        if (_entities.TryUseBracelet(player))
        {
            if (!wasCarrying && player.IsCarryingObject)
                BeginEntityLift(player, primaryButton);
            return true;
        }
        if (player.IsCarryingObject)
            return false;

        _primaryButton = primaryButton;
        _state = BraceletState.SeekingWall;
        _counter = 0;
        TryBeginWallGrab(player);
        return true;
    }

    /// <summary>
    /// Advances one original Bracelet parent/child update. Returns true only
    /// while the parent has Link movement disabled.
    /// </summary>
    public bool Update(
        Player player,
        Vector2 movementInput,
        bool primaryHeld,
        bool secondaryHeld,
        bool itemButtonJustPressed)
    {
        bool assignedButtonHeld = _primaryButton
            ? primaryHeld
            : secondaryHeld;
        switch (_state)
        {
            case BraceletState.Idle:
                // Non-tile Bracelet objects (for example Pumpkin Head's
                // exposed head) own their carried state. State 3 of the
                // original parent checks either newly pressed item button to
                // release such an object, regardless of which button started
                // the lift.
                if (player.IsCarryingObject && itemButtonJustPressed &&
                    _entities.TryUseBracelet(player))
                {
                    player.SetBraceletEntityOffset(null);
                    player.SetBraceletActionPose(
                        BraceletActionPose.Throw);
                    _playSound(_record.ThrowSound);
                    _counter = 0;
                    _state = BraceletState.Throwing;
                    return true;
                }
                return false;

            case BraceletState.SeekingWall:
                if (!assignedButtonHeld)
                {
                    ResetParent(player);
                    return false;
                }
                return TryBeginWallGrab(player);

            case BraceletState.GrabbingWall:
                return UpdateWallGrab(
                    player, movementInput, assignedButtonHeld);

            case BraceletState.Lifting:
                UpdateLift(player);
                return _state == BraceletState.Lifting;

            case BraceletState.LiftingEntity:
                UpdateEntityLift(player);
                return _state == BraceletState.LiftingEntity;

            case BraceletState.Holding:
                UpdateHeldPosition(player);
                if (itemButtonJustPressed)
                {
                    Throw(player);
                    AdvanceProjectile();
                    return true;
                }
                return false;

            case BraceletState.Throwing:
                AdvanceProjectile();
                _counter++;
                if (_counter >= _record.ThrowFrames)
                {
                    player.ClearBraceletActionPose();
                    _state = _object is null
                        ? BraceletState.Idle
                        : BraceletState.Projectile;
                }
                return true;

            case BraceletState.Projectile:
                AdvanceProjectile();
                return false;

            default:
                throw new InvalidOperationException(
                    $"Unsupported Bracelet state {_state}.");
        }
    }

    internal void Interrupt(Player player, bool discard)
    {
        if (_state == BraceletState.Idle)
            return;

        player.SetBraceletLiftCollisionsDisabled(false);
        player.ClearBraceletActionPose();
        player.SetBraceletEntityOffset(null);
        player.EndCarriedObjectPose();
        if (_object is null)
        {
            ResetParent(player);
            return;
        }

        if (discard)
        {
            DeleteObject();
            _state = BraceletState.Idle;
            return;
        }

        Vector2I heldOffset = new(
            Mathf.RoundToInt(_object.Position.X),
            Mathf.RoundToInt(_object.Position.Y));
        ReleaseObject(
            player,
            heldOffset,
            Vector2I.Zero,
            speedZ: 0,
            speedRaw: 0);
        _state = BraceletState.Projectile;
    }

    private bool TryBeginWallGrab(Player player)
    {
        OracleRoomData room = _rooms.CurrentRoom;
        Vector2 point = WallPoint(player);
        if (!_hasFullWall(player.Position, player.FacingVector))
            return false;

        _targetPackedPosition = room.GetPackedPosition(point);
        _targetTile = room.GetMetatile(point);
        _breakables.TryGet(
            room.ActiveCollisions, _targetTile, out _targetRecord);
        _counter = 0;
        _state = BraceletState.GrabbingWall;
        player.SetBraceletActionPose(BraceletActionPose.Pull);
        return true;
    }

    private bool UpdateWallGrab(
        Player player,
        Vector2 movementInput,
        bool assignedButtonHeld)
    {
        if (!assignedButtonHeld || !StillFacingTarget(player))
        {
            ResetParent(player);
            return false;
        }

        Vector2 opposite = -(Vector2)player.FacingVector;
        if (movementInput.Dot(opposite) <= 0.5f)
        {
            _counter = 0;
            player.SetBraceletActionPose(BraceletActionPose.Pull);
            return true;
        }

        player.SetBraceletActionPose(BraceletActionPose.PullStrain);
        _counter++;
        if (_counter < _record.GrabPullFrames)
            return true;

        if (!TryLiftTarget(player))
        {
            // LINK_ANIM_MODE_LIFT_3 remains on its terminal $e0 frame while
            // tryToBreakTile is retried. Restarting the 11-update animation
            // makes Link visibly flicker between pull and strain poses.
            _counter = _record.GrabPullFrames;
        }
        return true;
    }

    private bool TryLiftTarget(Player player)
    {
        OracleRoomData room = _rooms.CurrentRoom;
        if (!_targetRecord.AllowsSource(BreakableTileDatabase.SourceBracelet))
            return false;

        Vector2 tileCenter = PackedPositionCenter(_targetPackedPosition);
        Texture2D texture = room.BuildMimickedMetatileTexture(tileCenter);
        byte replacement = _targetRecord.ReplacementFor(room, tileCenter);
        bool changed = _targetRecord.Replacement == 0 ||
            room.ReplaceMetatile(
                tileCenter, _targetTile, replacement, _animationTick());
        if (!changed)
            return false;

        _targetRecord.ApplyPersistentEffects(
            _saveData,
            _rooms.ActiveGroup,
            room.Id,
            direction => _rooms.TryGetNeighbor(direction, out int neighbor)
                ? neighbor
                : null);
        if ((_targetRecord.Effect & 0x40) != 0)
            _playSound(OracleSoundEngine.SndSolvePuzzle);
        if (_targetRecord.Drop != 0)
            _entities.SpawnBreakableDrop(_targetRecord.Drop, tileCenter);

        _object = new BraceletLiftedObject
        {
            Name = $"LiftedTile_{_targetTile:x2}"
        };
        _object.Initialize(texture);
        player.AddChild(_object);
        _object.SetHeldOffset(GetLiftOffset(player, 0));
        _breakEffect = _targetRecord.Effect;
        _counter = 0;
        _state = BraceletState.Lifting;
        player.SetBraceletLiftCollisionsDisabled(true);
        _playSound(_record.PickupSound);
        _roomView.QueueRedraw();
        return true;
    }

    private void UpdateLift(Player player)
    {
        if (_object is null)
        {
            ResetParent(player);
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
            _object.SetHeldOffset(GetLiftOffset(player, 0));
            return;
        }
        if (_counter <= middleBoundary)
        {
            player.SetBraceletActionPose(BraceletActionPose.Pull);
            _object.SetHeldOffset(GetLiftOffset(player, 1));
            return;
        }

        _object.SetHeldOffset(GetLiftOffset(player, 2));
        if (_counter < finishedBoundary)
            return;

        player.ClearBraceletActionPose();
        player.SetBraceletLiftCollisionsDisabled(false);
        player.BeginCarriedObjectPose();
        _state = BraceletState.Holding;
        _counter = 0;
        UpdateHeldPosition(player);
    }

    private void BeginEntityLift(Player player, bool primaryButton)
    {
        _primaryButton = primaryButton;
        player.SetBraceletActionPose(BraceletActionPose.PullStrain);
        player.SetBraceletEntityOffset(GetLiftOffset(player, 0));
        _counter = 0;
        _state = BraceletState.LiftingEntity;
        player.SetBraceletLiftCollisionsDisabled(true);
        _playSound(_record.PickupSound);
    }

    private void UpdateEntityLift(Player player)
    {
        _counter++;
        int middleBoundary =
            _record.LiftLowFrames + _record.LiftMidFrames;
        int finishedBoundary =
            middleBoundary + _record.LiftHighFrames;
        if (_counter <= _record.LiftLowFrames)
        {
            player.SetBraceletActionPose(
                BraceletActionPose.PullStrain);
            player.SetBraceletEntityOffset(GetLiftOffset(player, 0));
            return;
        }
        if (_counter <= middleBoundary)
        {
            player.SetBraceletActionPose(BraceletActionPose.Pull);
            player.SetBraceletEntityOffset(GetLiftOffset(player, 1));
            return;
        }
        player.SetBraceletEntityOffset(GetLiftOffset(player, 2));
        if (_counter < finishedBoundary)
            return;

        player.ClearBraceletActionPose();
        player.SetBraceletEntityOffset(null);
        player.SetBraceletLiftCollisionsDisabled(false);
        player.BeginCarriedObjectPose();
        _counter = 0;
        _state = BraceletState.Idle;
    }

    private void UpdateHeldPosition(Player player)
    {
        _object?.SetHeldOffset(GetHeldOffset(player));
    }

    private void Throw(Player player)
    {
        if (_object is null)
        {
            ResetParent(player);
            return;
        }

        Vector2I heldOffset = GetHeldOffset(player);
        Vector2I direction = player.FacingVector;
        ReleaseObject(
            player,
            heldOffset,
            direction,
            _record.InitialSpeedZ,
            RingEffects.UsesStrongThrow(player.Inventory)
                ? _record.TossSpeedRaw
                : _record.SpeedRaw);
        player.EndCarriedObjectPose();
        player.SetBraceletActionPose(BraceletActionPose.Throw);
        _playSound(_record.ThrowSound);
        _counter = 0;
        _state = BraceletState.Throwing;
    }

    private void ReleaseObject(
        Player player,
        Vector2I heldOffset,
        Vector2I direction,
        int speedZ,
        int speedRaw)
    {
        if (_object is null)
            return;

        Vector2 groundPosition =
            player.Position + new Vector2(heldOffset.X, 0) + direction;
        _object.Release(
            _worldRoot,
            groundPosition,
            heldOffset.Y << 8,
            speedZ,
            direction,
            speedRaw);
    }

    private void AdvanceProjectile()
    {
        if (_object is null || !_object.Thrown)
            return;

        OracleRoomData room = _rooms.CurrentRoom;
        Vector2 ground = _object.GroundPosition;
        if (ground.X < 0 || ground.X >= room.Width ||
            ground.Y < 0 || ground.Y >= room.Height)
        {
            DeleteObject();
            FinishProjectileState();
            return;
        }

        Vector2 front = ground + ThrowCollisionOffset(
            _object.ThrowDirection);
        if (_object.ThrowDirection != Vector2I.Zero &&
            (front.X < 0 || front.X >= room.Width ||
             front.Y < 0 || front.Y >= room.Height ||
             room.IsSolid(front)))
        {
            BreakObject(ground);
            return;
        }

        _object.AdvanceLateral();
        if (_object.AdvanceVertical(_record.Gravity))
        {
            BreakObject(_object.GroundPosition);
            return;
        }

        // ITEM_BRACELET collision type $16 selects the target's collision-table
        // effect after the item update. Its Y/X values remain the ground-space
        // coordinates while zh is compared independently with a strict
        // seven-pixel range.
        _entities.ApplyThrownObjectHit(
            _object.CollisionBounds(
                _record.RadiusX, _record.RadiusY),
            _object.ZFixed >> 8,
            _record.CollisionZRadius,
            _record.Damage);
    }

    private void BreakObject(Vector2 position)
    {
        // objectReplaceWithAnimationIfOnHazard precedes the stored breakable
        // interaction on both lateral collision and ground contact.
        HazardType hazard =
            _rooms.CurrentRoom.GetTerrainInfo(position).Hazard;
        if (hazard != HazardType.None)
        {
            _entities.SpawnItemHazardEffect(position, hazard);
            DeleteObject();
            FinishProjectileState();
            return;
        }

        _combat.SpawnBreakEffect(position, _breakEffect);
        DeleteObject();
        FinishProjectileState();
    }

    private void DeleteObject()
    {
        if (_object is null)
            return;
        Node? parent = _object.GetParent();
        parent?.RemoveChild(_object);
        _object.QueueFree();
        _object = null;
    }

    private void FinishProjectileState()
    {
        if (_state != BraceletState.Throwing)
            _state = BraceletState.Idle;
    }

    private void ResetParent(Player player)
    {
        player.SetBraceletLiftCollisionsDisabled(false);
        player.ClearBraceletActionPose();
        player.SetBraceletEntityOffset(null);
        _counter = 0;
        _targetPackedPosition = -1;
        _targetTile = 0;
        _targetRecord = default;
        _state = BraceletState.Idle;
    }

    private bool StillFacingTarget(Player player)
    {
        OracleRoomData room = _rooms.CurrentRoom;
        Vector2 point = WallPoint(player);
        return _hasFullWall(player.Position, player.FacingVector) &&
            room.GetPackedPosition(point) == _targetPackedPosition &&
            room.GetMetatile(point) == _targetTile;
    }

    private static Vector2 WallPoint(Player player)
    {
        int direction = DirectionIndex(player.FacingVector);
        return player.Position + WallOffsets[direction];
    }

    private static Vector2I GetLiftOffset(Player player, int frame) =>
        LiftedObjectOffsets[frame, DirectionIndex(player.FacingVector)];

    private static Vector2I GetHeldOffset(Player player)
    {
        int frame = player.CarriedObjectAnimationFrame == 0 ? 2 : 3;
        return GetLiftOffset(player, frame);
    }

    private static Vector2 ThrowCollisionOffset(Vector2I direction) =>
        direction == Vector2I.Up ? new Vector2(0, -3)
        : direction == Vector2I.Right ? new Vector2(3, 0)
        : direction == Vector2I.Down ? new Vector2(0, 7)
        : direction == Vector2I.Left ? new Vector2(-3, 0)
        : Vector2.Zero;

    private static int DirectionIndex(Vector2I direction) =>
        direction == Vector2I.Up ? 0
        : direction == Vector2I.Right ? 1
        : direction == Vector2I.Down ? 2
        : direction == Vector2I.Left ? 3
        : throw new ArgumentOutOfRangeException(nameof(direction));

    private static Vector2 PackedPositionCenter(int packedPosition) =>
        new(
            (packedPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
            (packedPosition >> 4) * OracleRoomData.MetatileSize + 8);

}

internal enum BraceletState
{
    Idle,
    SeekingWall,
    GrabbingWall,
    Lifting,
    LiftingEntity,
    Holding,
    Throwing,
    Projectile
}
