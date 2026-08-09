using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Source-ordered placements for the globally dispatched
/// INTERAC_MOVING_SIDESCROLL_PLATFORM $a1 handler.
/// </summary>
internal sealed class MovingSideScrollPlatformDatabase
{
    private readonly Lookup<(int Group, int Room), MovingSideScrollPlatformPlacement>
        _placements = new();

    internal MovingSideScrollPlatformDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/moving_side_scroll_platform_placements.tsv",
            new GeneratedTableSchema(
                "moving side-scroll platform placements",
                GeneratedTableKeySemantics.Grouped,
                ["group", "room", "order", "id", "subid", "y", "x", "source"],
                ["group", "room"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            MovingSideScrollPlatformPlacement placement = new(
                row.Decimal(0, 0, 5),
                row.HexByte(1),
                row.UnsignedDecimal(2),
                row.HexByte(3),
                row.HexByte(4),
                row.HexByte(5),
                row.HexByte(6),
                row.RequiredString(7));
            if (placement.Id != 0xa1)
                throw row.Invalid(3, "INTERAC_MOVING_SIDESCROLL_PLATFORM $a1");
            _placements.Add((placement.Group, placement.Room), placement);
        }
        _placements.SortValues(
            static (left, right) => left.Order.CompareTo(right.Order));
        ValidateContract();
    }

    internal IReadOnlyList<MovingSideScrollPlatformPlacement> GetRoomRecords(
        int group,
        int room) =>
        _placements.ValuesOrEmpty((group, room));

    private void ValidateContract()
    {
        int count = 0;
        foreach (IReadOnlyList<MovingSideScrollPlatformPlacement> placements in
            _placements.Values)
        {
            count += placements.Count;
        }
        if (count != 17 ||
            GetRoomRecords(5, 0x06) is not
                [{ Order: 0, Id: 0xa1, SubId: 0x0b, Y: 0x68, X: 0x68 }])
        {
            throw new InvalidOperationException(
                "Imported moving side-scroll platform placements are incomplete.");
        }
    }
}

internal readonly record struct MovingSideScrollPlatformPlacement(
    int Group,
    int Room,
    int Order,
    int Id,
    int SubId,
    int Y,
    int X,
    string Source)
{
    internal Godot.Vector2 Position => new(X, Y);
}
