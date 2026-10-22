using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Native INTERAC_CHILD $35 movement and animation tables.</summary>
internal sealed class FamilyChildMotionDatabase
{
    private readonly List<int> _animationBases = new();
    private readonly List<int> _hyperactiveAngles = new();
    private readonly List<Vector2I>[] _waypoints =
        [new(), new(), new(), new(), new()];
    private readonly Dictionary<int, string> _animations = new();

    internal FamilyChildMotionDatabase()
    {
        GeneratedTable motion = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/family_child_motion.tsv",
            new GeneratedTableSchema("INTERAC_CHILD movement tables",
                GeneratedTableKeySemantics.Ordered,
                ["kind", "index", "y", "x"], headerRequired: true));
        foreach (GeneratedTableRow row in motion.Rows)
        {
            string kind = row.RequiredString(0);
            if (kind == "base") _animationBases.Add(row.HexByte(2));
            else if (kind == "angle") _hyperactiveAngles.Add(row.HexByte(2));
            else if (kind.StartsWith("waypoint", StringComparison.Ordinal) &&
                     int.TryParse(kind.AsSpan(8), out int list) && list is >= 0 and < 5)
                _waypoints[list].Add(new Vector2I(row.HexByte(3), row.HexByte(2)));
            else throw new InvalidOperationException(
                $"{row.Path}:{row.LineNumber}: unsupported child movement row {kind}.");
        }
        if (_animationBases.Count != 8 || _hyperactiveAngles.Count != 4 ||
            _waypoints[0].Count != 8 || _waypoints[1].Count != 4 ||
            _waypoints[2].Count != 12 || _waypoints[3].Count != 2 ||
            _waypoints[4].Count != 4)
            throw new InvalidOperationException("INTERAC_CHILD movement table lengths changed.");

        GeneratedTable animations = GeneratedTable.Load(
            "res://assets/oracle/cutscenes/family_child_animations.tsv",
            new GeneratedTableSchema("INTERAC_CHILD animation data",
                GeneratedTableKeySemantics.Unique,
                ["animation", "encoded-data"], ["animation"], headerRequired: true));
        foreach (GeneratedTableRow row in animations.Rows)
            _animations.Add(row.HexByte(0), row.Base64Utf8(1));
        if (_animations.Count != 0x19)
            throw new InvalidOperationException("INTERAC_CHILD animation table length changed.");
    }

    internal int AnimationBase(int subid) => subid is >= 0 and < 8
        ? _animationBases[subid]
        : throw new InvalidOperationException($"Invalid INTERAC_CHILD subid ${subid:x2}.");

    internal int HyperactiveAngle(int index) => _hyperactiveAngles[index & 3];

    internal IReadOnlyList<Vector2I> Waypoints(int list) => list is >= 0 and < 5
        ? _waypoints[list]
        : throw new InvalidOperationException($"Invalid child waypoint list ${list:x2}.");

    internal string Animation(int index) => _animations.TryGetValue(index,
        out string? value) ? value : throw new InvalidOperationException(
            $"INTERAC_CHILD requested missing animation ${index:x2}.");
}
