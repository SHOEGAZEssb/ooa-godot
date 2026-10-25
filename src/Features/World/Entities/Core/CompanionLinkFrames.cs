using Godot;
using System;

namespace Oracles;

internal static class CompanionLinkFrames
{
    internal static (
        Texture2D[] Textures,
        Texture2D[] ChargeTextures,
        Texture2D[] DamageTextures,
        Vector2[] Offsets) Load(
        string sprite, int palette, string[] frames, int[] sourceOffsets)
    {
        Image source = OracleGraphicsCache.LoadImage(
            AgesGameContent.AssetRoot + $"gfx/{sprite}.png");
        var textures = new Texture2D[frames.Length];
        var chargeTextures = new Texture2D[frames.Length];
        var damageTextures = new Texture2D[frames.Length];
        var offsets = new Vector2[frames.Length];
        for (int index = 0; index < frames.Length; index++)
        {
            AnimationFrameDefinition frame =
                OracleGraphicsCache.GetAnimationDefinition(
                    frames[index]).Frames[0];
            (textures[index], offsets[index]) =
                OracleOamCompositor.BuildPositionedOamTexture(
                    source,
                    frame.EncodedOam,
                    0,
                    palette,
                    paletteOverride: null,
                    sourceGrayscaleInverted: true,
                    sourceOffset: sourceOffsets[index]);
            (damageTextures[index], Vector2 damageOffset) =
                OracleOamCompositor.BuildPositionedOamTexture(
                    source,
                    frame.EncodedOam,
                    0,
                    palette,
                    OracleOamCompositor.GetStandardSpritePalette(5),
                    sourceGrayscaleInverted: true,
                    sourceOffset: sourceOffsets[index]);
            (chargeTextures[index], Vector2 chargeOffset) =
                OracleOamCompositor.BuildPositionedOamTexture(
                    source,
                    frame.EncodedOam,
                    0,
                    palette,
                    OracleOamCompositor.GetStandardSpritePalette(2),
                    sourceGrayscaleInverted: true,
                    sourceOffset: sourceOffsets[index]);
            if (damageOffset != offsets[index] ||
                chargeOffset != offsets[index])
                throw new InvalidOperationException(
                    "Mounted Link palette variant changed the OAM origin.");
        }
        return (textures, chargeTextures, damageTextures, offsets);
    }
}
