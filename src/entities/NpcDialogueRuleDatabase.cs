using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        string source = FileAccess.GetFileAsString(
            "res://assets/oracle/objects/npc_dialogue.tsv");
        var uniqueRules = new HashSet<(
            int Id,
            int SubId,
            int Var03,
            NpcStoryStateKind Kind,
            int Value,
            int Linked)>();
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;
            string[] fields = line.Split('\t');
            if (fields.Length != 9)
                throw new InvalidOperationException($"Malformed NPC dialogue row: {line}");

            int id = Convert.ToInt32(fields[0], 16);
            int subId = Convert.ToInt32(fields[1], 16);
            int var03 = fields[2] == "*" ? -1 : Convert.ToInt32(fields[2], 16);
            NpcStoryStateKind kind = NpcStoryState.ParseKind(fields[3], "dialogue");
            int value = Convert.ToInt32(fields[4], 16);
            int linked = fields[5] switch
            {
                "*" => -1,
                "0" => 0,
                "1" => 1,
                _ => throw new InvalidOperationException(
                    $"NPC dialogue linked selector must be *, 0, or 1: {line}")
            };
            int textId = Convert.ToInt32(fields[6], 16);
            string message = Encoding.UTF8.GetString(Convert.FromBase64String(fields[8]));
            if (value < 0 ||
                kind == NpcStoryStateKind.GameProgress1 && value > 5 ||
                kind == NpcStoryStateKind.GameProgress2 && value > 7 ||
                kind == NpcStoryStateKind.CurrentRoomFlag && value == 0 ||
                textId == 0 || string.IsNullOrEmpty(message) ||
                !uniqueRules.Add((id, subId, var03, kind, value, linked)))
            {
                throw new InvalidOperationException($"Invalid NPC dialogue rule: {line}");
            }

            int key = NpcStoryState.InteractionKey(id, subId);
            if (!_byInteraction.TryGetValue(key, out List<Rule>? rules))
            {
                rules = new List<Rule>();
                _byInteraction.Add(key, rules);
            }
            rules.Add(new Rule(
                var03, kind, value, linked, textId, message, fields[7]));
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
