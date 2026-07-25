using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Imported interaction scripts and native tables for the Fairies' Woods
/// hide-and-seek sequence beginning in present room $0:$82.
/// </summary>
internal sealed class FairiesWoodsDatabase
{
    private const string Root = "res://assets/oracle/cutscenes/";
    private readonly Dictionary<int, FairiesWoodsTextRecord> _texts = new();
    private readonly Dictionary<int, FairiesWoodsHiddenSpotRecord> _hiddenSpots = new();

    internal FairiesWoodsEventRecord Event { get; }
    internal IReadOnlyList<FairiesWoodsMovementRecord> Movements { get; }
    internal IReadOnlyList<FairiesWoodsVelocityRecord> Velocities { get; }
    internal IReadOnlyList<FairiesWoodsHidingRoomRecord> HidingRooms { get; }
    internal IReadOnlyList<FairiesWoodsDiscoveredRecord> DiscoveredFairies { get; }
    internal IReadOnlyList<CutsceneCommand> IntroCommands { get; }
    internal IReadOnlyList<CutsceneCommand> RevealCommands { get; }
    internal IReadOnlyList<CutsceneCommand> ExitCommands { get; }

    internal FairiesWoodsDatabase()
    {
        Event = LoadEvent();
        Movements = LoadMovements();
        Velocities = LoadVelocities();
        HidingRooms = LoadHidingRooms();
        DiscoveredFairies = LoadDiscoveredFairies();
        LoadTexts();
        LoadHiddenSpots();
        IntroCommands = CutsceneCommandCatalog.Load(
            Root + "fairies_woods_intro_commands.tsv");
        RevealCommands = CutsceneCommandCatalog.Load(
            Root + "fairies_woods_reveal_commands.tsv");
        ExitCommands = CutsceneCommandCatalog.Load(
            Root + "fairies_woods_exit_commands.tsv");
        Validate();
    }

    internal FairiesWoodsTextRecord Text(int textId) =>
        _texts.TryGetValue(textId, out FairiesWoodsTextRecord record)
            ? record
            : throw new InvalidOperationException(
                $"Fairies' Woods text TX_{textId:x4} was not imported.");

    internal bool TryHiddenSpot(
        int room,
        out FairiesWoodsHiddenSpotRecord record) =>
        _hiddenSpots.TryGetValue(room, out record);

