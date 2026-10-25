using Godot;

namespace Oracles;

internal static class TerrainShadow
{
    // bank0.s:_drawObjectTerrainEffects, before the terrain-on-ground branch.
    internal static bool ShouldDraw(int? zHigh, bool visible, int tilesetFlags,
        int cameraByte, int frameCounter, int objectPage) =>
        visible && zHigh is int z && (z & 0x80) != 0 &&
        (tilesetFlags & 0x20) == 0 && (cameraByte & 0xff) < 0x97 &&
        ((frameCounter ^ objectPage) & 1) != 0;

    private static TerrainShadowDefinition? _definition;

    internal static TerrainShadowDefinition Load()
    {
        if (_definition is not null)
            return _definition;

        GeneratedTableRow row = GeneratedTable.Load(
            AgesGameContent.AssetRoot + "effects/terrain_shadow.tsv",
            new GeneratedTableSchema(
                "default terrain-effect shadow",
                GeneratedTableKeySemantics.Ordered,
                [
                    "sprite", "tile-base", "palette", "oam", "source"
                ],
                headerRequired: true)).SingleRow();
        (Texture2D texture, Vector2 offset) =
            OracleOamCompositor.BuildPositionedOamTexture(
                OracleGraphicsCache.LoadImage(
                    AgesGameContent.AssetRoot + $"gfx/{row.RequiredString(0)}.png"),
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
