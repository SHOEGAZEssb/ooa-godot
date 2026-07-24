using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>
/// Typed runtime view of the Ages Maple location, flight, visual, loot, and
/// dialogue tables. All records are generated from the clean US disassembly.
/// </summary>
internal sealed class MapleEventDatabase
{
    private readonly HashSet<(int Group, int Companion, int Room)> _locations = [];
    private readonly Dictionary<(MaplePathKind Kind, int Index), MaplePathRecord>
        _paths = [];
    private readonly int[] _movementPatternIndices = new int[16];
    private readonly Dictionary<int, MapleItemRecord> _items = [];
    private readonly int[][] _distributions =
        [new int[14], new int[14], new int[14]];
    private readonly Dictionary<int, string> _texts = [];
    private readonly Dictionary<string, int> _constants = [];

    internal MapleVisualRecord Visual { get; }
    internal MapleBookVisualRecord BookVisual { get; }
    internal int LocationCount => _locations.Count;
    internal int PathStepCount => _paths.Values.Sum(path => path.Steps.Count);
    internal int ItemCount => _items.Count;

    internal MapleEventDatabase()
    {
        LoadLocations();
        LoadPaths();
        LoadMovementSelection();
        Visual = LoadVisual();
        LoadItems();
        BookVisual = LoadBookVisual();
        LoadTexts();
        LoadConstants();
        ValidateContract();
    }

    internal bool IsEligibleLocation(int group, int room, int animalCompanion)
    {
        int companion = NormalizeCompanion(animalCompanion);
        return group switch
        {
            0 => _locations.Contains((0, companion, room)),
            1 => _locations.Contains((1, -1, room)),
            _ => false
        };
    }

    internal (int Group, int Room) DebugLocation(
        int preferredGroup,
        int animalCompanion)
    {
        int group = preferredGroup == 1 ? 1 : 0;
        int companion = group == 1 ? -1 : NormalizeCompanion(animalCompanion);
        int room = _locations
            .Where(location =>
                location.Group == group &&
                location.Companion == companion)
            .Min(location => location.Room);
        return (group, room);
    }

    internal MaplePathRecord Path(MaplePathKind kind, int index) =>
        _paths.TryGetValue((kind, index), out MaplePathRecord? path)
            ? path
            : throw new KeyNotFoundException(
                $"Maple {kind} path {index} was not imported.");

    internal MapleItemRecord Item(int index) =>
        _items.TryGetValue(index, out MapleItemRecord record)
            ? record
            : throw new KeyNotFoundException(
                $"Maple item ${index:x2} was not imported.");

    internal int MovementPattern(int slot) =>
        slot is >= 0 and < 16
            ? _movementPatternIndices[slot]
            : throw new ArgumentOutOfRangeException(nameof(slot));

    internal IReadOnlyList<int> Distribution(MapleDistributionKind kind) =>
        kind is >= MapleDistributionKind.Rare and <= MapleDistributionKind.Link
            ? _distributions[(int)kind]
            : throw new ArgumentOutOfRangeException(nameof(kind));

    internal string Text(int textId) =>
        _texts.TryGetValue(textId, out string? text)
            ? text
            : throw new KeyNotFoundException(
                $"Maple text TX_{textId:x4} was not imported.");

    internal int Constant(string key) =>
        _constants.TryGetValue(key, out int value)
            ? value
            : throw new KeyNotFoundException(
                $"Maple constant {key} was not imported.");

    private void LoadLocations()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/maple_locations.tsv",
            new GeneratedTableSchema(
                "Maple eligible locations",
                GeneratedTableKeySemantics.Unique,
                ["group", "companion", "room", "source"],
                ["group", "companion", "room"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int group = row.Decimal(0, 0, 1);
            int companion = row.Decimal(1, -1, 2);
            int room = row.HexByte(2);
            if ((group == 0 && companion < 0) ||
                (group == 1 && companion != -1))
            {
                throw new InvalidOperationException(
                    $"Invalid Maple location at {row.Path}:{row.LineNumber}.");
            }
            _locations.Add((group, companion, room));
            _ = row.RequiredString(3);
        }
    }

    private static int NormalizeCompanion(int animalCompanion) =>
        animalCompanion switch
        {
            0 or 0x0b => 0,
            1 or 0x0c => 1,
            2 or 0x0d => 2,
            _ => 0
        };

