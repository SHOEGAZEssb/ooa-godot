using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>
/// Imported room $0:$6c placements, special-object visuals, predicates, and
/// the four independently advancing interaction-script lanes.
/// </summary>
internal sealed class MooshRescueEventDatabase
{
    private const string Root = "res://assets/oracle/cutscenes/";

    internal MooshRescueEventRecord Record { get; }
    internal MooshCompanionVisualRecord Visual { get; }
    internal IReadOnlyList<CutsceneCommand> Ghini0 { get; }
    internal IReadOnlyList<CutsceneCommand> Ghini1 { get; }
    internal IReadOnlyList<CutsceneCommand> Ghini2 { get; }
    internal IReadOnlyList<CutsceneCommand> Companion { get; }

    internal MooshRescueEventDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            Root + "moosh_rescue_event.tsv",
            new GeneratedTableSchema(
                "room 0:6c Moosh rescue",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "ghini-id",
                    "ghini0-y", "ghini0-x", "ghini1-y", "ghini1-x",
                    "ghini2-y", "ghini2-x", "controller-y", "controller-x",
                    "restrict-y", "restrict-x", "essence-address",
                    "essence-mask", "flag-group", "flag-room", "flag-mask",
                    "moosh-state-address", "active-mask", "rescued-mask",
                    "cheval-rope-treasure", "moosh-id", "moosh-y", "moosh-x",
                    "moosh-sprite", "moosh-tile-base", "moosh-palette",
                    "moosh-animation", "ghini-speed", "ghini-angle",
                    "ghini-frames", "shake-frames", "enemy-id", "enemy-subid",
                    "exclamation-id", "exclamation-subid", "exclamation-sprite",
                    "exclamation-tile-base", "exclamation-palette",
                    "exclamation-animation", "exclamation-y-offset",
                    "exclamation-x-offset", "exclamation-frames", "ding-sound",
                    "exclamation-sound", "jump-sound", "charge-sound",
                    "stomp-sound", "miniboss-music", "restrict-text-base64", "source"
                ],
                headerRequired: true)).SingleRow();
        Record = new MooshRescueEventRecord(
            row.Decimal(0, 0, 7), row.HexByte(1), row.HexByte(2),
            row.HexByte(3), row.HexByte(4), row.HexByte(5), row.HexByte(6),
            row.HexByte(7), row.HexByte(8), row.HexByte(9), row.HexByte(10),
            row.HexByte(11), row.HexByte(12), row.HexWord(13), row.HexByte(14),
            row.Decimal(15, 0, 7), row.HexByte(16), row.HexByte(17),
            row.HexWord(18), row.HexByte(19), row.HexByte(20), row.HexByte(21),
            row.HexByte(22), row.HexByte(23), row.HexByte(24),
            row.RequiredString(25), row.UnsignedDecimal(26),
            row.UnsignedDecimal(27), row.RequiredString(28), row.HexByte(29),
            row.HexByte(30), row.UnsignedDecimal(31), row.UnsignedDecimal(32),
            row.HexByte(33), row.HexByte(34), row.HexByte(35), row.HexByte(36),
            row.RequiredString(37), row.UnsignedDecimal(38),
            row.UnsignedDecimal(39), row.RequiredString(40),
            row.Decimal(41, -128, 127), row.Decimal(42, -128, 127),
            row.UnsignedDecimal(43), row.HexByte(44), row.HexByte(45),
            row.HexByte(46), row.HexByte(47), row.HexByte(48), row.HexByte(49),
            row.Base64Utf8(50), row.RequiredString(51));

        GeneratedTableRow visual = GeneratedTable.Load(
            Root + "moosh_companion_visual.tsv",
            new GeneratedTableSchema(
                "Moosh companion visual",
                GeneratedTableKeySemantics.Ordered,
                [
                    "sprite", "tile-base", "palette", "animations-base64",
                    "link-sprite", "link-palette", "link-frames-base64",
                    "link-source-offsets", "water-hazard",
                    "water-hover-frames", "water-exclamation-z-offset",
                    "water-exclamation-sound", "source"
                ],
                headerRequired: true)).SingleRow();
        Visual = new MooshCompanionVisualRecord(
            visual.RequiredString(0),
            visual.UnsignedDecimal(1),
            visual.UnsignedDecimal(2),
            visual.Base64Utf8(3).Split(
                '\n', StringSplitOptions.RemoveEmptyEntries),
            visual.RequiredString(4),
            visual.UnsignedDecimal(5),
            visual.Base64Utf8(6).Split(
                '\n', StringSplitOptions.RemoveEmptyEntries),
            visual.RequiredString(7).Split(',').Select(value =>
                Convert.ToInt32(value, 16)).ToArray(),
            visual.UnsignedDecimal(8),
            visual.UnsignedDecimal(9),
            visual.Decimal(10, -128, 127),
            visual.HexByte(11),
            visual.RequiredString(12));

        Ghini0 = CutsceneCommandCatalog.Load(Root + "moosh_rescue_ghini0.tsv");
        Ghini1 = CutsceneCommandCatalog.Load(Root + "moosh_rescue_ghini1.tsv");
        Ghini2 = CutsceneCommandCatalog.Load(Root + "moosh_rescue_ghini2.tsv");
        Companion = CutsceneCommandCatalog.Load(Root + "moosh_rescue_companion.tsv");
        Validate();
    }

    internal NpcRecord CreateMooshRecord() => new(
        Record.Group,
        Record.Room,
        Record.MooshId,
        0,
        Record.MooshY,
        Record.MooshX,
        0,
        0,
        Record.MooshSprite,
        Record.MooshTileBase,
        Record.MooshPalette,
        0,
        false,
        Record.MooshAnimation,
        Record.MooshAnimation,
        Record.MooshAnimation,
        Record.MooshAnimation,
        string.Empty,
        NpcImplementationClassification.EventOwned);

    internal NpcRecord CreateExclamationRecord(int y, int x) => new(
        Record.Group,
        Record.Room,
        Record.ExclamationId,
        Record.ExclamationSubId,
        y,
        x,
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
                Group: 0, Room: 0x6c, GhiniId: 0x73,
                Ghini0Y: 0x18, Ghini0X: 0x68,
                Ghini1Y: 0x18, Ghini1X: 0x48,
                Ghini2Y: 0x38, Ghini2X: 0x58,
                ControllerY: 0x28, ControllerX: 0x58,
                RestrictY: 0x6d, RestrictX: 0x38,
                EssenceAddress: 0xc6bf, EssenceMask: 0x02,
                FlagGroup: 1, FlagRoom: 0x79, FlagMask: 0x40,
                MooshStateAddress: 0xc648, ActiveMask: 0x60,
                RescuedMask: 0x20, ChevalRopeTreasure: 0x52,
                MooshId: 0x0d, MooshY: 0x28, MooshX: 0x58,
                MooshSprite: "spr_moosh", MooshTileBase: 0, MooshPalette: 1,
                GhiniSpeed: 0x32, GhiniAngle: 0x18, GhiniFrames: 32,
                ShakeFrames: 60, EnemyId: 0x17, EnemySubId: 0,
                ExclamationId: 0x9f, ExclamationSubId: 0,
                ExclamationYOffset: -16, ExclamationXOffset: 0,
                ExclamationFrames: 30, DingSound: OracleSoundEngine.SndDing,
                ExclamationSound: OracleSoundEngine.SndClink,
                JumpSound: OracleSoundEngine.SndJump,
                ChargeSound: OracleSoundEngine.SndChargeSword,
                StompSound: OracleSoundEngine.SndScentSeed,
                MinibossMusic: OracleSoundEngine.MusMiniboss
            } ||
            string.IsNullOrWhiteSpace(Record.MooshAnimation) ||
            string.IsNullOrWhiteSpace(Record.ExclamationAnimation) ||
            Ghini0.Count != 8 || Ghini1.Count != 12 || Ghini2.Count != 7 ||
            Visual is not
            {
                Sprite: "spr_moosh", TileBase: 0, Palette: 1,
                Animations.Length: 27, LinkSprite: "spr_link",
                LinkPalette: 0, LinkFrames.Length: 51,
                LinkSourceOffsets.Length: 51,
                WaterHazard: 1, WaterHoverFrames: 60,
                WaterExclamationZOffset: -32,
                WaterExclamationSound: OracleSoundEngine.SndClink
            } ||
            Visual.LinkSourceOffsets[0x1b] != 0x20c0 ||
            Visual.LinkSourceOffsets[0x1d] != 0x2100 ||
            Visual.LinkSourceOffsets[0x20] != 0x2180 ||
            Companion.Count != 32 ||
            Ghini0[1] is not CutsceneNativeBlockingCommand
                { Handler: "CircleGhini", Frames: 32 } ||
            Ghini1[8] is not CutsceneSetMusicCommand
                { Music: OracleSoundEngine.MusMiniboss } ||
            Ghini2[5] is not CutsceneNativeCommand
                { Handler: "SpawnEnemyGhini2" } ||
            Companion[5] is not CutsceneMemoryGateCommand
                { Binding: "RoomEnemyCount", Value: 0 } ||
            Companion[13] is not CutsceneMemoryGateCommand
                { Binding: "MooshTalked", Value: 1 } ||
            Companion[25] is not CutsceneWriteMemoryCommand
                { Binding: "MooshStateOr", Value: 0x20 } ||
            Companion[27] is not CutsceneNativeCommand
                { Handler: "BeginMooshMount" } ||
            Companion[28] is not CutsceneMemoryGateCommand
                { Binding: "MooshMounted", Value: 1 } ||
            Companion[29] is not CutsceneShowTextCommand { TextId: 0x2205 } ||
            Companion[30] is not CutsceneNativeCommand
                { Handler: "CompleteMooshRescue" } ||
            new[] { Ghini0, Ghini1, Ghini2, Companion }
                .SelectMany(commands => commands)
                .Any(command => command.Source.SourceLine <= 0))
        {
            throw new InvalidOperationException(
                "Room 0:6c Moosh rescue data diverges from the source contract.");
        }
    }
}

