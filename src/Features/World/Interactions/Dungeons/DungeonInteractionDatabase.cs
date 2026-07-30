using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Shared tables used by globally dispatched dungeon interaction handlers.
/// These records are keyed by interaction state/subid, not by the first
/// dungeon whose object stream happens to reference them.
/// </summary>
internal sealed class DungeonInteractionDatabase
{
    private readonly Dictionary<string, int> _constants =
        new(StringComparer.Ordinal);
    private readonly Dictionary<int, MovingSideScrollPlatformRecord> _platforms =
        new();

    internal DungeonInteractionDatabase()
    {
        LoadConstants(
            "res://assets/oracle/objects/dungeon_interaction_constants.tsv",
            "shared dungeon interaction constants");
        LoadConstants(
            "res://assets/oracle/objects/dungeon_object_behavior_constants.tsv",
            "shared dungeon object behavior constants");
        LoadPlatforms();
        ValidateContract();
    }

    internal int Constant(string key) =>
        _constants.TryGetValue(key, out int value)
            ? value
            : throw new KeyNotFoundException(
                $"Dungeon interaction constant {key} was not imported.");

    internal (int Off, int On) SwitchTiles(int index) => (
        Constant($"switch-{index}-off"),
        Constant($"switch-{index}-on"));

    internal MovingSideScrollPlatformRecord SidePlatform(int subId) =>
        _platforms.TryGetValue(subId, out MovingSideScrollPlatformRecord record)
            ? record
            : throw new KeyNotFoundException(
                $"Moving side-scroll platform subid ${subId:x2} was not imported.");

    internal Vector2 MovingPlatformCollisionRadii(int rawSubId)
    {
        int size = rawSubId & 0x07;
        return new Vector2(
            Constant($"platform-radius-{size}-x"),
            Constant($"platform-radius-{size}-y"));
    }

    private void LoadConstants(string path, string label)
    {
        GeneratedTable table = GeneratedTable.Load(
            path,
            new GeneratedTableSchema(
                label,
                GeneratedTableKeySemantics.Unique,
                ["key", "value"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            if (!_constants.TryAdd(
                    row.RequiredString(0), row.UnsignedDecimal(1)))
            {
                throw row.Invalid(
                    0,
                    "a unique shared dungeon interaction constant");
            }
        }
    }

    private void LoadPlatforms()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/moving_side_scroll_platforms.tsv",
            new GeneratedTableSchema(
                "moving side-scroll platform scripts",
                GeneratedTableKeySemantics.Unique,
                ["subid", "speed", "direction", "radius-y", "radius-x", "commands"],
                ["subid"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            string[] encoded = row.RequiredString(5).Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);
            var commands = new MovingSideScrollPlatformCommand[encoded.Length];
            for (int index = 0; index < encoded.Length; index++)
            {
                string[] parts = encoded[index].Split(
                    ':',
                    StringSplitOptions.TrimEntries);
                if (parts.Length != 2 ||
                    !byte.TryParse(
                        parts[1],
                        System.Globalization.NumberStyles.AllowHexSpecifier,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out byte endpoint))
                {
                    throw row.Invalid(
                        5,
                        "direction:hex-endpoint commands");
                }
                MovingSideScrollPlatformDirection direction = parts[0] switch
                {
                    "up" => MovingSideScrollPlatformDirection.Up,
                    "right" => MovingSideScrollPlatformDirection.Right,
                    "down" => MovingSideScrollPlatformDirection.Down,
                    "left" => MovingSideScrollPlatformDirection.Left,
                    _ => throw row.Invalid(5, "up, right, down, or left")
                };
                commands[index] = new(direction, endpoint);
            }
            MovingSideScrollPlatformRecord record = new(
                row.HexByte(0),
                row.UnsignedDecimal(1),
                row.UnsignedDecimal(2),
                row.UnsignedDecimal(3),
                row.UnsignedDecimal(4),
                commands);
            if (!_platforms.TryAdd(record.SubId, record))
                throw row.Invalid(0, "a unique side-scroll platform subid");
        }
    }

    private void ValidateContract()
    {
        if (_constants.Count != 91 ||
            _platforms.Count != 4 ||
            Constant("red-toggle-floor") != 0xad ||
            Constant("blue-toggle-floor") != 0xaf ||
            Constant("enemy-chest-wait") != 30 ||
            Constant("platform-speed") != 0x14 ||
            Constant("platform-wait") != 8 ||
            Constant("cube-push-frames") != 20 ||
            Constant("cube-hole-frames") != 10 ||
            Constant("miniboss-reward-wait") != 20 ||
            Constant("move-block-sound") != 0x7f ||
            MovingPlatformCollisionRadii(0x09) != new Vector2(8, 16) ||
            MovingPlatformCollisionRadii(0x05) != new Vector2(16, 16) ||
            SwitchTiles(0x13) != (0x5c, 0x5a) ||
            SidePlatform(0x06) is not
                { Speed: 20, RadiusY: 9, RadiusX: 7 } ||
            SidePlatform(0x07).Commands[1] is not
                {
                    Direction: MovingSideScrollPlatformDirection.Right,
                    Endpoint: 0xa8
                })
        {
            throw new InvalidOperationException(
                "Imported shared dungeon interaction contract is incomplete.");
        }
    }
}

internal readonly record struct MovingSideScrollPlatformRecord(
    int SubId,
    int Speed,
    int Direction,
    int RadiusY,
    int RadiusX,
    MovingSideScrollPlatformCommand[] Commands);

internal readonly record struct MovingSideScrollPlatformCommand(
    MovingSideScrollPlatformDirection Direction,
    int Endpoint);

internal enum MovingSideScrollPlatformDirection
{
    Up,
    Right,
    Down,
    Left
}
