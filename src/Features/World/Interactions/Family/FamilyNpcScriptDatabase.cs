using System;
using System.Collections.Generic;
using System.Globalization;

namespace oracleofages;

/// <summary>The imported Ages household talk programs and their text chains.</summary>
internal sealed class FamilyNpcScriptDatabase
{
    private readonly Dictionary<string, IReadOnlyList<CutsceneCommand>> _scripts =
        new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> _texts = new();
    private readonly Dictionary<int, string> _animations = new();
    private readonly Dictionary<(int Id, int Index), string> _actorScripts = new();

    internal FamilyNpcScriptDatabase()
    {
        GeneratedTable texts = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/family_npc_texts.tsv",
            new GeneratedTableSchema("family script texts",
                GeneratedTableKeySemantics.Unique,
                ["text-id", "utf8-base64"], ["text-id"], headerRequired: true));
        foreach (GeneratedTableRow row in texts.Rows)
            _texts.Add(row.HexWord(0), row.Base64Utf8(1));

        GeneratedTable animations = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/family_npc_animations.tsv",
            new GeneratedTableSchema("family Bipin animations",
                GeneratedTableKeySemantics.Unique,
                ["animation", "encoded-data"], ["animation"], headerRequired: true));
        foreach (GeneratedTableRow row in animations.Rows)
            _animations.Add(row.HexByte(0), row.Base64Utf8(1));

        GeneratedTable program = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/family_npc_scripts.tsv",
            new GeneratedTableSchema("family NPC source scripts",
                GeneratedTableKeySemantics.Ordered,
                ["script", "label", "index", "source-line", "opcode",
                    "operands-base64", "jump-targets"], headerRequired: true));
        var grouped = new Dictionary<string, List<SourceInstruction>>(StringComparer.Ordinal);
        foreach (GeneratedTableRow row in program.Rows)
        {
            string script = row.RequiredString(0);
            if (!grouped.TryGetValue(script, out List<SourceInstruction>? instructions))
                grouped.Add(script, instructions = new());
            int index = row.UnsignedDecimal(2);
            if (index != instructions.Count)
                throw new InvalidOperationException($"{row.Path}:{row.LineNumber}: {script} " +
                    $"command index {index} follows {instructions.Count} commands.");
            instructions.Add(new SourceInstruction(script, row.RequiredString(1), index,
                row.UnsignedDecimal(3), row.RequiredString(4), row.Base64Utf8(5),
                row.String(6)));
        }
        foreach ((string script, List<SourceInstruction> instructions) in grouped)
            _scripts.Add(script, Compile(instructions));
        if (_scripts.Count != 33)
            throw new InvalidOperationException($"Expected 33 household scripts, got {_scripts.Count}.");

        GeneratedTable actorScripts = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/family_actor_scripts.tsv",
            new GeneratedTableSchema("native family actor script tables",
                GeneratedTableKeySemantics.Unique,
                ["id", "index", "script"], ["id", "index"], headerRequired: true));
        foreach (GeneratedTableRow row in actorScripts.Rows)
            _actorScripts.Add((row.HexByte(0), row.HexByte(1)), row.RequiredString(2));
        if (_actorScripts.Count != 50)
            throw new InvalidOperationException(
                $"Expected 50 native family script slots, got {_actorScripts.Count}.");
    }

    internal IReadOnlyList<CutsceneCommand> Script(NpcRecord record)
    {
        string name = ScriptName(record);
        return _scripts.TryGetValue(name, out IReadOnlyList<CutsceneCommand>? commands)
            ? commands
            : throw new InvalidOperationException($"Missing imported family script {name}.");
    }

    internal bool HasScript(NpcRecord record) => ScriptName(record) switch
    {
        "blossomScript0" or "childScript00" => false,
        _ => true
    };

    internal string Text(int textId) => _texts.TryGetValue(textId, out string? text)
        ? text
        : throw new InvalidOperationException($"Family script requested missing TX_{textId:x4}.");

    internal string Animation(int animation) => _animations.TryGetValue(animation,
        out string? encoded) ? encoded : throw new InvalidOperationException(
            $"Family script requested missing Bipin animation ${animation:x2}.");

    private string ScriptName(NpcRecord record)
    {
        int index = record.Id == InteractionId.Child ? record.Var03 : record.SubId;
        return _actorScripts.TryGetValue((record.Id, index), out string? script)
            ? script : throw new InvalidOperationException(
                $"No native family script slot for {record.Group}:{record.Room:x2} " +
                $"${record.Id:x2} index ${index:x2}.");
    }

    private IReadOnlyList<CutsceneCommand> Compile(List<SourceInstruction> source)
    {
        var labelTargets = new Dictionary<string, int>(StringComparer.Ordinal);
        int count = 0;
        foreach (SourceInstruction instruction in source)
        {
            labelTargets.TryAdd(instruction.Label, count);
            count += instruction.Opcode == "rungenericnpclowindex" ? 4 : 1;
        }
        var result = new List<CutsceneCommand>(count);
        foreach (SourceInstruction instruction in source)
        {
            int sourceIndex = result.Count;
            CutsceneCommandSource provenance = new(instruction.Script,
                instruction.Label, sourceIndex, instruction.Line, instruction.Opcode);
            string[] operands = instruction.Operands.Length == 0
                ? [] : instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
            int Scalar(int n)
            {
                if (n >= operands.Length)
                    throw Invalid(instruction, "missing operand");
                string value = operands[n];
                if (value.StartsWith('$') &&
                    int.TryParse(value.AsSpan(1), NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture, out int hexadecimal))
                    return hexadecimal;
                if (int.TryParse(value, NumberStyles.None,
                    CultureInfo.InvariantCulture, out int number))
                    return number;
                throw Invalid(instruction, $"unresolved scalar {value}");
            }
            int Target(string label) => labelTargets.TryGetValue(label, out int target)
                ? target : throw Invalid(instruction, $"unresolved target {label}");
            int TextId(int n)
            {
                if (n >= operands.Length)
                    throw Invalid(instruction, "missing text ID");
                string value = operands[n].TrimStart('<');
                if (!value.StartsWith("TX_", StringComparison.Ordinal) ||
                    !int.TryParse(value.AsSpan(3), NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture, out int id))
                    throw Invalid(instruction, $"invalid text ID {value}");
                return id;
            }
            CutsceneCommand command = instruction.Opcode switch
            {
                "initcollisions" => new CutsceneInitCollisionsCommand(provenance, "Family"),
                "makeabuttonsensitive" => new CutsceneMakeAButtonSensitiveCommand(provenance, "Family"),
                "checkabutton" => new CutsceneCheckAButtonCommand(provenance, "Family"),
                "setcollisionradii" => new CutsceneSetCollisionRadiiCommand(provenance,
                    "Family", Scalar(0), Scalar(1)),
                "setdisabledobjectsto91" => new CutsceneSetDisabledObjectsCommand(provenance, 0x91),
                "enableallobjects" => new CutsceneSetDisabledObjectsCommand(provenance, 0),
                "disableinput" => new CutsceneDisableInputCommand(provenance),
                "enableinput" => new CutsceneEnableInputCommand(provenance),
                "wait" => new CutsceneWaitCommand(provenance, Scalar(0)),
                "showtext" or "showtextlowindex" => new CutsceneShowTextCommand(provenance,
                    TextId(0), Text(TextId(0)), null),
                "showloadedtext" => new CutsceneShowLoadedTextCommand(provenance),
                "scriptjump" => new CutsceneBranchCommand(provenance, Target(operands[0])),
                "callscript" => new CutsceneCallCommand(provenance, Target(operands[0])),
                "retscript" => new CutsceneReturnCommand(provenance),
                "jumpifmemoryeq" => new CutsceneMemoryBranchCommand(provenance,
                    operands[0], Scalar(1), Target(operands[2])),
                "jumpifobjectbyteeq" => new CutsceneMemoryBranchCommand(provenance,
                    operands[0], Scalar(1), Target(operands[2])),
                "jumptable_memoryaddress" or "jumptable_objectbyte" =>
                    new CutsceneMemoryJumpTableCommand(provenance, operands[0],
                        Array.ConvertAll(instruction.JumpTargets.Split(','), Target)),
                "giveitem" => new CutsceneGiveItemCommand(provenance,
                    operands[0] switch
                    {
                        "TREASURE_GASHA_SEED" => TreasureId.GashaSeed,
                        "TREASURE_BOMBS" => TreasureId.Bombs,
                        _ => throw Invalid(instruction, $"unsupported treasure {operands[0]}")
                    }, Scalar(1)),
                "setanimation" => new CutsceneSetAnimationCommand(provenance,
                    "Family", Scalar(0), Animation(Scalar(0))),
                "asm15" or "writeobjectbyte" or "getrandombits" or "addobjectbyte" =>
                    new CutsceneNativeCommand(provenance,
                        $"{instruction.Opcode} {instruction.Operands}"),
                "rungenericnpclowindex" => new CutsceneInitCollisionsCommand(provenance, "Family"),
                _ => throw Invalid(instruction, $"unsupported opcode {instruction.Opcode}")
            };
            result.Add(command);
            if (instruction.Opcode == "rungenericnpclowindex")
            {
                int textId = TextId(0);
                result.Add(new CutsceneCheckAButtonCommand(
                    provenance with { CommandIndex = sourceIndex + 1 }, "Family"));
                result.Add(new CutsceneShowTextCommand(
                    provenance with { CommandIndex = sourceIndex + 2 },
                    textId, Text(textId), null));
                result.Add(new CutsceneBranchCommand(
                    provenance with { CommandIndex = sourceIndex + 3 },
                    sourceIndex + 1));
            }
        }
        return result.AsReadOnly();
    }

    private static InvalidOperationException Invalid(SourceInstruction source, string message) =>
        new($"family scripts.s:{source.Line}: {source.Script}/{source.Label} " +
            $"command {source.Index}: {message}.");

    private readonly record struct SourceInstruction(string Script, string Label, int Index,
        int Line, string Opcode, string Operands, string JumpTargets);
}
