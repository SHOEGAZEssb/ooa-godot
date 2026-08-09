using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Source-derived INTERAC_TOKAY_SHOP_ITEM placement, visual, and room
/// contracts for the Tokay trading hut.
/// </summary>
internal sealed class TokayShopDatabase
{
    private readonly Dictionary<string, int> _constants =
        new(StringComparer.Ordinal);
    private readonly Dictionary<int, TokayShopPlacementRecord> _visuals = new();
    private readonly List<TokayShopPlacementRecord> _placements = new();

    internal int Group => Constant("group");
    internal int Room => Constant("room");
    internal int ItemCollisionRadius => Constant("item-collision-radius");
    internal int BoughtFeatherFlag => Constant("global-bought-feather");
    internal int BoughtBraceletFlag => Constant("global-bought-bracelet");
    internal IReadOnlyList<TokayShopPlacementRecord> Placements => _placements;

    internal TokayShopDatabase()
    {
        LoadConstants();
        LoadPlacements();
        Validate();
    }

    internal TokayShopPlacementRecord Visual(int subId) =>
        _visuals.TryGetValue(subId, out TokayShopPlacementRecord record)
            ? record
            : throw new KeyNotFoundException(
                $"INTERAC_TOKAY_SHOP_ITEM visual ${subId:x2} was not imported.");

    private int Constant(string key) =>
        _constants.TryGetValue(key, out int value)
            ? value
            : throw new KeyNotFoundException(
                $"Tokay shop constant '{key}' was not imported.");

    private void LoadConstants()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/tokay_shop_constants.tsv",
            new GeneratedTableSchema(
                "Tokay shop constants",
                GeneratedTableKeySemantics.Unique,
                ["key", "value"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
            _constants.Add(row.RequiredString(0), row.Decimal(1));
    }

    private void LoadPlacements()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/tokay_shop_items.tsv",
            new GeneratedTableSchema(
                "Tokay shop items",
                GeneratedTableKeySemantics.Ordered,
                [
                    "order", "placed-subid", "y", "x", "sprite", "tile-base",
                    "palette", "animation"
                ],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            var record = new TokayShopPlacementRecord(
                row.Decimal(0), row.HexByte(1), row.HexByte(2),
                row.HexByte(3), row.RequiredString(4), row.HexByte(5),
                row.HexByte(6), row.RequiredString(7));
            _visuals.Add(record.PlacedSubId, record);
            if (record.Order >= 0)
                _placements.Add(record);
        }
    }

    private void Validate()
    {
        if (Group != 2 || Room != 0xe4 || ItemCollisionRadius != 0x06 ||
            _placements.Count != 3 || _visuals.Count != 7)
        {
            throw new InvalidOperationException(
                "INTERAC_TOKAY_SHOP_ITEM generated data does not match the traced source contract.");
        }
    }
}

internal readonly record struct TokayShopPlacementRecord(
    int Order, int PlacedSubId, int Y, int X, string Sprite,
    int TileBase, int Palette, string Animation);
