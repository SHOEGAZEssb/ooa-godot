using Godot;
using System;

namespace Oracles;

internal static class EnemyVisualSource
{
    internal static Image LoadComposite(string[] sprites, string assetRoot)
    {
        if (sprites.Length == 0)
            throw new ArgumentException("A sprite sequence cannot be empty.", nameof(sprites));
        Image result = OracleGraphicsCache.LoadImage(
            assetRoot + $"gfx/{sprites[0]}.png");
        for (int index = 1; index < sprites.Length; index++)
        {
            result = OracleGraphicsCache.AppendGraphics(
                result,
                assetRoot + $"gfx/{sprites[index]}.png");
        }
        return result;
    }
}
