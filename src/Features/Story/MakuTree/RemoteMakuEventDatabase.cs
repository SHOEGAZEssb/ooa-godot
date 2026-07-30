using System;
using System.Collections.Generic;
using System.Globalization;

namespace oracleofages;

/// <summary>
/// Shared reader for one imported INTERAC_REMOTE_MAKU_CUTSCENE $8a
/// placement. Concrete databases retain each placement's source identity and
/// predicate while this type validates the genuinely shared script and native
/// era-selected confetti contract.
/// </summary>
internal abstract class RemoteMakuEventDatabase
{
    private const string Root = "res://assets/oracle/cutscenes/";
    private readonly Dictionary<string, RemoteMakuVisualRecord> _visuals =
        new(StringComparer.Ordinal);
    private readonly string _description;

    internal RemoteMakuEventRecord Record { get; }
    internal IReadOnlyList<CutsceneCommand> Commands { get; }

    protected RemoteMakuEventDatabase(
        string eventFile,
        string description,
        string commandFile)
    {
        _description = description;
        Record = LoadRecord(eventFile, description);
        LoadVisuals();
        Commands = CutsceneCommandCatalog.Load(Root + commandFile);
        ValidateSharedContract();
    }

    internal RemoteMakuVisualRecord Visual(string key) =>
        _visuals.TryGetValue(key, out RemoteMakuVisualRecord visual)
            ? visual
            : throw new KeyNotFoundException(
                $"Remote Maku visual '{key}' was not imported.");

