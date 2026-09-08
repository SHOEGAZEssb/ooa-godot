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
        if (duration is not (11 or 32)) throw new ArgumentOutOfRangeException(nameof(duration));
        _duration = duration;
        _direction = direction;
        Update = 0;
        SetAlpha(direction == Direction.ToWhite ? 0.0f : 1.0f);
    }

    internal bool AdvanceOneUpdate()
    {
        Update = Math.Min(_duration, Update + 1);
        // bank0.s:fastFade* sets speed $03 (ordinary fade speed $01).
        // bank1.s:paletteFadeHandler01/02 adds/subtracts it in 5-bit RGB,
        // starting at $00/$20. The scene's additive material saturates each
        // component independently instead of interpolating toward white.
        int speed = _duration == 11 ? 3 : 1;
        int offset = _direction == Direction.ToWhite ? Update * speed : 32 - Update * speed;
        SetAlpha(Math.Clamp(offset, 0, 31) / 31.0f);
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
