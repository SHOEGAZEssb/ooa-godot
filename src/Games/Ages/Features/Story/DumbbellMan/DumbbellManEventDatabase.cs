
namespace Oracles;

/// <summary>
/// Imported INTERAC_DUMBBELL_MAN metadata and dumbbellManScript command
/// stream for interior room $2:$e8.
/// </summary>
internal sealed class DumbbellManEventDatabase
{
    public TradeNpcEventRecord Record { get; }
    public IReadOnlyList<CutsceneCommand> Commands { get; }

    public DumbbellManEventDatabase()
    {
        Record = TradeNpcEventRecord.Load("dumbbell_man", "room 2:e8 Dumbbell Man event");
        ValidateRecord();

        Commands = CutsceneCommandCatalog.Load(
            AgesGameContent.AssetRoot + "cutscenes/dumbbell_man_commands.tsv");
        ValidateCommands();
    }

    private void ValidateRecord()
    {
        if (Record is not
            {
                Group: 2,
                Room: 0xe8,
                InteractionId: AgesInteractionId.DumbbellMan,
                SubId: 0,
                InitialAnimation: 0,
                CollisionRadiusY: 6,
                CollisionRadiusX: 6,
                RoomFlag: OracleSaveData.RoomFlagItem,
                RequiredTradeItem: 0x05,
                RewardTreasure: TreasureId.TradeItem,
                RewardParameter: 0x06,
                RewardObject: "TREASURE_OBJECT_TRADEITEM_06",
                InitialScriptUpdates: 1,
                AlwaysUpdate: true
            } ||
            string.IsNullOrWhiteSpace(Record.Animation0) ||
            string.IsNullOrWhiteSpace(Record.Animation1))
        {
            throw new InvalidOperationException(
                "Room 2:e8 Dumbbell Man metadata diverges from its source handlers.");
        }
    }

    private void ValidateCommands()
    {
        if (Commands.Count != 43 ||
            Commands[0] is not CutsceneRoomFlagBranchCommand { Flag: 0x20, TargetCommand: 3 } ||
            Commands[2] is not CutsceneBranchYieldCommand { TargetCommand: 4 } ||
            Commands[4] is not CutsceneInitCollisionsCommand { Actor: "DumbbellMan" } ||
            Commands[5] is not CutsceneCheckAButtonCommand { Actor: "DumbbellMan" } ||
            Commands[7] is not CutsceneRoomFlagBranchCommand { Flag: 0x20, TargetCommand: 39 } ||
            Commands[12] is not CutsceneTradeItemBranchCommand { Value: 0x05, TargetCommand: 15 } ||
            Commands[27] is not CutsceneTextOptionBranchCommand { Value: 0, TargetCommand: 31 } ||
            Commands[37] is not CutsceneSetAnimationCommand { Animation: 1 } ||
            Commands[38] is not CutsceneGiveItemCommand { TreasureId: TreasureId.TradeItem, Parameter: 0x06 } ||
            Commands[40] is not CutsceneWaitCommand { Frames: 30 } ||
            Commands[41] is not CutsceneEnableInputCommand ||
            Commands[42] is not CutsceneBranchYieldCommand { TargetCommand: 5 })
        {
            throw new InvalidOperationException(
                "dumbbellManScript command stream diverges from imported metadata.");
        }

        int[] expectedTextIds =
        [
            0x0b1d, 0x0b20, 0x0b1e, 0x0b20, 0x0b1f, 0x0b20,
            0x0b20, 0x0b21, 0x0b20, 0x0b22, 0x0b20, 0x0b23, 0x0b24
        ];
        int[] actualTextIds = Commands
            .OfType<CutsceneShowTextCommand>()
            .Select(command => command.TextId)
            .ToArray();
        if (!actualTextIds.SequenceEqual(expectedTextIds))
        {
            throw new InvalidOperationException(
                "dumbbellManScript text order diverges from TX_0b1d-$0b24.");
        }

        foreach (CutsceneCommand command in Commands)
        {
            if (command is CutsceneSetAnimationCommand animation &&
                animation.EncodedAnimation != Record.Animation(animation.Animation))
            {
                throw new InvalidOperationException(
                    $"Dumbbell Man animation ${animation.Animation:x2} diverges at " +
                    $"{animation.Source}.");
            }
        }
    }
}
