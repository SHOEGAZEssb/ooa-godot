using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>
/// Typed Link, parent-item presentation, sword geometry, and sword-tile data
/// imported from the supported Ages disassembly. Gameplay state machines
/// remain in their owning controllers.
/// </summary>
internal sealed class LinkItemDatabase
{
    private static LinkItemDatabase? _shared;

    private readonly Dictionary<(string Kind, int Variant, int Phase, int Direction),
        LinkGraphicRecord> _graphics = new();
    private readonly Vector2[] _attackPoseOffsets = new Vector2[4];
    private readonly Vector2[] _shovelOffsets = new Vector2[4];
    private readonly Vector2[] _shieldCenterOffsets = new Vector2[4];
    private readonly Vector2[] _shieldCollisionRadii = new Vector2[4];
    private readonly Vector2I[,] _braceletLiftOffsets = new Vector2I[4, 4];
    private readonly Vector2[] _swordTileOffsets = new Vector2[9];
    private readonly int[,] _swordAnimations = new int[4, 4];
    private readonly SwordArc[] _swordArcs = new SwordArc[28];
    private readonly SwordPart[][] _swordOam = new SwordPart[8][];
    private readonly int[] _swordSlashSounds = new int[8];
    private readonly byte[][] _bombableClinkTiles = new byte[6][];
    private readonly byte[][] _silentClinkTiles = new byte[6][];
    private readonly string[] _clinkListIds = new string[6];

    internal static LinkItemDatabase Shared => _shared ??= new LinkItemDatabase();

    internal LinkItemConstants Constants { get; }
    internal IReadOnlyList<SwordArc> SwordArcs => _swordArcs;
    internal IReadOnlyList<ClinkTileRecord> ClinkRows { get; private set; } = [];

    private LinkItemDatabase(
        string constantsPath = "res://assets/oracle/metadata/link_item_constants.tsv",
        string offsetsPath = "res://assets/oracle/metadata/link_item_offsets.tsv",
        string graphicsPath = "res://assets/oracle/metadata/link_item_graphics.tsv",
        string swordPath = "res://assets/oracle/metadata/sword_presentation.tsv",
        string clinkPath = "res://assets/oracle/metadata/sword_clink_tiles.tsv")
    {
        Constants = LoadConstants(constantsPath);
        LoadOffsets(offsetsPath);
        LoadGraphics(graphicsPath);
        LoadSwordPresentation(swordPath);
        LoadClinkTiles(clinkPath);
        ValidateCanonicalShape();
    }

    internal LinkGraphicRecord Graphic(
        string kind,
        int variant,
        int phase,
        int direction)
    {
        if (!_graphics.TryGetValue(
                (kind, variant, phase, direction),
                out LinkGraphicRecord record))
        {
            throw new ArgumentOutOfRangeException(
                nameof(direction),
                $"No imported Link graphic for {kind}/{variant}/{phase}/{direction}.");
        }
        return record;
    }

    internal Vector2 AttackPoseOffset(int direction) =>
        Directional(_attackPoseOffsets, direction);

    internal Vector2 ShovelOffset(int direction) =>
        Directional(_shovelOffsets, direction);

    internal Vector2 ShieldCenterOffset(int direction) =>
        Directional(_shieldCenterOffsets, direction);

    internal Vector2 ShieldCollisionRadius(int direction) =>
        Directional(_shieldCollisionRadii, direction);

    internal Vector2I BraceletLiftOffset(int frame, int direction)
    {
        if (frame is < 0 or >= 4)
            throw new ArgumentOutOfRangeException(nameof(frame));
        if (direction is < 0 or >= 4)
            throw new ArgumentOutOfRangeException(nameof(direction));
        return _braceletLiftOffsets[frame, direction];
    }

    internal Vector2 SwordTileOffset(int direction)
    {
        if (direction is < 0 or >= 9)
            throw new ArgumentOutOfRangeException(nameof(direction));
        return _swordTileOffsets[direction];
    }

    internal int SwordAnimation(int direction, int phase)
    {
        if (direction is < 0 or >= 4)
            throw new ArgumentOutOfRangeException(nameof(direction));
        if (phase is < 0 or >= 4)
            throw new ArgumentOutOfRangeException(nameof(phase));
        return _swordAnimations[direction, phase];
    }

    internal SwordArc SwordArc(int index) => index is >= 0 and < 28
        ? _swordArcs[index]
        : throw new ArgumentOutOfRangeException(nameof(index));

    internal IReadOnlyList<SwordPart> SwordOam(int animation) =>
        animation is >= 0 and < 8
            ? _swordOam[animation]
            : throw new ArgumentOutOfRangeException(nameof(animation));

