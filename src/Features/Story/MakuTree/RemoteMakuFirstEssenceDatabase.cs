using System;
using System.Collections.Generic;
using System.Globalization;

namespace oracleofages;

/// <summary>
/// Imported room 0:8d INTERAC_REMOTE_MAKU_CUTSCENE $8a:$00 data.
/// The command stream owns the interaction script; this record owns the
/// native present-day confetti object's fixed-point movement constants.
/// </summary>
internal sealed class RemoteMakuFirstEssenceDatabase
{
    private const string Root = "res://assets/oracle/cutscenes/";
    private readonly Dictionary<string, RemoteMakuVisualRecord> _visuals =
        new(StringComparer.Ordinal);

    internal RemoteMakuFirstEssenceRecord Record { get; }
    internal IReadOnlyList<CutsceneCommand> Commands { get; }

    internal RemoteMakuFirstEssenceDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            Root + "remote_maku_first_essence_event.tsv",
            new GeneratedTableSchema(
                "first-Essence remote Maku event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "id", "subid", "var03", "essence-mask",
                    "room-flag", "standard-text-id", "linked-text-id",
                    "standard-map-text", "linked-map-text", "music",
                    "hud-lock-byte", "fade-delay", "fade-frames", "initial-wait",
                    "confetti-hold1", "confetti-hold2", "post-text-wait",
                    "confetti-pieces", "spawn-delays",
                    "positions-and-accelerations", "y-offset-fixed",
                    "sparkle-initial-delay", "sparkle-repeat-delay",
                    "sound-counter", "sound", "y-speed-limit",
                    "x-speed-limit", "delete-y"
                ],
                headerRequired: true)).SingleRow();
        Record = new RemoteMakuFirstEssenceRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.HexByte(4),
            row.HexByte(5),
            row.HexByte(6),
            row.HexWord(7),
            row.HexWord(8),
            row.HexByte(9),
            row.HexByte(10),
            row.HexByte(11),
            row.HexByte(12),
            row.UnsignedDecimal(13),
            row.UnsignedDecimal(14),
            row.UnsignedDecimal(15),
            row.UnsignedDecimal(16),
            row.UnsignedDecimal(17),
            row.UnsignedDecimal(18),
            row.UnsignedDecimal(19),
            ParseUnsignedList(row, 20),
            ParsePieceList(row, 21),
            row.UnsignedDecimal(22),
            row.UnsignedDecimal(23),
            row.UnsignedDecimal(24),
            row.UnsignedDecimal(25),
            row.HexByte(26),
            row.UnsignedDecimal(27),
            row.UnsignedDecimal(28),
            row.UnsignedDecimal(29));
        LoadVisuals();
        Commands = CutsceneCommandCatalog.Load(
            Root + "remote_maku_first_essence_commands.tsv");
        Validate();
    }

    internal RemoteMakuVisualRecord Visual(string key) =>
        _visuals.TryGetValue(key, out RemoteMakuVisualRecord visual)
            ? visual
            : throw new KeyNotFoundException(
                $"Remote Maku visual '{key}' was not imported.");

    private void LoadVisuals()
    {
        GeneratedTable table = GeneratedTable.Load(
            Root + "remote_maku_first_essence_visuals.tsv",
            new GeneratedTableSchema(
                "first-Essence remote Maku visuals",
                GeneratedTableKeySemantics.Unique,
                ["key", "sprite", "tile-base", "palette", "animation"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            RemoteMakuVisualRecord visual = new(
                row.RequiredString(0),
                row.RequiredString(1),
                row.UnsignedDecimal(2),
                row.UnsignedDecimal(3),
                row.RequiredString(4));
            if (!_visuals.TryAdd(visual.Key, visual))
                throw row.Invalid(0, "a unique remote Maku visual key");
        }
    }

    private void Validate()
    {
        if (Record is not
            {
                Group: 0, Room: 0x8d, InteractionId: 0x8a,
                SubId: 0, Var03: 0, EssenceMask: 0x01, RoomFlag: 0x40,
                StandardTextId: 0x05b0, LinkedTextId: 0x05c0,
                StandardMapText: 0xb0, LinkedMapText: 0xc0,
                Music: 0x1e, HudLockByte: 0x77, FadeDelay: 2,
                FadeFrames: 65, InitialWait: 40,
                ConfettiHold1: 240, ConfettiHold2: 180,
                PostTextWait: 1, ConfettiPieces: 5,
                YOffsetFixed: 0x00c0, SparkleInitialDelay: 0x10,
                SparkleRepeatDelay: 0x18, SoundCounter: 180,
                Sound: 0x83, YSpeedLimit: 0x0100,
                XSpeedLimit: 0x0200, DeleteY: 0x88
            } ||
            Record.SpawnDelays.Count != 6 ||
            Record.SpawnDelays[0] != 1 ||
            Record.SpawnDelays[1] != 0x32 ||
            Record.Pieces.Count != 5 ||
            Record.Pieces[0] is not
                { Y: -24, X: 0x38, AccelerationY: 0x18, AccelerationX: 0x18 } ||
            _visuals.Count != 3 ||
            Visual("confetti-left") is not
                { TileBase: 4, Palette: 2 } ||
            Visual("confetti-right") is not
                { TileBase: 4, Palette: 2 } ||
            Visual("sparkle") is not
                { TileBase: 0x0a, Palette: 0 } ||
            Commands.Count != 20 ||
            Commands[0] is not CutsceneDisableInputCommand ||
            Commands[1] is not CutsceneWriteMemoryCommand
                { Binding: "TextboxFlags", Value: 0x04 } ||
            Commands[2] is not CutsceneSetMusicCommand { Music: 0x1e } ||
            Commands[3] is not CutsceneWaitCommand { Frames: 40 } ||
            Commands[4] is not CutsceneWriteMemoryCommand
                { Binding: "DontUpdateStatusBar", Value: 0x77 } ||
            Commands[5] is not CutsceneNativeCommand { Handler: "HideHud" } ||
            Commands[6] is not CutsceneNativeBlockingCommand
                { Handler: "FadeOutBlack", Frames: 65 } ||
            Commands[7] is not CutsceneNativeCommand
                { Handler: "SpawnPresentConfetti" } ||
            Commands[8] is not CutsceneWaitCommand { Frames: 240 } ||
            Commands[9] is not CutsceneWaitCommand { Frames: 180 } ||
            Commands[10] is not CutsceneShowTextVariantsCommand
                { StandardTextId: 0x05b0, LinkedTextId: 0x05c0 } ||
            Commands[11] is not CutsceneWaitCommand { Frames: 1 } ||
            Commands[12] is not CutsceneNativeCommand { Handler: "ShowHud" } ||
            Commands[13] is not CutsceneNativeCommand
                { Handler: "ClearFadingPalettes" } ||
            Commands[14] is not CutsceneNativeBlockingCommand
                { Handler: "FadeInWhite", Frames: 65 } ||
            Commands[15] is not CutsceneNativeCommand { Handler: "ResetMusic" } ||
            Commands[16] is not CutsceneOrRoomFlagCommand { Flag: 0x40 } ||
            Commands[17] is not CutsceneNativeCommand
                { Handler: "IncMakuTreeState" } ||
            Commands[18] is not CutsceneEnableInputCommand ||
            Commands[19] is not CutsceneEndCommand)
        {
            throw new InvalidOperationException(
                "Imported room 0:8d first-Essence remote Maku contract is incomplete.");
        }
    }

    private static IReadOnlyList<int> ParseUnsignedList(
        GeneratedTableRow row,
        int column)
    {
        string[] values = row.RequiredString(column).Split(
            ',', StringSplitOptions.RemoveEmptyEntries |
                 StringSplitOptions.TrimEntries);
        var result = new int[values.Length];
        for (int index = 0; index < values.Length; index++)
        {
            if (!int.TryParse(
                    values[index],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out result[index]) ||
                result[index] < 0)
            {
                throw row.Invalid(column, "comma-separated unsigned decimal values");
            }
        }
        return result;
    }

    private static IReadOnlyList<RemoteMakuConfettiPieceRecord> ParsePieceList(
        GeneratedTableRow row,
        int column)
    {
        string[] entries = row.RequiredString(column).Split(
            ',', StringSplitOptions.RemoveEmptyEntries |
                 StringSplitOptions.TrimEntries);
        var result = new RemoteMakuConfettiPieceRecord[entries.Length];
        for (int index = 0; index < entries.Length; index++)
        {
            string[] values = entries[index].Split(':');
            if (values.Length != 4 ||
                !int.TryParse(values[0], NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out int y) ||
                !int.TryParse(values[1], NumberStyles.None,
                    CultureInfo.InvariantCulture, out int x) ||
                !int.TryParse(values[2], NumberStyles.None,
                    CultureInfo.InvariantCulture, out int accelerationY) ||
                !int.TryParse(values[3], NumberStyles.None,
                    CultureInfo.InvariantCulture, out int accelerationX))
            {
                throw row.Invalid(
                    column,
                    "comma-separated y:x:acceleration-y:acceleration-x values");
            }
            result[index] = new RemoteMakuConfettiPieceRecord(
                y, x, accelerationY, accelerationX);
        }
        return result;
    }
}

internal readonly record struct RemoteMakuFirstEssenceRecord(
    int Group,
    int Room,
    int InteractionId,
    int SubId,
    int Var03,
    int EssenceMask,
    int RoomFlag,
    int StandardTextId,
    int LinkedTextId,
    int StandardMapText,
    int LinkedMapText,
    int Music,
    int HudLockByte,
    int FadeDelay,
    int FadeFrames,
    int InitialWait,
    int ConfettiHold1,
    int ConfettiHold2,
    int PostTextWait,
    int ConfettiPieces,
    IReadOnlyList<int> SpawnDelays,
    IReadOnlyList<RemoteMakuConfettiPieceRecord> Pieces,
    int YOffsetFixed,
    int SparkleInitialDelay,
    int SparkleRepeatDelay,
    int SoundCounter,
    int Sound,
    int YSpeedLimit,
    int XSpeedLimit,
    int DeleteY);

internal readonly record struct RemoteMakuConfettiPieceRecord(
    int Y,
    int X,
    int AccelerationY,
    int AccelerationX);

internal readonly record struct RemoteMakuVisualRecord(
    string Key,
    string Sprite,
    int TileBase,
    int Palette,
    string Animation);
