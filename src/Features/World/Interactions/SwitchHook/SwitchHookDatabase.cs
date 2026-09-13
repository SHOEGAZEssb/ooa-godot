using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Source data for ITEM_SWITCH_HOOK $0a and its $0b chain.</summary>
internal sealed class SwitchHookDatabase
{
    private readonly SwitchHookLevel[] _levels = new SwitchHookLevel[2];
    private readonly SwitchHookOffset[] _offsets = new SwitchHookOffset[4];
    private readonly SwitchHookGraphic[] _hook = new SwitchHookGraphic[6];
    internal SwitchHookGraphic Chain { get; }

    internal SwitchHookDatabase()
    {
        var levels = GeneratedTable.Load("res://assets/oracle/metadata/switch_hook.tsv",
            new GeneratedTableSchema("Switch Hook levels", GeneratedTableKeySemantics.Unique,
                ["level", "item-id", "chain-id", "helper-id", "speed-raw", "extension-frames",
                 "retracted-frames", "lift-frames", "sound-mask", "flight-sound", "exchange-sound",
                 "diamond-tile", "somaria-tile", "break-source", "source"], ["level"], headerRequired: true));
        if (levels.Rows.Count != 2) throw new InvalidOperationException("Expected two Switch Hook levels.");
        for (int index = 0; index < levels.Rows.Count; index++)
        {
            var row = levels.Rows[index];
            if (row.Decimal(0) != index + 1 || row.HexByte(1) != 0x0a ||
                row.HexByte(2) != 0x0b || row.HexByte(3) != 0x09)
                throw row.Invalid(0, "ordered Switch Hook levels 1/2 with items $0a/$0b/$09");
            _levels[index] = new(row.Decimal(4, 1, 255), row.Decimal(5, 1, 255),
                row.Decimal(6, 1, 255), row.Decimal(7, 1, 127), row.HexByte(8),
                row.HexByte(9), row.HexByte(10), row.HexByte(11), row.HexByte(12),
                row.HexByte(13), row.RequiredString(14));
        }

        var offsets = GeneratedTable.Load("res://assets/oracle/metadata/switch_hook_offsets.tsv",
            new GeneratedTableSchema("Switch Hook offsets", GeneratedTableKeySemantics.Unique,
                ["direction", "y", "x", "z", "placement-offset", "source"], ["direction"], headerRequired: true));
        if (offsets.Rows.Count != 4) throw new InvalidOperationException("Expected four Switch Hook offsets.");
        for (int direction = 0; direction < offsets.Rows.Count; direction++)
        {
            var row = offsets.Rows[direction];
            if (row.Decimal(0) != direction) throw row.Invalid(0, "ordered directions 0-3");
            _offsets[direction] = new(new Vector2I(row.Decimal(2, -128, 127), row.Decimal(1, -128, 127)),
                row.Decimal(3, -128, 127), row.Decimal(4, -128, 127), row.RequiredString(5));
        }

        var graphics = GeneratedTable.Load("res://assets/oracle/metadata/switch_hook_animations.tsv",
            new GeneratedTableSchema("Switch Hook animations", GeneratedTableKeySemantics.Unique,
                ["item-id", "animation", "sprite", "tile-base", "oam-flags", "collision", "radius-y",
                 "radius-x", "damage", "health", "frames", "source"], ["item-id", "animation"], headerRequired: true));
        if (graphics.Rows.Count != 7) throw new InvalidOperationException("Expected six hook animations and one chain animation.");
        for (int index = 0; index < graphics.Rows.Count; index++)
        {
            var row = graphics.Rows[index];
            if (row.HexByte(0) != (index < 6 ? 0x0a : 0x0b) || row.Decimal(1) != (index < 6 ? index : 0))
                throw row.Invalid(0, "ordered $0a animations 0-5 followed by $0b animation 0");
            var record = new SwitchHookGraphic(row.RequiredString(2), row.HexByte(3), row.HexByte(4),
                row.HexByte(5), new Vector2I(row.Decimal(7, 0, 15), row.Decimal(6, 0, 15)),
                row.HexByte(8), row.HexByte(9), row.RequiredString(10), row.RequiredString(11));
            if (index < 6) _hook[index] = record;
            else Chain = record;
        }
    }

    internal SwitchHookLevel Level(int level) => level is 1 or 2 ? _levels[level - 1]
        : throw new ArgumentOutOfRangeException(nameof(level), "Switch Hook level must be 1 or 2.");
    internal SwitchHookOffset Offset(int direction) => direction is >= 0 and < 4 ? _offsets[direction]
        : throw new ArgumentOutOfRangeException(nameof(direction));
    internal SwitchHookGraphic Graphic(int animation) => animation is >= 0 and < 6 ? _hook[animation]
        : throw new ArgumentOutOfRangeException(nameof(animation));
    internal IReadOnlyList<SwitchHookGraphic> HookGraphics => _hook;
}

internal readonly record struct SwitchHookLevel(int SpeedRaw, int ExtensionFrames, int RetractedFrames,
    int LiftFrames, int SoundMask, int FlightSound, int ExchangeSound, int DiamondTile, int SomariaTile,
    int BreakSource, string Source);
internal readonly record struct SwitchHookOffset(Vector2I Position, int Z, int PlacementOffset, string Source);
internal readonly record struct SwitchHookGraphic(string Sprite, int TileBase, int OamFlags,
    int Collision, Vector2I Radius, int Damage, int Health, string Animation, string Source);
