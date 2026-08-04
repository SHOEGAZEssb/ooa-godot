using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Routes Ricky's ITEM_28, landing, and tornado tile probes through the
/// imported breakable-tile source masks.
/// </summary>
internal sealed class RickyAttackTileBreaker(
    int group,
    OracleRoomData room,
    BreakableTileDatabase breakables,
    OracleSaveData? saveData,
    Func<Vector2I, int?>? linkedRoomNeighbor,
    Action roomTileChanged,
    Func<long> animationTick,
    Action<int> playSound,
    Func<int, int?> decideBreakableDrop)
{
    internal void TryBreak(
        Vector2 point,
        int source,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (point.X < 0 || point.X >= room.Width ||
            point.Y < 0 || point.Y >= room.Height)
        {
            return;
        }
        byte tile = room.GetMetatile(point);
        if (!breakables.TryGet(
                room.ActiveCollisions,
                tile,
                out BreakableTileRecord breakable) ||
            !breakable.AllowsSource(source))
        {
            return;
        }

        int packed = room.GetPackedPosition(point);
        Vector2 tileCenter = new(
            (packed & 0x0f) * OracleRoomData.MetatileSize + 8,
            (packed >> 4) * OracleRoomData.MetatileSize + 8);
        byte replacement = breakable.ReplacementFor(room, tileCenter);
        bool changed = breakable.Replacement == 0 ||
            room.ReplaceMetatile(
                tileCenter, tile, replacement, animationTick());
        if (!changed)
            return;
        breakable.ApplyPersistentEffects(
            saveData, group, room.Id, linkedRoomNeighbor);
        if ((breakable.Effect & 0x40) != 0)
            playSound(OracleSoundEngine.SndSolvePuzzle);
        if (breakable.Drop != 0 &&
            decideBreakableDrop(breakable.Drop) is int subId)
        {
            spawns.Add(new ItemDropSpawn(subId, tileCenter));
        }
        AddBreakEffect(spawns, tileCenter, breakable.Effect);
        roomTileChanged();
    }

    private void AddBreakEffect(
        ICollection<RoomEntitySpawn> spawns,
        Vector2 position,
        int effect)
    {
        int interaction = effect & 0x0f;
        bool flickers = (effect & 0x10) != 0;
        if (interaction is 0x06 or 0x0c)
        {
            spawns.Add(new RockDebrisSpawn(position, interaction));
        }
        else if (interaction is 0x00 or 0x01)
        {
            spawns.Add(new GrassDebrisSpawn(
                position,
                interaction,
                flickers,
                (room.TilesetFlags & 0x40) != 0));
        }
    }
}
