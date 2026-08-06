using Godot;
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
                    "kooloo-gravity", "kooloo-sparkle-interaction",
                    "kooloo-sparkle-subid", "kooloo-sparkle-angle",
                    "kooloo-sparkle-offsets", "kooloo-sparkle-sprite",
                    "kooloo-sparkle-tile-base", "kooloo-sparkle-palette",
                    "kooloo-sparkle-animation", "post-chart-wait",
                    "upgrade-glow-wait",
                    "seed-threshold", "met-flag", "upgrade-flag",
                    "began-secret-flag", "done-secret-flag",
                    "island-chart-treasure", "island-chart-object",
                    "satchel-treasure", "satchel-upgrade-object",
                    "balloon-tile-base", "balloon-palette",
                    "explosion-sprite", "explosion-tile-base",
                    "explosion-palette", "explosion-y-offset",
                    "explosion-x-offset",
                    "balloon-active-collisions", "source"
                ],
                headerRequired: true)).SingleRow();
        Record = new TingleRecord(
            row.Decimal(0, 0, 7), row.HexByte(1), row.HexByte(2), row.HexByte(3),
            row.HexByte(4), row.Decimal(5, -128, -1),
            row.UnsignedDecimal(6), row.Decimal(7, short.MinValue, -1),
            row.UnsignedDecimal(8), row.UnsignedDecimal(9),
            row.Decimal(10, short.MinValue, -1), row.UnsignedDecimal(11),
            row.HexByte(12), row.HexByte(13), row.HexByte(14),
            ParseOffsets(row, 15), row.RequiredString(16),
            row.UnsignedDecimal(17), row.UnsignedDecimal(18),
            row.UnsignedDecimal(19),
            row.UnsignedDecimal(20), row.UnsignedDecimal(21),
            row.UnsignedDecimal(22), row.HexByte(23), row.HexByte(24),
            row.HexByte(25), row.HexByte(26), row.HexByte(27),
            row.RequiredString(28), row.HexByte(29), row.RequiredString(30),
            row.UnsignedDecimal(31), row.UnsignedDecimal(32),
            row.RequiredString(33), row.UnsignedDecimal(34),
            row.UnsignedDecimal(35), row.Decimal(36, -128, 127),
            row.Decimal(37, -128, 127), row.RequiredString(38),
            row.RequiredString(39));

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
                KoolooSparkleInteraction: 0x84, KoolooSparkleSubId: 0x00,
                KoolooSparkleAngle: 0x10,
                KoolooSparkleSprite:
                    "spr_triforce_sparkle_vineseed_bookofseals",
                KoolooSparkleTileBase: 0x0a, KoolooSparklePalette: 0,
                KoolooSparkleAnimation: 1,
                PostChartWait: 60, UpgradeGlowWait: 120,
                SeedThreshold: 3, MetFlag: 0x1b, UpgradeFlag: 0x46,
                BeganSecretFlag: 0x6b, DoneSecretFlag: 0x75,
                IslandChartTreasure: 0x54, SatchelTreasure: 0x19,
                BalloonTileBase: 24, BalloonPalette: 2,
                ExplosionSprite: "spr_common_sprites",
                ExplosionTileBase: 0x0c, ExplosionPalette: 2,
                ExplosionYOffset: -16, ExplosionXOffset: 0,
                BalloonActiveCollisions: "00001111111101100001100100000000"
            } || Record.KoolooSparkleOffsets is not
            [
                { X: 0, Y: -24 },
                { X: 8, Y: -16 },
                { X: -8, Y: -16 }
            ] || _animations.Count != 7 || _texts.Count != 17)
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

    internal TingleBalloonExplosionVisual ExplosionVisual => new(
        Record.ExplosionSprite,
        Record.ExplosionTileBase,
        Record.ExplosionPalette,
        Animation("explosion", 0));

    internal TingleKoolooSparkleVisual KoolooSparkleVisual => new(
        Record.KoolooSparkleSprite,
        Record.KoolooSparkleTileBase,
        Record.KoolooSparklePalette,
        Animation("sparkle", Record.KoolooSparkleAnimation));

    private static Vector2[] ParseOffsets(GeneratedTableRow row, int column)
    {
        string[] values = row.RequiredString(column).Split(
            ';',
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);
        var offsets = new Vector2[values.Length];
        for (int index = 0; index < values.Length; index++)
        {
            string[] pair = values[index].Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);
            if (pair.Length != 2 ||
                !int.TryParse(pair[0], out int x) ||
                !int.TryParse(pair[1], out int y))
            {
                throw row.Invalid(column, "semicolon-separated x,y pairs");
            }
            offsets[index] = new Vector2(x, y);
        }
        return offsets;
    }
}

internal readonly record struct TingleBalloonExplosionVisual(
    string Sprite,
    int TileBase,
    int Palette,
    string Animation);

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
    int KoolooSparkleInteraction,
    int KoolooSparkleSubId,
    int KoolooSparkleAngle,
    Vector2[] KoolooSparkleOffsets,
    string KoolooSparkleSprite,
    int KoolooSparkleTileBase,
    int KoolooSparklePalette,
    int KoolooSparkleAnimation,
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
    string ExplosionSprite,
    int ExplosionTileBase,
    int ExplosionPalette,
    int ExplosionYOffset,
    int ExplosionXOffset,
    string BalloonActiveCollisions,
    string Source)
{
    internal bool BalloonAcceptsItemCollision(int sourceCollision)
    {
        if ((uint)sourceCollision >= BalloonActiveCollisions.Length ||
            BalloonActiveCollisions.Length != 0x20)
        {
            throw new InvalidOperationException(
                $"PART_TINGLE_BALLOON $44 cannot resolve item collision " +
                $"${sourceCollision:x2} from imported active-collision bits.");
        }
        char active = BalloonActiveCollisions[sourceCollision];
        return active switch
        {
            '0' => false,
            '1' => true,
            _ => throw new InvalidOperationException(
                "PART_TINGLE_BALLOON $44 imported a non-binary " +
                "active-collision bit.")
        };
    }
}
