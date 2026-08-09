using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Source-derived INTERAC_TOKAY dialogue, animation, held-item, and common
/// actor contracts shared by Tokay Island room entities and events.
/// </summary>
internal sealed class TokayInteractionDatabase
{
    private readonly Dictionary<string, int> _constants =
        new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> _texts = new();
    private readonly Dictionary<int, string> _animations = new();
    private readonly Dictionary<int, TokayHeldItemRecord> _heldItems = new();

    internal int DimitriStateAddress => Constant("dimitri-state-address");
    internal int SoundGetSeed => Constant("sound-get-seed");
    internal int SoundJump => Constant("sound-jump");

    internal TokayInteractionDatabase()
    {
        LoadConstants();
        LoadTexts();
        LoadAnimations();
        LoadHeldItems();
        Validate();
    }

    internal string Text(int textId) =>
        _texts.TryGetValue(textId, out string? text)
            ? text
            : throw new KeyNotFoundException(
                $"Tokay Island text TX_{textId:x4} was not imported.");

    internal string Animation(int animation) =>
        _animations.TryGetValue(animation, out string? encoded)
            ? encoded
            : throw new KeyNotFoundException(
                $"INTERAC_TOKAY animation ${animation:x2} was not imported.");

    internal TokayHeldItemRecord HeldItem(int subId) =>
        _heldItems.TryGetValue(subId, out TokayHeldItemRecord record)
            ? record
            : throw new KeyNotFoundException(
                $"INTERAC_TOKAY holder ${subId:x2} was not imported.");

    private int Constant(string key) =>
        _constants.TryGetValue(key, out int value)
            ? value
            : throw new KeyNotFoundException(
                $"Tokay interaction constant '{key}' was not imported.");

    private void LoadConstants()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/tokay_interaction_constants.tsv",
            new GeneratedTableSchema(
                "Tokay interaction constants",
                GeneratedTableKeySemantics.Unique,
                ["key", "value"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
            _constants.Add(row.RequiredString(0), row.Decimal(1));
    }

    private void LoadTexts()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/tokay_interaction_texts.tsv",
            new GeneratedTableSchema(
                "Tokay Island texts",
                GeneratedTableKeySemantics.Unique,
                ["text-id", "utf8-base64"],
                ["text-id"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
            _texts.Add(row.HexWord(0), row.Base64Utf8(1));
    }

    private void LoadAnimations()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/tokay_interaction_animations.tsv",
            new GeneratedTableSchema(
                "Tokay animations",
                GeneratedTableKeySemantics.Unique,
                ["animation", "encoded"],
                ["animation"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
            _animations.Add(row.HexByte(0), row.RequiredString(1));
    }

    private void LoadHeldItems()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/tokay_item_holders.tsv",
            new GeneratedTableSchema(
                "Tokay held items",
                GeneratedTableKeySemantics.Unique,
                [
                    "subid", "treasure", "item-graphic", "grant-object",
                    "grant-subid", "grant-parameter", "item-sprite",
                    "item-tile-base", "item-palette", "item-animation"
                ],
                ["subid"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            var record = new TokayHeldItemRecord(
                row.HexByte(0), row.HexByte(1), row.HexByte(2),
                row.RequiredString(3), row.HexByte(4), row.HexByte(5),
                row.RequiredString(6), row.HexByte(7), row.HexByte(8),
                row.RequiredString(9));
            _heldItems.Add(record.SubId, record);
        }
    }

    private void Validate()
    {
        if (DimitriStateAddress != 0xc647 ||
            _texts.Count != 96 || _animations.Count != 10 ||
            _heldItems.Count != 5 ||
            HeldItem(0x06).Treasure != TreasureDatabase.TreasureSword ||
            HeldItem(0x06).GrantSubId != 0x06 ||
            HeldItem(0x06).GrantParameter != 0x01 ||
            HeldItem(0x07).GrantObject != "TREASURE_OBJECT_SHOVEL_01" ||
            HeldItem(0x07).GrantSubId != 0x01 ||
            HeldItem(0x07).GrantParameter != 0x00 ||
            HeldItem(0x0a).Treasure != TreasureDatabase.TreasureSeedSatchel ||
            HeldItem(0x0a).ItemAnimation.Length == 0 ||
            !Text(0x0a01).Contains("Stink Bag", StringComparison.Ordinal) ||
            Text(0x0a13).Contains("\\jump", StringComparison.Ordinal) ||
            !Text(0x1c10).Contains("shovel", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "INTERAC_TOKAY generated data does not match the traced source contract.");
        }
    }
}

internal readonly record struct TokayHeldItemRecord(
    int SubId, int Treasure, int ItemGraphic, string GrantObject,
    int GrantSubId, int GrantParameter, string ItemSprite, int ItemTileBase,
    int ItemPalette, string ItemAnimation);
