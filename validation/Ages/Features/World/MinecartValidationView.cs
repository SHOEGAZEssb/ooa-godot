using Godot;
using System;
using System.Linq;

namespace Oracles;

// Read the two real allocations; fixtures must not retain one presenter across their handoff.
internal sealed class MinecartValidationView(RoomEntityManager entities, Player player, Vector2? waitingPosition = null)
{
    internal static void PrepareMountedBody(OracleRuntimeState memory, Vector2 position, int direction)
    {
        CompanionRuntimeState.Begin(memory, SpecialObjectId.Minecart, position, direction);
        memory.SetField("wLinkObjectIndex", 0xd1);
    }

    // These component fixtures start after native state zero; copy that executed
    // setup rather than keeping a production-only shortcut around initialization.
    internal static void CopyMountedBodyFromRom(RoomEntityManager entities, SomariaRom rom)
    {
        var memory = entities.RuntimeState;
        if (rom[0xd101] != 0x0a || rom[0xd104] != 1)
            throw new InvalidOperationException("Mounted cart fixture requires initialized SPECIALOBJECT $0a state$01.");
        for (int offset = 0; offset < 0x40; offset++)
            memory.SetWramByte(0xd100 + offset, rom[0xd100 + offset]);
        for (int page = 0xd7; page <= 0xdb; page++)
        {
            int at = page << 8;
            if (rom[at] == 0 || rom[at + 1] != 0x1d) continue;
            var item = entities.TryCreateNativeItem(() => new MinecartCollisionRoomEntity(memory, MinecartData.For(entities.AssetRoot)));
            if (item is null || entities.DynamicItemSlotOf(item) != page)
                throw new InvalidOperationException("Native mounted-cart fixture has inconsistent ITEM $1d occupancy.");
            for (int offset = 0; offset < 0x40; offset++) memory.SetWramByte(at + offset, rom[at + offset]);
        }
    }

    internal MinecartRoomEntity? Body => entities.Entities<MinecartRoomEntity>().SingleOrDefault(actor => !actor.Finished);
    internal MinecartInteractionRoomEntity? Interaction => entities.Entities<MinecartInteractionRoomEntity>()
        .FirstOrDefault(actor => !actor.Finished && actor.State != 3 &&
            (waitingPosition is null || actor.Position == waitingPosition.Value));
    private Node2D Actor => (Node2D?)Body ?? Interaction ??
        throw new InvalidOperationException("The native minecart has neither a live body nor an interaction.");
    internal bool Riding => Body?.Riding == true;
    internal bool AllocationPending => Body?.AllocationPending == true;
    internal bool Mounting => Interaction?.Mounting == true;
    internal bool Dismounting => Body is null && player.MinecartJumpActive && Interaction?.State is 0 or 1;
    internal Vector2 Position => Actor.Position;
    internal int Direction => Body?.Direction ?? Interaction?.Direction ?? throw new InvalidOperationException("No minecart direction owner.");
    internal int Angle => Body?.Angle ?? Interaction?.Angle ?? throw new InvalidOperationException("No minecart angle owner.");
    internal int PushCounter => Interaction?.PushCounter ?? 0;
    internal int ZIndex => Actor.ZIndex;
    internal int CurrentAnimationIndex => Body?.CurrentAnimationIndex ?? Interaction?.CurrentAnimationIndex ?? 0;
    internal int CurrentAnimationFrame => Body?.CurrentAnimationFrame ?? Interaction?.CurrentAnimationFrame ?? 0;
}
