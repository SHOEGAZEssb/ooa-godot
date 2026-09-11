using System;
using System.Globalization;
using System.Linq;

namespace oracleofages;

// Compiled by both the runtime and importer; no Godot or source-file dependency.
internal static class CutsceneFieldShape
{
    internal static bool IsValid(string shape, string value)
    {
        return shape switch
        {
            "none" => value.Length == 0,
            "optional" => true,
            "required" => !string.IsNullOrWhiteSpace(value),
            "hex" => int.TryParse(
                value,
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out _),
            "decimal" => int.TryParse(
                value,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out _),
            "optional-decimal" => value.Length == 0 || int.TryParse(
                value,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out _),
            "positive-decimal" => int.TryParse(
                    value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int positive) &&
                positive > 0,
            "text-variants" => HasExactlyOneSeparator(value, '\0'),
            "memory-jump-table" => ValidMemoryJumpTable(value),
            "translation" => ValidTranslation(value),
            "parallel-translation" => ValidParallelTranslation(value),
            "native-block" =>
                !string.IsNullOrWhiteSpace(value.Split('\0', 2)[0]),
            _ => false
        };
    }

    private static bool ValidMemoryJumpTable(string value)
    {
        string[] sections = value.Split('|');
        if (sections.Length != 2 || string.IsNullOrWhiteSpace(sections[0]))
            return false;
        string[] targets = sections[1].Split(',');
        return targets.Length != 0 && targets.All(target => int.TryParse(
            target,
            NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out _));
    }

    private static bool ValidTranslation(string value)
    {
        string[] values = value.Split(',');
        return values.Length == 3 &&
            Finite(values[0]) &&
            Finite(values[1]) &&
            values[2] is "0" or "1";
    }

    private static bool ValidParallelTranslation(string value)
    {
        string[] lanes = value.Split('|');
        if (lanes.Length != 3 || string.IsNullOrWhiteSpace(lanes[1]))
            return false;
        return ValidVector(lanes[0]) && ValidVector(lanes[2]);
    }

    private static bool ValidVector(string value)
    {
        string[] components = value.Split(',');
        return components.Length == 2 &&
            Finite(components[0]) &&
            Finite(components[1]);
    }

    private static bool Finite(string value) =>
        float.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out float parsed) &&
        float.IsFinite(parsed);

    private static bool HasExactlyOneSeparator(string value, char separator)
    {
        int first = value.IndexOf(separator);
        return first >= 0 && value.IndexOf(separator, first + 1) < 0;
    }

}
