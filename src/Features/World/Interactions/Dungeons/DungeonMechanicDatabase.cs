using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Imported placements and common constants for PART_SWITCH $05,
/// PART_BUTTON $09, the buttons' $20:$00/$21:$17 trigger-chest consumers,
/// INTERAC_DUNGEON_STUFF $12:$02, INTERAC_PUSHBLOCK_TRIGGER $13:$01, and
/// shutter-door controller variants $1e:$04-$0b. Moonlit Grotto's
/// INTERAC_DUNGEON_EVENTS $21:$09/$0a/$0d/$0e, PART_ORB $03, and
/// PART_GROTTO_CRYSTAL $24 share this
/// source-ordered dispatch because their switch state and reward are common
/// dungeon mechanics rather than room-authored exceptions.
/// </summary>
internal sealed class DungeonMechanicDatabase
{

    private readonly Lookup<int, DungeonMechanicDatabaseRecord> _recordsByRoom =
        new();
    private readonly Lookup<(int Id, int SubId), DungeonTilePatternRecord>
        _tilePatterns = new();
    private readonly Dictionary<string, int> _constants = new();
    private readonly Dictionary<int, string> _texts = new();

    internal int RecordCount { get; }
    internal int PushableBlock => Constant("pushable-block");
    internal int PushDelay => Constant("push-delay");
    internal int SolveWait => Constant("solve-wait");
    internal int DoorFrameWait => Constant("door-frame-wait");
    internal int OpenTile => Constant("open-tile");
    internal int SolveSound => Constant("solve-sound");
    internal int DoorSound => Constant("door-sound");
    internal int ButtonTile => Constant("button-tile");
    internal int PressedButtonTile => Constant("pressed-button-tile");
    internal int ButtonRadiusY => Constant("button-radius-y");
    internal int ButtonRadiusX => Constant("button-radius-x");
    internal int ButtonObjectReleaseDelay => Constant("button-object-release-delay");
    internal int ButtonSound => Constant("button-sound");
    internal int SwitchOffTile => Constant("switch-off-tile");
    internal int SwitchOnTile => Constant("switch-on-tile");
    internal int SwitchRadiusY => Constant("switch-radius-y");
    internal int SwitchRadiusX => Constant("switch-radius-x");
    internal int SwitchCollisionZ => Constant("switch-collision-z");
    internal int SwitchHitLockout => Constant("switch-hit-lockout");
    internal int SwitchSound => Constant("switch-sound");
    internal int ChestTile => Constant("chest-tile");
    internal int ChestWait => Constant("chest-wait");
    internal int PuffSound => Constant("puff-sound");
    internal int MoonlitGlobalFlag => Constant("moonlit-global-flag");
    internal int MoonlitAllCrystalsMask => Constant("moonlit-all-crystals-mask");
    internal int MoonlitRoomFlag => Constant("moonlit-room-flag");
    internal int MoonlitCrystalCollision => Constant("moonlit-crystal-collision");
    internal int MoonlitCrystalRadiusY => Constant("moonlit-crystal-radius-y");
    internal int MoonlitCrystalRadiusX => Constant("moonlit-crystal-radius-x");
    internal int MoonlitOrbPosition => Constant("moonlit-orb-position");
    internal int MoonlitOrbMask => Constant("moonlit-orb-mask");
    internal int MoonlitOrbCollision => Constant("moonlit-orb-collision");
    internal int MoonlitOrbRadiusY => Constant("moonlit-orb-radius-y");
    internal int MoonlitOrbRadiusX => Constant("moonlit-orb-radius-x");
    internal int MoonlitArmosChestPosition =>
        Constant("moonlit-armos-chest-position");
    internal int MoonlitArmosSourceTile =>
        Constant("moonlit-armos-source-tile");
    internal int MoonlitArmosReplacementTile =>
        Constant("moonlit-armos-replacement-tile");
    internal int MoonlitKeyGoalPosition => Constant("moonlit-key-goal-position");
    internal int MoonlitKeyGoalTile => Constant("moonlit-key-goal-tile");
    internal int MoonlitFirstWait => Constant("moonlit-first-wait");
    internal int MoonlitRumbleWait => Constant("moonlit-rumble-wait");
    internal int MoonlitAllWait => Constant("moonlit-all-wait");
    internal int MoonlitExplosionWait => Constant("moonlit-explosion-wait");
    internal int MoonlitSolveWait => Constant("moonlit-solve-wait");
    internal int MoonlitRumbleSound => Constant("moonlit-rumble-sound");
    internal int MoonlitBigExplosionSound => Constant("moonlit-big-explosion-sound");
    internal int MoonlitSolveSound => Constant("moonlit-solve-sound");
    internal int MoonlitBreakSound => Constant("moonlit-break-sound");
    internal int MoonlitBreakSoundDelay => Constant("moonlit-break-sound-delay");