internal readonly record struct MooshRescueEventRecord(
    int Group,
    int Room,
    int GhiniId,
    int Ghini0Y,
    int Ghini0X,
    int Ghini1Y,
    int Ghini1X,
    int Ghini2Y,
    int Ghini2X,
    int ControllerY,
    int ControllerX,
    int RestrictY,
    int RestrictX,
    int EssenceAddress,
    int EssenceMask,
    int FlagGroup,
    int FlagRoom,
    int FlagMask,
    int MooshStateAddress,
    int ActiveMask,
    int RescuedMask,
    int ChevalRopeTreasure,
    int MooshId,
    int MooshY,
    int MooshX,
    string MooshSprite,
    int MooshTileBase,
    int MooshPalette,
    string MooshAnimation,
    int GhiniSpeed,
    int GhiniAngle,
    int GhiniFrames,
    int ShakeFrames,
    int EnemyId,
    int EnemySubId,
    int ExclamationId,
    int ExclamationSubId,
    string ExclamationSprite,
    int ExclamationTileBase,
    int ExclamationPalette,
    string ExclamationAnimation,
    int ExclamationYOffset,
    int ExclamationXOffset,
    int ExclamationFrames,
    int DingSound,
    int ExclamationSound,
    int JumpSound,
    int ChargeSound,
    int StompSound,
    int MinibossMusic,
    string RestrictText,
    string Source);

internal readonly record struct MooshCompanionVisualRecord(
    string Sprite,
    int TileBase,
    int Palette,
    string[] Animations,
    string LinkSprite,
    int LinkPalette,
    string[] LinkFrames,
    int[] LinkSourceOffsets,
    int WaterHazard,
    int WaterHoverFrames,
    int WaterExclamationZOffset,
    int WaterExclamationSound,
    string Source);
