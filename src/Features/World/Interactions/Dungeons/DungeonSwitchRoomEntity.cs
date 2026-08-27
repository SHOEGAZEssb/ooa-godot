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
    IObjectCollisionHeightRoomEntity, ISeedPreMovementCollisionTarget
{
    private readonly DungeonMechanicDatabaseRecord _record;
    private readonly OracleRoomData _room;
    private readonly DungeonMechanicDatabase _data;
    private readonly OracleRuntimeState _runtime;
    private readonly Func<long> _animationTick;
    private readonly Action _roomTileChanged;
    private readonly Action<int> _playSound;
    private int _hitLockout;

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
        Action<int> playSound)
        : base(record, $"DungeonSwitch_{record.SubId:x2}_{record.Order}")
    {
        if (record.Id != 0x05 || record.SubId == 0)
            throw new ArgumentOutOfRangeException(nameof(record));
        _record = record;
        _room = room;
        _data = data;
        _runtime = runtime;
        _animationTick = animationTick;
        _roomTileChanged = roomTileChanged;
        _playSound = playSound;

        // replaceSwitchTiles runs before object parsing and restores each
        // switch's on metatile when its retained dungeon bit is already set.
        if (SwitchIsOn())
            SetSwitchTile(_data.SwitchOnTile);
    }

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        // ENEMYDMG_34 writes $e4 to the signed invincibility counter. The
        // standard part update increments it through zero over 28 updates.
        if (_hitLockout > 0)
            _hitLockout--;
    }

    public bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns)
    {
        TryToggle(hitbox);
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
        if (collision is RoomEntityItemCollision.ThrownObject or
            RoomEntityItemCollision.SwordBeam)
        {
            TryToggle(hitbox);
        }
        // LINKDMG_1c leaves the attacking item active.
        return false;
    }

    public SeedHitResult ApplySeedHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int seedItem,
        ICollection<RoomEntitySpawn> spawns) =>
        TryToggle(hitbox, applyHitLockout: false)
            ? SeedHitResult.Activate
            : SeedHitResult.None;

    private bool TryToggle(Rect2 hitbox, bool applyHitLockout = true)
    {
        if (_hitLockout != 0 || !hitbox.Intersects(CollisionBounds))
            return false;

        byte switchState = _runtime.ReadWramByte(
            OracleRuntimeState.SwitchStateAddress);
        switchState ^= (byte)SwitchMask;
        _runtime.SetWramByte(
            OracleRuntimeState.SwitchStateAddress, switchState);
        _hitLockout = applyHitLockout ? _data.SwitchHitLockout : 0;
        SetSwitchTile(
            (switchState & SwitchMask) != 0
                ? _data.SwitchOnTile
                : _data.SwitchOffTile);
        _playSound(_data.SwitchSound);
        return true;
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
