using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Ages wStaticObjects ($cd80-$cdbf) plus the active SPECIALOBJECT_MINECART
/// fields retained while a cart crosses room boundaries.
/// </summary>
internal static class MinecartRuntimeState
{
    private const int StaticStart = 0xcd80;
    private const int StaticSlotSize = 8;
    private const int StaticSlotCount = 8;
    private const int StaticInteraction = 3;
    private const int MinecartInteractionId = 0x16;

    // The active cart occupies the clone's otherwise-unused w1Companion
    // window, matching the original owner.
    private const int Active = 0xd100;
    private const int ActiveRoom = 0xd102;
    private const int ActiveDirection = 0xd103;
    private const int ActiveY = 0xd104;
    private const int ActiveX = 0xd105;

    internal static void EnsureInitialized(
        OracleRuntimeState state,
        IReadOnlyList<MinecartStaticRecord> records)
    {
        if (IsActive(state))
            return;
        for (int slot = 0; slot < StaticSlotCount; slot++)
        {
            if (state.ReadWramByte(Address(slot)) == StaticInteraction &&
                state.ReadWramByte(Address(slot) + 2) == MinecartInteractionId)
            {
                return;
            }
        }
        Reset(state, records);
    }

    internal static void Reset(
        OracleRuntimeState state,
        IReadOnlyList<MinecartStaticRecord> records)
    {
        for (int offset = 0; offset < StaticSlotSize * StaticSlotCount; offset++)
            state.SetWramByte(StaticStart + offset, 0);
        ClearActive(state);
        foreach (MinecartStaticRecord record in records)
            WriteStatic(state, record.Slot, record.Room, record.Y, record.X);
    }

    internal static IEnumerable<ActiveMinecart> StationaryInRoom(
        OracleRuntimeState state,
        int room)
    {
        for (int slot = 0; slot < StaticSlotCount; slot++)
        {
            int address = Address(slot);
            if (state.ReadWramByte(address) != StaticInteraction ||
                state.ReadWramByte(address + 1) != room ||
                state.ReadWramByte(address + 2) != MinecartInteractionId)
            {
                continue;
            }
            yield return new ActiveMinecart(
                slot,
                room,
                state.ReadWramByte(address + 4),
                state.ReadWramByte(address + 5),
                Direction: -1,
                Riding: false);
        }
    }

    internal static bool TryGetRide(
        OracleRuntimeState state,
        int room,
        out ActiveMinecart cart)
    {
        if (IsActive(state) &&
            state.ReadWramByte(ActiveRoom) == room)
        {
            cart = new ActiveMinecart(
                Slot: -1,
                room,
                state.ReadWramByte(ActiveY),
                state.ReadWramByte(ActiveX),
                state.ReadWramByte(ActiveDirection),
                Riding: true);
            return true;
        }
        cart = default;
        return false;
    }

    internal static void BeginRide(
        OracleRuntimeState state,
        int slot,
        int room,
        Vector2 position,
        int direction)
    {
        ClearStaticSlot(state, slot);
        state.SetWramByte(Active, 1);
        UpdateRide(state, room, position, direction);
    }

    internal static void UpdateRide(
        OracleRuntimeState state,
        int room,
        Vector2 position,
        int direction)
    {
        state.SetWramByte(ActiveRoom, (byte)room);
        state.SetWramByte(ActiveDirection, (byte)direction);
        state.SetWramByte(ActiveY, (byte)Mathf.FloorToInt(position.Y));
        state.SetWramByte(ActiveX, (byte)Mathf.FloorToInt(position.X));
    }

    internal static int FinishRide(
        OracleRuntimeState state,
        int room,
        Vector2 position)
    {
        int slot = FindFreeStaticSlot(state);
        if (slot < 0)
            throw new InvalidOperationException(
                "No free wStaticObjects slot remains for the active minecart.");
        WriteStatic(
            state,
            slot,
            room,
            Mathf.FloorToInt(position.Y),
            Mathf.FloorToInt(position.X));
        ClearActive(state);
        return slot;
    }

    internal static void ClearActive(OracleRuntimeState state)
    {
        for (int address = Active; address <= ActiveX; address++)
            state.SetWramByte(address, 0);
    }

    private static bool IsActive(OracleRuntimeState state) =>
        state.ReadWramByte(Active) != 0;

    private static int FindFreeStaticSlot(OracleRuntimeState state)
    {
        for (int slot = 0; slot < StaticSlotCount; slot++)
        {
            if (state.ReadWramByte(Address(slot)) == 0)
                return slot;
        }
        return -1;
    }

    private static void WriteStatic(
        OracleRuntimeState state,
        int slot,
        int room,
        int y,
        int x)
    {
        int address = Address(slot);
        state.SetWramByte(address, StaticInteraction);
        state.SetWramByte(address + 1, (byte)room);
        state.SetWramByte(address + 2, MinecartInteractionId);
        state.SetWramByte(address + 3, 0);
        state.SetWramByte(address + 4, (byte)y);
        state.SetWramByte(address + 5, (byte)x);
        state.SetWramByte(address + 6, 0);
        state.SetWramByte(address + 7, 0);
    }

    private static void ClearStaticSlot(OracleRuntimeState state, int slot)
    {
        int address = Address(slot);
        for (int offset = 0; offset < StaticSlotSize; offset++)
            state.SetWramByte(address + offset, 0);
    }

    private static int Address(int slot)
    {
        if (slot is < 0 or >= StaticSlotCount)
            throw new ArgumentOutOfRangeException(nameof(slot));
        return StaticStart + slot * StaticSlotSize;
    }
}

internal readonly record struct ActiveMinecart(
    int Slot,
    int Room,
    int Y,
    int X,
    int Direction,
    bool Riding)
{
    internal Vector2 Position => new(X, Y);
}

internal readonly record struct MinecartStaticRecord(
    int Slot,
    int Room,
    int Y,
    int X,
    string Source);
