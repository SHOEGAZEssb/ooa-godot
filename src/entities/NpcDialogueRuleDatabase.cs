using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>
/// Imported save-selected dialogue tables used by ordinary room NPCs. These
/// are kept separate from visibility because the same actor can remain alive
/// while its script and text change with story progress.
/// </summary>
public sealed class NpcDialogueRuleDatabase
{
    private readonly record struct Rule(
        int Var03,
        NpcStoryStateKind Kind,
        int Value,
        int Linked,
        int TextId,
        string Message,
        string Source);

    public readonly record struct Dialogue(int TextId, string Message);

    private readonly Dictionary<int, List<Rule>> _byInteraction = new();

    internal int RuleCount { get; private set; }

    public NpcDialogueRuleDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/npc_dialogue.tsv",
            new GeneratedTableSchema(
                "NPC dialogue rules",
                GeneratedTableKeySemantics.Grouped,
                [
                    "id", "subid", "var03", "kind", "value", "linked",
                    "text-id", "source", "utf8-base64"
                ],
                ["id", "subid"],
                headerRequired: true));
        var uniqueRules = new HashSet<(
            int Id,
            int SubId,
            int Var03,
            NpcStoryStateKind Kind,
            int Value,
            int Linked)>();
        foreach (GeneratedTableRow row in table.Rows)
        {
            int id = row.HexByte(0);
            int subId = row.HexByte(1);
            int var03 = row.HexByteOrSentinel(2, "*", -1);
            NpcStoryStateKind kind = NpcStoryState.ParseKind(row.RequiredString(3), "dialogue");
            int value = row.HexByte(4);
            int linked = row.String(5) switch
            {
                "*" => -1,
                "0" => 0,
                "1" => 1,
                _ => throw row.Invalid(5, "one of *, 0, 1")
            };
            int textId = row.HexWord(6);
            string message = row.Base64Utf8(8);
            if (value < 0 ||
                kind == NpcStoryStateKind.GameProgress1 && value > 5 ||
                kind == NpcStoryStateKind.GameProgress2 && value > 7 ||
                kind == NpcStoryStateKind.CurrentRoomFlag && value == 0 ||
                textId == 0 || string.IsNullOrEmpty(message) ||
                !uniqueRules.Add((id, subId, var03, kind, value, linked)))
            {
                throw new InvalidOperationException(
                    $"Invalid NPC dialogue rule at {row.Path}:{row.LineNumber}.");
            }

            int key = NpcStoryState.InteractionKey(id, subId);
            if (!_byInteraction.TryGetValue(key, out List<Rule>? rules))
            {
                rules = new List<Rule>();
                _byInteraction.Add(key, rules);
            }
            rules.Add(new Rule(
                var03, kind, value, linked, textId, message, row.RequiredString(7)));
            RuleCount++;
        }
    }

    public bool TryResolve(
        NpcDatabase.NpcRecord npc,
        OracleSaveData save,
        out Dialogue dialogue)
    {
        dialogue = default;
        if (!_byInteraction.TryGetValue(
            NpcStoryState.InteractionKey(npc.Id, npc.SubId), out List<Rule>? rules))
            return false;

        List<Rule> matches = rules.Where(rule =>
            (rule.Var03 < 0 || rule.Var03 == npc.Var03) &&
            (rule.Linked < 0 || rule.Linked == (save.IsLinkedGame ? 1 : 0)) &&
            NpcStoryState.GetState(rule.Kind, rule.Value, npc, save) == rule.Value).ToList();
        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Multiple NPC dialogue rules matched ${npc.Id:x2}:${npc.SubId:x2}.");
        }
        if (matches.Count == 0)
            return false;

        dialogue = new Dialogue(matches[0].TextId, matches[0].Message);
        return true;
    }

}
