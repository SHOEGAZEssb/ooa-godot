using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Imported room, script, flag, ring, text, and animation metadata for Vasu
/// Jewelers in past room $2:$ee.
/// </summary>
internal sealed class VasuShopDatabase
{
    private readonly Dictionary<string, int> _constants = new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> _texts = new();
    private readonly Dictionary<(int InteractionId, int Animation), string> _animations = new();

    public int Group => Constant("group");
    public int Room => Constant("room");
    public int TextboxPosition => Constant("textbox-position");
    public int SnakeProximityRadius => Constant("snake-proximity-radius");
    public int RedSnakeWait => Constant("red-snake-wait");
    public int BlueSnakeCableTimeout => Constant("blue-snake-cable-timeout");
    public int VasuRadiusY => Constant("vasu-radius-y");
    public int VasuRadiusX => Constant("vasu-radius-x");
    public int SnakeRadius => Constant("snake-radius");
    public int AButtonPointOffset => Constant("a-button-point-offset");
    public int RingBoxGrabMode => Constant("ring-box-grab-mode");
    public int RingGrabMode => Constant("ring-grab-mode");
    public int ObtainedRingBoxAddress => Constant("obtained-ring-box-address");
    public int RingsObtainedAddress => Constant("rings-obtained-address");
    public int RingsObtainedByteCount => Constant("rings-obtained-byte-count");
    public int RingsAppraisedAddress => Constant("rings-appraised-address");
    public int LinkedFirstMask => Constant("linked-first-mask");
    public int AppraisalCost => Constant("appraisal-cost");
    public int DuplicateRefund => Constant("duplicate-refund");
    public int MenuCloseWait => Constant("menu-close-wait");
    public int AppraisalResultWait => Constant("appraisal-result-wait");
    public int MenuExitWait => Constant("menu-exit-wait");
    public int GlobalEarnedSlayer => Constant("global-earned-slayer");
    public int GlobalEarnedWealth => Constant("global-earned-wealth");
    public int GlobalEarnedVictory => Constant("global-earned-victory");
    public int GlobalGotSlayer => Constant("global-got-slayer");
    public int GlobalGotWealth => Constant("global-got-wealth");
    public int GlobalGotVictory => Constant("global-got-victory");
    public int GlobalObtainedRingBox => Constant("global-obtained-ring-box");
    public int GlobalAppraisedHundredth => Constant("global-appraised-hundredth");
    public int RingFriendship => Constant("ring-friendship");
    public int RingSlayer => Constant("ring-slayer");
    public int RingWealth => Constant("ring-wealth");
    public int RingVictory => Constant("ring-victory");
    public int RingHundredth => Constant("ring-hundredth");

    public VasuShopDatabase()
    {
        LoadConstants();
        LoadTexts();
        LoadAnimations();
        Validate();
    }

    public string Text(int textId) => _texts.TryGetValue(textId, out string? message)
        ? message
        : throw new KeyNotFoundException(
            $"Vasu Jewelers text TX_{textId:x4} was not imported.");

    public string Animation(int interactionId, int animation) =>
        _animations.TryGetValue((interactionId, animation), out string? encoded)
            ? encoded
            : throw new KeyNotFoundException(
                $"Vasu Jewelers animation ${interactionId:x2}:${animation:x2} was not imported.");

    private int Constant(string key) => _constants.TryGetValue(key, out int value)
        ? value
        : throw new KeyNotFoundException(
            $"Vasu Jewelers constant '{key}' was not imported.");

    private void LoadConstants()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/vasu_shop_constants.tsv",
            new GeneratedTableSchema(
                "Vasu Jewelers constants",
                GeneratedTableKeySemantics.Unique,
                ["key", "value"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            _constants.Add(row.RequiredString(0), row.Decimal(1));
        }
    }

    private void LoadTexts()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/vasu_shop_texts.tsv",
            new GeneratedTableSchema(
                "Vasu Jewelers text",
                GeneratedTableKeySemantics.Unique,
                ["text-id", "utf8-base64"],
                ["text-id"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int textId = row.HexWord(0);
            string message = row.Base64Utf8(1);
            if (string.IsNullOrWhiteSpace(message))
                throw row.Invalid(1, "nonempty base64-encoded UTF-8 text");
            _texts.Add(textId, message);
        }
    }

    private void LoadAnimations()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/vasu_shop_animations.tsv",
            new GeneratedTableSchema(
                "Vasu Jewelers animations",
                GeneratedTableKeySemantics.Unique,
                ["interaction-id", "animation", "encoded-animation"],
                ["interaction-id", "animation"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int interactionId = row.HexByte(0);
            int animation = row.HexByte(1);
            string encoded = row.RequiredString(2);
            if (OracleGraphicsCache.GetAnimationDefinition(encoded).Frames.Length == 0)
            {
                throw row.Invalid(2, "a nonempty encoded animation");
            }
            _animations.Add((interactionId, animation), encoded);
        }
    }

    private void Validate()
    {
        if (_constants.Count != 35 || _texts.Count != 44 || _animations.Count != 11 ||
            Group != 2 || Room != 0xee || TextboxPosition != 2 ||
            SnakeProximityRadius != 0x18 || RedSnakeWait != 30 ||
            BlueSnakeCableTimeout != 0x200 || VasuRadiusY != 0x12 ||
            VasuRadiusX != 6 || SnakeRadius != 6 || AButtonPointOffset != 10 ||
            RingBoxGrabMode != 2 || RingGrabMode != 1 ||
            ObtainedRingBoxAddress != 0xc615 || RingsObtainedAddress != 0xc616 ||
            RingsObtainedByteCount != 8 || RingsAppraisedAddress != 0xc6ce ||
            LinkedFirstMask != 1 || AppraisalCost != 20 || DuplicateRefund != 30 ||
            MenuCloseWait != 10 || AppraisalResultWait != 40 || MenuExitWait != 60 ||
            GlobalEarnedSlayer != 0 || GlobalEarnedWealth != 1 ||
            GlobalEarnedVictory != 2 || GlobalGotSlayer != 4 ||
            GlobalGotWealth != 5 || GlobalGotVictory != 6 ||
            GlobalObtainedRingBox != 8 || GlobalAppraisedHundredth != 9 ||
            RingFriendship != 0 || RingSlayer != 0x34 ||
            RingWealth != 0x35 || RingVictory != 0x36 || RingHundredth != 0x38 ||
            !_texts[0x3000].Contains("Is this\nyour first time?", StringComparison.Ordinal) ||
            !_texts[0x3000].Contains("Understood?", StringComparison.Ordinal) ||
            _texts[0x3000].Contains("\\jump", StringComparison.Ordinal) ||
            _texts[0x3036].Contains("\\call", StringComparison.Ordinal) ||
            _texts[0x300b].Contains("\\jump", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Vasu Jewelers data does not match the imported room $2:$ee contract.");
        }
    }

}