    private static FairiesWoodsEventRecord LoadEvent()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            Root + "fairies_woods_event.tsv",
            new GeneratedTableSchema(
                "Fairies' Woods event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "start-room", "exit-room", "reset-room",
                    "essence-treasure", "active-address", "found-address",
                    "signal-address", "completion-flag", "unscrambled-flag",
                    "hidden-delay", "exit-y", "exit-x", "exit-radius-y",
                    "exit-radius-x", "magic-sound", "puff-sound",
                    "mystery-sound", "normal-fade-out", "normal-fade-in",
                    "fast-fade-in", "completion-hold", "delayed-fade-in",
                    "normal-fade-speed", "fast-fade-speed",
                    "delayed-fade-refill",
                    "forest-fairy-sprite", "fairy-tile-base", "animation0",
                    "animation1", "sparkle-sprite", "sparkle-tile-base",
                    "sparkle-palette", "sparkle-animation"
                ],
                headerRequired: true)).SingleRow();
        return new FairiesWoodsEventRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.HexByte(4),
            row.HexWord(5),
            row.HexWord(6),
            row.HexWord(7),
            row.HexByte(8),
            row.HexByte(9),
            row.UnsignedDecimal(10),
            row.HexByte(11),
            row.HexByte(12),
            row.HexByte(13),
            row.HexByte(14),
            row.HexByte(15),
            row.HexByte(16),
            row.HexByte(17),
            row.UnsignedDecimal(18),
            row.UnsignedDecimal(19),
            row.UnsignedDecimal(20),
            row.UnsignedDecimal(21),
            row.UnsignedDecimal(22),
            row.UnsignedDecimal(23),
            row.UnsignedDecimal(24),
            row.UnsignedDecimal(25),
            row.RequiredString(26),
            row.UnsignedDecimal(27),
            row.RequiredString(28),
            row.RequiredString(29),
            row.RequiredString(30),
            row.UnsignedDecimal(31),
            row.UnsignedDecimal(32),
            row.RequiredString(33));
    }

    private static IReadOnlyList<FairiesWoodsMovementRecord> LoadMovements()
    {
        GeneratedTable table = GeneratedTable.Load(
            Root + "fairies_woods_movement.tsv",
            new GeneratedTableSchema(
                "forest-fairy movement presets",
                GeneratedTableKeySemantics.Unique,
                [
                    "index", "initial-y", "initial-x", "angle", "counter",
                    "target-y", "target-x", "direction", "palette", "source"
                ],
                ["index"],
                headerRequired: true));
        var result = new List<FairiesWoodsMovementRecord>();
        foreach (GeneratedTableRow row in table.Rows)
        {
            int index = row.UnsignedDecimal(0);
            if (index != result.Count)
                throw row.Invalid(0, $"sequential movement index {result.Count}");
            result.Add(new FairiesWoodsMovementRecord(
                index,
                row.HexByte(1),
                row.HexByte(2),
                row.HexByte(3),
                row.UnsignedDecimal(4),
                row.HexByte(5),
                row.HexByte(6),
                row.Boolean01(7) ? 1 : 0,
                row.UnsignedDecimal(8),
                row.RequiredString(9)));
        }
        return result.AsReadOnly();
    }

    private static IReadOnlyList<FairiesWoodsVelocityRecord> LoadVelocities()
    {
        GeneratedTable table = GeneratedTable.Load(
            Root + "fairies_woods_velocity.tsv",
            new GeneratedTableSchema(
                "SPEED_200 object velocities",
                GeneratedTableKeySemantics.Unique,
                ["angle", "y-fixed", "x-fixed", "source"],
                ["angle"],
                headerRequired: true));
        var result = new List<FairiesWoodsVelocityRecord>();
        foreach (GeneratedTableRow row in table.Rows)
        {
            int angle = row.HexByte(0);
            if (angle != result.Count)
                throw row.Invalid(0, $"sequential angle ${result.Count:x2}");
            result.Add(new FairiesWoodsVelocityRecord(
                angle,
                row.Decimal(1, short.MinValue, short.MaxValue),
                row.Decimal(2, short.MinValue, short.MaxValue),
                row.RequiredString(3)));
        }
        return result.AsReadOnly();
    }

    private static IReadOnlyList<FairiesWoodsHidingRoomRecord> LoadHidingRooms()
    {
        GeneratedTable table = GeneratedTable.Load(
            Root + "fairies_woods_hiding_rooms.tsv",
            new GeneratedTableSchema(
                "Fairies' Woods hiding rooms",
                GeneratedTableKeySemantics.Unique,
                ["index", "room", "preset", "source"],
                ["index"],
                headerRequired: true));
        var result = new List<FairiesWoodsHidingRoomRecord>();
        foreach (GeneratedTableRow row in table.Rows)
        {
            int index = row.UnsignedDecimal(0);
            if (index != result.Count)
                throw row.Invalid(0, $"sequential hiding-room index {result.Count}");
            result.Add(new FairiesWoodsHidingRoomRecord(
                index,
                row.HexByte(1),
                row.HexByte(2),
                row.RequiredString(3)));
        }
        return result.AsReadOnly();
    }

    private static IReadOnlyList<FairiesWoodsDiscoveredRecord>
        LoadDiscoveredFairies()
    {
        GeneratedTable table = GeneratedTable.Load(
            Root + "fairies_woods_discovered.tsv",
            new GeneratedTableSchema(
                "Fairies' Woods discovered fairies",
                GeneratedTableKeySemantics.Unique,
                ["index", "y", "x", "palette", "animation", "source"],
                ["index"],
                headerRequired: true));
        var result = new List<FairiesWoodsDiscoveredRecord>();
        foreach (GeneratedTableRow row in table.Rows)
        {
            int index = row.UnsignedDecimal(0);
            if (index != result.Count)
                throw row.Invalid(0, $"sequential discovered-fairy index {result.Count}");
            result.Add(new FairiesWoodsDiscoveredRecord(
                index,
                row.HexByte(1),
                row.HexByte(2),
                row.UnsignedDecimal(3),
                row.RequiredString(4),
                row.RequiredString(5)));
        }
        return result.AsReadOnly();
    }

    private void LoadTexts()
    {
        GeneratedTable table = GeneratedTable.Load(
            Root + "fairies_woods_text.tsv",
            new GeneratedTableSchema(
                "Fairies' Woods text",
                GeneratedTableKeySemantics.Unique,
                ["text-id", "text-base64"],
                ["text-id"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int textId = row.HexWord(0);
            _texts.Add(textId, new FairiesWoodsTextRecord(
                textId, row.Base64Utf8(1)));
        }
    }

    private void LoadHiddenSpots()
    {
        GeneratedTable table = GeneratedTable.Load(
            Root + "fairies_woods_hidden_spots.tsv",
            new GeneratedTableSchema(
                "Fairies' Woods hidden spots",
                GeneratedTableKeySemantics.Unique,
                ["room", "packed-position", "fairy-index", "source"],
                ["room"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            var record = new FairiesWoodsHiddenSpotRecord(
                row.HexByte(0),
                row.HexByte(1),
                row.HexByte(2),
                row.RequiredString(3));
            _hiddenSpots.Add(record.Room, record);
        }
    }

    private void Validate()
    {
        if (Event is not
            {
                Group: 0,
                StartRoom: 0x82,
                ExitRoom: 0x92,
                ResetRoom: 0x93,
                EssenceTreasure: TreasureDatabase.TreasureEssence,
                ActiveAddress: 0xcfd0,
                FoundAddress: 0xcfd1,
                SignalAddress: 0xcfd2,
                CompletionFlag: OracleSaveData.GlobalFlagWonFairyHidingGame,
                UnscrambledFlag: OracleSaveData.GlobalFlagForestUnscrambled,
                HiddenDelay: 12,
                ExitY: 0x28,
                ExitX: 0x9f,
                ExitRadiusY: 0x20,
                ExitRadiusX: 0x01,
                MagicSound: OracleSoundEngine.SndMagicPowder,
                PuffSound: OracleSoundEngine.SndPoof,
                MysterySound: OracleSoundEngine.SndMysterySeed,
                NormalFadeOut: 32,
                NormalFadeIn: 33,
                FastFadeIn: 11,
                CompletionHold: 12,
                DelayedFadeIn: 257,
                NormalFadeSpeed: 1,
                FastFadeSpeed: 3,
                DelayedFadeRefill: 8
            } ||
            Movements.Count != 22 ||
            Velocities.Count != 32 ||
            Velocities[0] is not { YFixed: -512, XFixed: 0 } ||
            Velocities[8] is not { YFixed: 0, XFixed: 512 } ||
            Velocities[16] is not { YFixed: 512, XFixed: 0 } ||
            Velocities[24] is not { YFixed: 0, XFixed: -512 } ||
            HidingRooms.Count != 3 ||
            HidingRooms[0] is not { Room: 0x81, Preset: 0x0c } ||
            HidingRooms[1] is not { Room: 0x80, Preset: 0x0d } ||
            HidingRooms[2] is not { Room: 0x91, Preset: 0x0e } ||
            DiscoveredFairies.Count != 3 ||
            DiscoveredFairies[0] is not
                { Y: 0x48, X: 0x38, Palette: 1 } ||
            DiscoveredFairies[1] is not
                { Y: 0x48, X: 0x68, Palette: 2 } ||
            DiscoveredFairies[2] is not
                { Y: 0x28, X: 0x50, Palette: 3 } ||
            _texts.Count != 13 ||
            _hiddenSpots.Count != 3 ||
            IntroCommands.Count != 17 ||
            RevealCommands.Count != 6 ||
            ExitCommands.Count != 9 ||
            IntroCommands[0] is not CutsceneNativeYieldCommand
                { Handler: "SpawnForestFairy:0" } ||
            IntroCommands[16] is not CutsceneEndCommand ||
            RevealCommands[2] is not CutsceneNativeYieldCommand
                { Handler: "ShowFairyFoundText" } ||
            ExitCommands[2] is not CutsceneNativeBlockingCommand
                { Handler: "WaitForExitCollision" } ||
            ExitCommands[8] is not CutsceneEndCommand)
        {
            throw new InvalidOperationException(
                "Fairies' Woods imported data diverges from interactions $6c/$49.");
        }
    }
}

internal readonly record struct FairiesWoodsEventRecord(
    int Group,
    int StartRoom,
    int ExitRoom,
    int ResetRoom,
    int EssenceTreasure,
    int ActiveAddress,
    int FoundAddress,
    int SignalAddress,
    int CompletionFlag,
    int UnscrambledFlag,
    int HiddenDelay,
    int ExitY,
    int ExitX,
    int ExitRadiusY,
    int ExitRadiusX,
    int MagicSound,
    int PuffSound,
    int MysterySound,
    int NormalFadeOut,
    int NormalFadeIn,
    int FastFadeIn,
    int CompletionHold,
    int DelayedFadeIn,
    int NormalFadeSpeed,
    int FastFadeSpeed,
    int DelayedFadeRefill,
    string FairySprite,
    int FairyTileBase,
    string Animation0,
    string Animation1,
    string SparkleSprite,
    int SparkleTileBase,
    int SparklePalette,
    string SparkleAnimation);

internal readonly record struct FairiesWoodsMovementRecord(
    int Index,
    int InitialY,
    int InitialX,
    int Angle,
    int Counter,
    int TargetY,
    int TargetX,
    int Direction,
    int Palette,
    string Source);

internal readonly record struct FairiesWoodsVelocityRecord(
    int Angle,
    int YFixed,
    int XFixed,
    string Source);

internal readonly record struct FairiesWoodsHidingRoomRecord(
    int Index,
    int Room,
    int Preset,
    string Source);

internal readonly record struct FairiesWoodsDiscoveredRecord(
    int Index,
    int Y,
    int X,
    int Palette,
    string Animation,
    string Source);

internal readonly record struct FairiesWoodsHiddenSpotRecord(
    int Room,
    int PackedPosition,
    int FairyIndex,
    string Source);

internal readonly record struct FairiesWoodsTextRecord(
    int TextId,
    string Message);
