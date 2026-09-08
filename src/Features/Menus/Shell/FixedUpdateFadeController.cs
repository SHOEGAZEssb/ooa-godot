using Godot;
using System;

namespace oracleofages;

internal sealed class FixedUpdateFadeController
{

    private readonly ColorRect _overlay;
    private Direction _direction;
    private int _duration;

    internal int Update { get; private set; }

    internal FixedUpdateFadeController(ColorRect overlay)
    {
        _overlay = overlay;
    }

    internal void Begin(Direction direction, int duration = OracleMenuLifecycle.FastFadeUpdates)
    {
        if (duration < 1) throw new ArgumentOutOfRangeException(nameof(duration));
        _duration = duration;
        _direction = direction;
        Update = 0;
        SetAlpha(direction == Direction.ToWhite ? 0.0f : 1.0f);
    }

    internal bool AdvanceOneUpdate()
    {
        Update = Math.Min(_duration, Update + 1);
        float progress = _duration == 32 ? (Update - 1) / 31.0f : Update / (float)_duration;
        SetAlpha(_direction == Direction.ToWhite ? progress : 1.0f - progress);
        return Update == _duration;
    }

    internal void SetTransparent()
    {
        Update = 0;
        SetAlpha(0.0f);
    }

    private void SetAlpha(float alpha)
    {
        _overlay.Color = new Color(1, 1, 1, Mathf.Clamp(alpha, 0.0f, 1.0f));
    }
}
