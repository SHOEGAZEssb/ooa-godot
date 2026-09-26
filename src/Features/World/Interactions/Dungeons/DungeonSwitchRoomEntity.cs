using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Common PART_SWITCH $05 handler. The tile supplies the visible switch; this
/// invisible part owns item collision, wSwitchState, and the tile flip.
/// </summary>
internal sealed partial class DungeonSwitchRoomEntity : DungeonMechanicRoomEntity,
    IFixedRoomEntity, ISwordHittableRoomEntity,
    IItemCollisionHittableRoomEntity, ISeedHittableRoomEntity,
    IObjectCollisionHeightRoomEntity, ISeedPreMovementCollisionTarget, IRoomEntityLifetime,
    ISwitchHookHittableRoomEntity, INativePartHealthRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity
{
    private readonly DungeonMechanicDatabaseRecord _record;
    private readonly OracleRoomData _room;
    private readonly DungeonMechanicDatabase _data;
    private readonly OracleRuntimeState _runtime;
    private readonly Func<long> _animationTick;
    private readonly Action _roomTileChanged;
    private readonly Action<int> _playSound;
    private int _hitLockout;
    private bool _pendingHookHit;
    private bool _initialized;
    private bool _healthCleared;
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;
    internal bool CollisionEnabled => !Finished && !_healthCleared;
    public void ClearHealthAndCollision() => _healthCleared = true;
    private readonly PartSwitchCollisionDatabase _collisions = PartSwitchCollisionDatabase.Shared;
    private readonly OracleSaveData? _save;
    public bool Finished { get; private set; }

    public int CollisionZ => _data.SwitchCollisionZ;
    internal int PackedPosition => _record.PackedPosition;
    internal int SwitchMask => _record.SubId;
    internal int HitLockout => _hitLockout;
    internal Rect2 CollisionBounds => new(
        Position - new Vector2(_data.SwitchRadiusX, _data.SwitchRadiusY),
        new Vector2(_data.SwitchRadiusX * 2, _data.SwitchRadiusY * 2));

    internal DungeonSwitchRoomEntity(
        DungeonMechanicDatabaseRecord record,
        OracleRoomData room,
        DungeonMechanicDatabase data,
        OracleRuntimeState runtime,
        Func<long> animationTick,
        Action roomTileChanged,
        Action<int> playSound,
        OracleSaveData? save = null)
        : base(record, $"DungeonSwitch_{record.SubId:x2}_{record.Order}")
    {
        if (record.Id != InteractionId.Puff || record.SubId == 0)
            throw new ArgumentOutOfRangeException(nameof(record));
        _record = record;
        _room = room;
        _data = data;
        _runtime = runtime;
        _animationTick = animationTick;
        _roomTileChanged = roomTileChanged;
        _playSound = playSound;
        _save = save;
        if (record.Group == 0 && save is null)
            throw new InvalidOperationException("Overworld PART_SWITCH $05 requires live save state.");

        // replaceSwitchTiles runs before object parsing and restores each
        // switch's on metatile when its retained dungeon bit is already set.
        if (record.Group != 0 && SwitchIsOn())
            SetSwitchTile(_data.SwitchOnTile);
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized)
        {
            InitializePart();
            return;
        }
        // ENEMYDMG_34 writes $e4 to the signed invincibility counter. The
        // standard part update increments it through zero over 28 updates.
        if (_hitLockout > 0)
            _hitLockout--;
        if (_pendingHookHit || _healthCleared)
        {
            _pendingHookHit = false;
            Toggle();
        }
    }

    private void InitializePart()
    {
        _initialized = true;
        _healthCleared = false; // partCommon_standardUpdate reloads state-zero properties.
        _pendingHookHit = false;
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized) InitializePart();
        return ScreenTransitionPresentation.Visible;
    }

    public bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns)
    {
        TryToggle(hitbox, ItemCollisionType.L1Sword);
        // LINKDMG_1c does not mark ordinary enemy contact on ITEM_SWORD, so
        // this must not trigger Double-Edged Ring recoil or consume the hit.
        return false;
    }

    public bool ApplyItemCollision(
        RoomEntityItemCollision collision,
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        ICollection<RoomEntitySpawn> spawns)
    {
        // PART_SWITCH's active-collision row includes thrown objects and the
        // sword beam, but excludes bombs.
        if (collision == RoomEntityItemCollision.SwordBeam)
            // Effect20 clears the beam's collision bit and delivers part
            // damage $ff through LINKDMG24, making ITEM_SWORD_BEAM clink/delete.
            return TryToggle(hitbox, ItemCollisionType.SwordBeam);
        if (collision == RoomEntityItemCollision.ThrownObject)
            TryToggle(hitbox, ItemCollisionType.ThrownObject);
        // Thrown objects use effect26 / LINKDMG1c and remain active.
        return false;
    }

    public SeedHitResult ApplySeedHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int seedItem,
        ICollection<RoomEntitySpawn> spawns) =>
        TryToggle(hitbox, seedItem == ItemId.MysterySeed ? ItemCollisionType.MysterySeed : seedItem - 0x20 + 0x1b)
            ? SeedHitResult.Activate
            : SeedHitResult.None;

    public bool ApplySwitchHookHit(SwitchHookItem hook, Vector2 linkPosition)
    {
        if (!RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, hook.ZHigh, 7) ||
            !TryAcceptHit(hook.CollisionBounds, ItemCollisionType.SwitchHook)) return false;
        // collisionEffect26 writes var2a=$8d / invincibility=$e4 on the
        // collision pass. The next part update increments $e4 before switch.s
        // toggles the bit; subsequent interactions see it on that update.
        _pendingHookHit = true;
        hook.NotifyObjectCollision(); // Part.var3e=$08 retracts without a clink.
        return true;
    }

    private bool TryAcceptHit(Rect2 hitbox, int itemCollision)
    {
        int lockout = _collisions.HitLockout(itemCollision);
        if (!CollisionEnabled || _pendingHookHit || _hitLockout != 0 || lockout < 0 || !hitbox.Intersects(CollisionBounds))
            return false;
        _hitLockout = lockout;
        return true;
    }

    private bool TryToggle(Rect2 hitbox, int itemCollision)
    {
        if (!TryAcceptHit(hitbox, itemCollision)) return false;
        Toggle();
        return true;
    }

    private void Toggle()
    {
        byte switchState = _runtime.ReadWramByte(
            OracleRuntimeState.SwitchStateAddress);
        switchState ^= (byte)SwitchMask;
        _runtime.SetWramByte(
            OracleRuntimeState.SwitchStateAddress, switchState);
        if (_record.Group == 0)
        {
            // switch.s writes the visible $9e tile, then clears only the
            // logical layout byte before deleting the one-shot part.
            SetSwitchTile(_data.OverworldSwitchOnTile);
            _room.SetPositionTileAndCollision(Position, 0,
                _room.GetCollision((byte)_data.OverworldSwitchOnTile),
                _animationTick(), preserveRenderedTile: true);
            _save!.SetRoomFlag(_record.Group, _record.Room, OracleSaveData.RoomFlag40);
            Finished = true;
        }
        else SetSwitchTile(
            (switchState & SwitchMask) != 0
                ? _data.SwitchOnTile
                : _data.SwitchOffTile);
        _playSound(_data.SwitchSound);
    }

    private bool SwitchIsOn() =>
        (_runtime.ReadWramByte(OracleRuntimeState.SwitchStateAddress) &
            SwitchMask) != 0;

    private void SetSwitchTile(int tile)
    {
        _room.SetPositionTileAndCollision(
            Position, (byte)tile, null, _animationTick());
        _roomTileChanged();
    }
}

internal sealed record DungeonSwitchSpawn(DungeonMechanicDatabaseRecord Record) : RoomEntitySpawn;
