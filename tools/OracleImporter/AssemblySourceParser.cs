using System.Globalization;
using System.Text.RegularExpressions;

namespace OracleOfAges.Importer;

internal static partial class AssemblySourceParser
{
    private static readonly HashSet<string> Instructions = new(
        new[]
        {
            "adc", "add", "and", "bit", "call", "ccf", "cp", "cpd", "cpdr",
            "cpi", "cpir", "cpl", "daa", "dec", "di", "djnz", "ei", "ex",
            "exx", "halt", "im", "in", "inc", "ind", "indr", "ini", "inir",
            "jp", "jr", "ld", "ldd", "lddr", "ldi", "ldir", "neg", "nop",
            "or", "otdr", "otir", "out", "outd", "outi", "pop", "push",
            "res", "ret", "reti", "retn", "rl", "rla", "rlc", "rlca", "rld",
            "rr", "rra", "rrc", "rrca", "rrd", "rst", "sbc", "scf", "set",
            "sla", "sll", "sra", "srl", "stop", "sub", "xor",
        },
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> DataDirectives = new(
        new[] { ".db", ".dw", ".dl", ".ds", "db", "dw", "dl", "ds" },
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> Directives = new(
        new[]
        {
            ".align", ".bank", ".define", ".else", ".elif", ".endc",
            ".endif", ".enum", ".fail", ".if", ".ifdef", ".ifndef",
            ".incbin", ".include", ".macro", ".redefine", ".rept",
            ".section", ".shift", ".undef", "else", "endc", "endif",
            "enum_end", "enum_start", "enum_value",
        },
        StringComparer.OrdinalIgnoreCase);

    public static AssemblySourceFile Parse(
        string relativePath,
        string fullPath,
        string text,
        IReadOnlySet<string> configuredSymbols)
    {
        (string[] lines, int[] starts) = SplitLines(text);
        var nodes = new List<AssemblyNode>(lines.Length);
        var labels = NewMultiMap<AssemblyLabel>();
        var constants = NewMultiMap<AssemblyConstant>();
        var directives = NewMultiMap<AssemblyNode>();
        var macros = NewMultiMap<AssemblyNode>();
        var instructions = NewMultiMap<AssemblyNode>();
        var conditionals = new Stack<ConditionalFrame>();
        bool active = true;
        string? globalLabel = null;

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            string raw = lines[lineIndex];
            int offset = starts[lineIndex];
            (string code, string comment) = SplitComment(raw);
            string trimmed = code.Trim();
            int firstColumn = FirstNonWhitespaceColumn(raw);
            var span = new SourceSpan(
                new SourcePosition(relativePath, offset + firstColumn, lineIndex + 1, firstColumn + 1),
                Math.Max(0, raw.Length - firstColumn));

            if (trimmed.Length == 0)
            {
                nodes.Add(new AssemblyNode(
                    comment.Length == 0 ? AssemblyNodeKind.Blank : AssemblyNodeKind.Comment,
                    span, raw, code, comment, string.Empty, Array.Empty<string>(),
                    active, globalLabel));
                continue;
            }

            string firstToken = FirstToken(trimmed);
            string normalizedToken = firstToken.ToLowerInvariant();
            if (IsConditionalDirective(normalizedToken))
            {
                if (IsConditionalContinuation(normalizedToken) &&
                    conditionals.Count == 0)
                {
                    throw new AssemblySourceException(
                        $"{relativePath}:{lineIndex + 1}:{firstColumn + 1}: " +
                        $"conditional directive '{firstToken}' has no open block.");
                }
                bool nodeActive = active;
                ProcessConditional(
                    normalizedToken,
                    trimmed[firstToken.Length..].Trim(),
                    configuredSymbols,
                    conditionals,
                    lineIndex + 1,
                    ref active);
                var conditionalNode = new AssemblyNode(
                    AssemblyNodeKind.Directive, span, raw, code, comment,
                    firstToken, SplitOperands(trimmed[firstToken.Length..]),
                    nodeActive, globalLabel);
                nodes.Add(conditionalNode);
                Add(directives, firstToken, conditionalNode);
                continue;
            }

            Match labelMatch = LabelRegex().Match(trimmed);
            if (labelMatch.Success)
            {
                string labelName = labelMatch.Groups["name"].Value;
                bool local = AssemblySourceFile.IsLocalLabel(labelName);
                if (!local)
                    globalLabel = labelName;
                var node = new AssemblyNode(
                    AssemblyNodeKind.Label, span, raw, code, comment, labelName,
                    Array.Empty<string>(), active, globalLabel);
                int nodeIndex = nodes.Count;
                nodes.Add(node);
                Add(labels, labelName, new AssemblyLabel(
                    labelName, span, nodeIndex, local, active));

                string tail = labelMatch.Groups["tail"].Value.Trim();
                if (tail.Length != 0)
                {
                    AddInlineNode(
                        relativePath, raw, comment, lineIndex, offset, firstColumn,
                        tail, active, globalLabel, nodes, constants, directives,
                        macros, instructions);
                }
                continue;
            }

            AddParsedNode(
                span, raw, code, comment, trimmed, active, globalLabel, nodes,
                constants, directives, macros, instructions);
        }

        if (conditionals.Count != 0)
        {
            ConditionalFrame frame = conditionals.Peek();
            throw new AssemblySourceException(
                $"{relativePath}:{frame.Line}:1: conditional block was not closed.");
        }

        return new AssemblySourceFile(
            relativePath, fullPath, text, lines, starts, nodes.AsReadOnly(),
            labels, constants, directives, macros, instructions);
    }

    private static void AddInlineNode(
        string relativePath,
        string raw,
        string comment,
        int lineIndex,
        int offset,
        int firstColumn,
        string tail,
        bool active,
        string? globalLabel,
        List<AssemblyNode> nodes,
        Dictionary<string, List<AssemblyConstant>> constants,
        Dictionary<string, List<AssemblyNode>> directives,
        Dictionary<string, List<AssemblyNode>> macros,
        Dictionary<string, List<AssemblyNode>> instructions)
    {
        int tailColumn = raw.IndexOf(tail, firstColumn, StringComparison.Ordinal);
        var span = new SourceSpan(
            new SourcePosition(relativePath, offset + tailColumn, lineIndex + 1, tailColumn + 1),
            tail.Length);
        AddParsedNode(
            span, raw, tail, comment, tail, active, globalLabel, nodes,
            constants, directives, macros, instructions);
    }

    private static void AddParsedNode(
        SourceSpan span,
        string raw,
        string code,
        string comment,
        string trimmed,
        bool active,
        string? globalLabel,
        List<AssemblyNode> nodes,
        Dictionary<string, List<AssemblyConstant>> constants,
        Dictionary<string, List<AssemblyNode>> directives,
        Dictionary<string, List<AssemblyNode>> macros,
        Dictionary<string, List<AssemblyNode>> instructions)
    {
        Match constantMatch = ConstantRegex().Match(trimmed);
        if (constantMatch.Success)
        {
            string name = constantMatch.Groups["defineName"].Success
                ? constantMatch.Groups["defineName"].Value
                : constantMatch.Groups["equName"].Value;
            string expression = constantMatch.Groups["defineValue"].Success
                ? constantMatch.Groups["defineValue"].Value.Trim()
                : constantMatch.Groups["equValue"].Value.Trim();
            var node = new AssemblyNode(
                AssemblyNodeKind.Constant, span, raw, code, comment, name,
                new[] { expression }, active, globalLabel);
            int nodeIndex = nodes.Count;
            nodes.Add(node);
            Add(constants, name, new AssemblyConstant(name, expression, span, nodeIndex, active));
            return;
        }

        string token = FirstToken(trimmed);
        string operandText = trimmed[token.Length..].Trim();
        IReadOnlyList<string> operands = SplitOperands(operandText);
        AssemblyNodeKind kind;
        if (DataDirectives.Contains(token))
            kind = AssemblyNodeKind.Data;
        else if (Directives.Contains(token) || token.StartsWith('.'))
            kind = AssemblyNodeKind.Directive;
        else if (Instructions.Contains(token))
            kind = AssemblyNodeKind.Instruction;
        else if (IdentifierRegex().IsMatch(token))
            kind = AssemblyNodeKind.MacroInvocation;
        else
            kind = AssemblyNodeKind.Unrecognized;

        var parsed = new AssemblyNode(
            kind, span, raw, code, comment, token, operands, active, globalLabel);
        nodes.Add(parsed);
        switch (kind)
        {
            case AssemblyNodeKind.Data:
            case AssemblyNodeKind.Directive:
                Add(directives, token, parsed);
                break;
            case AssemblyNodeKind.MacroInvocation:
                Add(macros, token, parsed);
                break;
            case AssemblyNodeKind.Instruction:
                Add(instructions, token, parsed);
                break;
        }
    }

    private static void ProcessConditional(
        string directive,
        string expression,
        IReadOnlySet<string> symbols,
        Stack<ConditionalFrame> stack,
        int line,
        ref bool active)
    {
        if (directive is ".ifdef" or "ifdef" or ".ifndef" or "ifndef" or ".if" or "if")
        {
            bool condition = directive switch
            {
                ".ifdef" or "ifdef" => symbols.Contains(FirstToken(expression)),
                ".ifndef" or "ifndef" => !symbols.Contains(FirstToken(expression)),
                _ => EvaluateCondition(expression, symbols) ?? true,
            };
            var frame = new ConditionalFrame(active, condition, condition, line);
            stack.Push(frame);
            active = frame.ParentActive && condition;
            return;
        }

        ConditionalFrame current = stack.Pop();
        if (directive is ".else" or "else")
        {
            bool branch = !current.AnyTaken;
            current = current with { BranchActive = branch, AnyTaken = true };
            stack.Push(current);
            active = current.ParentActive && branch;
        }
        else if (directive is ".elif" or "elif")
        {
            bool condition = !current.AnyTaken &&
                (EvaluateCondition(expression, symbols) ?? true);
            current = current with
            {
                BranchActive = condition,
                AnyTaken = current.AnyTaken || condition,
            };
            stack.Push(current);
            active = current.ParentActive && condition;
        }
        else
        {
            active = current.ParentActive;
        }
    }

    private static bool? EvaluateCondition(string expression, IReadOnlySet<string> symbols)
    {
        string value = expression.Trim();
        if (value.Length == 0)
            return null;

        string[] orParts = value.Split("||", StringSplitOptions.TrimEntries);
        if (orParts.Length > 1)
        {
            bool anyUnknown = false;
            foreach (string part in orParts)
            {
                bool? result = EvaluateCondition(part, symbols);
                if (result == true)
                    return true;
                anyUnknown |= result is null;
            }
            return anyUnknown ? null : false;
        }

        string[] andParts = value.Split("&&", StringSplitOptions.TrimEntries);
        if (andParts.Length > 1)
        {
            bool anyUnknown = false;
            foreach (string part in andParts)
            {
                bool? result = EvaluateCondition(part, symbols);
                if (result == false)
                    return false;
                anyUnknown |= result is null;
            }
            return anyUnknown ? null : true;
        }

        if (value.StartsWith('!'))
        {
            bool? nested = EvaluateCondition(value[1..], symbols);
            return nested is null ? null : !nested.Value;
        }

        Match defined = DefinedRegex().Match(value);
        if (defined.Success)
            return symbols.Contains(defined.Groups["name"].Value);
        if (IdentifierRegex().IsMatch(value))
            return symbols.Contains(value);
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
            return number != 0;
        return null;
    }

    private static bool IsConditionalDirective(string value) =>
        value is ".if" or "if" or ".ifdef" or "ifdef" or ".ifndef" or "ifndef" or
            ".elif" or "elif" or ".else" or "else" or ".endc" or "endc" or
            ".endif" or "endif";

    private static bool IsConditionalContinuation(string value) =>
        value is ".elif" or "elif" or ".else" or "else" or ".endc" or "endc" or
            ".endif" or "endif";

    private static (string Code, string Comment) SplitComment(string line)
    {
        bool quoted = false;
        bool escaped = false;
        for (int index = 0; index < line.Length; index++)
        {
            char character = line[index];
            if (escaped)
            {
                escaped = false;
                continue;
            }
            if (character == '\\' && quoted)
            {
                escaped = true;
                continue;
            }
            if (character == '"')
            {
                quoted = !quoted;
                continue;
            }
            if (character == ';' && !quoted)
                return (line[..index], line[(index + 1)..]);
        }
        return (line, string.Empty);
    }

    private static IReadOnlyList<string> SplitOperands(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        var result = new List<string>();
        int start = 0;
        int nesting = 0;
        bool quoted = false;
        bool escaped = false;
        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (escaped)
            {
                escaped = false;
                continue;
            }
            if (character == '\\' && quoted)
            {
                escaped = true;
                continue;
            }
            if (character == '"')
            {
                quoted = !quoted;
                continue;
            }
            if (quoted)
                continue;
            if (character is '(' or '[' or '{')
                nesting++;
            else if (character is ')' or ']' or '}')
                nesting = Math.Max(0, nesting - 1);
            else if (character == ',' && nesting == 0)
            {
                result.Add(text[start..index].Trim());
                start = index + 1;
            }
        }
        result.Add(text[start..].Trim());
        return result;
    }

