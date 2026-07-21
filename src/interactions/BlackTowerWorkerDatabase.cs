using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Typed disassembly records shared by the construction interactions in
/// lower Black Tower rooms $e0/$e1/$e2/$e7/$e8.
/// </summary>
internal sealed class BlackTowerWorkerDatabase
{
    internal readonly record struct VisualRecord(
        string Sprite, int TileBase, int Palette, string Animation);
    internal readonly record struct PatrolLeg(int Direction, int Counter);

    private readonly Dictionary<int, string> _texts = new();
    private readonly Dictionary<string, VisualRecord> _visuals = new();
    private readonly Dictionary<int, PatrolLeg[]> _patrols = new();
    private readonly Dictionary<string, int> _constants = new();

    internal int Speed80 => Constant("speed-80");
    internal int Speed100 => Constant("speed-100");
    internal int PatrolWait => Constant("patrol-wait");
    internal int TalkWait => Constant("talk-wait");
    internal int BlockerDistance => Constant("blocker-distance");
    internal int BlockerWait => Constant("blocker-wait");
    internal int EntranceMinimumY => Constant("entrance-y-min");
    internal int EntranceRadius => Constant("entrance-radius");

    public BlackTowerWorkerDatabase()
    {
        GeneratedTable texts = GeneratedTable.Load(
            "res://assets/oracle/objects/black_tower_texts.tsv",
            new GeneratedTableSchema(
                "lower Black Tower text",
                GeneratedTableKeySemantics.Unique,
                ["text-id", "utf8-base64"],
                ["text-id"],
                headerRequired: true));
        foreach (GeneratedTableRow row in texts.Rows)
        {
            _texts.Add(row.HexWord(0), row.Base64Utf8(1));
        }

        GeneratedTable visuals = GeneratedTable.Load(
            "res://assets/oracle/objects/black_tower_visuals.tsv",
            new GeneratedTableSchema(
                "lower Black Tower visuals",
                GeneratedTableKeySemantics.Unique,
                ["key", "sprite", "tile-base", "palette", "animation"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in visuals.Rows)
        {
            _visuals.Add(row.RequiredString(0), new VisualRecord(
                row.RequiredString(1), row.UnsignedDecimal(2),
                row.UnsignedDecimal(3), row.RequiredString(4)));
        }

        GeneratedTable patrols = GeneratedTable.Load(
            "res://assets/oracle/objects/black_tower_patrols.tsv",
            new GeneratedTableSchema(
                "lower Black Tower patrols",
                GeneratedTableKeySemantics.Unique,
                ["var03", "direction:counter,..."],
                ["var03"],
                headerRequired: true));
        foreach (GeneratedTableRow row in patrols.Rows)
        {
            string[] encodedLegs = row.RequiredString(1).Split(',');
            var legs = new PatrolLeg[encodedLegs.Length];
            for (int index = 0; index < encodedLegs.Length; index++)
            {
                string[] values = encodedLegs[index].Split(':');
                if (values.Length != 2)
                    throw Malformed("patrol leg", encodedLegs[index]);
                legs[index] = new PatrolLeg(
                    int.Parse(values[0]), int.Parse(values[1]));
            }
            _patrols.Add(row.UnsignedDecimal(0), legs);
        }

        GeneratedTable constants = GeneratedTable.Load(
            "res://assets/oracle/objects/black_tower_constants.tsv",
            new GeneratedTableSchema(
                "lower Black Tower constants",
                GeneratedTableKeySemantics.Unique,
                ["key", "value"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in constants.Rows)
        {
            _constants.Add(row.RequiredString(0), row.Decimal(1));
        }

        if (_texts.Count != 17 || _visuals.Count != 12 ||
            _patrols.Count != 5 || _constants.Count != 8 ||
            Text(0x020f) != " The Black Tower" ||
            Visual("hardhat-work").Animation.Length == 0 ||
            Visual("shovel").Animation.Length == 0 ||
            Speed80 != 0x14 || Speed100 != 0x28 ||
            PatrolWait != 20 || TalkWait != 30 ||
            BlockerDistance != 16 || BlockerWait != 10 ||
            EntranceMinimumY != 0x78 || EntranceRadius != 8)
        {
            throw new InvalidOperationException(
                "Imported lower Black Tower interaction contract is incomplete.");
        }
    }

    internal string Text(int textId) => _texts.TryGetValue(textId, out string? text)
        ? text
        : throw new KeyNotFoundException(
            $"Black Tower text TX_{textId:x4} was not imported.");

    internal VisualRecord Visual(string key) =>
        _visuals.TryGetValue(key, out VisualRecord visual)
            ? visual
            : throw new KeyNotFoundException(
                $"Black Tower visual '{key}' was not imported.");

    internal PatrolLeg[] Patrol(int var03) =>
        _patrols.TryGetValue(var03, out PatrolLeg[]? patrol)
            ? patrol
            : throw new KeyNotFoundException(
                $"Black Tower hardhat patrol var03=${var03:x2} was not imported.");

    private int Constant(string key) => _constants.TryGetValue(key, out int value)
        ? value
        : throw new KeyNotFoundException(
            $"Black Tower constant '{key}' was not imported.");

    private static InvalidOperationException Malformed(string kind, string line) =>
        new($"Malformed Black Tower {kind} row: {line}");
}
