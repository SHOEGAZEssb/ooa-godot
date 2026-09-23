using Godot;
using System;

namespace oracleofages;

internal sealed class SomariaGraphicsDatabase
{
    private readonly SomariaGraphic[] _weapon = new SomariaGraphic[8];
    private readonly SomariaGraphic[] _block = new SomariaGraphic[3];

    internal SomariaGraphicsDatabase()
    {
        var table = GeneratedTable.Load("res://assets/oracle/metadata/somaria_animations.tsv",
            new GeneratedTableSchema("Somaria animations", GeneratedTableKeySemantics.Unique,
                ["item-id", "animation", "sprite", "tile-base", "oam-flags", "collision",
                 "radius-y", "radius-x", "damage", "health", "frames", "source"],
                ["item-id", "animation"], headerRequired: true));
        if (table.Rows.Count != 11)
            throw new InvalidOperationException("Somaria requires eight ITEM$04 poses and three ITEM$18 animations.");
        for (int index = 0; index < table.Rows.Count; index++)
        {
            var row = table.Rows[index];
            int animation = index < 8 ? index : index - 8;
            if (row.HexByte(0) != (index < 8 ? 0x04 : 0x18) || row.Decimal(1) != animation)
                throw row.Invalid(0, "ordered ITEM$04 poses0-7 followed by ITEM$18 animations0-2");
            var graphic = new SomariaGraphic(row.RequiredString(2), row.HexByte(3), row.HexByte(4),
                row.HexByte(5), new(row.Decimal(7, 0, 15), row.Decimal(6, 0, 15)),
                row.HexByte(8), row.HexByte(9), row.RequiredString(10), row.RequiredString(11));
            // Parse eagerly so malformed OAM cannot remain hidden until item use.
            _ = OracleGraphicsCache.GetAnimationDefinition(graphic.Animation);
            (index < 8 ? _weapon : _block)[animation] = graphic;
        }
    }

    internal SomariaGraphic Weapon(int pose) => pose is >= 0 and < 8 ? _weapon[pose]
        : throw new ArgumentOutOfRangeException(nameof(pose));
    internal SomariaGraphic Block(int animation) => animation is >= 0 and < 3 ? _block[animation]
        : throw new ArgumentOutOfRangeException(nameof(animation));
}

// Collision attributes describe itemLoadAttributesAndGraphics, before the
// block's phase-in handler enables collisions and reduces its radii.
internal readonly record struct SomariaGraphic(string Sprite, int TileBase, int OamFlags,
    int InitialCollision, Vector2I InitialRadius, int Damage, int Health, string Animation, string Source);
