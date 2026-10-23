using System;
using System.Collections.Generic;
using System.Globalization;

namespace oracleofages;

/// <summary>Source command streams for INTERAC_FARORE $10 in room 5:cf.</summary>
internal sealed class FaroreScriptDatabase
{
    private readonly Dictionary<string, IReadOnlyList<CutsceneCommand>> _scripts = new();
    private readonly Dictionary<int, string> _texts = new();
    private readonly FaroreRewardRecord[] _rewards = new FaroreRewardRecord[10];

    internal FaroreScriptDatabase()
    {
        foreach (GeneratedTableRow row in GeneratedTable.Load(
            "res://assets/oracle/cutscenes/farore_texts.tsv",
            new GeneratedTableSchema("Farore text", GeneratedTableKeySemantics.Unique,
                ["text-id", "utf8-base64"], ["text-id"], headerRequired: true)).Rows)
            _texts.Add(row.HexWord(0), row.Base64Utf8(1));
        if (_texts.Count != 28) throw new InvalidOperationException("Farore TX_5501-$551c import is incomplete.");

        var groups = new Dictionary<string, List<Instruction>>();
        foreach (GeneratedTableRow row in GeneratedTable.Load(
            "res://assets/oracle/cutscenes/farore_scripts.tsv",
            new GeneratedTableSchema("Farore scripts", GeneratedTableKeySemantics.Ordered,
                ["script", "label", "index", "source-line", "opcode", "operands-base64", "jump-targets"],
                headerRequired: true)).Rows)
        {
            string name = row.RequiredString(0);
            if (!groups.TryGetValue(name, out List<Instruction>? group))
                groups.Add(name, group = new());
            int index = row.UnsignedDecimal(2);
            if (index != group.Count) throw row.Invalid(2, "source script command order");
            group.Add(new(name, row.RequiredString(1), index, row.UnsignedDecimal(3),
                row.RequiredString(4), row.Base64Utf8(5), row.String(6)));
        }
        foreach ((string name, List<Instruction> group) in groups)
            _scripts.Add(name, Compile(group));
        if (_scripts.Count != 2 || !_scripts.ContainsKey("faroreUnlinked") ||
            !_scripts.ContainsKey("faroreLinked"))
            throw new InvalidOperationException("Farore linked/unlinked source streams are missing.");

        int rewardIndex = 0;
        foreach (GeneratedTableRow row in GeneratedTable.Load(
            "res://assets/oracle/cutscenes/farore_rewards.tsv",
            new GeneratedTableSchema("Farore rewards", GeneratedTableKeySemantics.Unique,
                ["index", "treasure-symbol", "subid", "kind"], ["index"], headerRequired: true)).Rows)
        {
            if (row.HexByte(0) != rewardIndex || rewardIndex >= _rewards.Length)
                throw row.Invalid(0, "ten ordered Farore rewards $00-$09");
            string kind = row.RequiredString(3);
            if (kind is not ("chest" or "upgrade")) throw row.Invalid(3, "chest or upgrade");
            _rewards[rewardIndex++] = new(row.RequiredString(1), row.HexByte(2), kind == "upgrade");
        }
        if (rewardIndex != 10) throw new InvalidOperationException("Farore reward table is incomplete.");
    }

    internal IReadOnlyList<CutsceneCommand> Script(bool linked) =>
        _scripts[linked ? "faroreLinked" : "faroreUnlinked"];
    internal string Text(int id) => _texts.TryGetValue(id, out string? value) ? value :
        throw new InvalidOperationException($"Missing Farore TX_{id:x4}.");
    internal FaroreRewardRecord Reward(int index) => (uint)index < 10 ? _rewards[index] :
        throw new InvalidOperationException($"Invalid Farore secret index ${index:x2}.");

