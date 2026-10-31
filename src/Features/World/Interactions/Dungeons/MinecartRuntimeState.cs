using Godot;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

// Minecart projections read the shared static buffer and the independent D1 owner.
internal static class MinecartRuntimeState
{
    internal static IEnumerable<ActiveMinecart> StationaryInRoom(OracleRuntimeState state, int room) =>
        StaticObjectState.InRoom(state, room).Where(record => record.Type == 3 && (record.Id & 0x7f) == 0x16)
            .Select(record => new ActiveMinecart(record.Slot, room, record.Y, record.X, -1, false,
                record.SubId, (record.Id & 0x80) != 0 ? 3 : 1));

    internal static bool TryGetRide(
        OracleRuntimeState state,
        int room,
        out ActiveMinecart cart)
    {
        if (CompanionRuntimeState.IsActive(
                state, CompanionRuntimeState.MinecartId) &&
            state.ReadField("wLinkObjectIndex") == 0xd1 &&
            CompanionRuntimeState.Read(state) is ActiveCompanion active)
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

    internal static void ClearActive(OracleRuntimeState state)
    {
        bool minecartOwnedSlot = CompanionRuntimeState.IsActive(state, CompanionRuntimeState.MinecartId);
        CompanionRuntimeState.Clear(state, CompanionRuntimeState.MinecartId);
        if (minecartOwnedSlot) state.SetField("wLinkObjectIndex", 0xd0);
    }

}

internal readonly record struct ActiveMinecart(
    int Slot,
    int Room,
    int Y,
    int X,
    int Direction,
    bool Riding,
    int SubId = 0,
    int Enabled = 1)
{
    internal Vector2 Position => new(X, Y);
}

internal readonly record struct MinecartStaticRecord(
    int Slot,
    int Room,
    int Y,
    int X,
    string Source);
