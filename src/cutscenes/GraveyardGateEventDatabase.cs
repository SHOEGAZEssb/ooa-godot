using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace oracleofages;

/// <summary>
/// Imported INTERAC_MISCELLANEOUS_2 $dc:$01 metadata and its
/// interactiondcSubid01Script command stream for present room $0:$5c.
/// </summary>
internal sealed class GraveyardGateEventDatabase
{
    internal readonly record struct InterleavedTile(
        int Position,
        byte Tile1,
        byte Tile2,
        int Type);

    internal readonly record struct EventRecord(
        int Group,
        int Room,
        int InteractionId,
        int SubId,
        byte RoomFlag,
        byte ClearTile,
        int ShakeFrames,
        IReadOnlyList<int> Phase1Ordinary,
        IReadOnlyList<InterleavedTile> Phase1Interleaved,
        IReadOnlyList<Vector2> Phase1Puffs,
        IReadOnlyList<int> Phase2Ordinary,
        IReadOnlyList<Vector2> Phase2Puffs,
        string Source);

    internal EventRecord Record { get; }
    internal IReadOnlyList<CutsceneCommand> Commands { get; }

    internal GraveyardGateEventDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/graveyard_gate_event.tsv",
            new GeneratedTableSchema(
                "graveyard gate event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "id", "subid", "room-flag", "clear-tile",
                    "shake-frames", "phase1-ordinary", "phase1-interleaved",
                    "phase1-puffs", "phase2-ordinary", "phase2-puffs", "source"
                ],
                headerRequired: true)).SingleRow();
        Record = new EventRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            (byte)row.HexByte(4),
            (byte)row.HexByte(5),
            row.UnsignedDecimal(6),
            ParsePackedList(row, 7),
            ParseInterleaved(row, 8),
            ParsePuffs(row, 9),
            ParsePackedList(row, 10),
            ParsePuffs(row, 11),
            row.RequiredString(12));
        Commands = CutsceneCommandCatalog.Load(
            "res://assets/oracle/cutscenes/graveyard_gate_commands.tsv");
        Validate();
    }

    private void Validate()
    {
        if (Record is not
            {
                Group: 0,
                Room: 0x5c,
                InteractionId: 0xdc,
                SubId: 0x01,
                RoomFlag: OracleSaveData.RoomFlag80,
                ClearTile: 0x3a,
                ShakeFrames: 10
            } ||
            Record.Phase1Ordinary is not [0x34, 0x44] ||
            Record.Phase1Interleaved is not
            [
                { Position: 0x33, Tile1: 0x3a, Tile2: 0x89, Type: 1 },
                { Position: 0x35, Tile1: 0x3a, Tile2: 0x89, Type: 3 },
                { Position: 0x43, Tile1: 0x98, Tile2: 0xec, Type: 1 },
                { Position: 0x45, Tile1: 0x9a, Tile2: 0xec, Type: 3 }
            ] ||
            Record.Phase1Puffs is not [{ X: 0x40, Y: 0x48 }, { X: 0x50, Y: 0x48 }] ||
            Record.Phase2Ordinary is not [0x33, 0x35, 0x43, 0x45] ||
            Record.Phase2Puffs is not [{ X: 0x30, Y: 0x48 }, { X: 0x60, Y: 0x48 }] ||
            Commands.Count != 10 ||
            Commands[0] is not CutsceneSetMusicCommand
                { Music: OracleSoundEngine.SndCtrlStopMusic } ||
            Commands[1] is not CutsceneWaitCommand { Frames: 60 } ||
            Commands[2] is not CutsceneNativeCommand { Handler: "RemoveGateTiles1" } ||
            Commands[3] is not CutsceneWaitCommand { Frames: 45 } ||
            Commands[4] is not CutsceneNativeCommand { Handler: "RemoveGateTiles2" } ||
            Commands[5] is not CutsceneWaitCommand { Frames: 60 } ||
            Commands[6] is not CutsceneSetMusicCommand { Music: 0xff } ||
            Commands[7] is not CutscenePlaySoundCommand
                { Sound: OracleSoundEngine.SndSolvePuzzle } ||
            Commands[8] is not CutsceneEnableInputCommand ||
            Commands[9] is not CutsceneEndCommand)
        {
            throw new InvalidOperationException(
                "Room 0:5c graveyard-gate command/data contract is incomplete.");
        }
    }

    private static IReadOnlyList<int> ParsePackedList(
        GeneratedTableRow row,
        int column)
    {
        string[] values = row.RequiredString(column).Split(',');
        var result = new List<int>(values.Length);
        foreach (string value in values)
            result.Add(ParseHex(row, column, value));
        return result.AsReadOnly();
    }

    private static IReadOnlyList<InterleavedTile> ParseInterleaved(
        GeneratedTableRow row,
        int column)
    {
        string[] values = row.RequiredString(column).Split(',');
        var result = new List<InterleavedTile>(values.Length);
        foreach (string value in values)
        {
            string[] fields = value.Split(':');
            if (fields.Length != 4)
                throw row.Invalid(column, "position:tile1:tile2:type entries");
            int type = ParseHex(row, column, fields[3]);
            if (type is < 0 or > 3)
                throw row.Invalid(column, "interleaved type 0-3");
            result.Add(new InterleavedTile(
                ParseHex(row, column, fields[0]),
                (byte)ParseHex(row, column, fields[1]),
                (byte)ParseHex(row, column, fields[2]),
                type));
        }
        return result.AsReadOnly();
    }

    private static IReadOnlyList<Vector2> ParsePuffs(
        GeneratedTableRow row,
        int column)
    {
        string[] values = row.RequiredString(column).Split(',');
        var result = new List<Vector2>(values.Length);
        foreach (string value in values)
        {
            string[] fields = value.Split(':');
            if (fields.Length != 2)
                throw row.Invalid(column, "y:x puff positions");
            int y = ParseHex(row, column, fields[0]);
            int x = ParseHex(row, column, fields[1]);
            result.Add(new Vector2(x, y));
        }
        return result.AsReadOnly();
    }

    private static int ParseHex(GeneratedTableRow row, int column, string value)
    {
        if (!int.TryParse(value, NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out int parsed) || parsed is < 0 or > 0xff)
        {
            throw row.Invalid(column, "hexadecimal byte lists");
        }
        return parsed;
    }
}
