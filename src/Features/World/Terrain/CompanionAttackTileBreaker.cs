using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Routes animal companion attacks through the
/// imported breakable-tile source masks.
/// </summary>
internal sealed class CompanionAttackTileBreaker(
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
    internal bool TryBreak(
        Vector2 point,
        int source,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (point.X < 0 || point.X >= room.Width ||
            point.Y < 0 || point.Y >= room.Height)
        {
            return false;
        }
        if (breakables.TryBreak(
                room,
                source,
                point,
                saveData,
                group,
                animationTick,
                linkedRoomNeighbor,
                out BreakableTileBreak result) !=
            BreakableTileBreakStatus.Broken)
        {
            return false;
        }
        result.ApplyCommonEffects(
            playSound, decideBreakableDrop, spawns);
        // BREAKABLETILESOURCE_DIMITRI_EAT still rolls the drop, but skips
        // makeInteractionForBreakableTile (including its break sound).
        if (source != 0x12 && BreakableTileEffectSpawn.Create(
                room, result.TileCenter, result.Record.Effect) is { } effect)
        {
            spawns.Add(effect);
        }
        roomTileChanged();
        return true;
    }
}