    private IReadOnlyList<CutsceneCommand> Compile(List<Instruction> source)
    {
        var labels = new Dictionary<string, int>(StringComparer.Ordinal);
        int next = 0;
        foreach (Instruction entry in source)
        {
            labels.TryAdd(entry.Label, next);
            next += entry.Opcode == "rungenericnpclowindex" ? 4 : 1;
        }
        var compiled = new List<CutsceneCommand>(next);
        foreach (Instruction entry in source)
        {
            int index = compiled.Count;
            var origin = new CutsceneCommandSource(entry.Script, entry.Label,
                index, entry.Line, entry.Opcode);
            string[] fields = entry.Operands.Split(',', StringSplitOptions.TrimEntries);
            int Number(string value)
            {
                if (value.StartsWith('$') && int.TryParse(value.AsSpan(1),
                    NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hexadecimal))
                    return hexadecimal;
                if (int.TryParse(value, NumberStyles.None,
                    CultureInfo.InvariantCulture, out int decimalValue))
                    return decimalValue;
                throw new InvalidOperationException($"{origin}: invalid scalar {value}.");
            }
            int Target(string value) => labels.TryGetValue(value, out int target) ? target :
                throw new InvalidOperationException($"{origin}: missing target {value}.");
            int TextId(string value) => value.StartsWith("<TX_", StringComparison.Ordinal) &&
                int.TryParse(value.AsSpan(4), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out int id) ? id :
                throw new InvalidOperationException($"{origin}: invalid text {value}.");
            CutsceneCommand command = entry.Opcode switch
            {
                "initcollisions" => new CutsceneInitCollisionsCommand(origin, "Farore"),
                "checkabutton" => new CutsceneCheckAButtonCommand(origin, "Farore"),
                "enableinput" => new CutsceneEnableInputCommand(origin),
                "disableinput" => new CutsceneDisableInputCommand(origin),
                "setdisabledobjectsto91" => new CutsceneSetDisabledObjectsCommand(origin, 0x91),
                "showtextlowindex" => new CutsceneShowTextCommand(origin,
                    TextId(fields[0]), Text(TextId(fields[0])), 2),
                "rungenericnpclowindex" => new CutsceneInitCollisionsCommand(origin, "Farore"),
                "jumpifglobalflagset" => new CutsceneMemoryBranchCommand(origin,
                    fields[0], 1, Target(fields[1])),
                "jumpiftextoptioneq" => new CutsceneTextOptionBranchCommand(origin,
                    Number(fields[0]), Target(fields[1])),
                "scriptjump" => new CutsceneBranchCommand(origin, Target(fields[0])),
                "jumptable_objectbyte" => new CutsceneMemoryJumpTableCommand(origin,
                    fields[0], Array.ConvertAll(entry.JumpTargets.Split(','), Target)),
                "askforsecret" => fields[0] == "$ff"
                    ? new CutsceneNativeYieldCommand(origin, "AskAnySecret")
                    : throw new InvalidOperationException($"{origin}: expected askforsecret $ff."),
                "asm15" => new CutsceneNativeCommand(origin, fields[0]),
                "wait" => new CutsceneWaitCommand(origin, int.Parse(fields[0], CultureInfo.InvariantCulture)),
                "checkcfc0bit" => Number(fields[0]) == 1
                    ? new CutsceneMemoryGateCommand(origin, "cfc0bit1", 1)
                    : throw new InvalidOperationException($"{origin}: unsupported cfc0 bit."),
                "xorcfc0bit" => Number(fields[0]) == 1
                    ? new CutsceneNativeCommand(origin, "ClearCfc0Bit1")
                    : throw new InvalidOperationException($"{origin}: unsupported cfc0 bit."),
                _ => throw new InvalidOperationException($"{origin}: unsupported Farore opcode.")
            };
            compiled.Add(command);
            if (entry.Opcode == "rungenericnpclowindex")
            {
                int text = TextId(fields[0]);
                compiled.Add(new CutsceneCheckAButtonCommand(origin with { CommandIndex = index + 1 }, "Farore"));
                compiled.Add(new CutsceneShowTextCommand(origin with { CommandIndex = index + 2 },
                    text, Text(text), 2));
                compiled.Add(new CutsceneBranchCommand(origin with { CommandIndex = index + 3 }, index + 1));
            }
        }
        return compiled.AsReadOnly();
    }

    private readonly record struct Instruction(string Script, string Label, int Index,
        int Line, string Opcode, string Operands, string JumpTargets);
}

internal readonly record struct FaroreRewardRecord(string Symbol, int SubId, bool Upgrade);