    internal int SwordSlashSound(int randomIndex) =>
        randomIndex is >= 0 and < 8
            ? _swordSlashSounds[randomIndex]
            : throw new ArgumentOutOfRangeException(nameof(randomIndex));

    internal int SwingPhase(int frame) =>
        PhaseAt(frame, Constants.SwingPhaseStarts);

    internal int SpinPhase(int frame) =>
        PhaseAt(frame, Constants.SpinPhaseStarts) & 7;

    internal bool IsBombableClinkTile(int collisionSet, byte tile) =>
        Array.IndexOf(
            _bombableClinkTiles[CollisionSet(collisionSet)],
            tile) >= 0;

    internal bool IsSilentClinkTile(int collisionSet, byte tile) =>
        Array.IndexOf(
            _silentClinkTiles[CollisionSet(collisionSet)],
            tile) >= 0;

    private static LinkItemConstants LoadConstants(string path)
    {
        GeneratedTable table = GeneratedTable.Load(
            path,
            new GeneratedTableSchema(
                "Link/item constants",
                GeneratedTableKeySemantics.Unique,
                [
                    "sword-swing-frames", "sword-tile-hit-frame",
                    "sword-restart-frame", "sword-charge-counter",
                    "sword-poke-frames", "sword-spin-frames",
                    "shovel-action-frames", "shovel-dig-frame",
                    "shovel-second-pose-frame", "swing-phase-starts",
                    "spin-phase-starts", "shield-sound",
                    "shield-collision-effect", "shield-link-response",
                    "shield-projectile-response", "projectile-collision-mode",
                    "ring-projectile-collision-mode", "source"
                ],
                ["source"],
                headerRequired: true));
        if (table.Rows.Count != 1)
        {
            throw new InvalidOperationException(
                $"Expected one Link/item constants row, got {table.Rows.Count}.");
        }
        GeneratedTableRow row = table.Rows[0];
        return new LinkItemConstants(
            row.UnsignedDecimal(0),
            row.UnsignedDecimal(1),
            row.UnsignedDecimal(2),
            row.UnsignedDecimal(3),
            row.UnsignedDecimal(4),
            row.UnsignedDecimal(5),
            row.UnsignedDecimal(6),
            row.UnsignedDecimal(7),
            row.UnsignedDecimal(8),
            ParseCsv(row.RequiredString(9)),
            ParseCsv(row.RequiredString(10)),
            row.HexByte(11),
            row.HexByte(12),
            row.HexByte(13),
            row.HexByte(14),
            row.HexByte(15),
            row.HexByte(16),
            row.RequiredString(17));
    }

