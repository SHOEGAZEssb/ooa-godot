using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>
/// Imported INTERAC_RAFTON metadata, text, and the independent scripts used
/// by the two halves of Rafton's house in rooms $2:$1e and $2:$1f.
/// </summary>
internal sealed class RaftonEventDatabase
{
    private const string Root = "res://assets/oracle/cutscenes/";
    private readonly Dictionary<int, string> _texts = new();

    internal RaftonEventRecord Record { get; }
    internal IReadOnlyList<CutsceneCommand> LeftCommands { get; }
    internal IReadOnlyList<CutsceneCommand> RightCommands { get; }

    internal RaftonEventDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            Root + "rafton_event.tsv",
            new GeneratedTableSchema(
                "rooms 2:1e/2:1f Rafton event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "left-room", "right-room", "id", "left-subid",
                    "right-subid", "animation0", "animation1", "animation2",
                    "animation3", "initial-animation", "collision-y",
                    "collision-x", "room-flag", "gave-rope-flag",
                    "changed-rooms-flag", "cheval-rope-treasure",
                    "island-chart-treasure", "d2-essence-mask",
                    "d3-essence-mask", "required-trade", "reward-treasure",
                    "reward-parameter", "reward-object", "speed", "right-angle",
                    "move-counter", "anim-counter-address", "freeze-counter",
                    "effect-id", "effect-subid", "effect-sprite",
                    "effect-tile-base", "effect-palette", "effect-animation",
                    "effect-y", "effect-x", "effect-frames", "clink-sound",
                    "initial-script-updates"
                ],
                headerRequired: true)).SingleRow();
        Record = new RaftonEventRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.HexByte(4),
            row.HexByte(5),
            row.RequiredString(6),
            row.RequiredString(7),
            row.RequiredString(8),
            row.RequiredString(9),
            row.HexByte(10),
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
            row.RequiredString(23),
            row.HexByte(24),
            row.HexByte(25),
            row.HexByte(26),
            row.HexByte(27),
            row.HexByte(28),
            row.HexByte(29),
            row.HexByte(30),
            row.RequiredString(31),
            row.UnsignedDecimal(32),
            row.UnsignedDecimal(33),
            row.RequiredString(34),
            row.Decimal(35, -128, 127),
            row.Decimal(36, -128, 127),
            row.UnsignedDecimal(37),
            row.HexByte(38),
            row.UnsignedDecimal(39));

        LoadTexts();
        LeftCommands = CutsceneCommandCatalog.Load(
            Root + "rafton_left_commands.tsv");
        RightCommands = CutsceneCommandCatalog.Load(
            Root + "rafton_right_commands.tsv");
        Validate();
    }

    internal bool Matches(NpcRecord record) =>
        record.Group == Record.Group &&
        record.Id == Record.InteractionId &&
        (record.Room == Record.LeftRoom && record.SubId == Record.LeftSubId ||
         record.Room == Record.RightRoom && record.SubId == Record.RightSubId);

    internal string Text(int textId) =>
        _texts.TryGetValue(textId, out string? text)
            ? text
            : throw new InvalidOperationException(
                $"INTERAC_RAFTON requested unimported TX_{textId:x4}.");

    internal string DialogueText(int textId)
    {
        string text = Text(textId);
        if (textId != 0x270a)
            return text;

        const string jump = @"\jump(TX_2709)";
        if (!text.Contains(jump, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Rafton TX_270a lost its source jump to TX_2709.");
        }
        return text.Replace(jump, Text(0x2709), StringComparison.Ordinal);
    }

    internal NpcRecord CreateExclamationRecord(int y, int x) => new(
        Record.Group,
        Record.LeftRoom,
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

    private void LoadTexts()
    {
        GeneratedTable table = GeneratedTable.Load(
            Root + "rafton_text.tsv",
            new GeneratedTableSchema(
                "Rafton text",
                GeneratedTableKeySemantics.Unique,
                ["id", "text-base64"],
                ["id"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
            _texts.Add(row.HexWord(0), row.Base64Utf8(1));
    }

    private void Validate()
    {
        if (Record is not
            {
                Group: 2,
                LeftRoom: 0x1e,
                RightRoom: 0x1f,
                InteractionId: 0x69,
                LeftSubId: 0,
                RightSubId: 1,
                InitialAnimation: 2,
                CollisionRadiusY: 6,
                CollisionRadiusX: 6,
                RoomFlag: OracleSaveData.RoomFlagItem,
                GaveRopeFlag: 0x15,
                ChangedRoomsFlag: 0x26,
                ChevalRopeTreasure: 0x52,
                IslandChartTreasure: 0x54,
                D2EssenceMask: 0x02,
                D3EssenceMask: 0x04,
                RequiredTradeItem: 0x09,
                RewardTreasure: TreasureDatabase.TreasureTradeItem,
                RewardParameter: 0x0a,
                RewardObject: "TREASURE_OBJECT_TRADEITEM_0a",
                Speed: 0x28,
                RightAngle: 0x08,
                MoveCounter: 0x40,
                AnimationCounterAddress: 0x20,
                FreezeCounter: 0x7f,
                EffectId: 0x9f,
                EffectSubId: 0,
                EffectY: -13,
                EffectX: 0,
                EffectFrames: 60,
                ClinkSound: OracleSoundEngine.SndClink,
                InitialScriptUpdates: 0
            } ||
            Enumerable.Range(0, 4).Any(index =>
                string.IsNullOrWhiteSpace(Record.Animation(index))) ||
            string.IsNullOrWhiteSpace(Record.EffectAnimation) ||
            _texts.Count != 16)
        {
            throw new InvalidOperationException(
                "Rooms 2:1e/2:1f Rafton metadata diverges from the source contract.");
        }

        int[] expectedTextIds =
        [
            0x2700, 0x2701, 0x2702, 0x2703, 0x2704, 0x2705, 0x2706,
            0x2707, 0x2708, 0x2709, 0x270a, 0x2710, 0x2711, 0x2712, 0x2713, 0x2714
        ];
        if (!_texts.Keys.Order().SequenceEqual(expectedTextIds))
        {
            throw new InvalidOperationException(
                "Rafton text coverage diverges from TX_2700-$2714 source usage.");
        }

        if (LeftCommands.Count != 53 ||
            LeftCommands[0] is not CutsceneInitCollisionsCommand
                { Actor: "Rafton" } ||
            LeftCommands[1] is not CutsceneMemoryJumpTableCommand leftTable ||
            leftTable.Binding != "RaftonBehaviour" ||
            !leftTable.TargetCommands.SequenceEqual([2, 9, 15, 37, 39]) ||
            LeftCommands[18] is not CutsceneNativeCommand
                { Handler: "CreateExclamationMark" } ||
            LeftCommands[23] is not CutsceneTextOptionBranchCommand
                { Value: 0, TargetCommand: 31 } ||
            LeftCommands[31] is not CutsceneNativeCommand
                { Handler: "LoseChevalRope" } ||
            LeftCommands[35] is not CutsceneSetGlobalFlagCommand
                { Flag: 0x15 } ||
            LeftCommands[41] is not CutsceneWriteObjectByteCommand
                { Actor: "Rafton", Address: 0x20, Value: 0x7f } ||
            LeftCommands[45] is not CutsceneMoveCommand
            {
                Actor: "Rafton", Angle: 0x08, Counter: 0x40
            } ||
            LeftCommands[46] is not CutsceneSetGlobalFlagCommand
                { Flag: 0x26 } ||
            LeftCommands[48] is not CutsceneEndCommand ||
            LeftCommands[52] is not CutsceneReturnCommand)
        {
            throw new InvalidOperationException(
                "rafton_subid00Script command stream diverges from imported metadata.");
        }

        if (RightCommands.Count != 30 ||
            RightCommands[0] is not CutsceneInitCollisionsCommand
                { Actor: "Rafton" } ||
            RightCommands[1] is not CutsceneNativeCommand
                { Handler: "CheckD3Essence" } ||
            RightCommands[2] is not CutsceneMemoryBranchYieldOnMissCommand
            {
                Binding: "D3EssenceObtained", Value: 1, TargetCommand: 11
            } ||
            RightCommands[13] is not CutsceneRoomFlagBranchCommand
                { Flag: OracleSaveData.RoomFlagItem, TargetCommand: 27 } ||
            RightCommands[16] is not CutsceneTradeItemBranchCommand
                { Value: 0x09, TargetCommand: 18 } ||
            RightCommands[20] is not CutsceneTextOptionBranchCommand
                { Value: 0, TargetCommand: 23 } ||
            RightCommands[25] is not CutsceneGiveItemCommand
            {
                TreasureId: TreasureDatabase.TreasureTradeItem,
                Parameter: 0x0a
            } ||
            RightCommands[29] is not CutsceneBranchCommand
                { TargetCommand: 11 })
        {
            throw new InvalidOperationException(
                "rafton_subid01Script command stream diverges from imported metadata.");
        }

        foreach (CutsceneCommand command in LeftCommands.Concat(RightCommands))
        {
            if (command is CutsceneSetAnimationCommand animation &&
                animation.EncodedAnimation != Record.Animation(animation.Animation))
            {
                throw new InvalidOperationException(
                    $"Rafton animation ${animation.Animation:x2} diverges at {animation.Source}.");
            }
            if (command is CutsceneMoveCommand movement &&
                movement.EncodedAnimation != Record.Animation(1))
            {
                throw new InvalidOperationException(
                    $"Rafton movement animation diverges at {movement.Source}.");
            }
        }
    }
}

internal readonly record struct RaftonEventRecord(
    int Group,
    int LeftRoom,
    int RightRoom,
    int InteractionId,
    int LeftSubId,
    int RightSubId,
    string Animation0,
    string Animation1,
    string Animation2,
    string Animation3,
    int InitialAnimation,
    int CollisionRadiusY,
    int CollisionRadiusX,
    int RoomFlag,
    int GaveRopeFlag,
    int ChangedRoomsFlag,
    int ChevalRopeTreasure,
    int IslandChartTreasure,
    int D2EssenceMask,
    int D3EssenceMask,
    int RequiredTradeItem,
    int RewardTreasure,
    int RewardParameter,
    string RewardObject,
    int Speed,
    int RightAngle,
    int MoveCounter,
    int AnimationCounterAddress,
    int FreezeCounter,
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
    int InitialScriptUpdates)
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
