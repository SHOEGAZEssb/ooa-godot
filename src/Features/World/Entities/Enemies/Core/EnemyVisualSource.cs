using Godot;
using System;

namespace Oracles;

internal static class EnemyVisualSource
{
    internal static Image LoadComposite(string[] sprites)
    {
        if (sprites.Length == 0)
            throw new ArgumentException("A sprite sequence cannot be empty.", nameof(sprites));
        Image result = OracleGraphicsCache.LoadImage(
            AgesGameContent.AssetRoot + $"gfx/{sprites[0]}.png");
        for (int index = 1; index < sprites.Length; index++)
        {
            result = OracleGraphicsCache.AppendGraphics(
                result,
                AgesGameContent.AssetRoot + $"gfx/{sprites[index]}.png");
        }
        return result;
    }
}