    private void LoadOffsets(string path)
    {
        GeneratedTable table = GeneratedTable.Load(
            path,
            new GeneratedTableSchema(
                "Link/item offsets",
                GeneratedTableKeySemantics.Unique,
                [
                    "kind", "index", "subindex", "offset-y", "offset-x",
                    "radius-y", "radius-x", "source"
                ],
                ["kind", "index", "subindex"],
                headerRequired: true));
        var expected = new List<(string Kind, int Index, int Subindex)>();
        AddExpected(expected, "attack-pose", 4, 1);
        AddExpected(expected, "shovel-child", 4, 1);
        AddExpected(expected, "shield-collision", 4, 1);
        AddExpected(expected, "bracelet-lift", 4, 4);
        AddExpected(expected, "sword-tile", 9, 1);
        if (table.Rows.Count != expected.Count)
        {
            throw new InvalidOperationException(
                $"Expected {expected.Count} Link/item offset rows, got {table.Rows.Count}.");
        }

        for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            GeneratedTableRow row = table.Rows[rowIndex];
            string kind = row.RequiredString(0);
            int index = row.UnsignedDecimal(1);
            int subindex = row.UnsignedDecimal(2);
            if ((kind, index, subindex) != expected[rowIndex])
                throw row.Invalid(0, $"ordered key {expected[rowIndex]}");
            int y = row.Decimal(3);
            int x = row.Decimal(4);
            int radiusY = row.UnsignedDecimal(5);
            int radiusX = row.UnsignedDecimal(6);
            _ = row.RequiredString(7);

            switch (kind)
            {
                case "attack-pose":
                    _attackPoseOffsets[index] = new Vector2(x, y);
                    break;
                case "shovel-child":
                    _shovelOffsets[index] = new Vector2(x, y);
                    break;
                case "shield-collision":
                    _shieldCenterOffsets[index] = new Vector2(x, y);
                    _shieldCollisionRadii[index] =
                        new Vector2(radiusX, radiusY);
                    break;
                case "bracelet-lift":
                    _braceletLiftOffsets[index, subindex] =
                        new Vector2I(x, y);
                    break;
                case "sword-tile":
                    _swordTileOffsets[index] = new Vector2(x, y);
                    break;
            }
        }
    }

    private void LoadGraphics(string path)
    {
        GeneratedTable table = GeneratedTable.Load(
            path,
            new GeneratedTableSchema(
                "Link item graphics",
                GeneratedTableKeySemantics.Unique,
                [
                    "kind", "variant", "phase", "direction",
                    "graphics-index", "oam-index", "byte-offset", "oam",
                    "source"
                ],
                ["kind", "variant", "phase", "direction"],
                headerRequired: true));
        List<(string Kind, int Variant, int Phase, int Direction)> expected =
            ExpectedGraphicKeys();
        if (table.Rows.Count != expected.Count)
        {
            throw new InvalidOperationException(
                $"Expected {expected.Count} Link item graphics rows, got {table.Rows.Count}.");
        }

        for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            GeneratedTableRow row = table.Rows[rowIndex];
            var key = (
                row.RequiredString(0),
                row.UnsignedDecimal(1),
                row.UnsignedDecimal(2),
                row.UnsignedDecimal(3));
            if (key != expected[rowIndex])
                throw row.Invalid(0, $"ordered key {expected[rowIndex]}");
            var record = new LinkGraphicRecord(
                key.Item1,
                key.Item2,
                key.Item3,
                key.Item4,
                row.HexByte(4),
                row.HexByte(5),
                row.HexWord(6),
                row.RequiredString(7),
                OamMirrorsX(row.RequiredString(7)),
                row.RequiredString(8));
            _graphics.Add(key, record);
        }
    }

    private void LoadSwordPresentation(string path)
    {
        GeneratedTable table = GeneratedTable.Load(
            path,
            new GeneratedTableSchema(
                "sword presentation",
                GeneratedTableKeySemantics.Unique,
                [
                    "kind", "index", "subindex", "value-a", "value-b",
                    "value-c", "value-d", "source"
                ],
                ["kind", "index", "subindex"],
                headerRequired: true));
        int[] oamPartCounts = [1, 2, 2, 2, 1, 2, 2, 2];
        int expectedCount = 16 + 28 + 8 + oamPartCounts.Sum();
        if (table.Rows.Count != expectedCount)
        {
            throw new InvalidOperationException(
                $"Expected {expectedCount} sword presentation rows, got {table.Rows.Count}.");
        }

        int rowIndex = 0;
        for (int direction = 0; direction < 4; direction++)
        for (int phase = 0; phase < 4; phase++, rowIndex++)
        {
            GeneratedTableRow row = OrderedRow(
                table, rowIndex, "animation", direction, phase);
            _swordAnimations[direction, phase] =
                row.Decimal(3, 0, 7);
            RequireZeroTail(row);
        }
        for (int index = 0; index < 28; index++, rowIndex++)
        {
            GeneratedTableRow row =
                OrderedRow(table, rowIndex, "arc", index, 0);
            _swordArcs[index] = new SwordArc(
                row.UnsignedDecimal(3),
                row.UnsignedDecimal(4),
                row.Decimal(5),
                row.Decimal(6));
            _ = row.RequiredString(7);
        }
        for (int index = 0; index < 8; index++, rowIndex++)
        {
            GeneratedTableRow row =
                OrderedRow(table, rowIndex, "sound", index, 0);
            _swordSlashSounds[index] = row.HexByte(3);
            RequireZeroTail(row);
        }
        for (int animation = 0; animation < 8; animation++)
        {
            var parts = new SwordPart[oamPartCounts[animation]];
            for (int part = 0; part < parts.Length; part++, rowIndex++)
            {
                GeneratedTableRow row =
                    OrderedRow(table, rowIndex, "oam", animation, part);
                int flags = row.HexByte(6);
                if ((flags & ~0x60) != 0)
                    throw row.Invalid(6, "only OAM X/Y flip bits");
                parts[part] = new SwordPart(
                    row.Decimal(3),
                    row.Decimal(4),
                    row.UnsignedDecimal(5),
                    (flags & 0x20) != 0,
                    (flags & 0x40) != 0);
                _ = row.RequiredString(7);
            }
            _swordOam[animation] = parts;
        }
    }

    private void LoadClinkTiles(string path)
    {
        GeneratedTable table = GeneratedTable.Load(
            path,
            new GeneratedTableSchema(
                "sword clink tiles",
                GeneratedTableKeySemantics.Unique,
                [
                    "collision-set", "kind", "list-id", "order", "tile",
                    "terminal", "source"
                ],
                ["collision-set", "kind", "order"],
                headerRequired: true));
        var records = new List<ClinkTileRecord>(table.Rows.Count);
        int expectedGroup = 0;
        int expectedOrder = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            int collisionSet = row.Decimal(0, 0, 5);
            string kind = row.RequiredString(1);
            int kindIndex = kind switch
            {
                "bombable" => 0,
                "silent" => 1,
                _ => throw row.Invalid(1, "bombable or silent")
            };
            int group = collisionSet * 2 + kindIndex;
            if (group != expectedGroup || row.UnsignedDecimal(3) != expectedOrder)
                throw row.Invalid(3, $"ordered group {expectedGroup}, row {expectedOrder}");
            var record = new ClinkTileRecord(
                collisionSet,
                kind,
                row.RequiredString(2),
                expectedOrder,
                (byte)row.HexByte(4),
                row.Boolean01(5),
                row.RequiredString(6));
            records.Add(record);
            if (record.Terminal)
            {
                if (record.Tile != 0)
                    throw row.Invalid(4, "00 for a terminal row");
                expectedGroup++;
                expectedOrder = 0;
            }
            else
            {
                expectedOrder++;
            }
        }
        if (expectedGroup != 12 || expectedOrder != 0)
        {
            throw new InvalidOperationException(
                "Sword clink rows did not contain 12 zero-terminated ordered lists.");
        }

        for (int collisionSet = 0; collisionSet < 6; collisionSet++)
        {
            ClinkTileRecord[] bombable = records.Where(record =>
                record.CollisionSet == collisionSet &&
                record.Kind == "bombable").ToArray();
            ClinkTileRecord[] silent = records.Where(record =>
                record.CollisionSet == collisionSet &&
                record.Kind == "silent").ToArray();
            _clinkListIds[collisionSet] = bombable[0].ListId;
            if (silent[0].ListId != _clinkListIds[collisionSet] ||
                bombable.Any(record =>
                    record.ListId != _clinkListIds[collisionSet]) ||
                silent.Any(record =>
                    record.ListId != _clinkListIds[collisionSet]))
            {
                throw new InvalidOperationException(
                    $"Collision set {collisionSet} changed list alias mid-row.");
            }
            _bombableClinkTiles[collisionSet] = bombable
                .Where(record => !record.Terminal)
                .Select(record => record.Tile)
                .ToArray();
            _silentClinkTiles[collisionSet] = silent
                .Where(record => !record.Terminal)
                .Select(record => record.Tile)
                .ToArray();
        }
        ClinkRows = records.AsReadOnly();
    }

    private void ValidateCanonicalShape()
    {
        if (Constants is not
            {
                SwordSwingFrames: 17,
                SwordTileHitFrame: 6,
                SwordRestartFrame: 3,
                SwordChargeCounter: 40,
                SwordPokeFrames: 12,
                SwordSpinFrames: 23,
                ShovelActionFrames: 23,
                ShovelDigFrame: 4,
                ShovelSecondPoseFrame: 8,
                ShieldSound: OracleSoundEngine.SndShield,
                ShieldCollisionEffect: 0x1f,
                ShieldLinkResponse: 0x20,
                ShieldProjectileResponse: 0x34,
                ProjectileCollisionMode: 0x06,
                RingProjectileCollisionMode: 0x07
            } ||
            !Constants.SwingPhaseStarts.SequenceEqual([0, 3, 6, 14]) ||
            !Constants.SpinPhaseStarts.SequenceEqual(
                [0, 3, 5, 8, 10, 13, 15, 18, 20]))
        {
            throw new InvalidOperationException(
                $"Invalid Link/item action boundaries imported from {Constants.Source}.");
        }

        string[] expectedAliases =
            ["overworld", "indoors", "indoors", "sidescrolling", "overworld", "indoors"];
        if (!_clinkListIds.SequenceEqual(expectedAliases) ||
            !_bombableClinkTiles[0].SequenceEqual(_bombableClinkTiles[4]) ||
            !_silentClinkTiles[0].SequenceEqual(_silentClinkTiles[4]) ||
            !_bombableClinkTiles[1].SequenceEqual(_bombableClinkTiles[2]) ||
            !_bombableClinkTiles[1].SequenceEqual(_bombableClinkTiles[5]) ||
            !_silentClinkTiles[1].SequenceEqual(_silentClinkTiles[2]) ||
            !_silentClinkTiles[1].SequenceEqual(_silentClinkTiles[5]))
        {
            throw new InvalidOperationException(
                "Ages collision-mode aliases changed in sword_clink_tiles.tsv.");
        }
    }

    private static GeneratedTableRow OrderedRow(
        GeneratedTable table,
        int rowIndex,
        string kind,
        int index,
        int subindex)
    {
        GeneratedTableRow row = table.Rows[rowIndex];
        if (row.RequiredString(0) != kind ||
            row.UnsignedDecimal(1) != index ||
            row.UnsignedDecimal(2) != subindex)
        {
            throw row.Invalid(0, $"ordered key ({kind}, {index}, {subindex})");
        }
        return row;
    }

    private static void RequireZeroTail(GeneratedTableRow row)
    {
        if (row.UnsignedDecimal(4) != 0 ||
            row.UnsignedDecimal(5) != 0 ||
            row.UnsignedDecimal(6) != 0)
        {
            throw row.Invalid(4, "zero-filled unused fields");
        }
        _ = row.RequiredString(7);
    }

    private static List<(string Kind, int Variant, int Phase, int Direction)>
        ExpectedGraphicKeys()
    {
        var result =
            new List<(string Kind, int Variant, int Phase, int Direction)>();
        for (int phase = 0; phase < 3; phase++)
        for (int direction = 0; direction < 4; direction++)
            result.Add(("attack", 0, phase, direction));
        for (int phase = 0; phase < 4; phase++)
        for (int direction = 0; direction < 4; direction++)
            result.Add(("minecart-attack", 0, phase, direction));
        for (int phase = 0; phase < 2; phase++)
        for (int direction = 0; direction < 4; direction++)
            result.Add(("shovel", 0, phase, direction));
        for (int pose = 0; pose < 3; pose++)
        for (int direction = 0; direction < 4; direction++)
            result.Add(("bracelet", pose, 0, direction));
        for (int phase = 0; phase < 2; phase++)
        for (int direction = 0; direction < 4; direction++)
            result.Add(("minecart", 0, phase, direction));
        for (int variant = 0; variant < 4; variant++)
        for (int phase = 0; phase < 2; phase++)
        for (int direction = 0; direction < 4; direction++)
            result.Add(("shield", variant, phase, direction));
        return result;
    }

    private static void AddExpected(
        List<(string Kind, int Index, int Subindex)> result,
        string kind,
        int indexes,
        int subindexes)
    {
        for (int index = 0; index < indexes; index++)
        for (int subindex = 0; subindex < subindexes; subindex++)
            result.Add((kind, index, subindex));
    }

    private static Vector2 Directional(Vector2[] values, int direction) =>
        direction is >= 0 and < 4
            ? values[direction]
            : throw new ArgumentOutOfRangeException(nameof(direction));

    private static int CollisionSet(int collisionSet) =>
        Math.Clamp(collisionSet, 0, 5);

    private static int PhaseAt(int frame, IReadOnlyList<int> starts)
    {
        for (int phase = starts.Count - 1; phase >= 0; phase--)
        {
            if (frame >= starts[phase])
                return phase;
        }
        return 0;
    }

    private static int[] ParseCsv(string value) => value.Split(',')
        .Select(part => int.Parse(part))
        .ToArray();

    private static bool OamMirrorsX(string oam) => oam.Split(';')
        .Select(part => part.Split(','))
        .All(part => (int.Parse(part[3]) & 0x20) != 0);
}

internal readonly record struct LinkItemConstants(
    int SwordSwingFrames,
    int SwordTileHitFrame,
    int SwordRestartFrame,
    int SwordChargeCounter,
    int SwordPokeFrames,
    int SwordSpinFrames,
    int ShovelActionFrames,
    int ShovelDigFrame,
    int ShovelSecondPoseFrame,
    int[] SwingPhaseStarts,
    int[] SpinPhaseStarts,
    int ShieldSound,
    int ShieldCollisionEffect,
    int ShieldLinkResponse,
    int ShieldProjectileResponse,
    int ProjectileCollisionMode,
    int RingProjectileCollisionMode,
    string Source);

internal readonly record struct LinkGraphicRecord(
    string Kind,
    int Variant,
    int Phase,
    int Direction,
    int GraphicsIndex,
    int OamIndex,
    int ByteOffset,
    string Oam,
    bool MirrorX,
    string Source);

internal readonly record struct ClinkTileRecord(
    int CollisionSet,
    string Kind,
    string ListId,
    int Order,
    byte Tile,
    bool Terminal,
    string Source);
