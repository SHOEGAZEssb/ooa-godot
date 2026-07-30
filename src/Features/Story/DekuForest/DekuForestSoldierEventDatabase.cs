using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Importer-owned predicate, visuals, script, and hardcoded warp for the red
/// soldier allocated by roomSpecificCode3 in past room $1:$81.
/// </summary>
internal sealed class DekuForestSoldierEventDatabase
{
    private const string Root = "res://assets/oracle/cutscenes/";

    internal DekuForestSoldierEventRecord Record { get; }
    internal IReadOnlyList<CutsceneCommand> Commands { get; }

    internal DekuForestSoldierEventDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            Root + "deku_forest_soldier_event.tsv",
            new GeneratedTableSchema(
                "room 1:81 Deku Forest soldier event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "id", "subid", "trigger-treasure",
                    "room-flag", "trigger-y", "initial-y", "initial-x",
                    "palette", "initial-animation", "sprite", "tile-base",
                    "animation-0", "animation-1", "animation-2", "animation-3",
                    "slow-speed", "fast-speed", "effect-id", "effect-subid",
                    "effect-sprite", "effect-tile-base", "effect-palette",
                    "effect-animation", "effect-y-offset", "effect-x-offset",
                    "effect-frames", "clink-sound", "destination-group",
                    "destination-room", "destination-position",
                    "destination-parameter", "source-transition",
                    "destination-transition", "text-id", "text-base64", "source"
                ],
                headerRequired: true)).SingleRow();
        Record = new DekuForestSoldierEventRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.HexByte(4),
            row.HexByte(5),
            row.HexByte(6),
            row.HexByte(7),
            row.HexByte(8),
            row.UnsignedDecimal(9),
            row.UnsignedDecimal(10),
            row.RequiredString(11),
            row.UnsignedDecimal(12),
            row.RequiredString(13),
            row.RequiredString(14),
            row.RequiredString(15),
            row.RequiredString(16),
            row.HexByte(17),
            row.HexByte(18),
            row.HexByte(19),
            row.HexByte(20),
            row.RequiredString(21),
            row.UnsignedDecimal(22),
            row.UnsignedDecimal(23),
            row.RequiredString(24),
            row.Decimal(25),
            row.Decimal(26),
            row.UnsignedDecimal(27),
            row.HexByte(28),
            row.Decimal(29, 0, 7),
            row.HexByte(30),
            row.HexByte(31),
            row.HexByte(32),
            row.HexByte(33),
            row.HexByte(34),
            row.HexWord(35),
            row.Base64Utf8(36),
            row.RequiredString(37));
        Commands = CutsceneCommandCatalog.Load(
            Root + "deku_forest_soldier_commands.tsv");
        Validate();
    }

    internal NpcRecord CreateSoldierRecord() => new(
        Record.Group,
        Record.Room,
        Record.InteractionId,
        Record.SubId,
        Record.InitialY,
        Record.InitialX,
        0,
        0,
        Record.Sprite,
        Record.TileBase,
        Record.Palette,
        Record.InitialAnimation,
        false,
        Record.Animation0,
        Record.Animation1,
        Record.Animation2,
        Record.Animation3,
        string.Empty,
        NpcImplementationClassification.EventOwned);

    internal NpcRecord CreateExclamationRecord(int y, int x) => new(
        Record.Group,
        Record.Room,
        Record.EffectId,
        Record.EffectSubId,
        y,
        x,
        0,
        0,
        Record.EffectSprite,
        Record.EffectTileBase,
        Record.EffectPalette,
        0,
        false,
        Record.EffectAnimation,
        Record.EffectAnimation,
        Record.EffectAnimation,
        Record.EffectAnimation,
        string.Empty,
        NpcImplementationClassification.EventOwned);

    internal Warp CreateWarp() => new(
        Record.Group,
        Record.Room,
        -1,
        0,
        Record.SourceTransition,
        Record.DestinationGroup,
        Record.DestinationRoom,
        Record.DestinationPosition,
        Record.DestinationParameter,
        Record.DestinationTransition);

    private void Validate()
    {
        if (Record is not
            {
                Group: 1,
                Room: 0x81,
                InteractionId: 0x40,
                SubId: 0x0a,
                TriggerTreasure: 0x24,
                RoomFlag: OracleSaveData.RoomFlag40,
                TriggerY: 0x2a,
                InitialY: 0x68,
                InitialX: 0xf0,
                Palette: 2,
                InitialAnimation: 2,
                Sprite: "spr_soldier",
                TileBase: 0,
                SlowSpeed: 0x1e,
                FastSpeed: 0x3c,
                EffectId: 0x9f,
                EffectSubId: 0,
                EffectY: -13,
                EffectX: 0,
                EffectFrames: 0x28,
                ClinkSound: OracleSoundEngine.SndClink,
                DestinationGroup: 1,
                DestinationRoom: 0x46,
                DestinationPosition: 0x34,
                DestinationParameter: 0,
                SourceTransition: 0,
                DestinationTransition: 3,
                TextId: 0x590b
            } ||
            string.IsNullOrEmpty(Record.Animation0) ||
            string.IsNullOrEmpty(Record.Animation1) ||
            string.IsNullOrEmpty(Record.Animation2) ||
            string.IsNullOrEmpty(Record.Animation3) ||
            string.IsNullOrEmpty(Record.EffectAnimation) ||
            Commands.Count != 20 ||
            Commands[0] is not CutsceneMemoryGateCommand
                { Binding: "PlayerY", Value: 0x2a } ||
            Commands[1] is not CutsceneNativeCommand
                { Handler: "ObjectSetVisible82" } ||
            Commands[2] is not CutsceneNativeCommand
                { Handler: "DropLinkHeldItem" } ||
            Commands[3] is not CutsceneWriteMemoryCommand
                { Binding: "DisabledObjects", Value: 0x01 } ||
            Commands[4] is not CutsceneDisableMenuCommand ||
            Commands[5] is not CutsceneWaitCommand { Frames: 30 } ||
            Commands[6] is not CutsceneSetSpeedCommand
                { Actor: "Soldier", Speed: var slowSpeed } ||
            slowSpeed != Record.SlowSpeed ||
            Commands[7] is not CutsceneMoveCommand
            {
                Actor: "Soldier",
                Angle: 0x08,
                Counter: 0x4b,
                EncodedAnimation: var rightAnimation
            } ||
            rightAnimation != Record.Animation1 ||
            Commands[8] is not CutsceneWaitCommand { Frames: 6 } ||
            Commands[9] is not CutsceneSetAnimationCommand
            {
                Actor: "Soldier",
                Animation: 0,
                EncodedAnimation: var upAnimation
            } ||
            upAnimation != Record.Animation0 ||
            Commands[10] is not CutsceneWaitCommand { Frames: 20 } ||
            Commands[11] is not CutsceneNativeCommand
                { Handler: "CreateExclamationMark" } ||
            Commands[12] is not CutsceneWaitCommand { Frames: 60 } ||
            Commands[13] is not CutsceneSetSpeedCommand
                { Actor: "Soldier", Speed: var fastSpeed } ||
            fastSpeed != Record.FastSpeed ||
            Commands[14] is not CutsceneMoveCommand
            {
                Actor: "Soldier",
                Angle: 0,
                Counter: 0x1e,
                EncodedAnimation: var moveUpAnimation
            } ||
            moveUpAnimation != Record.Animation0 ||
            Commands[15] is not CutsceneWaitCommand { Frames: 30 } ||
            Commands[16] is not CutsceneShowTextCommand text ||
            text.TextId != Record.TextId ||
            text.Message != Record.Text ||
            Commands[17] is not CutsceneWaitCommand { Frames: 30 } ||
            Commands[18] is not CutsceneOrRoomFlagCommand
                { Flag: OracleSaveData.RoomFlag40 } ||
            Commands[19] is not CutsceneEndCommand)
        {
            throw new InvalidOperationException(
                "Room 1:81 Deku Forest soldier contract is incomplete.");
        }
    }
}

internal readonly record struct DekuForestSoldierEventRecord(
    int Group,
    int Room,
    int InteractionId,
    int SubId,
    int TriggerTreasure,
    int RoomFlag,
    int TriggerY,
    int InitialY,
    int InitialX,
    int Palette,
    int InitialAnimation,
    string Sprite,
    int TileBase,
    string Animation0,
    string Animation1,
    string Animation2,
    string Animation3,
    int SlowSpeed,
    int FastSpeed,
    int EffectId,
    int EffectSubId,
    string EffectSprite,
    int EffectTileBase,
    int EffectPalette,
    string EffectAnimation,
    int EffectY,
    int EffectX,
    int EffectFrames,
    int ClinkSound,
    int DestinationGroup,
    int DestinationRoom,
    int DestinationPosition,
    int DestinationParameter,
    int SourceTransition,
    int DestinationTransition,
    int TextId,
    string Text,
    string Source)
{
    internal string Animation(int index) => index switch
    {
        0 => Animation0,
        1 => Animation1,
        2 => Animation2,
        3 => Animation3,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
}
