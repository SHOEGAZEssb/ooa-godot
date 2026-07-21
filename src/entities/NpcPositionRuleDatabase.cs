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
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/npc_positions.tsv",
            new GeneratedTableSchema(
                "NPC position rules",
                GeneratedTableKeySemantics.Grouped,
                ["id", "subid", "var03", "kind", "value", "y", "x", "source"],
                ["id", "subid"],
                headerRequired: true));
        var uniqueRules = new HashSet<(
            int Id,
            int SubId,
            int Var03,
            NpcStoryStateKind Kind,
            int Value)>();
        foreach (GeneratedTableRow row in table.Rows)
        {
            int id = row.HexByte(0);
            int subId = row.HexByte(1);
            int var03 = row.HexByteOrSentinel(2, "*", -1);
            NpcStoryStateKind kind = NpcStoryState.ParseKind(row.RequiredString(3), "position");
            int value = row.HexByte(4);
            int y = row.HexByte(5);
            int x = row.HexByte(6);
            if (value is < 0 or > 0xff ||
                kind == NpcStoryStateKind.GameProgress1 ||
                kind == NpcStoryStateKind.GameProgress2 && value > 7 ||
                kind == NpcStoryStateKind.CurrentRoomFlag && value == 0 ||
                y is < 0 or > 0xff ||
                x is < 0 or > 0xff ||
                !uniqueRules.Add((id, subId, var03, kind, value)))
            {
                throw new InvalidOperationException(
                    $"Invalid NPC position rule at {row.Path}:{row.LineNumber}.");
            }

            int key = NpcStoryState.InteractionKey(id, subId);
            if (!_byInteraction.TryGetValue(key, out List<Rule>? rules))
            {
                rules = new List<Rule>();
                _byInteraction.Add(key, rules);
            }
            rules.Add(new Rule(
                var03, kind, value, y, x, row.RequiredString(7)));
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
