using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// PART$40 owns a distinct physical slot after its ordinary PART$01 pickup.
internal sealed class HeadThwompBombDropRoomEntity : RoomEntityAdapter<Node2D>,
    IFixedRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity,
    IUpdatesDuringRoomEntityFreeze
{
    private readonly OracleRoomData _room;
    private readonly OracleRandom _random;
    private readonly OracleRuntimeState _memory;
    private readonly Func<ItemDropEffect?> _drop;
    private OracleObjectPosition _position;
    private int _speed;
    private int _angle;
    private int _speedY;
    private bool _initialized;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;

    internal HeadThwompBombDropRoomEntity(OracleRoomData room, OracleRandom random,
        Func<ItemDropEffect?> drop, Vector2 position, OracleRuntimeState memory)
        : base(new Node2D { Name = "HeadThwompBombDropper_40", Visible = false }, static _ => { })
    {
        _room = room; _random = random; _drop = drop; _memory = memory;
        _position = OracleObjectMovement.Shared.PositionFromPixels(position.Floor());
        Entity.Position = _position.PixelPosition;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        // The source compares the current related page's PART id, so a reused
        // ordinary drop slot is followed; a stale managed object is not retained.
        ItemDropEffect? drop = _drop();
        if (drop is null || drop.Finished) { Finished = true; return; }
        if (!_initialized)
        {
            _initialized = true;
            var roll = _random.Next();
            _speed = new[] { 0x14, 0x19, 0x1e, 0x23 }[roll.Value & 3];
            _speedY = new[] { -0x300, -0x320, -0x340, -0x360 }[(roll.Value & 0x60) >> 5];
            _angle = (roll.High & 0x10) + 8;
            return;
        }
        if (OracleObjectMovement.Shared.UpdateSpeedZSidescroll(ref _position, ref _speedY, 0x20,
            point => _room.IsSolid(point) && _room.GetTerrainInfo(point).Hazard != HazardType.Hole))
        { Finished = true; return; }
        _position = NativeObjectMovement.ApplySpeed(_memory, _position, _speed, _angle);
        Entity.Position = _position.PixelPosition;
        drop.CopyHeadThwompBombDropperPosition(_position);
    }
}

internal sealed record HeadThwompBombDropperSpawn(int DropSlot, Vector2 Position) : RoomEntitySpawn;
