using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>
/// Imported save-selected position overrides applied by ordinary NPC handlers
/// during initialization. A living NPC without a matching state uses its
/// original object-data coordinates.
/// </summary>
public sealed class NpcPositionRuleDatabase
{
    private readonly record struct Rule(
        int Var03,
        NpcStoryStateKind Kind,
        int Value,
        int Y,
        int X,
        string Source);

    private readonly Dictionary<int, List<Rule>> _byInteraction = new();

    internal int RuleCount { get; private set; }

    public NpcPositionRuleDatabase()
    {
        string source = FileAccess.GetFileAsString(
            "res://assets/oracle/objects/npc_positions.tsv");
        var uniqueRules = new HashSet<(
            int Id,
            int SubId,
            int Var03,
            NpcStoryStateKind Kind,
            int Value)>();
        foreach (string rawLine in source.Split(
            '\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;
            string[] fields = line.Split('\t');
            if (fields.Length != 8)
                throw new InvalidOperationException(
                    $"Malformed NPC position row: {line}");

            int id = Convert.ToInt32(fields[0], 16);
            int subId = Convert.ToInt32(fields[1], 16);
            int var03 = fields[2] == "*"
                ? -1
                : Convert.ToInt32(fields[2], 16);
            NpcStoryStateKind kind = NpcStoryState.ParseKind(fields[3], "position");
            int value = Convert.ToInt32(fields[4], 16);
            int y = Convert.ToInt32(fields[5], 16);
            int x = Convert.ToInt32(fields[6], 16);
            if (value is < 0 or > 0xff ||
                kind == NpcStoryStateKind.GameProgress1 ||
                kind == NpcStoryStateKind.GameProgress2 && value > 7 ||
                kind == NpcStoryStateKind.CurrentRoomFlag && value == 0 ||
                y is < 0 or > 0xff ||
                x is < 0 or > 0xff ||
                !uniqueRules.Add((id, subId, var03, kind, value)))
            {
                throw new InvalidOperationException(
                    $"Invalid NPC position rule: {line}");
            }

            int key = NpcStoryState.InteractionKey(id, subId);
            if (!_byInteraction.TryGetValue(key, out List<Rule>? rules))
            {
                rules = new List<Rule>();
                _byInteraction.Add(key, rules);
            }
            rules.Add(new Rule(
                var03, kind, value, y, x, fields[7]));
            RuleCount++;
        }
    }

    public bool TryResolve(
        NpcDatabase.NpcRecord npc,
        OracleSaveData save,
        out Vector2 position)
    {
        position = new Vector2(npc.X, npc.Y);
        if (!_byInteraction.TryGetValue(
            NpcStoryState.InteractionKey(npc.Id, npc.SubId), out List<Rule>? rules))
        {
            return false;
        }

        List<Rule> applicable = rules.Where(rule =>
            rule.Var03 < 0 || rule.Var03 == npc.Var03).ToList();
        if (applicable.Count == 0)
            return false;

        List<Rule> matches = applicable.Where(rule =>
            NpcStoryState.GetState(rule.Kind, rule.Value, npc, save) == rule.Value).ToList();
        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Multiple NPC position rules matched " +
                $"${npc.Id:x2}:${npc.SubId:x2}.");
        }
        if (matches.Count == 1)
            position = new Vector2(matches[0].X, matches[0].Y);
        return true;
    }

}
