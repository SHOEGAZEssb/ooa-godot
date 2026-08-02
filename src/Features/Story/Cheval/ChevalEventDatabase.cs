using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>
/// Imported INTERAC_CHEVAL metadata, text, and cheval_subid00Script for
/// past interior room $2:$0f.
/// </summary>
internal sealed class ChevalEventDatabase
{
    private const string Root = "res://assets/oracle/cutscenes/";
    private readonly Dictionary<int, string> _texts = new();

    internal ChevalEventRecord Record { get; }
    internal IReadOnlyList<CutsceneCommand> Commands { get; }

    internal ChevalEventDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            Root + "cheval_event.tsv",
            new GeneratedTableSchema(
                "room 2:0f Cheval event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "id", "subid", "sprite", "tile-base",
                    "palette", "animation0", "initial-animation", "collision-y",
                    "collision-x", "cheval-rope-treasure", "talked-global-flag",
                    "initial-script-updates"
                ],
                headerRequired: true)).SingleRow();
        Record = new ChevalEventRecord(
            row.Decimal(0, 0, 7),
            row.HexByte(1),
            row.HexByte(2),
            row.HexByte(3),
            row.RequiredString(4),
            row.HexByte(5),
            row.HexByte(6),
            row.RequiredString(7),
            row.HexByte(8),
            row.HexByte(9),
            row.HexByte(10),
            row.HexByte(11),
            row.HexByte(12),
            row.UnsignedDecimal(13));

        LoadTexts();
        Commands = CutsceneCommandCatalog.Load(
            Root + "cheval_commands.tsv");
        Validate();
    }

    internal bool Matches(NpcRecord record) =>
        record.Group == Record.Group &&
        record.Room == Record.Room &&
        record.Id == Record.InteractionId &&
        record.SubId == Record.SubId;

    internal string Text(int textId) =>
        _texts.TryGetValue(textId, out string? text)
            ? text
            : throw new InvalidOperationException(
                $"INTERAC_CHEVAL requested unimported TX_{textId:x4}.");

    internal string DialogueText(int textId)
    {
        if (textId is not (0x270c or 0x270d))
        {
            throw new InvalidOperationException(
                $"INTERAC_CHEVAL cannot display TX_{textId:x4} directly.");
        }

        string text = Text(textId);
        const string call = @"\call(TX_270b)";
        if (!text.StartsWith(call, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Cheval TX_{textId:x4} lost its source call to TX_270b.");
        }
        return text.Replace(call, Text(0x270b), StringComparison.Ordinal);
    }

    private void LoadTexts()
    {
        foreach (GeneratedTableRow row in GeneratedTable.Load(
            Root + "cheval_text.tsv",
            new GeneratedTableSchema(
                "room 2:0f Cheval text",
                GeneratedTableKeySemantics.Unique,
                ["id", "text-base64"],
                ["id"],
                headerRequired: true)).Rows)
        {
            int id = row.HexWord(0);
            if (!_texts.TryAdd(id, row.Base64Utf8(1)))
            {
                throw new InvalidOperationException(
                    $"Duplicate INTERAC_CHEVAL text TX_{id:x4}.");
            }
        }
    }

    private void Validate()
    {
        if (Record is not
            {
                Group: 2,
                Room: 0x0f,
                InteractionId: 0x6a,
                SubId: 0,
                Sprite: "spr_oldzora_cheval",
                TileBase: 0x1a,
                Palette: 0,
                InitialAnimation: 0,
                CollisionRadiusY: 0x0c,
                CollisionRadiusX: 0x06,
                ChevalRopeTreasure: 0x52,
                TalkedGlobalFlag: OracleSaveData.GlobalFlagTalkedToCheval,
                InitialScriptUpdates: 0
            } ||
            string.IsNullOrWhiteSpace(Record.Animation0) ||
            !_texts.Keys.Order().SequenceEqual(new[] { 0x270b, 0x270c, 0x270d }))
        {
            throw new InvalidOperationException(
                "Room 2:0f Cheval metadata diverges from its source contract.");
        }

        if (Commands.Count != 11 ||
            Commands[0] is not CutsceneInitCollisionsCommand { Actor: "Cheval" } ||
            Commands[1] is not CutsceneSetCollisionRadiiCommand
                { Actor: "Cheval", RadiusY: 0x0c, RadiusX: 0x06 } ||
            Commands[2] is not CutsceneMemoryBranchCommand
                { Binding: "HasChevalRope", Value: 1, TargetCommand: 7 } ||
            Commands[3] is not CutsceneCheckAButtonCommand { Actor: "Cheval" } ||
            Commands[4] is not CutsceneShowTextCommand { TextId: 0x270c } ||
            Commands[5] is not CutsceneSetGlobalFlagCommand
                { Flag: OracleSaveData.GlobalFlagTalkedToCheval } ||
            Commands[6] is not CutsceneBranchCommand { TargetCommand: 3 } ||
            Commands[7] is not CutsceneCheckAButtonCommand { Actor: "Cheval" } ||
            Commands[8] is not CutsceneShowTextCommand { TextId: 0x270d } ||
            Commands[9] is not CutsceneSetGlobalFlagCommand
                { Flag: OracleSaveData.GlobalFlagTalkedToCheval } ||
            Commands[10] is not CutsceneBranchCommand { TargetCommand: 7 })
        {
            throw new InvalidOperationException(
                "cheval_subid00Script command stream diverges from imported metadata.");
        }

        foreach (CutsceneShowTextCommand command in
                 Commands.OfType<CutsceneShowTextCommand>())
        {
            if (command.Message != DialogueText(command.TextId))
            {
                throw new InvalidOperationException(
                    $"Cheval TX_{command.TextId:x4} expanded command payload " +
                    "diverges from text metadata.");
            }
        }
    }
}

internal readonly record struct ChevalEventRecord(
    int Group,
    int Room,
    int InteractionId,
    int SubId,
    string Sprite,
    int TileBase,
    int Palette,
    string Animation0,
    int InitialAnimation,
    int CollisionRadiusY,
    int CollisionRadiusX,
    int ChevalRopeTreasure,
    int TalkedGlobalFlag,
    int InitialScriptUpdates);
