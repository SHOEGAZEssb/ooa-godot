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
    private enum FlagKind
    {
        Global,
        CurrentRoom,
        SpecificRoom,
        Treasure,
        Linked,
        Essence,
        Wram,
        RuntimeEquals,
        GameProgress1
    }

    private readonly record struct Rule(
        int Var03,
        int Alternative,
        FlagKind Kind,
        int Group,
        int Room,
        int Value,
        bool ExpectedSet,
        string Source);

    private readonly Dictionary<int, List<Rule>> _byInteraction = new();

    internal int RuleCount { get; private set; }

    public NpcVisibilityRuleDatabase()
    {
        string source = FileAccess.GetFileAsString(
            "res://assets/oracle/objects/npc_visibility.tsv");
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;
            string[] fields = line.Split('\t');
            if (fields.Length != 10)
                throw new InvalidOperationException($"Malformed NPC visibility row: {line}");

            int id = Convert.ToInt32(fields[0], 16);
            int subId = Convert.ToInt32(fields[1], 16);
            int var03 = fields[2] == "*" ? -1 : Convert.ToInt32(fields[2], 16);
            int alternative = int.Parse(fields[3]);
            FlagKind kind = fields[4] switch
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
                _ => throw new InvalidOperationException(
                    $"Unknown NPC visibility flag kind '{fields[4]}'.")
            };
            int group = fields[5] == "-" ? -1 : int.Parse(fields[5]);
            int room = fields[6] == "-" ? -1 : Convert.ToInt32(fields[6], 16);
            int value = Convert.ToInt32(fields[7], 16);
            bool expectedSet = fields[8] switch
            {
                "0" => false,
                "1" => true,
                _ => throw new InvalidOperationException(
                    $"NPC visibility expected-set value must be 0 or 1: {line}")
            };
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
                kind == FlagKind.SpecificRoom && (group is < 0 or > 7 || room is < 0 or > 0xff))
            {
                throw new InvalidOperationException($"Invalid NPC visibility rule: {line}");
            }

            int key = MakeKey(id, subId);
            if (!_byInteraction.TryGetValue(key, out List<Rule>? rules))
            {
                rules = new List<Rule>();
                _byInteraction.Add(key, rules);
            }
            rules.Add(new Rule(
                var03, alternative, kind, group, room, value, expectedSet, fields[9]));
            RuleCount++;
        }
    }

    public bool ShouldShow(
        NpcDatabase.NpcRecord npc,
        OracleSaveData save,
        OracleRuntimeState runtimeState)
    {
        if (!_byInteraction.TryGetValue(MakeKey(npc.Id, npc.SubId), out List<Rule>? rules))
            return true;

        List<Rule> applicable = rules.Where(rule =>
            rule.Var03 < 0 || rule.Var03 == npc.Var03).ToList();
        if (applicable.Count == 0)
            return true;

        return applicable.GroupBy(rule => rule.Alternative).Any(alternative =>
            alternative.All(rule => IsSatisfied(rule, npc, save, runtimeState)));
    }

    private static bool IsSatisfied(
        Rule rule,
        NpcDatabase.NpcRecord npc,
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
            FlagKind.GameProgress1 => GetGameProgress1(save) == rule.Value,
            _ => throw new InvalidOperationException(
                $"Unhandled NPC visibility rule from {rule.Source}.")
        };
        return set == rule.ExpectedSet;
    }

    internal static int GetGameProgress1(OracleSaveData save)
    {
        if (save.HasGlobalFlag(OracleSaveData.GlobalFlagFinishedGame))
            return 5;
        if (save.HasGlobalFlag(OracleSaveData.GlobalFlagSawTwinrovaBeforeEndgame))
            return 4;

        byte essences = save.ReadWramByte(0xc6bf);
        if (essences == 0)
            return 0;
        int highestEssence = 7;
        while ((essences & (1 << highestEssence)) == 0)
            highestEssence--;
        if (highestEssence >= 6)
            return 3;
        if (save.HasGlobalFlag(OracleSaveData.GlobalFlagSavedNayru))
            return 2;
        return highestEssence >= 2 ? 1 : 0;
    }

    private static int MakeKey(int id, int subId) => (id << 8) | subId;
}
