using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>
/// Imported interaction-initialization predicates which delete room-placed
/// NPCs when their save-backed initialization requirements are not satisfied.
/// Alternative groups are ORed; every condition within one group is ANDed.
/// </summary>
public sealed class NpcVisibilityRuleDatabase
{

    private readonly Dictionary<int, List<NpcVisibilityRuleDatabaseRule>> _byInteraction = new();

    internal int RuleCount { get; private set; }

    public NpcVisibilityRuleDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/npc_visibility.tsv",
            new GeneratedTableSchema(
                "NPC visibility rules",
                GeneratedTableKeySemantics.Grouped,
                [
                    "id", "subid", "var03", "alternative", "kind", "group",
                    "room", "value", "expected-set", "source"
                ],
                ["id", "subid"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int id = row.HexByte(0);
            int subId = row.HexByte(1);
            int var03 = row.HexByteOrSentinel(2, "*", -1);
            int alternative = row.UnsignedDecimal(3);
            FlagKind kind = row.RequiredString(4) switch
            {
                "global" => FlagKind.Global,
                "current-room" => FlagKind.CurrentRoom,
                "specific-room" => FlagKind.SpecificRoom,
                "treasure" => FlagKind.Treasure,
                "linked" => FlagKind.Linked,
                "essence" => FlagKind.Essence,
                "wram" => FlagKind.Wram,
                "runtime-equals" => FlagKind.RuntimeEquals,
                "game-progress-1" => FlagKind.GameProgress1,
                "game-progress-2" => FlagKind.GameProgress2,
                _ => throw row.Invalid(4, "a supported NPC visibility kind")
            };
            int group = row.DecimalOrSentinel(5, "-", -1);
            int room = row.HexWordOrSentinel(6, "-", -1);
            int value = row.HexByte(7);
            bool expectedSet = row.Boolean01(8);
            if (alternative < 0 || value is < 0 or > 0xff ||
                kind == FlagKind.Global && value >= OracleSaveData.GlobalFlagCount ||
                kind is FlagKind.CurrentRoom or FlagKind.SpecificRoom && value == 0 ||
                kind == FlagKind.Treasure && value >= 0x80 ||
                kind == FlagKind.Linked && value != 0 ||
                kind == FlagKind.Essence && value == 0 ||
                kind == FlagKind.Wram &&
                    (room is < 0xc5b0 or > 0xcaff || value == 0) ||
                kind == FlagKind.RuntimeEquals &&
                    room is < OracleRuntimeState.WramStart or > OracleRuntimeState.WramEnd ||
                kind == FlagKind.GameProgress1 && value > 5 ||
                kind == FlagKind.GameProgress2 && value > 7 ||
                kind == FlagKind.SpecificRoom && (group is < 0 or > 7 || room is < 0 or > 0xff))
            {
                throw new InvalidOperationException(
                    $"Invalid NPC visibility rule at {row.Path}:{row.LineNumber}.");
            }

            int key = NpcStoryState.InteractionKey(id, subId);
            if (!_byInteraction.TryGetValue(key, out List<NpcVisibilityRuleDatabaseRule>? rules))
            {
                rules = new List<NpcVisibilityRuleDatabaseRule>();
                _byInteraction.Add(key, rules);
            }
            rules.Add(new NpcVisibilityRuleDatabaseRule(
                var03, alternative, kind, group, room, value, expectedSet,
                row.RequiredString(9)));
            RuleCount++;
        }
    }

    public bool ShouldShow(
        NpcRecord npc,
        OracleSaveData save,
        OracleRuntimeState runtimeState)
    {
        if (!_byInteraction.TryGetValue(
            NpcStoryState.InteractionKey(npc.Id, npc.SubId), out List<NpcVisibilityRuleDatabaseRule>? rules))
            return true;

        List<NpcVisibilityRuleDatabaseRule> applicable = rules.Where(rule =>
            rule.Var03 < 0 || rule.Var03 == npc.Var03).ToList();
        if (applicable.Count == 0)
            return true;

        return applicable.GroupBy(rule => rule.Alternative).Any(alternative =>
            alternative.All(rule => IsSatisfied(rule, npc, save, runtimeState)));
    }

    private static bool IsSatisfied(
        NpcVisibilityRuleDatabaseRule rule,
        NpcRecord npc,
        OracleSaveData save,
        OracleRuntimeState runtimeState)
    {
        bool set = rule.Kind switch
        {
            FlagKind.Global => save.HasGlobalFlag(rule.Value),
            FlagKind.CurrentRoom => save.HasRoomFlag(
                npc.Group, npc.Room, (byte)rule.Value),
            FlagKind.SpecificRoom => save.HasRoomFlag(
                rule.Group, rule.Room, (byte)rule.Value),
            FlagKind.Treasure => save.HasTreasure(rule.Value),
            FlagKind.Linked => save.IsLinkedGame,
            FlagKind.Essence => (save.ReadWramByte(0xc6bf) & rule.Value) != 0,
            FlagKind.Wram => (save.ReadWramByte(rule.Room) & rule.Value) != 0,
            FlagKind.RuntimeEquals =>
                runtimeState.ReadWramByte(rule.Room) == rule.Value,
            FlagKind.GameProgress1 => NpcStoryState.GetGameProgress1(save) == rule.Value,
            FlagKind.GameProgress2 => NpcStoryState.GetGameProgress2(save) == rule.Value,
            _ => throw new InvalidOperationException(
                $"Unhandled NPC visibility rule from {rule.Source}.")
        };
        return set == rule.ExpectedSet;
    }

    internal static int GetGameProgress1(OracleSaveData save) =>
        NpcStoryState.GetGameProgress1(save);

    internal static int GetGameProgress2(OracleSaveData save) =>
        NpcStoryState.GetGameProgress2(save);
}
