
namespace Oracles;

/// <summary>
/// Imported room $0:$6c placements, special-object visuals, predicates, and
/// the four independently advancing interaction-script lanes.
/// </summary>
internal sealed class MooshRescueEventDatabase
{
    private const string Root = AgesGameContent.AssetRoot + "cutscenes/";

    internal MooshRescueEventRecord Record { get; }
    private readonly MooshDatabase _body = new(AgesGameContent.AssetRoot);
    internal CompanionVisualDatabase Visual => _body.Visual;
    internal IReadOnlyList<CutsceneCommand> Ghini0 { get; }
    internal IReadOnlyList<CutsceneCommand> Ghini1 { get; }
    internal IReadOnlyList<CutsceneCommand> Ghini2 { get; }
    internal IReadOnlyList<CutsceneCommand> Companion { get; }
    internal IReadOnlyList<MooshRescuePlacementRecord> Placements { get; }
    internal bool Matches(int group, int room) => group == Record.Group && room == Record.Room;

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

        Ghini0 = CutsceneCommandCatalog.Load(Root + "moosh_rescue_ghini0.tsv");
        Ghini1 = CutsceneCommandCatalog.Load(Root + "moosh_rescue_ghini1.tsv");
        Ghini2 = CutsceneCommandCatalog.Load(Root + "moosh_rescue_ghini2.tsv");
        Companion = CutsceneCommandCatalog.Load(Root + "moosh_rescue_companion.tsv");
        Placements = GeneratedTable.Load(Root + "moosh_rescue_placements.tsv",
            new GeneratedTableSchema("Moosh native placements", GeneratedTableKeySemantics.Ordered,
                ["order", "id", "subid", "y", "x", "source"], headerRequired: true)).Rows.Select(row =>
                    new MooshRescuePlacementRecord(row.UnsignedDecimal(0), row.HexByte(1), row.HexByte(2),
                        row.HexByte(3), row.HexByte(4), row.RequiredString(5))).ToArray();
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
                ShakeFrames: 60, EnemyId: EnemyId.Ghini, EnemySubId: 0,
                ExclamationId: 0x9f, ExclamationSubId: 0,
                ExclamationYOffset: -16, ExclamationXOffset: 0,
                ExclamationFrames: 30, DingSound: SoundId.SndDing,
                ExclamationSound: SoundId.SndClink,
                JumpSound: SoundId.SndJump,
                ChargeSound: SoundId.SndChargeSword,
                StompSound: SoundId.SndScentSeed,
                MinibossMusic: SoundId.MusMiniboss
            } ||
            string.IsNullOrWhiteSpace(Record.MooshAnimation) ||
            string.IsNullOrWhiteSpace(Record.ExclamationAnimation) ||
            Ghini0.Count != 12 || Ghini1.Count != 20 || Ghini2.Count != 12 ||
            !Placements.Select(p => (p.Order, p.Id, p.SubId, p.Y, p.X)).SequenceEqual(new[]
            { (0, 0x73, 0, 0x18, 0x68), (1, 0x73, 1, 0x18, 0x48), (2, 0x73, 2, 0x38, 0x58),
              (3, 0x71, 0, 0x28, 0x58), (4, 0x71, 2, 0x6d, 0x38), (5, 0x67, 0, 0, 0) }) ||
            Visual is not
            {
                Sprite: "spr_moosh", TileBase: 0, Palette: 1,
                Animations.Length: 27, LinkSprite: "spr_link",
                LinkPalette: 0, LinkFrames.Length: 51,
                LinkSourceOffsets.Length: 51
            } ||
            _body is not { WaterHazard: 1, WaterHoverFrames: 60, WaterExclamationZOffset: -32 } ||
            Visual.LinkSourceOffsets[0x1b] != 0x20c0 ||
            Visual.LinkSourceOffsets[0x1d] != 0x2100 ||
            Visual.LinkSourceOffsets[0x20] != 0x2180 ||
            Companion.Count != 42 ||
            Ghini0[1] is not CutsceneNativeCommand
                { Handler: "BeginGhini0Circle" } ||
            Ghini0[3] is not CutsceneWaitCommand { Frames: 1 } ||
            Ghini1[16] is not CutsceneSetMusicCommand
                { Music: SoundId.MusMiniboss } ||
            Ghini2[10] is not CutsceneNativeCommand
                { Handler: "SpawnEnemyGhini2" } ||
            Companion[8] is not CutsceneMemoryGateCommand
                { Binding: "RoomEnemyCount", Value: 0 } ||
            Companion[16] is not CutsceneMemoryGateCommand
                { Binding: "MooshButton", Value: 1 } ||
            Companion[31] is not CutsceneWriteMemoryCommand
                { Binding: "MooshStateOr", Value: 0x20 } ||
            Companion[15] is not CutsceneWriteMemoryCommand
                { Binding: "MooshVar03", Value: 2 } ||
            Companion[20] is not CutsceneWriteMemoryCommand
                { Binding: "MooshVar03", Value: 3 } ||
            Companion[33] is not CutsceneMemoryGateCommand
                { Binding: "MooshMounted", Value: 1 } ||
            Companion[34] is not CutsceneShowTextCommand { TextId: 0x2205 } ||
            Companion[35] is not CutsceneNativeCommand
                { Handler: "CompleteMooshRescue" } ||
            Companion[36] is not CutsceneEnableMenuCommand ||
            Companion[2] is not CutsceneWriteObjectByteCommand { Address: 0x7a, Value: 60 } ||
            Companion[17] is not CutsceneWriteObjectByteCommand { Address: 0x7a, Value: 60 } ||
            Companion[3] is not CutsceneCallCommand { TargetCommand: 38 } ||
            Companion[18] is not CutsceneCallCommand { TargetCommand: 38 } ||
            Companion[38] is not CutsceneMemoryBranchCommand { Binding: "ShakeCounter", Value: 0, TargetCommand: 41 } ||
            Companion[39] is not CutsceneWaitCommand { Frames: 1 } ||
            Companion[40] is not CutsceneBranchCommand { TargetCommand: 38 } ||
            Companion[41] is not CutsceneReturnCommand ||
            new[] { Ghini0, Ghini1, Ghini2, Companion }
                .SelectMany(commands => commands)
                .Any(command => command.Source.SourceLine <= 0))
        {
            throw new InvalidOperationException(
                "Room 0:6c Moosh rescue data diverges from the source contract.");
        }
    }
}

internal readonly record struct MooshRescuePlacementRecord(int Order, int Id, int SubId, int Y, int X, string Source);

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
