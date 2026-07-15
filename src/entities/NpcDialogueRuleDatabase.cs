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
    private enum StateKind
    {
        GameProgress1
    }

    private readonly record struct Rule(
        int Var03,
        StateKind Kind,
        int Value,
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
        var uniqueRules = new HashSet<(int Id, int SubId, int Var03, StateKind Kind, int Value)>();
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;
            string[] fields = line.Split('\t');
            if (fields.Length != 8)
                throw new InvalidOperationException($"Malformed NPC dialogue row: {line}");

            int id = Convert.ToInt32(fields[0], 16);
            int subId = Convert.ToInt32(fields[1], 16);
            int var03 = fields[2] == "*" ? -1 : Convert.ToInt32(fields[2], 16);
            StateKind kind = fields[3] switch
            {
                "game-progress-1" => StateKind.GameProgress1,
                _ => throw new InvalidOperationException(
                    $"Unknown NPC dialogue state kind '{fields[3]}'.")
            };
            int value = Convert.ToInt32(fields[4], 16);
            int textId = Convert.ToInt32(fields[5], 16);
            string message = Encoding.UTF8.GetString(Convert.FromBase64String(fields[7]));
            if (value is < 0 or > 5 || textId == 0 || string.IsNullOrEmpty(message) ||
                !uniqueRules.Add((id, subId, var03, kind, value)))
            {
                throw new InvalidOperationException($"Invalid NPC dialogue rule: {line}");
            }

            int key = MakeKey(id, subId);
            if (!_byInteraction.TryGetValue(key, out List<Rule>? rules))
            {
                rules = new List<Rule>();
                _byInteraction.Add(key, rules);
            }
            rules.Add(new Rule(var03, kind, value, textId, message, fields[6]));
            RuleCount++;
        }
    }

    public bool TryResolve(
        NpcDatabase.NpcRecord npc,
        OracleSaveData save,
        out Dialogue dialogue)
    {
        dialogue = default;
        if (!_byInteraction.TryGetValue(MakeKey(npc.Id, npc.SubId), out List<Rule>? rules))
            return false;

        List<Rule> matches = rules.Where(rule =>
            (rule.Var03 < 0 || rule.Var03 == npc.Var03) &&
            GetState(rule, save) == rule.Value).ToList();
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

    private static int GetState(Rule rule, OracleSaveData save) => rule.Kind switch
    {
        StateKind.GameProgress1 => NpcVisibilityRuleDatabase.GetGameProgress1(save),
        _ => throw new InvalidOperationException(
            $"Unhandled NPC dialogue rule from {rule.Source}.")
    };

    private static int MakeKey(int id, int subId) => (id << 8) | subId;
}
