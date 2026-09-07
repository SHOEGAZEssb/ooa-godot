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
    IObjectCollisionHeightRoomEntity, ISeedPreMovementCollisionTarget, IRoomEntityLifetime
{
    private readonly DungeonMechanicDatabaseRecord _record;
    private readonly OracleRoomData _room;
    private readonly DungeonMechanicDatabase _data;
    private readonly OracleRuntimeState _runtime;
    private readonly Func<long> _animationTick;
    private readonly Action _roomTileChanged;
    private readonly Action<int> _playSound;
    private int _hitLockout;
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
        if (record.Id != 0x05 || record.SubId == 0)
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
        if (Finished || _hitLockout != 0 || !hitbox.Intersects(CollisionBounds))
            return false;

        byte switchState = _runtime.ReadWramByte(
            OracleRuntimeState.SwitchStateAddress);
        switchState ^= (byte)SwitchMask;
        _runtime.SetWramByte(
            OracleRuntimeState.SwitchStateAddress, switchState);
        _hitLockout = applyHitLockout ? _data.SwitchHitLockout : 0;
        if (_record.Group == 0)
        {
            // switch.s writes the visible $9e tile, then clears only the
            // logical layout byte before deleting the one-shot part.
            SetSwitchTile(_data.OverworldSwitchOnTile);
            _room.SetPositionTileAndCollision(Position, 0,
                _room.GetCollision((byte)_data.OverworldSwitchOnTile),
                _animationTick(), preserveRenderedTile: true);
            _save!.SetRoomFlag(_record.Group, _record.Room, 0x40);
            Finished = true;
        }
        else SetSwitchTile(
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

internal sealed record DungeonSwitchSpawn(DungeonMechanicDatabaseRecord Record) : RoomEntitySpawn;
