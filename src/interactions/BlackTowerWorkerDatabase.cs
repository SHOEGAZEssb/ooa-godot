using Godot;
using System;
using System.Collections.Generic;
using System.Text;

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
        foreach (string line in DataLines("black_tower_texts.tsv"))
        {
            string[] fields = line.Split('\t');
            if (fields.Length != 2)
                throw Malformed("text", line);
            _texts.Add(
                Convert.ToInt32(fields[0], 16),
                Encoding.UTF8.GetString(Convert.FromBase64String(fields[1])));
        }

        foreach (string line in DataLines("black_tower_visuals.tsv"))
        {
            string[] fields = line.Split('\t');
            if (fields.Length != 5)
                throw Malformed("visual", line);
            _visuals.Add(fields[0], new VisualRecord(
                fields[1], int.Parse(fields[2]), int.Parse(fields[3]), fields[4]));
        }

        foreach (string line in DataLines("black_tower_patrols.tsv"))
        {
            string[] fields = line.Split('\t');
            if (fields.Length != 2)
                throw Malformed("patrol", line);
            string[] encodedLegs = fields[1].Split(',');
            var legs = new PatrolLeg[encodedLegs.Length];
            for (int index = 0; index < encodedLegs.Length; index++)
            {
                string[] values = encodedLegs[index].Split(':');
                if (values.Length != 2)
                    throw Malformed("patrol leg", encodedLegs[index]);
                legs[index] = new PatrolLeg(
                    int.Parse(values[0]), int.Parse(values[1]));
            }
            _patrols.Add(int.Parse(fields[0]), legs);
        }

        foreach (string line in DataLines("black_tower_constants.tsv"))
        {
            string[] fields = line.Split('\t');
            if (fields.Length != 2)
                throw Malformed("constant", line);
            _constants.Add(fields[0], int.Parse(fields[1]));
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

    private static IEnumerable<string> DataLines(string file)
    {
        string source = FileAccess.GetFileAsString(
            $"res://assets/oracle/objects/{file}");
        foreach (string raw in source.Split(
            '\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = raw.TrimEnd('\r');
            if (!line.StartsWith('#'))
                yield return line;
        }
    }

    private static InvalidOperationException Malformed(string kind, string line) =>
        new($"Malformed Black Tower {kind} row: {line}");
}
