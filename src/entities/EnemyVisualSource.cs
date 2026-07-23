using Godot;
using System;

namespace oracleofages;

internal static class EnemyVisualSource
{
    internal static Image LoadComposite(string[] sprites)
    {
        if (sprites.Length == 0)
            throw new ArgumentException("A sprite sequence cannot be empty.", nameof(sprites));
        Image result = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{sprites[0]}.png");
        for (int index = 1; index < sprites.Length; index++)
        {
            result = OracleGraphicsCache.AppendGraphics(
                result,
                $"res://assets/oracle/gfx/{sprites[index]}.png");
        }
        return result;
    }
}
