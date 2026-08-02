using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>
/// Imported INTERAC_RALPH $37:$10 metadata and ralphSubid10Script for past
/// overworld room $1:$79.
/// </summary>
internal sealed class RalphAfterChevalEventDatabase
{
    private const string Root = "res://assets/oracle/cutscenes/";

    internal RalphAfterChevalEventRecord Record { get; }
    internal IReadOnlyList<CutsceneCommand> Commands { get; }

    internal RalphAfterChevalEventDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            Root + "ralph_after_cheval_event.tsv",
            new GeneratedTableSchema(
                "room 1:79 Ralph-after-Cheval event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "id", "subid", "sprite", "tile-base",
                    "palette", "animation0", "animation1", "animation2",
                    "animation3", "initial-animation", "room-flag",
                    "talked-global-flag", "warp-destination", "music",
                    "speed-200", "speed-100", "speed-080", "puff-id",
                    "puff-subid", "puff-y-offset", "puff-x-offset",
                    "facing-threshold", "disabled-objects", "menu-disabled",
                    "initial-native-updates"
                ],
                headerRequired: true)).SingleRow();
        Record = new RalphAfterChevalEventRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.RequiredString(4),
            row.HexByte(5),
            row.HexByte(6),
            row.RequiredString(7),
            row.RequiredString(8),
            row.RequiredString(9),
            row.RequiredString(10),
            row.HexByte(11),
            row.HexByte(12),
            row.HexByte(13),
            row.HexByte(14),
            row.HexByte(15),
            row.HexByte(16),
            row.HexByte(17),
            row.HexByte(18),
            row.HexByte(19),
            row.HexByte(20),
            row.HexByte(21),
            row.HexByte(22),
            row.HexByte(23),
            row.HexByte(24),
            row.HexByte(25),
            row.UnsignedDecimal(26));
        Commands = CutsceneCommandCatalog.Load(
            Root + "ralph_after_cheval_commands.tsv");
        Validate();
    }

    internal bool Matches(NpcRecord record) =>
        record.Group == Record.Group &&
        record.Room == Record.Room &&
        record.Id == Record.InteractionId &&
        record.SubId == Record.SubId;

    private void Validate()
    {
        if (Record is not
            {
                Group: 1,
                Room: 0x79,
                InteractionId: 0x37,
                SubId: 0x10,
                Sprite: "spr_ralph_1",
                TileBase: 0,
                Palette: 1,
                InitialAnimation: 2,
                RoomFlag: OracleSaveData.RoomFlag40,
                TalkedGlobalFlag: OracleSaveData.GlobalFlagTalkedToCheval,
                WarpDestination: 0x17,
                Music: 0x35,
                Speed200: 0x50,
                Speed100: 0x28,
                Speed080: 0x14,
                PuffId: 0x05,
                PuffSubId: 0x81,
                PuffYOffset: 0x08,
                PuffXOffset: 0x04,
                FacingThreshold: 0x0c,
                DisabledObjects: 0x81,
                MenuDisabled: 0x81,
                InitialNativeUpdates: 1
            } ||
            Enumerable.Range(0, 4).Any(animation =>
                string.IsNullOrWhiteSpace(Record.Animation(animation))))
        {
            throw new InvalidOperationException(
                "Room 1:79 Ralph metadata diverges from its source contract.");
        }

        if (Commands.Count != 22 ||
            Commands[0] is not CutsceneWaitCommand { Frames: 90 } ||
            Commands[1] is not CutsceneSetMusicCommand { Music: 0x35 } ||
            Commands[2] is not CutsceneNativeYieldCommand
                { Handler: "ToggleFacingBit" } ||
            Commands[3] is not CutsceneSetSpeedCommand
                { Actor: "Ralph", Speed: 0x50 } ||
            Commands[4] is not CutsceneMoveCommand
                { Actor: "Ralph", Angle: 0x00, Counter: 0x18 } ||
            Commands[5] is not CutsceneNativeYieldCommand
                { Handler: "IncrementSubstate" } ||
            Commands[6] is not CutsceneSetSpeedCommand { Speed: 0x28 } ||
            Commands[7] is not CutsceneMoveCommand
                { Angle: 0x00, Counter: 0x20 } ||
            Commands[8] is not CutsceneSetSpeedCommand { Speed: 0x14 } ||
            Commands[9] is not CutsceneMoveCommand
                { Angle: 0x00, Counter: 0x20 } ||
            Commands[10] is not CutsceneNativeYieldCommand
                { Handler: "IncrementSubstate" } ||
            Commands[11] is not CutsceneWaitCommand { Frames: 30 } ||
            Commands[12] is not CutsceneShowTextCommand { TextId: 0x2a20 } ||
            Commands[13] is not CutsceneWaitCommand { Frames: 30 } ||
            Commands[14] is not CutsceneSetSpeedCommand { Speed: 0x50 } ||
            Commands[15] is not CutsceneNativeYieldCommand
                { Handler: "SetSubstate0" } ||
            Commands[16] is not CutsceneMoveCommand
                { Angle: 0x10, Counter: 0x38 } ||
            Commands[17] is not CutsceneOrRoomFlagCommand { Flag: 0x40 } ||
            Commands[18] is not CutsceneWaitCommand { Frames: 30 } ||
            Commands[19] is not CutsceneNativeYieldCommand
                { Handler: "ResetMusic" } ||
            Commands[20] is not CutsceneEnableInputCommand ||
            Commands[21] is not CutsceneEndCommand)
        {
            throw new InvalidOperationException(
                "ralphSubid10Script command stream diverges from imported metadata.");
        }

        CutsceneShowTextCommand text = (CutsceneShowTextCommand)Commands[12];
        if (string.IsNullOrWhiteSpace(text.Message))
        {
            throw new InvalidOperationException(
                "Ralph-after-Cheval TX_2a20 has no imported payload.");
        }
    }
}

internal readonly record struct RalphAfterChevalEventRecord(
    int Group,
    int Room,
    int InteractionId,
    int SubId,
    string Sprite,
    int TileBase,
    int Palette,
    string Animation0,
    string Animation1,
    string Animation2,
    string Animation3,
    int InitialAnimation,
    int RoomFlag,
    int TalkedGlobalFlag,
    int WarpDestination,
    int Music,
    int Speed200,
    int Speed100,
    int Speed080,
    int PuffId,
    int PuffSubId,
    int PuffYOffset,
    int PuffXOffset,
    int FacingThreshold,
    int DisabledObjects,
    int MenuDisabled,
    int InitialNativeUpdates)
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
