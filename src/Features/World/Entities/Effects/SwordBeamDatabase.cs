using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Typed ITEM_SWORD_BEAM ($27) data. The importer preserves the original
/// offsets, attributes, speed, sound, and four directional OAM compositions.
/// </summary>
internal sealed class SwordBeamDatabase
{

    private readonly SwordBeamDatabaseRecord[] _records = new SwordBeamDatabaseRecord[4];
    private readonly Texture2D[,] _textures = new Texture2D[4, 2];

    internal IReadOnlyList<SwordBeamDatabaseRecord> Records => _records;

    internal SwordBeamDatabase(
        string path = "res://assets/oracle/metadata/sword_beam.tsv")
    {
        GeneratedTable table = GeneratedTable.Load(
            path,
            new GeneratedTableSchema(
                "sword beam",
                GeneratedTableKeySemantics.Unique,
                [
                    "direction", "offset-y", "offset-x", "sprite", "tile-base",
                    "palette", "radius-y", "radius-x", "damage", "speed-raw", "sound", "oam"
                ],
                ["direction"],
                headerRequired: true));
        int count = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            SwordBeamDatabaseRecord record = new SwordBeamDatabaseRecord(
                row.Decimal(0, 0, 3),
                row.Decimal(1),
                row.Decimal(2),
                row.RequiredString(3),
                row.UnsignedDecimal(4),
                row.UnsignedDecimal(5),
                row.UnsignedDecimal(6),
                row.UnsignedDecimal(7),
                row.UnsignedDecimal(8),
                row.HexByte(9),
                row.HexByte(10),
                row.RequiredString(11),
                "object_code/common/items/swordBeam.s:itemCode27");
            Validate(record);
            if (_records[record.Direction].Sprite is not null)
                throw new InvalidOperationException(
                    $"Duplicate sword-beam direction {record.Direction} in {path}.");
            _records[record.Direction] = record;
            count++;
        }

        if (count != 4)
            throw new InvalidOperationException(
                $"Expected four sword-beam directions, got {count}.");

        for (int direction = 0; direction < 4; direction++)
        {
            SwordBeamDatabaseRecord record = _records[direction];
            Image image = OracleGraphicsCache.LoadImage(
                $"res://assets/oracle/gfx/{record.Sprite}.png");
            _textures[direction, 0] = NpcCharacter.BuildOamTexture(
                image, record.Oam, record.TileBase, record.Palette);
            _textures[direction, 1] = NpcCharacter.BuildOamTexture(
                image, record.Oam, record.TileBase, record.Palette ^ 1);
        }
    }

    internal SwordBeamDatabaseRecord Get(int direction) => direction is >= 0 and < 4
        ? _records[direction]
        : throw new ArgumentOutOfRangeException(nameof(direction));

    internal Texture2D Texture(int direction, int palettePhase) =>
        _textures[direction, palettePhase & 1];

    private static void Validate(SwordBeamDatabaseRecord record)
    {
        Vector2I[] expectedOffsets =
        [
            new(-4, -11), new(12, 0), new(3, 10), new(-13, 0)
        ];
        if (record.Direction is < 0 or > 3 ||
            new Vector2I(record.OffsetX, record.OffsetY) !=
                expectedOffsets[record.Direction] ||
            record.Sprite != "spr_common_items" || record.TileBase != 0x38 ||
            record.Palette != 4 || record.RadiusY != 2 || record.RadiusX != 2 ||
            record.Damage != 2 || record.SpeedRaw != 0x78 ||
            record.Sound != 0x5d || record.Oam.Length == 0)
        {
            throw new InvalidOperationException(
                $"Invalid ITEM_SWORD_BEAM direction {record.Direction} imported from {record.Source}.");
        }
    }

}

internal readonly record struct SwordBeamDatabaseRecord(int Direction, int OffsetY, int OffsetX, string Sprite, int TileBase, int Palette, int RadiusY, int RadiusX, int Damage, int SpeedRaw, int Sound, string Oam, string Source);
