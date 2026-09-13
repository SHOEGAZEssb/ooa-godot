using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class LeverDatabase
{
    private readonly Dictionary<int, LeverProfile> _profiles = new();

    internal LeverDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/objects/dungeon_levers.tsv",
            new GeneratedTableSchema("bracelet lever profiles", GeneratedTableKeySemantics.Unique,
                ["subid", "sprite", "tile-base", "palette", "animation", "connections", "length", "speed",
                    "radius-y", "radius-x", "link-y-offset", "connection-step", "move-sound", "full-sound", "source"],
                ["subid"], headerRequired: true));
        foreach (var row in table.Rows)
        {
            var profile = new LeverProfile(row.RequiredString(1), row.HexByte(2), row.HexByte(3),
                row.RequiredString(4), row.RequiredString(5).Split('^'),
                new LeverBehavior(row.HexByte(6), row.HexByte(7), row.HexByte(8), row.HexByte(9),
                    unchecked((sbyte)row.HexByte(10)), row.HexByte(11), row.HexByte(12), row.HexByte(13)));
            if (profile.Connections.Count != 5)
                throw row.Invalid(5, "the five native lever connection animations");
            _profiles.Add(row.HexByte(0), profile);
        }
        if (_profiles.Count != 2 || !_profiles.ContainsKey(0x30) || !_profiles.ContainsKey(0x31))
            throw new InvalidOperationException("Missing INTERAC_LEVER $30/$31 profiles.");
    }

    internal LeverProfile Profile(int subid) => _profiles.TryGetValue(subid, out var profile) ? profile
        : throw new InvalidOperationException($"INTERAC_LEVER ${subid:x2} profile is not imported.");
}

internal readonly record struct LeverProfile(string Sprite, int TileBase, int Palette,
    string Animation, IReadOnlyList<string> Connections, LeverBehavior Behavior)
{
    internal NpcRecord ToNpcRecord(DungeonObjectRecord record, bool connection = false)
    {
        string animation = connection ? Connections[0] : Animation;
        return new NpcRecord(record.Group, record.Room, 0x61, connection ? 0x80 : record.SubId,
            record.Y, record.X, 0, 0, Sprite, TileBase, Palette, connection ? 2 : record.SubId & 1,
            false, animation, animation, animation, animation, string.Empty,
            NpcImplementationClassification.SpecializedNative);
    }
}