    private void LoadPaths()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/maple_paths.tsv",
            new GeneratedTableSchema(
                "Maple flight paths",
                GeneratedTableKeySemantics.Unique,
                [
                    "kind", "index", "start-y", "start-x", "turn-delay",
                    "step", "angle", "duration"
                ],
                ["kind", "index", "step"],
                headerRequired: true));
        var builders = new Dictionary<
            (MaplePathKind Kind, int Index),
            (int Y, int X, int Delay, List<MaplePathStep> Steps)>();
        foreach (GeneratedTableRow row in table.Rows)
        {
            MaplePathKind kind = row.RequiredString(0) switch
            {
                "shadow" => MaplePathKind.Shadow,
                "movement" => MaplePathKind.Movement,
                string value => throw new InvalidOperationException(
                    $"Unknown Maple path kind {value} at {row.Path}:{row.LineNumber}.")
            };
            int index = row.UnsignedDecimal(1);
            int y = row.HexByte(2);
            int x = row.HexByte(3);
            int delay = row.UnsignedDecimal(4);
            int step = row.UnsignedDecimal(5);
            int angle = row.HexByte(6);
            int duration = row.UnsignedDecimal(7);
            if (angle >= 0x20 || duration <= 0 || delay <= 0)
            {
                throw new InvalidOperationException(
                    $"Invalid Maple path step at {row.Path}:{row.LineNumber}.");
            }
            var key = (kind, index);
            if (!builders.TryGetValue(key, out var builder))
            {
                builder = (y, x, delay, []);
                builders.Add(key, builder);
            }
            if (builder.Y != y || builder.X != x ||
                builder.Delay != delay || builder.Steps.Count != step)
            {
                throw new InvalidOperationException(
                    $"Non-contiguous Maple path at {row.Path}:{row.LineNumber}.");
            }
            builder.Steps.Add(new MaplePathStep(angle, duration));
        }
        foreach (var pair in builders)
        {
            _paths.Add(
                pair.Key,
                new MaplePathRecord(
                    pair.Value.Y,
                    pair.Value.X,
                    pair.Value.Delay,
                    pair.Value.Steps.AsReadOnly()));
        }
    }

    private static MapleVisualRecord LoadVisual()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            "res://assets/oracle/objects/maple_visual.tsv",
            new GeneratedTableSchema(
                "Maple special-object visual",
                GeneratedTableKeySemantics.Ordered,
                [
                    "sprite", "tile-base", "palette",
                    "animations-base64", "source"
                ],
                headerRequired: true)).SingleRow();
        return new MapleVisualRecord(
            row.RequiredString(0),
            row.UnsignedDecimal(1),
            row.UnsignedDecimal(2),
            row.Base64Utf8(3).Split(
                '\n', StringSplitOptions.RemoveEmptyEntries),
            row.RequiredString(4));
    }

    private void LoadMovementSelection()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/maple_movement_selection.tsv",
            new GeneratedTableSchema(
                "Maple movement path selection",
                GeneratedTableKeySemantics.Unique,
                ["slot", "path"],
                ["slot"],
                headerRequired: true));
        if (table.Rows.Count != _movementPatternIndices.Length)
        {
            throw new InvalidOperationException(
                $"Maple movement selection should contain 16 slots, got {table.Rows.Count}.");
        }
        foreach (GeneratedTableRow row in table.Rows)
        {
            int slot = row.Decimal(0, 0, 15);
            if (slot != row.LineNumber - 2)
            {
                throw new InvalidOperationException(
                    $"Maple movement selection is reordered at {row.Path}:{row.LineNumber}.");
            }
            _movementPatternIndices[slot] = row.Decimal(1, 0, 7);
        }
    }

    private void LoadItems()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/maple_items.tsv",
            new GeneratedTableSchema(
                "Maple scattered items",
                GeneratedTableKeySemantics.Unique,
                [
                    "index", "sprite", "tile-base", "palette", "animation",
                    "value", "treasure", "normal-parameter",
                    "boosted-parameter", "boost-ring", "rare-weight",
                    "standard-weight", "link-weight", "unique-mask", "source"
                ],
                ["index"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            int index = row.Decimal(0, 0, 13);
            var item = new MapleItemRecord(
                index,
                row.RequiredString(1),
                row.UnsignedDecimal(2),
                row.UnsignedDecimal(3),
                row.RequiredString(4),
                row.UnsignedDecimal(5),
                row.UnsignedDecimal(6),
                row.UnsignedDecimal(7),
                row.UnsignedDecimal(8),
                row.Decimal(9, -1, 0x3f),
                row.UnsignedDecimal(10),
                row.UnsignedDecimal(11),
                row.UnsignedDecimal(12),
                row.UnsignedDecimal(13),
                row.RequiredString(14));
            _items.Add(index, item);
            _distributions[(int)MapleDistributionKind.Rare][index] =
                item.RareWeight;
            _distributions[(int)MapleDistributionKind.Standard][index] =
                item.StandardWeight;
            _distributions[(int)MapleDistributionKind.Link][index] =
                item.LinkWeight;
        }
    }

    private static MapleBookVisualRecord LoadBookVisual()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            "res://assets/oracle/objects/maple_book.tsv",
            new GeneratedTableSchema(
                "Maple Touching Book actor",
                GeneratedTableKeySemantics.Ordered,
                ["sprite", "tile-base", "palette", "animation", "source"],
                headerRequired: true)).SingleRow();
        return new MapleBookVisualRecord(
            row.RequiredString(0),
            row.UnsignedDecimal(1),
            row.UnsignedDecimal(2),
            row.RequiredString(3),
            row.RequiredString(4));
    }

    private void LoadTexts()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/maple_text.tsv",
            new GeneratedTableSchema(
                "Maple dialogue",
                GeneratedTableKeySemantics.Unique,
                ["text-id", "message-base64"],
                ["text-id"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
            _texts.Add(row.HexWord(0), row.Base64Utf8(1));
    }

    private void LoadConstants()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/maple_constants.tsv",
            new GeneratedTableSchema(
                "Maple constants",
                GeneratedTableKeySemantics.Unique,
                ["key", "value", "source"],
                ["key"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            _constants.Add(
                row.RequiredString(0),
                row.Decimal(1, -0x10000, 0x10000));
            _ = row.RequiredString(2);
        }
    }

    private void ValidateContract()
    {
        if (_locations.Count != 119 ||
            _locations.Count(location => location.Group == 0) != 90 ||
            _locations.Count(location => location.Group == 1) != 29 ||
            _paths.Count != 10 ||
            PathStepCount != 61 ||
            Visual is not { Sprite: "spr_maple", Animations.Length: 32 } ||
            _items.Count != 14 ||
            _texts.Count != 20 ||
            MovementPattern(0) != 0 ||
            MovementPattern(7) != 3 ||
            MovementPattern(15) != 7 ||
            Distribution(MapleDistributionKind.Rare).Sum() != 0x100 ||
            Distribution(MapleDistributionKind.Standard).Sum() != 0x100 ||
            Distribution(MapleDistributionKind.Link).Sum() != 0x100 ||
            BookVisual.Sprite != "spr_quest_items_1" ||
            Constant("normal-kill-threshold") != 30 ||
            Constant("ring-kill-threshold") != 15 ||
            Constant("initial-z") != -120 ||
            Item(0).Value != 0x3c ||
            Item(13).Value != 1)
        {
            throw new InvalidOperationException(
                "Imported Maple encounter contract is incomplete.");
        }
    }
}

internal enum MaplePathKind
{
    Shadow,
    Movement
}

internal enum MapleDistributionKind
{
    Rare,
    Standard,
    Link
}

internal sealed record MaplePathRecord(
    int StartY,
    int StartX,
    int TurnDelay,
    IReadOnlyList<MaplePathStep> Steps);

internal readonly record struct MaplePathStep(int Angle, int Duration);

internal readonly record struct MapleVisualRecord(
    string Sprite,
    int TileBase,
    int Palette,
    string[] Animations,
    string Source);

internal readonly record struct MapleBookVisualRecord(
    string Sprite,
    int TileBase,
    int Palette,
    string Animation,
    string Source);

internal readonly record struct MapleItemRecord(
    int Index,
    string Sprite,
    int TileBase,
    int Palette,
    string Animation,
    int Value,
    int Treasure,
    int NormalParameter,
    int BoostedParameter,
    int BoostRing,
    int RareWeight,
    int StandardWeight,
    int LinkWeight,
    int UniqueMask,
    string Source);
