using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Event-owned rendering for the $84:$02 sparkles emitted by moving forest
/// fairies. The source interaction checks the previous animation parameter
/// before advancing, so terminal frames survive for one full update.
/// </summary>
internal sealed partial class FairiesWoodsSparkleLayer : Node2D
{
    private readonly List<Sparkle> _sparkles = new();
    private FairiesWoodsEventRecord _record;
    private int _frame;

    internal int Count => _sparkles.Count;

    internal void Initialize(FairiesWoodsEventRecord record)
    {
        _record = record;
        ZIndex = NpcCharacter.BehindLinkZIndex;
    }

    internal void Spawn(Vector2 position)
    {
        var animation = new EnemyAnimationPlayer(this, 1);
        animation.Load(
            EnemyVisualSource.LoadComposite([_record.SparkleSprite]),
            [_record.SparkleAnimation],
            _record.SparkleTileBase,
            _record.SparklePalette);
        animation.SetAnimation(0);
        _sparkles.Add(new Sparkle(animation, position, _frame));
        QueueRedraw();
    }

    internal void UpdateFrame()
    {
        _frame++;
        for (int index = 0; index < _sparkles.Count; index++)
        {
            Sparkle sparkle = _sparkles[index];
            if (sparkle.BornFrame == _frame)
                continue;
            if (sparkle.Animation.CurrentParameter == 0xff)
            {
                _sparkles.RemoveAt(index--);
                continue;
            }
            sparkle.Animation.Advance();
        }
        QueueRedraw();
    }

    internal void Clear()
    {
        _sparkles.Clear();
        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (Sparkle sparkle in _sparkles)
        {
            DrawTexture(
                sparkle.Animation.CurrentTexture,
                sparkle.Position - new Vector2(16, 16));
        }
    }

    private sealed record Sparkle(
        EnemyAnimationPlayer Animation,
        Vector2 Position,
        int BornFrame);
}
