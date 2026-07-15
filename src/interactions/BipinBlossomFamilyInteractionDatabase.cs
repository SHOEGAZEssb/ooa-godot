using Godot;
using System;
using System.Collections.Generic;
using System.Text;

namespace oracleofages;

/// <summary>
/// Dialogue used by the scripted parts of the Bipin/Blossom family which are
/// not an actor's ordinary stage-selected talk loop.
/// </summary>
internal sealed class BipinBlossomFamilyInteractionDatabase
{
    private readonly Dictionary<int, string> _texts = new();

    public BipinBlossomFamilyInteractionDatabase()
    {
        string source = FileAccess.GetFileAsString(
            "res://assets/oracle/objects/bipin_blossom_family_texts.tsv");
        foreach (string rawLine in source.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;

            string[] columns = line.Split('\t');
            if (columns.Length != 2)
                throw new InvalidOperationException($"Malformed family interaction text row: {line}");
            int textId = Convert.ToInt32(columns[0], 16);
            _texts.Add(
                textId,
                Encoding.UTF8.GetString(Convert.FromBase64String(columns[1])));
        }

        if (_texts.Count != 5)
            throw new InvalidOperationException(
                $"Expected five Bipin/Blossom interaction texts, got {_texts.Count}.");
    }

    public Dialogue Text(
        int textId,
        OracleSaveData save,
        string? childNameOverride = null)
    {
        if (!_texts.TryGetValue(textId, out string? message))
            throw new KeyNotFoundException($"Family interaction text TX_{textId:x4} was not imported.");
        return new Dialogue(textId, SubstituteChildName(
            message,
            childNameOverride ?? save.ChildName));
    }

    public static string SubstituteChildName(string message, OracleSaveData save)
        => SubstituteChildName(message, save.ChildName);

    private static string SubstituteChildName(string message, string childName)
    {
        if (string.IsNullOrEmpty(childName))
            childName = "Child";
        return message.Replace("\\Child", childName, StringComparison.Ordinal);
    }

    internal readonly record struct Dialogue(int TextId, string Message);
}