    private static string FirstToken(string text)
    {
        int end = 0;
        while (end < text.Length && !char.IsWhiteSpace(text[end]))
            end++;
        return text[..end];
    }

    private static int FirstNonWhitespaceColumn(string line)
    {
        int index = 0;
        while (index < line.Length && char.IsWhiteSpace(line[index]))
            index++;
        return index;
    }

    private static (string[] Lines, int[] Starts) SplitLines(string text)
    {
        var lines = new List<string>();
        var starts = new List<int>();
        int start = 0;
        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] != '\r' && text[index] != '\n')
                continue;
            starts.Add(start);
            lines.Add(text[start..index]);
            if (text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                index++;
            start = index + 1;
        }
        starts.Add(start);
        lines.Add(text[start..]);
        return (lines.ToArray(), starts.ToArray());
    }

    private static Dictionary<string, List<T>> NewMultiMap<T>() =>
        new(StringComparer.OrdinalIgnoreCase);

    private static void Add<T>(Dictionary<string, List<T>> map, string key, T value)
    {
        if (!map.TryGetValue(key, out List<T>? values))
        {
            values = new List<T>();
            map.Add(key, values);
        }
        values.Add(value);
    }

    private readonly record struct ConditionalFrame(
        bool ParentActive,
        bool BranchActive,
        bool AnyTaken,
        int Line);

    [GeneratedRegex(
        @"^(?<name>[A-Za-z_@.][A-Za-z0-9_@.#?]*):(?<tail>.*)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex LabelRegex();

    [GeneratedRegex(
        @"^(?:(?:\.define|\.redefine)\s+(?<defineName>[A-Za-z_][A-Za-z0-9_@.#?]*)\s+(?<defineValue>.+)|(?<equName>[A-Za-z_][A-Za-z0-9_@.#?]*)\s+(?:equ|=)\s*(?<equValue>.+))$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ConstantRegex();

    [GeneratedRegex(
        @"^[A-Za-z_@.][A-Za-z0-9_@.#?]*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierRegex();

    [GeneratedRegex(
        @"^defined\s*\(\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DefinedRegex();
}
