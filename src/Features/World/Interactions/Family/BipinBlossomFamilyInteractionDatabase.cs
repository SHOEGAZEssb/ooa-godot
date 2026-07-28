using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Dialogue used by scripted Bipin/Blossom interactions which are not an
/// actor's ordinary stage-selected talk loop, including past Bipin $28:$0a.
/// </summary>
internal sealed class BipinBlossomFamilyInteractionDatabase
{
    private readonly Dictionary<int, string> _texts = new();
    internal RunningBipinRecord RunningBipin { get; }

    public BipinBlossomFamilyInteractionDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/bipin_blossom_family_texts.tsv",
            new GeneratedTableSchema(
                "Bipin and Blossom interaction text",
                GeneratedTableKeySemantics.Unique,
                ["text-id", "utf8-base64"],
                ["text-id"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int textId = row.HexWord(0);
            _texts.Add(textId, row.Base64Utf8(1));
        }

        if (_texts.Count != 8)
            throw new InvalidOperationException(
                $"Expected eight Bipin/Blossom interaction texts, got {_texts.Count}.");

        GeneratedTable runningBipin = GeneratedTable.Load(
            "res://assets/oracle/objects/running_bipin.tsv",
            new GeneratedTableSchema(
                "Running Bipin",
                GeneratedTableKeySemantics.Ordered,
                [
                    "speed-raw", "initial-angle", "minimum-x", "span-x",
                    "reverse-angle-xor", "initial-animation",
                    "animation-toggle-xor", "initial-animation-data",
                    "alternate-animation-data", "source"
                ],
                headerRequired: true));
        if (runningBipin.Rows.Count != 1)
            throw new InvalidOperationException(
                $"Expected one Running Bipin record, got {runningBipin.Rows.Count}.");
        GeneratedTableRow runningRow = runningBipin.Rows[0];
        RunningBipin = new RunningBipinRecord(
            runningRow.HexByte(0),
            runningRow.HexByte(1),
            runningRow.HexByte(2),
            runningRow.HexByte(3),
            runningRow.HexByte(4),
            runningRow.HexByte(5),
            runningRow.HexByte(6),
            runningRow.RequiredString(7),
            runningRow.RequiredString(8),
            runningRow.RequiredString(9));
        if (RunningBipin.SpeedRaw == 0 ||
            RunningBipin.InitialAngle >= 0x20 ||
            RunningBipin.SpanX == 0 ||
            RunningBipin.ReverseAngleXor >= 0x20 ||
            RunningBipin.AnimationToggleXor == 0)
        {
            throw new InvalidOperationException(
                $"Malformed Running Bipin record from {RunningBipin.Source}.");
        }
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
}

internal readonly record struct Dialogue(int TextId, string Message);

internal readonly record struct RunningBipinRecord(
    int SpeedRaw,
    int InitialAngle,
    int MinimumX,
    int SpanX,
    int ReverseAngleXor,
    int InitialAnimation,
    int AnimationToggleXor,
    string InitialAnimationData,
    string AlternateAnimationData,
    string Source);
