using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Typed disassembly records shared by the construction interactions in
/// lower Black Tower rooms $e0/$e1/$e2/$e7/$e8.
/// </summary>
internal sealed class BlackTowerWorkerDatabase
{

    private readonly Dictionary<int, string> _texts = new();
    private readonly Lookup<string, int> _selectors =
        new(StringComparer.Ordinal);
    private readonly Dictionary<
        string, BlackTowerWorkerDatabaseVisualRecord> _visuals = new();
    private readonly Dictionary<int, PatrolLeg[]> _patrols = new();
    private readonly Dictionary<string, int> _constants = new();

    internal int Speed80 => Constant("speed-80");
    internal int Speed100 => Constant("speed-100");
    internal int PatrolWait => Constant("patrol-wait");
    internal int TalkWait => Constant("talk-wait");
    internal int BlockerDistance => Constant("blocker-distance");
    internal int BlockerWait => Constant("blocker-wait");
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

        GeneratedTable selectors = GeneratedTable.Load(
            "res://assets/oracle/objects/black_tower_selectors.tsv",
            new GeneratedTableSchema(
                "lower Black Tower selectors",
                GeneratedTableKeySemantics.Unique,
                ["selector", "index", "value"],
                ["selector", "index"],
                headerRequired: true));
        foreach (GeneratedTableRow row in selectors.Rows)
        {
            string selector = row.RequiredString(0);
            int value = selector switch
            {
                "pickaxe-animation" => row.HexByte(2),
                "pickaxe-text" or "hardhat-text" or "soldier-text" =>
                    row.HexWord(2),
                _ => throw row.Invalid(
                    0,
                    "pickaxe-animation, pickaxe-text, hardhat-text, or soldier-text")
            };
            List<int> values = _selectors.GetOrAdd(selector);
            int index = row.UnsignedDecimal(1);
            if (index != values.Count)
                throw row.Invalid(1, $"the next contiguous index {values.Count}");
            values.Add(value);
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
            _visuals.Add(row.RequiredString(0), new BlackTowerWorkerDatabaseVisualRecord(
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

        if (_texts.Count != 16 || _selectors.Count != 4 ||
            SelectorCount("pickaxe-animation") != 8 ||
            SelectorCount("pickaxe-text") != 8 ||
            SelectorCount("hardhat-text") != 5 ||
            SelectorCount("soldier-text") != 4 ||
            _visuals.Count != 12 ||
            _patrols.Count != 5 || _constants.Count != 6 ||
            Visual("hardhat-work").Animation.Length == 0 ||
            Visual("shovel").Animation.Length == 0 ||
            Speed80 != 0x14 || Speed100 != 0x28 ||
            PatrolWait != 20 || TalkWait != 30 ||
            BlockerDistance != 16 || BlockerWait != 10)
        {
            throw new InvalidOperationException(
                "Imported lower Black Tower interaction contract is incomplete.");
        }
        foreach (string selector in new[]
                 {
                     "pickaxe-text", "hardhat-text", "soldier-text"
                 })
        {
            foreach (int textId in _selectors[selector])
            {
                if (!_texts.ContainsKey(textId))
                {
                    throw new InvalidOperationException(
                        $"Black Tower selector '{selector}' references " +
                        $"unimported TX_{textId:x4}.");
                }
            }
        }
    }

    internal string Text(int textId) => _texts.TryGetValue(textId, out string? text)
        ? text
        : throw new KeyNotFoundException(
            $"Black Tower text TX_{textId:x4} was not imported.");

    internal BlackTowerWorkerDatabaseVisualRecord Visual(string key) =>
        _visuals.TryGetValue(key, out BlackTowerWorkerDatabaseVisualRecord visual)
            ? visual
            : throw new KeyNotFoundException(
                $"Black Tower visual '{key}' was not imported.");

    internal PatrolLeg[] Patrol(int var03) =>
        _patrols.TryGetValue(var03, out PatrolLeg[]? patrol)
            ? patrol
            : throw new KeyNotFoundException(
                $"Black Tower hardhat patrol var03=${var03:x2} was not imported.");

    internal int PickaxeAnimation(int index) =>
        Selector("pickaxe-animation", index);
    internal int PickaxeText(int index) => Selector("pickaxe-text", index);
    internal int HardhatText(int index) => Selector("hardhat-text", index);
    internal int SoldierText(int index) => Selector("soldier-text", index);

    private int Selector(string key, int index) =>
        _selectors.TryGetValues(key, out IReadOnlyList<int> values) &&
        index >= 0 && index < values.Count
            ? values[index]
            : throw new KeyNotFoundException(
                $"Black Tower selector '{key}' has no index {index}.");

    private int SelectorCount(string key) =>
        _selectors.TryGetValues(key, out IReadOnlyList<int> values)
            ? values.Count
            : 0;

    private int Constant(string key) => _constants.TryGetValue(key, out int value)
        ? value
        : throw new KeyNotFoundException(
            $"Black Tower constant '{key}' was not imported.");

    private static InvalidOperationException Malformed(string kind, string line) =>
        new($"Malformed Black Tower {kind} row: {line}");
}

internal readonly record struct BlackTowerWorkerDatabaseVisualRecord(
    string Sprite,
    int TileBase,
    int Palette,
    string Animation);

internal readonly record struct PatrolLeg(int Direction, int Counter);
