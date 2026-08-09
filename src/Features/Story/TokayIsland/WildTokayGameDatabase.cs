using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Source-derived Wild Tokay manager, participant, prize, terrain, and
/// held-accessory contracts used by the minigame event.
/// </summary>
internal sealed class WildTokayGameDatabase
{
    private readonly Dictionary<string, int> _constants =
        new(StringComparer.Ordinal);
    private readonly Dictionary<int, WildTokayPrizeRecord> _prizes = new();
    private readonly List<WildTokayStartTileRecord> _startTiles = new();
    private readonly Dictionary<int, WildTokayMeatAccessoryRecord>
        _meatAccessories = new();
    private readonly Dictionary<(int Level, int RandomIndex), WildTokayPatternRecord>
        _patterns = new();

    internal int PastGameGroup => Constant("group-past-manager");
    internal int PastGameRoom => Constant("room-past-manager");
    internal int PresentGameGroup => Constant("group-present-manager");
    internal int PresentGameRoom => Constant("room-present-manager");
    internal int WildLevelAddress => Constant("wild-level-address");
    internal int FinishedGameFlag => Constant("global-finished-game");
    internal int BeganSecretFlag => Constant("global-began-secret");
    internal int DoneSecretFlag => Constant("global-done-secret");
    internal int SoundError => Constant("sound-error");
    internal int SoundSuccess => Constant("sound-success");
    internal int ParticipantLeftX => Constant("participant-left-x");
    internal int ParticipantRightX => Constant("participant-right-x");
    internal int ParticipantStartY => Constant("participant-start-y");
    internal int ParticipantAnimation => Constant("participant-animation");
    internal int GameLinkY => Constant("game-link-y");
    internal int GameLinkX => Constant("game-link-x");
    internal int GameReturnPosition => Constant("game-return-position");
    internal int WildCycleCount(int level) =>
        Constant($"wild-cycle-count-level-{level}");
    internal int GameSpawnDelay => Constant("game-spawn-delay");
    internal int GameStartDelay => Constant("game-start-delay");
    internal int GameFadeInDelay => Constant("game-fade-in-delay");
    internal int SoundWhistle => Constant("sound-whistle");
    internal int SoundOpenChest => Constant("sound-open-chest");
    internal IReadOnlyList<WildTokayStartTileRecord> StartTiles => _startTiles;

    internal WildTokayGameDatabase()
    {
        LoadConstants();
        LoadPrizes();
        LoadStartTiles();
        LoadMeatAccessories();
        LoadPatterns();
        Validate();
    }

    internal WildTokayPrizeRecord Prize(int prizeCode) =>
        _prizes.TryGetValue(prizeCode, out WildTokayPrizeRecord record)
            ? record
            : throw new KeyNotFoundException(
                $"Wild Tokay prize code ${prizeCode:x2} was not imported.");

    internal WildTokayMeatAccessoryRecord MeatAccessory(int parameter) =>
        _meatAccessories.TryGetValue(
            parameter, out WildTokayMeatAccessoryRecord record)
            ? record
            : throw new KeyNotFoundException(
                $"Wild Tokay held-meat parameter ${parameter:x2} was not imported.");

    internal WildTokayPatternRecord Pattern(int level, int randomIndex) =>
        _patterns.TryGetValue((level, randomIndex), out WildTokayPatternRecord record)
            ? record
            : throw new KeyNotFoundException(
                $"Wild Tokay pattern level {level}, random ${randomIndex:x2} was not imported.");

    private int Constant(string key) =>
        _constants.TryGetValue(key, out int value)
            ? value
            : throw new KeyNotFoundException(
                $"Wild Tokay constant '{key}' was not imported.");

    private void LoadConstants()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/wild_tokay_constants.tsv",
            new GeneratedTableSchema(
                "Wild Tokay constants",
                GeneratedTableKeySemantics.Unique,
                ["key", "value"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            _constants.Add(row.RequiredString(0), row.Decimal(1));
        }
    }

    private void LoadPrizes()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/wild_tokay_prizes.tsv",
            new GeneratedTableSchema(
                "Wild Tokay prizes",
                GeneratedTableKeySemantics.Unique,
                [
                    "prize-code", "accessory-subid", "sprite", "tile-base",
                    "palette", "animation"
                ],
                ["prize-code"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            var record = new WildTokayPrizeRecord(
                row.UnsignedDecimal(0), row.HexByte(1), row.RequiredString(2),
                row.HexByte(3), row.HexByte(4), row.RequiredString(5));
            _prizes.Add(record.PrizeCode, record);
        }
    }

