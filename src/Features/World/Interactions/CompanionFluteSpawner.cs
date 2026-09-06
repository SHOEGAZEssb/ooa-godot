using Godot;
using System;
using System.Linq;

namespace oracleofages;

/// <summary>INTERAC_COMPANION_SPAWNER $67:$80 source-ordered entrance search.</summary>
internal sealed class CompanionFluteSpawner(RoomSession rooms, RoomEntityManager entities,
    FluteDatabase data, Action<string, Player> showText)
{
    internal void Call(Player player, int icon)
    {
        var occupants = entities.EntityAdapters<IRoomEntity>().Where(actor => actor is IPlayerRideableRoomEntity).ToArray();
        if (occupants.Any(actor => actor is DimitriCompanionRoomEntity or RickyCompanionRoomEntity or MooshCompanionRoomEntity)) return;
        if ((rooms.CurrentRoom.TilesetFlags & 0x81) != 1) return;
        if (icon == 0) { showText(data.Text(0x510f), player); return; }
        if (!data.Callable(rooms.CurrentRoom.Id)) { showText(data.Text(0x510c), player); return; }
        if (occupants.Length != 0) return;

        int column = (Mathf.FloorToInt(player.Position.X) >> 4) & 15;
        int row = Mathf.FloorToInt(player.Position.Y) & 0xf0;
        (int Packed, int Stride, int Direction)[] direct =
            [(column, 16, 2), (0x60 + column, 16, 0), (row + 8, 1, 3), (row, 1, 1)];
        foreach (var probe in direct)
            if (TryPair(probe.Packed, probe.Stride, out Vector2 point))
            { Spawn(probe.Direction, point, player); return; }
        (int Packed, int Stride, int Step, int Direction)[] ranges =
            [(3, 16, 1, 2), (0x63, 16, 1, 0), (0x28, 1, 16, 3), (0x20, 1, 16, 1)];
        foreach (var range in ranges)
        for (int index = 0; index < 4; index++)
            if (TryPair(range.Packed + index * range.Step, range.Stride, out Vector2 point))
            { Spawn(range.Direction, point, player); return; }
        showText(data.Text(0x510c), player);
    }

    private bool TryPair(int packed, int stride, out Vector2 point)
    {
        Vector2 first = new((packed & 15) * 16 + 8, (packed >> 4) * 16 + 8);
        int second = (packed + stride) & 255;
        point = new((second & 15) * 16 + 8, (second >> 4) * 16 + 8);
        return rooms.CurrentRoom.GetTerrainInfo(first).Collision == 0 &&
            rooms.CurrentRoom.GetTerrainInfo(point).Collision == 0;
    }

    private void Spawn(int direction, Vector2 point, Player player)
    {
        Vector2 start = direction switch
        {
            0 => new(point.X, 136), 1 => new(-8, point.Y),
            2 => new(point.X, -8), 3 => new(168, point.Y),
            _ => throw new ArgumentOutOfRangeException(nameof(direction))
        };
        Vector2 destination = direction switch
        {
            0 => new(point.X, 112), 1 => new(16, point.Y),
            2 => new(point.X, 16), _ => new(144, point.Y)
        };
        if (player.Inventory.AnimalCompanion != 0x0c)
            throw new InvalidOperationException($"companionSpawner.s:@fluteCall has no runtime entrance owner for companion ${player.Inventory.AnimalCompanion:x2}.");
        entities.Spawn<DimitriCompanionRoomEntity>(new DimitriCompanionSpawn(start, direction,
            rooms.ActiveGroup, rooms.CurrentRoom.Id, FluteDestination: destination));
    }
}
