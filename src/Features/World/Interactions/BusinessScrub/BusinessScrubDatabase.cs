using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Imported INTERAC_BUSINESS_SCRUB $ce:$03 shield-sale data used by past
/// room 1:81.
/// </summary>
internal sealed class BusinessScrubDatabase
{
    private readonly Dictionary<string, int> _constants =
        new(StringComparer.Ordinal);
    private readonly Dictionary<int, BusinessScrubOffer> _offers = new();
    private readonly Dictionary<int, string> _animations = new();
    private readonly Dictionary<int, string> _texts = new();

    public int Group => Constant("group");
    public int Room => Constant("room");
    public int InteractionId => Constant("interaction-id");
    public int PlacedSubId => Constant("placed-subid");
    public int CollisionRadius => Constant("collision-radius");
    public int ProximityRadius => Constant("proximity-radius");
    public int AButtonPointOffset => Constant("a-button-point-offset");
    public int FloorTile => Constant("floor-tile");
    public int FloorCollision => Constant("floor-collision");
    public int BushTile => Constant("bush-tile");
    public bool SourceGrayscaleInverted =>
        Constant("source-grayscale-inverted") != 0;
    public int PromptText => Constant("prompt-text");
    public int SuccessText => Constant("success-text");
    public int DeclineText => Constant("decline-text");
    public int InsufficientText => Constant("insufficient-text");
    public int AlreadyOwnedText => Constant("already-owned-text");

    public BusinessScrubDatabase()
    {
        LoadConstants();
        LoadOffers();
        LoadAnimations();
        LoadTexts();
        Validate();
    }

    public bool Matches(NpcRecord npc) =>
        npc.Group == Group &&
        npc.Room == Room &&
        npc.Id == InteractionId &&
        npc.SubId == PlacedSubId;

    public BusinessScrubOffer OfferForShieldLevel(int shieldLevel) =>
        _offers.TryGetValue(shieldLevel, out BusinessScrubOffer offer)
            ? offer
            : throw new InvalidOperationException(
                $"Business Scrub cannot price shield level {shieldLevel}.");

    public string Animation(int animation) =>
        _animations.TryGetValue(animation, out string? encoded)
            ? encoded
            : throw new KeyNotFoundException(
                $"Business Scrub animation ${animation:x2} was not imported.");

    public string Text(int textId) =>
        _texts.TryGetValue(textId, out string? message)
            ? message
            : throw new KeyNotFoundException(
                $"Business Scrub text TX_{textId:x4} was not imported.");

    public int BushOffsetForParameter(int parameter) => parameter switch
    {
        0 => Constant("bush-normal-offset"),
        1 => Constant("bush-near-offset"),
        2 => Constant("bush-talk-offset"),
        _ => throw new InvalidOperationException(
            $"Business Scrub animation parameter ${parameter:x2} has no bush offset.")
    };

    private int Constant(string key) =>
        _constants.TryGetValue(key, out int value)
            ? value
            : throw new KeyNotFoundException(
                $"Business Scrub constant '{key}' was not imported.");

    private void LoadConstants()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/business_scrub_constants.tsv",
            new GeneratedTableSchema(
                "Business Scrub constants",
                GeneratedTableKeySemantics.Unique,
                ["key", "value"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
            _constants.Add(row.RequiredString(0), row.Decimal(1));
    }

    private void LoadOffers()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/business_scrub.tsv",
            new GeneratedTableSchema(
                "Business Scrub offers",
                GeneratedTableKeySemantics.Unique,
                [
                    "shield-level", "effective-subid", "price", "treasure",
                    "parameter", "source"
                ],
                ["shield-level"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int shieldLevel = row.Decimal(0, 0, 3);
            _offers.Add(
                shieldLevel,
                new BusinessScrubOffer(
                    shieldLevel,
                    row.HexByte(1),
                    row.UnsignedDecimal(2),
                    row.HexByte(3),
                    row.HexByte(4),
                    row.RequiredString(5)));
        }
    }

    private void LoadAnimations()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/business_scrub_animations.tsv",
            new GeneratedTableSchema(
                "Business Scrub animations",
                GeneratedTableKeySemantics.Unique,
                ["animation", "encoded-animation"],
                ["animation"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
            _animations.Add(row.HexByte(0), row.RequiredString(1));
    }

    private void LoadTexts()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/business_scrub_texts.tsv",
            new GeneratedTableSchema(
                "Business Scrub texts",
                GeneratedTableKeySemantics.Unique,
                ["text-id", "utf8-base64"],
                ["text-id"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int textId = row.HexWord(0);
            string message = row.Base64Utf8(1);
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new InvalidOperationException(
                    $"Invalid Business Scrub text TX_{textId:x4}.");
            }
            _texts.Add(textId, message);
        }
    }

    private void Validate()
    {
        if (Group != 1 || Room != 0x81 || InteractionId != 0xce ||
            PlacedSubId != 0x03 || CollisionRadius != 0x06 ||
            ProximityRadius != 0x20 || AButtonPointOffset != 10 ||
            FloorTile != 0x00 || FloorCollision != 0x0f ||
            BushTile != 0xc5 || SourceGrayscaleInverted ||
            BushOffsetForParameter(0) != 0 ||
            BushOffsetForParameter(1) != -8 ||
            BushOffsetForParameter(2) != -11 ||
            PromptText != 0x4509 || SuccessText != 0x4505 ||
            DeclineText != 0x4506 || InsufficientText != 0x4507 ||
            AlreadyOwnedText != 0x4508 ||
            _constants.Count != 19 || _offers.Count != 4 ||
            _animations.Count != 5 || _texts.Count != 5 ||
            OfferForShieldLevel(0) is not
                { EffectiveSubId: 0x03, Price: 30, Treasure: 0x01, Parameter: 0x01 } ||
            OfferForShieldLevel(1) is not
                { EffectiveSubId: 0x03, Price: 30, Treasure: 0x01, Parameter: 0x01 } ||
            OfferForShieldLevel(2) is not
                { EffectiveSubId: 0x04, Price: 50, Treasure: 0x01, Parameter: 0x02 } ||
            OfferForShieldLevel(3) is not
                { EffectiveSubId: 0x05, Price: 80, Treasure: 0x01, Parameter: 0x03 } ||
            Animation(0) != "127@" ||
            !Text(PromptText).Contains("\\num1", StringComparison.Ordinal) ||
            !Text(PromptText).Contains("\\opt()OK", StringComparison.Ordinal) ||
            !Text(PromptText).Contains("No thanks", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Business Scrub data does not match room $1:$81's imported contract.");
        }
    }
}

internal readonly record struct BusinessScrubOffer(
    int ShieldLevel,
    int EffectiveSubId,
    int Price,
    int Treasure,
    int Parameter,
    string Source);