    private static RemoteMakuEventRecord LoadRecord(
        string file,
        string description)
    {
        GeneratedTableRow row = GeneratedTable.Load(
            Root + file,
            new GeneratedTableSchema(
                description,
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "id", "subid", "var03", "essence-mask",
                    "required-treasure", "room-flag", "standard-text-id",
                    "linked-text-id", "standard-map-text", "linked-map-text",
                    "music", "hud-lock-byte", "fade-delay", "fade-frames",
                    "initial-wait", "confetti-hold1", "confetti-hold2",
                    "post-text-wait", "confetti-pieces", "spawn-delays",
                    "positions-and-accelerations", "y-offset-fixed",
                    "sparkle-initial-delay", "sparkle-repeat-delay",
                    "sound-counter", "sound", "y-speed-limit",
                    "x-speed-limit", "delete-y", "confetti-kind",
                    "sound-initial-counter", "initial-speed-y",
                    "initial-speed-x", "acceleration-x"
                ],
                headerRequired: true)).SingleRow();
        return new RemoteMakuEventRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.HexByte(4),
            row.HexByte(5),
            row.HexByte(6),
            row.HexByte(7),
            row.HexWord(8),
            row.HexWord(9),
            row.HexByte(10),
            row.HexByte(11),
            row.HexByte(12),
            row.HexByte(13),
            row.UnsignedDecimal(14),
            row.UnsignedDecimal(15),
            row.UnsignedDecimal(16),
            row.UnsignedDecimal(17),
            row.UnsignedDecimal(18),
            row.UnsignedDecimal(19),
            row.UnsignedDecimal(20),
            ParseUnsignedList(row, 21),
            ParsePieceList(row, 22),
            row.UnsignedDecimal(23),
            row.UnsignedDecimal(24),
            row.UnsignedDecimal(25),
            row.UnsignedDecimal(26),
            row.HexByte(27),
            row.UnsignedDecimal(28),
            row.UnsignedDecimal(29),
            row.UnsignedDecimal(30),
            row.RequiredString(31) switch
            {
                "present" => RemoteMakuConfettiKind.Present,
                "past" => RemoteMakuConfettiKind.Past,
                _ => throw row.Invalid(31, "'present' or 'past'")
            },
            row.UnsignedDecimal(32),
            row.Decimal(33, short.MinValue, short.MaxValue),
            row.Decimal(34, short.MinValue, short.MaxValue),
            row.Decimal(35, short.MinValue, short.MaxValue));
    }

    private void LoadVisuals()
    {
        GeneratedTable table = GeneratedTable.Load(
            Root + "remote_maku_first_essence_visuals.tsv",
            new GeneratedTableSchema(
                "remote Maku visuals",
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

    private void ValidateSharedContract()
    {
        bool commonValid = Record is
            {
                InteractionId: 0x8a, RoomFlag: 0x40,
                Music: 0x1e, HudLockByte: 0x77, FadeDelay: 2,
                FadeFrames: 65, InitialWait: 40,
                ConfettiHold1: 240, PostTextWait: 1
            } &&
            _visuals.Count == 4 &&
            Visual("confetti-left") is
                { TileBase: 4, Palette: 2 } &&
            Visual("confetti-right") is
                { TileBase: 4, Palette: 2 } &&
            Visual("confetti-past") is
                { TileBase: 0, Palette: 3 } &&
            Visual("sparkle") is
                { TileBase: 0x0a, Palette: 0 };
        bool confettiValid = Record.ConfettiKind switch
        {
            RemoteMakuConfettiKind.Present =>
                Record is
                {
                    SubId: 0, ConfettiHold2: 180, ConfettiPieces: 5,
                    YOffsetFixed: 0x00c0, SparkleInitialDelay: 0x10,
                    SparkleRepeatDelay: 0x18, SoundInitialCounter: 180,
                    SoundCounter: 180, Sound: 0x83,
                    YSpeedLimit: 0x0100, XSpeedLimit: 0x0200,
                    DeleteY: 0x88, InitialSpeedY: 0,
                    InitialSpeedX: 0, AccelerationX: 0
                } &&
                Record.SpawnDelays.Count == 6 &&
                Record.SpawnDelays[0] == 1 &&
                Record.SpawnDelays[1] == 0x32 &&
                Record.Pieces.Count == 5 &&
                Record.Pieces[0] is
                {
                    Y: -24, X: 0x38,
                    AccelerationY: 0x18, AccelerationX: 0x18
                },
            RemoteMakuConfettiKind.Past =>
                Record is
                {
                    SubId: 1, ConfettiHold2: 60, ConfettiPieces: 12,
                    YOffsetFixed: 0, SparkleInitialDelay: 0,
                    SparkleRepeatDelay: 0, SoundInitialCounter: 10,
                    SoundCounter: 45,
                    Sound: OracleSoundEngine.SndMakuTreePast,
                    YSpeedLimit: 0, XSpeedLimit: 0, DeleteY: 0,
                    InitialSpeedY: -0x280, InitialSpeedX: 0x400,
                    AccelerationX: -0x10
                } &&
                Record.SpawnDelays.Count == 12 &&
                Record.SpawnDelays[0] == 1 &&
                Record.SpawnDelays[1] == 0x32 &&
                Record.SpawnDelays[2] == 0x1e &&
                Record.SpawnDelays[11] == 0x14 &&
                Record.Pieces.Count == 12 &&
                Record.Pieces[0] is
                {
                    Y: 0x80, X: 0x10,
                    AccelerationY: 0, AccelerationX: 0
                } &&
                Record.Pieces[6] == Record.Pieces[0],
            _ => false
        };
        string confettiHandler = Record.ConfettiKind ==
            RemoteMakuConfettiKind.Past
                ? "SpawnPastConfetti"
                : "SpawnPresentConfetti";
        if (!commonValid ||
            !confettiValid ||
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
            Commands[7] is not CutsceneNativeCommand spawn ||
            spawn.Handler != confettiHandler ||
            Commands[8] is not CutsceneWaitCommand { Frames: 240 } ||
            Commands[9] is not CutsceneWaitCommand hold2 ||
            hold2.Frames != Record.ConfettiHold2 ||
            Commands[10] is not CutsceneShowTextVariantsCommand text ||
            text.StandardTextId != Record.StandardTextId ||
            text.LinkedTextId != Record.LinkedTextId ||
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
                $"Imported {_description} shared presentation contract is " +
                "incomplete.");
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
                throw row.Invalid(
                    column,
                    "comma-separated unsigned decimal values");
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

internal readonly record struct RemoteMakuEventRecord(
    int Group,
    int Room,
    int InteractionId,
    int SubId,
    int Var03,
    int EssenceMask,
    int RequiredTreasure,
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
    int DeleteY,
    RemoteMakuConfettiKind ConfettiKind,
    int SoundInitialCounter,
    int InitialSpeedY,
    int InitialSpeedX,
    int AccelerationX);

internal enum RemoteMakuConfettiKind
{
    Present,
    Past
}

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