    private void LoadStartTiles()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/wild_tokay_start_tiles.tsv",
            new GeneratedTableSchema(
                "Wild Tokay start tiles",
                GeneratedTableKeySemantics.Ordered,
                ["order", "tile", "packed-position"],
                headerRequired: true));
        for (int index = 0; index < table.Rows.Count; index++)
        {
            GeneratedTableRow row = table.Rows[index];
            int order = row.UnsignedDecimal(0);
            if (order != index)
                throw row.Invalid(0, $"ordered index {index}");
            _startTiles.Add(new WildTokayStartTileRecord(
                order, row.HexByte(1), row.HexByte(2)));
        }
    }

    private void LoadMeatAccessories()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/wild_tokay_meat_accessory.tsv",
            new GeneratedTableSchema(
                "Wild Tokay held-meat accessory",
                GeneratedTableKeySemantics.Unique,
                [
                    "parameter", "y-offset", "x-offset", "visible",
                    "animation", "sprite", "tile-base", "palette", "encoded"
                ],
                ["parameter"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            var record = new WildTokayMeatAccessoryRecord(
                row.UnsignedDecimal(0), row.Decimal(1), row.Decimal(2),
                row.HexByte(3), row.HexByte(4), row.RequiredString(5),
                row.HexByte(6), row.HexByte(7), row.RequiredString(8));
            _meatAccessories.Add(record.Parameter, record);
        }
    }

    private void LoadPatterns()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/wild_tokay_patterns.tsv",
            new GeneratedTableSchema(
                "Wild Tokay patterns",
                GeneratedTableKeySemantics.Unique,
                ["level", "random-index", "pattern", "left-count", "right-count"],
                ["level", "random-index"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int[] left = Pair(row.RequiredString(3), row, "left-count");
            int[] right = Pair(row.RequiredString(4), row, "right-count");
            _patterns.Add(
                (row.UnsignedDecimal(0), row.UnsignedDecimal(1)),
                new WildTokayPatternRecord(
                    row.UnsignedDecimal(2), left[0], left[1], right[0], right[1]));
        }
    }

    private static int[] Pair(string encoded, GeneratedTableRow row, string field)
    {
        string[] values = encoded.Split(',');
        if (values.Length != 2 ||
            !int.TryParse(values[0], out int first) ||
            !int.TryParse(values[1], out int second))
        {
            throw new InvalidOperationException(
                $"{row.Path}:{row.LineNumber}: invalid {field} pair '{encoded}'.");
        }
        return [first, second];
    }

    private void Validate()
    {
        if (PastGameGroup != 2 || PastGameRoom != 0xde ||
            PresentGameGroup != 2 || PresentGameRoom != 0xe5 ||
            ParticipantAnimation != 0x02 || GameReturnPosition != 0x57 ||
            WildCycleCount(0) != 5 || WildCycleCount(1) != 5 ||
            WildCycleCount(2) != 5 || WildCycleCount(3) != 6 ||
            WildCycleCount(4) != 7 || GameStartDelay != 30 ||
            GameFadeInDelay != 10 || WildLevelAddress != 0xc6ea ||
            _prizes.Count != 6 || _patterns.Count != 80 ||
            _startTiles.Count != 6 || _meatAccessories.Count != 5 ||
            Prize(0).AccessorySubId != 0x3e ||
            Prize(4).AccessorySubId != 0x2d ||
            Prize(5).AccessorySubId != 0x0e ||
            _startTiles[0] != new WildTokayStartTileRecord(0, 0xef, 0x01) ||
            _startTiles[3] != new WildTokayStartTileRecord(3, 0xef, 0x78) ||
            _startTiles[5] != new WildTokayStartTileRecord(5, 0x7a, 0x75) ||
            MeatAccessory(0) is not { YOffset: 0, XOffset: -13 } ||
            MeatAccessory(3) is not { YOffset: -12, XOffset: -1 } ||
            MeatAccessory(4) is not { YOffset: -12, XOffset: 0 })
        {
            throw new InvalidOperationException(
                "Wild Tokay generated data does not match the traced source contract.");
        }
    }
}

internal readonly record struct WildTokayPrizeRecord(
    int PrizeCode, int AccessorySubId, string Sprite, int TileBase,
    int Palette, string Animation);
internal readonly record struct WildTokayStartTileRecord(
    int Order, int Tile, int PackedPosition);
internal readonly record struct WildTokayMeatAccessoryRecord(
    int Parameter, int YOffset, int XOffset, int Visible, int Animation,
    string Sprite, int TileBase, int Palette, string EncodedAnimation);
internal readonly record struct WildTokayPatternRecord(
    int Pattern, int LeftBlue, int LeftRed, int RightBlue, int RightRed);
