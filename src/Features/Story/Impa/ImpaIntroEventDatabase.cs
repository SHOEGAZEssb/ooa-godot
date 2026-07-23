using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Imported records for the first possessed-Impa encounter in present room
/// $6a, including linkCutscene1 and objectData.impaOctoroks.
/// </summary>
public sealed class ImpaIntroEventDatabase
{
    public ImpaIntroEventRecord Record { get; }
    public ImpaHelpEventRecord HelpRecord { get; }
    public ImpaStoneEventRecord StoneRecord { get; }
    public IReadOnlyList<FakeOctorokRecord> Octoroks { get; }
    public Color[] PossessedPalette { get; }
    public Color[] StonePalette { get; }
    internal IReadOnlyList<CutsceneCommand> EncounterCommands { get; }
    internal IReadOnlyList<CutsceneCommand> HelpCommands { get; }
    internal IReadOnlyList<CutsceneCommand> StonePrePushCommands { get; }
    internal IReadOnlyList<CutsceneCommand> StonePostPushCommands { get; }

    public ImpaIntroEventDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/impa_intro_event.tsv",
            new GeneratedTableSchema(
                "Impa intro event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "id", "subid", "room-flag", "link-wait", "target-x",
                    "center-wait", "approach-frames", "link-speed", "impa-wait", "text-id",
                    "post-text", "impa-speed", "impa-move-frames", "follow-lag", "up-animation",
                    "right-animation", "down-animation", "left-animation", "text-base64",
                    "linked-text-id", "linked-text-base64"
                ],
                headerRequired: true)).SingleRow();

        Record = new ImpaIntroEventRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.HexByte(4),
            row.UnsignedDecimal(5),
            row.UnsignedDecimal(6),
            row.UnsignedDecimal(7),
            row.UnsignedDecimal(8),
            row.HexByte(9),
            row.UnsignedDecimal(10),
            row.HexWord(11),
            row.UnsignedDecimal(12),
            row.HexByte(13),
            row.UnsignedDecimal(14),
            row.UnsignedDecimal(15),
            row.RequiredString(16),
            row.RequiredString(17),
            row.RequiredString(18),
            row.RequiredString(19),
            row.Base64Utf8(20),
            row.HexWord(21),
            row.Base64Utf8(22));

        EncounterCommands = CutsceneCommandCatalog.Load(
            "res://assets/oracle/cutscenes/impa_intro_commands.tsv");
        ValidateEncounterCommands();

        GeneratedTableRow stone = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/impa_stone_event.tsv",
            new GeneratedTableSchema(
                "Impa stone event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "id", "subid", "initial-y", "initial-x", "moved-y",
                    "left-x", "right-x", "radius-y", "radius-x", "left-flag", "right-flag",
                    "approach-y", "approach-x", "target-y", "target-x", "close-radius",
                    "spot-hold", "spot-speed-z", "gravity", "first-land-wait", "first-text",
                    "first-post", "approach-speed", "stone-wait", "second-hold", "second-speed-z",
                    "second-land-wait", "sign-text", "sign-post", "link-axis-wait", "link-target-wait",
                    "link-face-wait", "link-speed", "request-lead", "request-text", "request-post",
                    "back-speed", "back-frames-1", "between-back-1", "hesitation-text",
                    "hesitation-post", "back-frames-2", "between-back-2", "failure-text",
                    "failure-post", "push-hold", "stone-move-frames", "stone-speed",
                    "link-push-speed", "reaction-lead", "left-correct-frames", "left-correct-speed",
                    "right-branch-wait", "common-wait", "response-right-frames", "response-right-speed",
                    "response-wait-1", "response-up-frames", "response-wait-2", "pose-wait",
                    "thanks-text", "thanks-post", "final-speed", "final-frames", "leave-y", "leave-x",
                    "leave-text", "talk-text", "spot-sound", "push-sound", "stop-sound", "solve-sound",
                    "stone-sprite", "stone-tile-base", "stone-palette", "stone-animation",
                    "final-layout-tile", "final-collision", "link-target-y", "link-target-x",
                    "first-text-base64", "sign-text-base64", "request-text-base64",
                    "hesitation-text-base64", "failure-text-base64", "thanks-text-base64",
                    "leave-text-base64", "talk-text-base64", "stone-source-inverted"
                ],
                headerRequired: true)).SingleRow();
        static int Decimal(GeneratedTableRow values, int index) => values.Decimal(index);
        static int Hex(GeneratedTableRow values, int index) => values.HexInt(index);
        static string Text(GeneratedTableRow values, int index) => values.Base64Utf8(index);

        StoneRecord = new ImpaStoneEventRecord(
            new ImpaStoneActorRecord(
                Decimal(stone, 0), Hex(stone, 1), Hex(stone, 2), Hex(stone, 3),
                Decimal(stone, 4), Decimal(stone, 5), Decimal(stone, 6),
                Decimal(stone, 7), Decimal(stone, 8), Decimal(stone, 9),
                Decimal(stone, 10), Hex(stone, 11), Hex(stone, 12),
                Decimal(stone, 13), Decimal(stone, 14), Decimal(stone, 15),
                Decimal(stone, 16), Decimal(stone, 17), Decimal(stone, 66),
                Decimal(stone, 67), stone.RequiredString(74), Decimal(stone, 75),
                Decimal(stone, 76), stone.RequiredString(77), Hex(stone, 78), Hex(stone, 79),
                Decimal(stone, 80), Decimal(stone, 81), Decimal(stone, 90) != 0),
            new ImpaStoneTimingRecord(
                Decimal(stone, 18), Decimal(stone, 19), Decimal(stone, 20),
                Decimal(stone, 21), Decimal(stone, 23), Decimal(stone, 24),
                Decimal(stone, 25), Decimal(stone, 26), Decimal(stone, 27),
                Decimal(stone, 28), Decimal(stone, 30), Decimal(stone, 31),
                Decimal(stone, 32), Decimal(stone, 33), Decimal(stone, 34),
                Decimal(stone, 35), Decimal(stone, 37), Decimal(stone, 38),
                Decimal(stone, 39), Decimal(stone, 40), Decimal(stone, 42),
                Decimal(stone, 43), Decimal(stone, 44), Decimal(stone, 46),
                Decimal(stone, 47), Decimal(stone, 48), Decimal(stone, 49),
                Decimal(stone, 50), Decimal(stone, 51), Decimal(stone, 52),
                Decimal(stone, 53), Decimal(stone, 54), Decimal(stone, 55),
                Decimal(stone, 56), Decimal(stone, 57), Decimal(stone, 58),
                Decimal(stone, 59), Decimal(stone, 60), Decimal(stone, 61),
                Decimal(stone, 63), Decimal(stone, 64), Decimal(stone, 65)),
            new ImpaStoneTexts(
                new ImpaStoneText(Hex(stone, 22), Text(stone, 82)),
                new ImpaStoneText(Hex(stone, 29), Text(stone, 83)),
                new ImpaStoneText(Hex(stone, 36), Text(stone, 84)),
                new ImpaStoneText(Hex(stone, 41), Text(stone, 85)),
                new ImpaStoneText(Hex(stone, 45), Text(stone, 86)),
                new ImpaStoneText(Hex(stone, 62), Text(stone, 87)),
                new ImpaStoneText(Hex(stone, 68), Text(stone, 88)),
                new ImpaStoneText(Hex(stone, 69), Text(stone, 89))),
            new ImpaStoneSounds(
                Hex(stone, 70), Hex(stone, 71), Hex(stone, 72), Hex(stone, 73)));
        StonePrePushCommands = CutsceneCommandCatalog.Load(
            "res://assets/oracle/cutscenes/impa_stone_prepush_commands.tsv");
        ValidateStonePrePushCommands();
        StonePostPushCommands = CutsceneCommandCatalog.Load(
            "res://assets/oracle/cutscenes/impa_stone_postpush_commands.tsv");
        ValidateStonePostPushCommands();

        GeneratedTableRow help = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/impa_help_event.tsv",
            new GeneratedTableSchema(
                "Impa help event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "id", "subid", "room-flag", "edge-y", "post-text",
                    "input-up", "text-id", "textbox-position", "text-base64"
                ],
                headerRequired: true)).SingleRow();
        HelpRecord = new ImpaHelpEventRecord(
            help.Decimal(0, 0, 7),
            help.HexByte(1),
            help.HexByte(2),
            help.HexByte(3),
            help.HexByte(4),
            help.UnsignedDecimal(5),
            help.UnsignedDecimal(6),
            help.UnsignedDecimal(7),
            help.HexWord(8),
            help.UnsignedDecimal(9),
            help.Base64Utf8(10));
        HelpCommands = CutsceneCommandCatalog.Load(
            "res://assets/oracle/cutscenes/impa_help_commands.tsv");
        ValidateHelpCommands();

        var octoroks = new List<FakeOctorokRecord>();
        GeneratedTable octorokTable = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/impa_intro_octoroks.tsv",
            new GeneratedTableSchema(
                "Impa intro fake Octoroks",
                GeneratedTableKeySemantics.Unique,
                [
                    "index", "id", "subid", "y", "x", "var03", "sprite", "tile-base",
                    "palette", "initial-animation", "flee-animation", "signal-wait",
                    "flee-counter", "angle", "speed"
                ],
                ["index"],
                headerRequired: true));
        foreach (GeneratedTableRow actor in octorokTable.Rows)
        {
            octoroks.Add(new FakeOctorokRecord(
                actor.UnsignedDecimal(0),
                actor.HexByte(1),
                actor.HexByte(2),
                actor.HexByte(3),
                actor.HexByte(4),
                actor.HexByte(5),
                actor.RequiredString(6),
                actor.UnsignedDecimal(7),
                actor.UnsignedDecimal(8),
                actor.RequiredString(9),
                actor.RequiredString(10),
                actor.UnsignedDecimal(11),
                actor.UnsignedDecimal(12),
                actor.HexByte(13),
                actor.HexByte(14)));
        }
        if (octoroks.Count != 3)
            throw new InvalidOperationException($"Expected three fake Octoroks, got {octoroks.Count}.");
        Octoroks = octoroks;

        byte[] palette = FileAccess.GetFileAsBytes(
            "res://assets/oracle/metadata/impa_possessed_palette.bin");
        if (palette.Length != 12)
        {
            throw new InvalidOperationException(
                $"Possessed Impa palette should contain 12 bytes, got {palette.Length}.");
        }
        PossessedPalette = new Color[4];
        PossessedPalette[0] = Colors.Transparent;
        for (int color = 1; color < PossessedPalette.Length; color++)
        {
            int offset = color * 3;
            PossessedPalette[color] = new Color(
                palette[offset] / 31.0f,
                palette[offset + 1] / 31.0f,
                palette[offset + 2] / 31.0f);
        }

        StonePalette = ReadSpritePalette(
            "res://assets/oracle/metadata/impa_stone_palette.bin",
            "Triforce-stone PALH_98 palette");
    }

    private static Color[] ReadSpritePalette(string path, string description)
    {
        byte[] palette = FileAccess.GetFileAsBytes(path);
        if (palette.Length != 12)
        {
            throw new InvalidOperationException(
                $"{description} should contain 12 bytes, got {palette.Length}.");
        }
        var result = new Color[4];
        result[0] = Colors.Transparent;
        for (int color = 1; color < result.Length; color++)
        {
            int offset = color * 3;
            result[color] = new Color(
                palette[offset] / 31.0f,
                palette[offset + 1] / 31.0f,
                palette[offset + 2] / 31.0f);
        }
        return result;
    }

    private void ValidateEncounterCommands()
    {
        if (EncounterCommands.Count != 8 ||
            EncounterCommands[0] is not CutsceneMemoryGateCommand
            {
                Binding: "wTmpcfc0.genericCutscene.cfd0",
                Value: 0x01
            } ||
            !MatchesWait(EncounterCommands[1], Record.ImpaWaitFrames) ||
            EncounterCommands[2] is not CutsceneShowTextVariantsCommand
            {
                StandardTextId: var standardTextId,
                StandardMessage: var standardMessage,
                LinkedTextId: var linkedTextId,
                LinkedMessage: var linkedMessage
            } ||
            standardTextId != Record.TextId || standardMessage != Record.Text ||
            linkedTextId != Record.LinkedTextId || linkedMessage != Record.LinkedText ||
            !MatchesWait(EncounterCommands[3], Record.PostTextFrames) ||
            EncounterCommands[4] is not CutsceneSetSpeedCommand
                { Actor: "Impa", Speed: var speed } ||
            speed != Record.ImpaSpeed ||
            EncounterCommands[5] is not CutsceneMoveCommand
            {
                Actor: "Impa",
                Angle: 0x10,
                Counter: var moveFrames,
                EncodedAnimation: var animation
            } ||
            moveFrames != Record.ImpaMoveFrames || animation != Record.DownAnimation ||
            EncounterCommands[6] is not CutsceneOrRoomFlagCommand
                { Flag: var roomFlag } ||
            roomFlag != Record.RoomFlag ||
            EncounterCommands[7] is not CutsceneEndCommand)
        {
            throw new InvalidOperationException(
                "impaScript0 command stream diverges from imported encounter metadata.");
        }
    }

    private static bool MatchesWait(CutsceneCommand command, int frames) =>
        command is CutsceneWaitCommand { Frames: var actual } && actual == frames;

    private void ValidateHelpCommands()
    {
        if (HelpCommands.Count != 9 ||
            HelpCommands[0] is not CutsceneDisableMenuCommand ||
            HelpCommands[1] is not CutsceneSetDisabledObjectsContinueCommand
                { Value: 0x01 } ||
            HelpCommands[2] is not CutsceneSetCounterCommand
                { Frames: var postTextFrames } ||
            postTextFrames != HelpRecord.PostTextFrames ||
            HelpCommands[3] is not CutsceneShowTextCommand
            {
                TextId: var textId,
                Message: var message
            } ||
            textId != HelpRecord.TextId || message != HelpRecord.Text ||
            HelpCommands[4] is not CutsceneWaitPreloadedCounterCommand ||
            HelpCommands[5] is not CutsceneSetDisabledObjectsContinueCommand
                { Value: 0x00 } ||
            HelpCommands[6] is not CutsceneNativeCommand
                { Handler: "installHelpSimulatedInput" } ||
            HelpCommands[7] is not CutsceneOrRoomFlagContinueCommand
                { Flag: var roomFlag } ||
            roomFlag != HelpRecord.RoomFlag ||
            HelpCommands[8] is not CutsceneEndCommand)
        {
            throw new InvalidOperationException(
                "interaction6b_subid00 command stream diverges from imported help metadata.");
        }
    }

    private void ValidateStonePrePushCommands()
    {
        ImpaStoneTimingRecord timing = StoneRecord.Timing;
        ImpaStoneTexts texts = StoneRecord.Texts;
        if (StonePrePushCommands.Count != 18 ||
            StonePrePushCommands[0] is not CutsceneMemoryGateCommand
            {
                Binding: "wTmpcfc0.genericCutscene.cfd0",
                Value: 0x03
            } ||
            StonePrePushCommands[1] is not CutsceneSetAnimationCommand
            {
                Actor: "Impa",
                Animation: 0x02,
                EncodedAnimation: var downAnimation
            } || downAnimation != Record.DownAnimation ||
            !MatchesWait(StonePrePushCommands[2], timing.RequestLeadFrames) ||
            !MatchesText(StonePrePushCommands[3], texts.Request) ||
            !MatchesWait(StonePrePushCommands[4], timing.RequestPostFrames) ||
            StonePrePushCommands[5] is not CutsceneSetAnimationCommand
            {
                Actor: "Impa",
                Animation: 0x01,
                EncodedAnimation: var rightAnimation
            } || rightAnimation != Record.RightAnimation ||
            StonePrePushCommands[6] is not CutsceneSetAngleCommand
                { Actor: "Impa", Angle: 0x18 } ||
            StonePrePushCommands[7] is not CutsceneSetSpeedCommand
                { Actor: "Impa", Speed: var speed } ||
            speed != timing.BackAwaySpeed ||
            !MatchesApplySpeed(
                StonePrePushCommands[8], timing.FirstBackAwayFrames) ||
            !MatchesWait(
                StonePrePushCommands[9], timing.BetweenFirstBackAwayFrames) ||
            !MatchesText(StonePrePushCommands[10], texts.Hesitation) ||
            !MatchesWait(StonePrePushCommands[11], timing.HesitationPostFrames) ||
            !MatchesApplySpeed(
                StonePrePushCommands[12], timing.SecondBackAwayFrames) ||
            !MatchesWait(
                StonePrePushCommands[13], timing.BetweenSecondBackAwayFrames) ||
            !MatchesText(StonePrePushCommands[14], texts.Failure) ||
            !MatchesWait(StonePrePushCommands[15], timing.FailurePostFrames) ||
            StonePrePushCommands[16] is not CutsceneWriteMemoryCommand
            {
                Binding: "wTmpcfc0.genericCutscene.cfd0",
                Value: 0x04
            } ||
            StonePrePushCommands[17] is not CutsceneEndCommand)
        {
            throw new InvalidOperationException(
                "impaScript_moveAwayFromRock command stream diverges from imported stone metadata.");
        }

        for (int index = 0; index < StonePrePushCommands.Count; index++)
        {
            CutsceneCommandSource source = StonePrePushCommands[index].Source;
            if (source.Script != "impaScript_moveAwayFromRock" ||
                source.Label != "impaScript_moveAwayFromRock" ||
                source.CommandIndex != index || source.SourceLine <= 0 ||
                (index > 0 && source.SourceLine <=
                    StonePrePushCommands[index - 1].Source.SourceLine))
            {
                throw new InvalidOperationException(
                    $"Malformed impaScript_moveAwayFromRock source metadata at command {index}.");
            }
        }
    }

    private static bool MatchesText(CutsceneCommand command, ImpaStoneText text) =>
        command is CutsceneShowTextCommand
        {
            TextId: var textId,
            Message: var message
        } && textId == text.Id && message == text.Message;

    private static bool MatchesApplySpeed(CutsceneCommand command, int frames) =>
        command is CutsceneApplySpeedCommand
            { Actor: "Impa", Counter: var actual } && actual == frames;

    private void ValidateStonePostPushCommands()
    {
        ImpaStoneTimingRecord timing = StoneRecord.Timing;
        ImpaStoneText thanks = StoneRecord.Texts.Thanks;
        if (StonePostPushCommands.Count != 23 ||
            !MatchesWait(StonePostPushCommands[0], timing.ReactionLeadFrames) ||
            StonePostPushCommands[1] is not CutsceneMemoryBranchCommand
            {
                Binding: "w1Link.angle",
                Value: 0x08,
                TargetCommand: 6
            } ||
            StonePostPushCommands[2] is not CutsceneSetAngleCommand
                { Actor: "Impa", Angle: 0x10 } ||
            StonePostPushCommands[3] is not CutsceneSetSpeedCommand
                { Actor: "Impa", Speed: var correctionSpeed } ||
            correctionSpeed != timing.LeftCorrectionSpeed ||
            !MatchesApplySpeed(
                StonePostPushCommands[4], timing.LeftCorrectionFrames) ||
            StonePostPushCommands[5] is not CutsceneBranchCommand
                { TargetCommand: 7 } ||
            !MatchesWait(
                StonePostPushCommands[6], timing.RightBranchWaitFrames) ||
            !MatchesWait(StonePostPushCommands[7], timing.CommonWaitFrames) ||
            StonePostPushCommands[8] is not CutsceneSetAngleCommand
                { Actor: "Impa", Angle: 0x08 } ||
            StonePostPushCommands[9] is not CutsceneSetSpeedCommand
                { Actor: "Impa", Speed: var responseSpeed } ||
            responseSpeed != timing.ResponseRightSpeed ||
            !MatchesApplySpeed(
                StonePostPushCommands[10], timing.ResponseRightFrames) ||
            !MatchesWait(
                StonePostPushCommands[11], timing.ResponseWait1Frames) ||
            StonePostPushCommands[12] is not CutsceneMemoryBranchCommand
            {
                Binding: "w1Link.angle",
                Value: 0x08,
                TargetCommand: 15
            } ||
            StonePostPushCommands[13] is not CutsceneMoveCommand
            {
                Actor: "Impa",
                Angle: 0x00,
                Counter: var correctionFrames,
                EncodedAnimation: var correctionAnimation
            } || correctionFrames != timing.ResponseUpFrames ||
            correctionAnimation != Record.UpAnimation ||
            !MatchesWait(
                StonePostPushCommands[14], timing.ResponseWait2Frames) ||
            StonePostPushCommands[15] is not CutsceneWriteMemoryCommand
            {
                Binding: "wTmpcfc0.genericCutscene.cfd0",
                Value: 0x07
            } ||
            StonePostPushCommands[16] is not CutsceneSetAnimationCommand
            {
                Actor: "Impa",
                Animation: 0x00,
                EncodedAnimation: var poseAnimation
            } || poseAnimation != Record.UpAnimation ||
            !MatchesWait(StonePostPushCommands[17], timing.PoseWaitFrames) ||
            !MatchesText(StonePostPushCommands[18], thanks) ||
            !MatchesWait(StonePostPushCommands[19], timing.ThanksPostFrames) ||
            StonePostPushCommands[20] is not CutsceneSetSpeedCommand
                { Actor: "Impa", Speed: var finalSpeed } ||
            finalSpeed != timing.FinalSpeed ||
            StonePostPushCommands[21] is not CutsceneMoveCommand
            {
                Actor: "Impa",
                Angle: 0x00,
                Counter: var finalFrames,
                EncodedAnimation: var finalAnimation
            } || finalFrames != timing.FinalMoveFrames ||
            finalAnimation != Record.UpAnimation ||
            StonePostPushCommands[22] is not CutsceneEndCommand)
        {
            throw new InvalidOperationException(
                "impaScript_rockJustMoved command stream diverges from imported stone metadata.");
        }

        for (int index = 0; index < StonePostPushCommands.Count; index++)
        {
            CutsceneCommandSource source = StonePostPushCommands[index].Source;
            if (source.Script != "impaScript_rockJustMoved" ||
                source.CommandIndex != index || source.SourceLine <= 0 ||
                (index > 0 && source.SourceLine <=
                    StonePostPushCommands[index - 1].Source.SourceLine))
            {
                throw new InvalidOperationException(
                    $"Malformed impaScript_rockJustMoved source metadata at command {index}.");
            }
        }
    }
}
