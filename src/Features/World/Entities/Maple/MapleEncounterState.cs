using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Shared PART_ITEM_FROM_MAPLE slot stream and race scores for one encounter.
/// List order is the original part-slot allocation order.
/// </summary>
internal sealed class MapleEncounterState
{
    private readonly List<MapleDroppedItem> _items = [];

    internal int MapleScore { get; set; }
    internal int LinkScore { get; set; }
    internal bool ObjectsDisabled { get; set; }
    internal int NextSlot { get; private set; }
    internal IReadOnlyList<MapleDroppedItem> Items => _items;

    internal int AllocateSlot() => NextSlot++;

    internal void Register(MapleDroppedItem item)
    {
        if (_items.Count >= 16)
            throw new InvalidOperationException(
                "Maple encounter exceeded the original 16 part slots.");
        _items.Add(item);
    }

    internal MapleDroppedItem? ChooseTarget(float mapleY, float mapleX)
    {
        for (int itemIndex = 0; itemIndex < 5; itemIndex++)
        {
            foreach (MapleDroppedItem item in _items)
            {
                if (item.CanMapleTarget && item.ItemIndex == itemIndex)
                    return item;
            }
        }

        for (int itemIndex = 5; itemIndex <= 13; itemIndex++)
        {
            MapleDroppedItem? closest = null;
            int closestDistance = 0;
            foreach (MapleDroppedItem item in _items)
            {
                if (!item.CanMapleTarget || item.ItemIndex != itemIndex)
                    continue;
                int distance =
                    Math.Abs((int)mapleY - (int)item.Position.Y) +
                    Math.Abs((int)mapleX - (int)item.Position.X);
                // The source replaces on equal distance, so the later part
                // slot wins ties.
                if (closest is null || distance <= closestDistance)
                {
                    closest = item;
                    closestDistance = distance;
                }
            }
            if (closest is not null)
                return closest;
        }
        return null;
    }
}
