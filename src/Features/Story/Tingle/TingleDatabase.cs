using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class TingleDatabase
{
    private readonly Dictionary<(string Owner, int Animation), string> _animations = new();
    private readonly Dictionary<int, string> _texts = new();

    internal TingleRecord Record { get; }

    internal TingleDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            "res://assets/oracle/objects/tingle.tsv",
            new GeneratedTableSchema(
                "Tingle interaction",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "id", "subid", "balloon-part",
                    "initial-z", "balloon-counter", "balloon-speed-z",
                    "fall-wait", "fall-gravity", "kooloo-speed-z",
                    "kooloo-gravity", "post-chart-wait", "upgrade-glow-wait",
                    "seed-threshold", "met-flag", "upgrade-flag",
                    "began-secret-flag", "done-secret-flag",
                    "island-chart-treasure", "island-chart-object",
                    "satchel-treasure", "satchel-upgrade-object",
                    "balloon-tile-base", "balloon-palette", "source"
                ],
                headerRequired: true)).SingleRow();
        Record = new TingleRecord(
            row.Decimal(0, 0, 7), row.HexByte(1), row.HexByte(2), row.HexByte(3),
            row.HexByte(4), row.Decimal(5, -128, -1),
            row.UnsignedDecimal(6), row.Decimal(7, short.MinValue, -1),
            row.UnsignedDecimal(8), row.UnsignedDecimal(9),
            row.Decimal(10, short.MinValue, -1), row.UnsignedDecimal(11),
            row.UnsignedDecimal(12), row.UnsignedDecimal(13),
            row.UnsignedDecimal(14), row.HexByte(15), row.HexByte(16),
            row.HexByte(17), row.HexByte(18), row.HexByte(19),
            row.RequiredString(20), row.HexByte(21), row.RequiredString(22),
            row.UnsignedDecimal(23), row.UnsignedDecimal(24),
            row.RequiredString(25));

        GeneratedTable animations = GeneratedTable.Load(
            "res://assets/oracle/objects/tingle_animations.tsv",
            new GeneratedTableSchema(
                "Tingle animations",
                GeneratedTableKeySemantics.Unique,
                ["owner", "animation", "encoded", "source"],
                ["owner", "animation"],
                headerRequired: true));
        foreach (GeneratedTableRow animation in animations.Rows)
        {
            string owner = animation.RequiredString(0);
            int index = animation.UnsignedDecimal(1);
            string encoded = animation.RequiredString(2);
            if (!_animations.TryAdd((owner, index), encoded))
                throw animation.Invalid(1, "unique owner/animation pair");
            _ = OracleGraphicsCache.GetAnimationDefinition(encoded);
        }

        GeneratedTable texts = GeneratedTable.Load(
            "res://assets/oracle/objects/tingle_texts.tsv",
            new GeneratedTableSchema(
                "Tingle text",
                GeneratedTableKeySemantics.Unique,
                ["text-id", "utf8-base64", "source"],
                ["text-id"],
                headerRequired: true));
        foreach (GeneratedTableRow text in texts.Rows)
        {
            int textId = text.HexWord(0);
            if (!_texts.TryAdd(textId, text.Base64Utf8(1)))
                throw text.Invalid(0, "unique text ID");
        }

        if (Record is not
            {
                Group: 0, Room: 0x79, InteractionId: 0xc8, SubId: 0x00,
                BalloonPart: 0x44, InitialZ: -15, BalloonCounter: 56,
                BalloonSpeedZ: -16, FallWait: 15, FallGravity: 16,
                KoolooSpeedZ: -512, KoolooGravity: 32,
                PostChartWait: 60, UpgradeGlowWait: 120,
                SeedThreshold: 3, MetFlag: 0x1b, UpgradeFlag: 0x46,
                BeganSecretFlag: 0x6b, DoneSecretFlag: 0x75,
                IslandChartTreasure: 0x54, SatchelTreasure: 0x19,
                BalloonTileBase: 24, BalloonPalette: 2
            } || _animations.Count != 5 || _texts.Count != 17)
        {
            throw new InvalidOperationException(
                "Imported room 0:79 INTERAC_TINGLE `$c8:$00 contract is incomplete.");
        }
    }

    internal bool Matches(NpcRecord record) =>
        record.Group == Record.Group && record.Room == Record.Room &&
        record.Id == Record.InteractionId && record.SubId == Record.SubId &&
        record.Var03 == 0;

    internal string Animation(string owner, int animation) =>
        _animations.TryGetValue((owner, animation), out string? encoded)
            ? encoded
            : throw new InvalidOperationException(
                $"Missing Tingle {owner} animation ${animation:x2}.");

    internal string Text(int textId) =>
        _texts.TryGetValue(textId, out string? text)
            ? text
            : throw new InvalidOperationException(
                $"Missing Tingle TX_{textId:x4}.");
}

internal readonly record struct TingleRecord(
    int Group,
    int Room,
    int InteractionId,
    int SubId,
    int BalloonPart,
    int InitialZ,
    int BalloonCounter,
    int BalloonSpeedZ,
    int FallWait,
    int FallGravity,
    int KoolooSpeedZ,
    int KoolooGravity,
    int PostChartWait,
    int UpgradeGlowWait,
    int SeedThreshold,
    int MetFlag,
    int UpgradeFlag,
    int BeganSecretFlag,
    int DoneSecretFlag,
    int IslandChartTreasure,
    string IslandChartObject,
    int SatchelTreasure,
    string SatchelUpgradeObject,
    int BalloonTileBase,
    int BalloonPalette,
    string Source);
