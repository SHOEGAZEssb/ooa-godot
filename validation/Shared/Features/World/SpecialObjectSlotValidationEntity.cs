using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// Declares a source category/identity without running unrelated species AI.
internal sealed class SpecialObjectSlotValidationEntity(int id, OracleRuntimeState memory, bool credits = false) :
    INativeSpecialObjectRoomEntity, IFixedRoomEntity, IRoomEntityLifetime
{
    public Node2D Node { get; } = new();
    public bool Finished { get; set; }
    public int NativeSpecialObjectId => credits ? memory.ReadWramByte(0xd101) : id;
    public bool MatchesNativeSpecialObjectId(int candidate) => credits ? candidate is >= 0x0f and <= 0x11 : candidate == id;
    public Vector2 NativeSpecialObjectPosition { get => Node.Position; set => Node.Position = value; }
    internal int Updates { get; private set; }
    internal Action? OnUpdate { get; set; }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    { Updates++; OnUpdate?.Invoke(); }
    public void SetTransitionDrawOffset(Vector2 offset) { }
}
