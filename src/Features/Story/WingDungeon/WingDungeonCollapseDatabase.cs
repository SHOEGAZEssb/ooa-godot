using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Imported INTERAC_MISCELLANEOUS_2 $dc:$02 and CUTSCENE_D2_COLLAPSE data
/// for present room $0:$83, including the four 6x6 background-tile phases.
/// </summary>
internal sealed class WingDungeonCollapseDatabase
{
    internal WingDungeonCollapseRecord Record { get; }
    internal IReadOnlyList<WingDungeonCollapseMapRecord> Maps { get; }

    internal WingDungeonCollapseDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/wing_dungeon_collapse_event.tsv",
            new GeneratedTableSchema(
                "Wing Dungeon collapse event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "id", "subid", "y", "x",
                    "rock-position", "rock-tile", "ground-tile", "dug-tile",
                    "room-flag", "linked-room", "linked-room-flag",
                    "pickup-wait", "exclamation-frames", "pre-collapse-shake",
                    "collapse-initial-wait", "phase-wait", "final-wait",
                    "collapse-shake", "dust-y", "dust-x", "dust-frames",
                    "dust-interval", "exclamation-id", "exclamation-subid",
                    "exclamation-sprite", "exclamation-tile-base",
                    "exclamation-palette", "exclamation-animation",
                    "facade-position", "facade-width", "facade-height",
                    "final-tiles", "final-collisions", "source"
                ],
                headerRequired: true)).SingleRow();
        Record = new WingDungeonCollapseRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.HexByte(4),
            row.HexByte(5),
            row.HexByte(6),
            row.HexByte(7),
            row.HexByte(8),
            row.HexByte(9),
            (byte)row.HexByte(10),
            row.HexByte(11),
            (byte)row.HexByte(12),
            row.UnsignedDecimal(13),
            row.UnsignedDecimal(14),
            row.UnsignedDecimal(15),
            row.UnsignedDecimal(16),
            row.UnsignedDecimal(17),
            row.UnsignedDecimal(18),
            row.UnsignedDecimal(19),
            row.HexByte(20),
            row.HexByte(21),
            row.UnsignedDecimal(22),
            row.UnsignedDecimal(23),
            row.HexByte(24),
            row.HexByte(25),
            row.RequiredString(26),
            row.UnsignedDecimal(27),
            row.UnsignedDecimal(28),
            row.RequiredString(29),
            row.HexByte(30),
            row.UnsignedDecimal(31),
            row.UnsignedDecimal(32),
            ParseBytes(row, 33),
            ParseBytes(row, 34),
            row.RequiredString(35));

        GeneratedTable maps = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/wing_dungeon_collapse_maps.tsv",
            new GeneratedTableSchema(
                "Wing Dungeon collapse background maps",
                GeneratedTableKeySemantics.Unique,
                ["phase", "gfx-header", "tile-ids", "source"],
                ["phase"],
                headerRequired: true));
        var records = new List<WingDungeonCollapseMapRecord>(maps.Rows.Count);
        foreach (GeneratedTableRow mapRow in maps.Rows)
        {
            records.Add(new WingDungeonCollapseMapRecord(
                mapRow.UnsignedDecimal(0),
                mapRow.HexByte(1),
                ParseBytes(mapRow, 2),
                mapRow.RequiredString(3)));
        }
        Maps = records.AsReadOnly();
        Validate();
    }

    internal NpcRecord CreateExclamationRecord() => new(
        Record.Group,
        Record.Room,
        Record.ExclamationId,
        Record.ExclamationSubId,
        Record.Y - 8,
        Record.X,
        0,
        0,
        Record.ExclamationSprite,
        Record.ExclamationTileBase,
        Record.ExclamationPalette,
        0,
        false,
        Record.ExclamationAnimation,
        Record.ExclamationAnimation,
        Record.ExclamationAnimation,
        Record.ExclamationAnimation,
        string.Empty,
        NpcImplementationClassification.EventOwned);

    private void Validate()
    {
        if (Record is not
            {
                Group: 0,
                Room: 0x83,
                InteractionId: 0xdc,
                SubId: 0x02,
                Y: 0x48,
                X: 0x38,
                RockPosition: 0x43,
                RockTile: 0xc3,
                GroundTile: 0x3a,
                DugTile: 0x1c,
                RoomFlag: OracleSaveData.RoomFlag80,
                LinkedRoom: 0x73,
                LinkedRoomFlag: OracleSaveData.RoomFlag80,
                PickupWait: 30,
                ExclamationFrames: 60,
                PreCollapseShake: 0x28,
                CollapseInitialWait: 60,
                PhaseWait: 30,
                FinalWait: 60,
                CollapseShake: 0x0f,
                DustY: 0x2c,
                DustX: 0x58,
                DustFrames: 0x6a,
                DustInterval: 3,
                ExclamationId: 0x9f,
                ExclamationSubId: 0,
                FacadePosition: 0x04,
                FacadeWidth: 3,
                FacadeHeight: 3
            } ||
            Record.FinalTiles is not
                [0x3b, 0x3b, 0x3b, 0x3b, 0x3b, 0x3b, 0x00, 0x00, 0x00] ||
            Record.FinalCollisions is not
                [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x0f, 0x0a] ||
            Maps.Count != 4)
        {
            throw new InvalidOperationException(
                "Room 0:83 Wing Dungeon collapse contract is incomplete.");
        }

        for (int phase = 0; phase < Maps.Count; phase++)
        {
            WingDungeonCollapseMapRecord map = Maps[phase];
            if (map.Phase != phase || map.GfxHeader != 0x50 + phase ||
                map.TileIds.Count != 36)
            {
                throw new InvalidOperationException(
                    $"Wing Dungeon collapse phase {phase} is invalid.");
            }
        }
    }

    private static IReadOnlyList<byte> ParseBytes(
        GeneratedTableRow row,
        int column)
    {
        string[] values = row.RequiredString(column).Split(',');
        var result = new List<byte>(values.Length);
        foreach (string value in values)
        {
            if (!int.TryParse(
                    value,
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out int parsed) ||
                parsed is < 0 or > 0xff)
            {
                throw row.Invalid(column, "comma-separated hexadecimal bytes");
            }
            result.Add((byte)parsed);
        }
        return result.AsReadOnly();
    }
}

internal readonly record struct WingDungeonCollapseRecord(
    int Group,
    int Room,
    int InteractionId,
    int SubId,
    int Y,
    int X,
    int RockPosition,
    int RockTile,
    int GroundTile,
    int DugTile,
    byte RoomFlag,
    int LinkedRoom,
    byte LinkedRoomFlag,
    int PickupWait,
    int ExclamationFrames,
    int PreCollapseShake,
    int CollapseInitialWait,
    int PhaseWait,
    int FinalWait,
    int CollapseShake,
    int DustY,
    int DustX,
    int DustFrames,
    int DustInterval,
    int ExclamationId,
    int ExclamationSubId,
    string ExclamationSprite,
    int ExclamationTileBase,
    int ExclamationPalette,
    string ExclamationAnimation,
    int FacadePosition,
    int FacadeWidth,
    int FacadeHeight,
    IReadOnlyList<byte> FinalTiles,
    IReadOnlyList<byte> FinalCollisions,
    string Source);

internal readonly record struct WingDungeonCollapseMapRecord(
    int Phase,
    int GfxHeader,
    IReadOnlyList<byte> TileIds,
    string Source);
