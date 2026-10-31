using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// common/items/minecartCollision.s: an invisible, independent ITEM $1d.
internal sealed class MinecartCollisionRoomEntity(OracleRuntimeState memory, MinecartData data, int? restoredSlot = null)
    : INativeItemRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IScrollPersistentRoomEntity
{
    private int _address;
    private bool _finished;
    public Node2D? PresentationNode => null;
    public int NativeItemId => ItemId.MinecartCollision;
    public int? NativeReconstructionSlot => restoredSlot;
    public bool PersistsAcrossScroll => !Finished && (Read(0) & 3) == 3;
    public void EnterRoom(OracleRoomData room) { }
    public bool Finished
    {
        get
        {
            if (!_finished && _address != 0 && Read(0) == 0) _finished = true;
            return _finished;
        }
    }
    public bool NativeInitializationPending => !Finished && (_address == 0 || Read(4) == 0);
    internal int Address => _address;
    internal bool CollisionEnabled => !Finished && (Read(0x24) & 0x80) != 0;
    internal int CollisionType => Read(0x24) & 0x7f;
    internal int Damage => -unchecked((sbyte)Read(0x28));
    internal int Z => unchecked((sbyte)Read(0x0f));
    internal Vector2 Position => new(Read(0x0d), Read(0x0b));
    internal Rect2 Bounds => new(Position - new Vector2(Read(0x27), Read(0x26)),
        new(Read(0x27) * 2, Read(0x26) * 2));
    internal void NotifyTargetFlags(int flags) => Write(0x2a, Read(0x2a) | flags);

    public void BindNativeItemSlot(int page)
    {
        if (_address != 0 || page is < 0xd7 or > 0xdb)
            throw new InvalidOperationException($"ITEM $1d cannot bind page${page:x2}.");
        _address = page << 8;
        if (restoredSlot is { } saved)
        {
            if (page != saved || !CanReconstruct(memory, page, data))
                throw new NotSupportedException($"ITEM $1d slot${page:x2}: unsupported saved state.");
            return;
        }
        // minecartCreateCollisionItem writes only the header at birth.
        Write(0, 1); Write(1, NativeItemId);
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished || Read(4) != 0) return;
        Write(0x24, data.Constant("collision-type"));
        int radii = data.Constant("collision-radii");
        Write(0x26, radii >> 4); Write(0x27, radii & 15);
        Write(0x28, data.Constant("collision-damage")); Write(0x3a, Read(0x28));
        Write(0x29, data.Constant("collision-health")); Write(0x3c, 0xff);
        // itemData[$1d] has no graphics, tile offset or OAM flags.
        Write(0x1b, 0); Write(0x1c, 0); Write(0x1d, 0);
        Write(4, 1); Write(0, Read(0) | 2);
    }

    public void UpdateItemPost()
    {
        if (Finished) return;
        // The original tests the ID alone, including disabled D1 headers.
        if (memory.ReadWramByte(0xd101) != SpecialObjectId.Minecart)
        { ReleaseNativeItemSlot(); return; }
        for (int offset = 0x0b; offset <= 0x0f; offset += 2)
            Write(offset, memory.ReadWramByte(0xd100 + offset));
    }

    public void ReleaseNativeItemSlot()
    {
        if (_address != 0 && !_finished)
            for (int offset = 0; offset < 0x40; offset++) Write(offset, 0);
        _finished = true;
    }
    public void SetTransitionDrawOffset(Vector2 offset) { }
    internal static bool CanReconstruct(OracleRuntimeState memory, int page, MinecartData data)
    {
        int at = page << 8;
        if (page is < 0xd7 or > 0xdb || memory.ReadWramByte(at) == 0 || memory.ReadWramByte(at + 1) != ItemId.MinecartCollision)
            return false;
        int state = memory.ReadWramByte(at + 4);
        if (state == 0) return true;
        return state == 1 && memory.ReadWramByte(at + 0x24) == data.Constant("collision-type") &&
            memory.ReadWramByte(at + 0x26) == data.Constant("collision-radii") >> 4 &&
            memory.ReadWramByte(at + 0x27) == (data.Constant("collision-radii") & 15) &&
            memory.ReadWramByte(at + 0x28) == data.Constant("collision-damage");
    }
    private int Read(int offset) => memory.ReadWramByte(_address + offset);
    private void Write(int offset, int value) => memory.SetWramByte(_address + offset, checked((byte)value));
}