    public DungeonMechanicDatabase()
    {
        int count = 0;
        GeneratedTable mechanics = GeneratedTable.Load(
            "res://assets/oracle/objects/dungeon_mechanics.tsv",
            new GeneratedTableSchema(
                "dungeon mechanics",
                GeneratedTableKeySemantics.Grouped,
                [
                    "group", "room", "order", "id", "subid", "position", "parameter",
                    "trigger-predicate", "count-source-complete"
                ],
                ["group", "room"],
                headerRequired: true));
        foreach (GeneratedTableRow row in mechanics.Rows)
        {
            DungeonMechanicDatabaseRecord record = new DungeonMechanicDatabaseRecord(
                row.Decimal(0, 0, 7),
                row.HexByte(1),
                row.UnsignedDecimal(2),
                row.HexByte(3),
                row.HexByte(4),
                row.HexByte(5),
                row.HexByte(6),
                row.RequiredString(7) switch
                {
                    "none" => TriggerPredicate.None,
                    "bit" => TriggerPredicate.BitSet,
                    "exact" => TriggerPredicate.Exact,
                    _ => throw row.Invalid(7, "one of none, bit, exact")
                },
                row.Boolean01(8));
            if (record.Id is not (0x05 or 0x09 or 0x12 or 0x13 or 0x1e or 0x20 or 0x21 or 0x24) ||
                record.Id == 0x12 && record.SubId != 0x02 ||
                record.Id == 0x20 && record.SubId != 0x00 ||
                record.Id == 0x21 && record.SubId is not (0x09 or 0x0a or 0x0d or 0x0e or 0x17) ||
                record.Id == 0x24 && record.SubId is not (0x10 or 0x20 or 0x40 or 0x80))
                throw row.Invalid(3, "a supported dungeon mechanic interaction id");
            List<DungeonMechanicDatabaseRecord> records =
                _recordsByRoom.GetOrAdd(
                    MakeKey(record.Group, record.Room));
            if (records.Count > 0 && records[^1].Order >= record.Order)
            {
                throw new InvalidOperationException(
                    $"Room {record.Group:x1}:{record.Room:x2} dungeon interaction order " +
                    $"did not increase at source object {record.Order}.");
            }
            records.Add(record);
            count++;
        }
        RecordCount = count;

        GeneratedTable tilePatterns = GeneratedTable.Load(
            "res://assets/oracle/objects/dungeon_event_tile_patterns.tsv",
            new GeneratedTableSchema(
                "dungeon event tile patterns",
                GeneratedTableKeySemantics.Unique,
                ["id", "subid", "order", "tile", "position", "source"],
                ["id", "subid", "order"],
                headerRequired: true));
        foreach (GeneratedTableRow row in tilePatterns.Rows)
        {
            DungeonTilePatternRecord record = new(
                row.HexByte(0),
                row.HexByte(1),
                row.UnsignedDecimal(2),
                row.HexByte(3),
                row.HexByte(4),
                row.RequiredString(5));
            List<DungeonTilePatternRecord> pattern =
                _tilePatterns.GetOrAdd((record.Id, record.SubId));
            if (record.Order != pattern.Count)
            {
                throw new InvalidOperationException(
                    $"{record.Source}: ${record.Id:x2}:${record.SubId:x2} tile " +
                    $"pattern order {record.Order} was not contiguous.");
            }
            pattern.Add(record);
        }

        GeneratedTable constants = GeneratedTable.Load(
            "res://assets/oracle/objects/dungeon_mechanic_constants.tsv",
            new GeneratedTableSchema(
                "dungeon mechanic constants",
                GeneratedTableKeySemantics.Unique,
                ["key", "value"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in constants.Rows)
        {
            _constants.Add(row.RequiredString(0), row.Decimal(1));
        }

        GeneratedTable texts = GeneratedTable.Load(
            "res://assets/oracle/objects/dungeon_mechanic_text.tsv",
            new GeneratedTableSchema(
                "dungeon mechanic text",
                GeneratedTableKeySemantics.Unique,
                ["text-id", "message-base64"],
                ["text-id"],
                headerRequired: true));
        foreach (GeneratedTableRow row in texts.Rows)
            _texts.Add(row.HexWord(0), row.Base64Utf8(1));

        IReadOnlyList<DungeonMechanicDatabaseRecord> room0c = GetRoomRecords(4, 0x0c);
        IReadOnlyList<DungeonMechanicDatabaseRecord> room0b = GetRoomRecords(4, 0x0b);
        IReadOnlyList<DungeonMechanicDatabaseRecord> room08 = GetRoomRecords(4, 0x08);
        IReadOnlyList<DungeonMechanicDatabaseRecord> room09 = GetRoomRecords(4, 0x09);
        IReadOnlyList<DungeonMechanicDatabaseRecord> room22 = GetRoomRecords(4, 0x22);
        IReadOnlyList<DungeonMechanicDatabaseRecord> room2f = GetRoomRecords(4, 0x2f);
        IReadOnlyList<DungeonMechanicDatabaseRecord> room65 = GetRoomRecords(4, 0x65);
        IReadOnlyList<DungeonMechanicDatabaseRecord> room56 = GetRoomRecords(4, 0x56);
        IReadOnlyList<DungeonMechanicDatabaseRecord> room61 = GetRoomRecords(4, 0x61);
        IReadOnlyList<DungeonMechanicDatabaseRecord> room64 = GetRoomRecords(4, 0x64);
        IReadOnlyList<DungeonMechanicDatabaseRecord> room7a = GetRoomRecords(4, 0x7a);
        IReadOnlyList<DungeonTilePatternRecord> room64Pattern =
            TilePattern(0x21, 0x09);
        if (RecordCount != 185 || _constants.Count != 53 || _texts.Count != 2 ||
            room08.Count != 2 ||
            room08[0] != new DungeonMechanicDatabaseRecord(
                4, 0x08, 0, 0x20, 0x00, 0x57, 0x01,
                TriggerPredicate.Exact, true) ||
            room08[1] != new DungeonMechanicDatabaseRecord(
                4, 0x08, 1, 0x09, 0x00, 0x17, 0x00,
                TriggerPredicate.None, true) ||
            room09.Count != 4 ||
            room09[0] != new DungeonMechanicDatabaseRecord(
                4, 0x09, 0, 0x1e, 0x04, 0x07, 0x00,
                TriggerPredicate.BitSet, true) ||
            room09[1] != new DungeonMechanicDatabaseRecord(
                4, 0x09, 1, 0x1e, 0x05, 0x5e, 0x00,
                TriggerPredicate.BitSet, true) ||
            room09[2] != new DungeonMechanicDatabaseRecord(
                4, 0x09, 3, 0x13, 0x01, 0x2a, 0x00,
                TriggerPredicate.None, true) ||
            room09[3] != new DungeonMechanicDatabaseRecord(
                4, 0x09, 5, 0x09, 0x00, 0x14, 0x00,
                TriggerPredicate.None, true) ||
            room22.Count != 2 || room22[1] !=
                new DungeonMechanicDatabaseRecord(
                    4, 0x22, 1, 0x09, 0x80, 0x5b, 0x00,
                    TriggerPredicate.None, true) ||
            room2f.Count != 1 || room2f[0] !=
                new DungeonMechanicDatabaseRecord(
                    4, 0x2f, 5, 0x05, 0x02, 0x79, 0x00,
                    TriggerPredicate.None, true) ||
            room65.Count != 1 || room65[0] !=
                new DungeonMechanicDatabaseRecord(
                    4, 0x65, 0, 0x12, 0x02, 0x58, 0x00,
                    TriggerPredicate.None, true) ||
            room56.Count != 1 || room56[0] !=
                new DungeonMechanicDatabaseRecord(
                    4, 0x56, 0, 0x21, 0x0a, 0x00, 0x00,
                    TriggerPredicate.None, true) ||
            room61.Count != 3 ||
            room61[0] != new DungeonMechanicDatabaseRecord(
                4, 0x61, 0, 0x21, 0x0d, 0x00, 0x00,
                TriggerPredicate.None, true) ||
            room61[1] != new DungeonMechanicDatabaseRecord(
                4, 0x61, 1, 0x21, 0x0e, 0x58, 0xb8,
                TriggerPredicate.None, true) ||
            room61[2] != new DungeonMechanicDatabaseRecord(
                4, 0x61, 2, 0x24, 0x40, 0x57, 0x00,
                TriggerPredicate.None, true) ||
            room64.Count != 1 || room64[0] !=
                new DungeonMechanicDatabaseRecord(
                    4, 0x64, 0, 0x21, 0x09, 0x68, 0xb8,
                    TriggerPredicate.None, true) ||
            room64Pattern.Count != 3 ||
            room64Pattern[0] != new DungeonTilePatternRecord(
                0x21, 0x09, 0, 0x1d, 0x3b,
                "object_code/ages/interactions/dungeonEvents.s:interaction21_subid09@tileData") ||
            room64Pattern[1].Tile != 0x1d ||
            room64Pattern[1].PackedPosition != 0x59 ||
            room64Pattern[2].Tile != 0x1d ||
            room64Pattern[2].PackedPosition != 0x5d ||
            room7a.Count != 2 || room7a[0] !=
                new DungeonMechanicDatabaseRecord(
                    4, 0x7a, 0, 0x21, 0x17, 0x39, 0x01,
                    TriggerPredicate.Exact, true) ||
            room0c.Count != 2 ||
            room0c[0] != new DungeonMechanicDatabaseRecord(
                4, 0x0c, 0, 0x13, 0x01, 0x47, 0x00,
                TriggerPredicate.None, true) ||
            room0c[1] != new DungeonMechanicDatabaseRecord(
                4, 0x0c, 1, 0x1e, 0x08, 0x07, 0x00,
                TriggerPredicate.None, true) ||
            room0b.Count != 2 || room0b[0].SubId != 0x08 || room0b[1].SubId != 0x0b ||
            PushableBlock != 0x1d || PushDelay != 30 || SolveWait != 8 ||
            DoorFrameWait != 6 || OpenTile != 0xa0 ||
            ClosedTile(0x08) != 0x78 || ClosedTile(0x09) != 0x79 ||
            ClosedTile(0x0a) != 0x7a || ClosedTile(0x0b) != 0x7b ||
            ClosedTile(0x04) != 0x78 || ClosedTile(0x07) != 0x7b ||
            SolveSound != 0x4d || DoorSound != 0x70 ||
            ButtonTile != 0x0c || PressedButtonTile != 0x0d ||
            ButtonRadiusY != 2 || ButtonRadiusX != 2 ||
            ButtonObjectReleaseDelay != 0x1c || ButtonSound != 0x87 ||
            SwitchOffTile != 0x0a || SwitchOnTile != 0x0b ||
            SwitchRadiusY != 4 || SwitchRadiusX != 4 ||
            SwitchCollisionZ != -6 || SwitchHitLockout != 0x1c ||
            SwitchSound != 0x7e ||
            ChestTile != 0xf1 || ChestWait != 15 || PuffSound != 0x98 ||
            MoonlitGlobalFlag != 0x0f || MoonlitAllCrystalsMask != 0xf0 ||
            MoonlitRoomFlag != 0x40 || MoonlitCrystalCollision != 0x0a ||
            MoonlitCrystalRadiusY != 4 || MoonlitCrystalRadiusX != 4 ||
            MoonlitOrbPosition != 0x75 || MoonlitOrbMask != 0x10 ||
            MoonlitOrbCollision != 0x0a ||
            MoonlitOrbRadiusY != 4 || MoonlitOrbRadiusX != 4 ||
            MoonlitArmosChestPosition != 0x69 ||
            MoonlitArmosSourceTile != 0x26 ||
            MoonlitArmosReplacementTile != 0xa0 ||
            MoonlitKeyGoalPosition != 0x4a || MoonlitKeyGoalTile != 0x2a ||
            MoonlitFirstWait != 30 || MoonlitRumbleWait != 180 ||
            MoonlitAllWait != 30 || MoonlitExplosionWait != 90 ||
            MoonlitSolveWait != 30 || MoonlitRumbleSound != 0xb8 ||
            MoonlitBigExplosionSound != 0x79 || MoonlitSolveSound != 0x4d ||
            MoonlitBreakSound != 0x73 || MoonlitBreakSoundDelay != 2 ||
            string.IsNullOrWhiteSpace(Text(0x1200)) ||
            string.IsNullOrWhiteSpace(Text(0x1201)))
        {
            throw new InvalidOperationException(
                "Imported dungeon enemy-clear chest / switch / button / " +
                "trigger-chest / $13:$01 / $1e:$04-$0b / Moonlit Grotto " +
                "orb / Armos / crystal / falling-key contract is incomplete.");
        }
    }

    internal IReadOnlyList<DungeonMechanicDatabaseRecord> GetRoomRecords(int group, int room) =>
        _recordsByRoom.ValuesOrEmpty(MakeKey(group, room));

    internal IReadOnlyList<DungeonTilePatternRecord> TilePattern(int id, int subId) =>
        _tilePatterns.ValuesOrEmpty((id, subId));

    internal string Text(int textId) => _texts.TryGetValue(textId, out string? text)
        ? text
        : throw new KeyNotFoundException(
            $"Dungeon mechanic text TX_{textId:x4} was not imported.");

    internal int ClosedTile(int subId) => subId switch
    {
        0x04 or 0x08 => Constant("closed-up"),
        0x05 or 0x09 => Constant("closed-right"),
        0x06 or 0x0a => Constant("closed-down"),
        0x07 or 0x0b => Constant("closed-left"),
        _ => throw new ArgumentOutOfRangeException(
            nameof(subId), $"Unsupported shutter subid ${subId:x2}.")
    };

    private int Constant(string key) => _constants.TryGetValue(key, out int value)
        ? value
        : throw new KeyNotFoundException(
            $"Dungeon mechanic constant '{key}' was not imported.");

    private static int MakeKey(int group, int room) => (group << 8) | room;

}

internal enum TriggerPredicate
{
    None,
    BitSet,
    Exact
}

internal readonly record struct DungeonMechanicDatabaseRecord(int Group, int Room, int Order, int Id, int SubId, int PackedPosition, int Parameter, TriggerPredicate Predicate, bool CountSourceComplete);

internal readonly record struct DungeonTilePatternRecord(
    int Id,
    int SubId,
    int Order,
    int Tile,
    int PackedPosition,
    string Source);
