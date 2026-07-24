using Godot;

namespace oracleofages;

internal static class TerrainShadow
{
    private static TerrainShadowDefinition? _definition;

    internal static TerrainShadowDefinition Load()
    {
        if (_definition is not null)
            return _definition;

        GeneratedTableRow row = GeneratedTable.Load(
            "res://assets/oracle/effects/terrain_shadow.tsv",
            new GeneratedTableSchema(
                "default terrain-effect shadow",
                GeneratedTableKeySemantics.Ordered,
                [
                    "sprite", "tile-base", "palette", "oam", "source"
                ],
                headerRequired: true)).SingleRow();
        (Texture2D texture, Vector2 offset) =
            NpcCharacter.BuildPositionedOamTexture(
                OracleGraphicsCache.LoadImage(
                    $"res://assets/oracle/gfx/{row.RequiredString(0)}.png"),
                row.RequiredString(3),
                row.UnsignedDecimal(1),
                row.UnsignedDecimal(2),
                paletteOverride: null,
                sourceGrayscaleInverted: true);
        _ = row.RequiredString(4);
        _definition = new TerrainShadowDefinition(texture, offset);
        return _definition;
    }
}

internal sealed record TerrainShadowDefinition(
    Texture2D Texture,
    Vector2 Offset);
