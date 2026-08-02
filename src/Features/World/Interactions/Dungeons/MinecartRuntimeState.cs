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

    internal static void EnsureInitialized(
        OracleRuntimeState state,
        IReadOnlyList<MinecartStaticRecord> records)
    {
        if (CompanionRuntimeState.IsActive(
                state, CompanionRuntimeState.MinecartId))
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
        if (CompanionRuntimeState.IsActive(
                state, CompanionRuntimeState.MinecartId) &&
            CompanionRuntimeState.Read(state) is ActiveCompanion active &&
            active.Room == room)
        {
            cart = new ActiveMinecart(
                Slot: -1,
                room,
                active.Y,
                active.X,
                active.Direction,
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
        CompanionRuntimeState.Begin(
            state, CompanionRuntimeState.MinecartId,
            room, position, direction);
    }

    internal static void UpdateRide(
        OracleRuntimeState state,
        int room,
        Vector2 position,
        int direction)
    {
        CompanionRuntimeState.Update(
            state, CompanionRuntimeState.MinecartId,
            room, position, direction);
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
        CompanionRuntimeState.Clear(state, CompanionRuntimeState.MinecartId);
    }

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
