using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;
internal sealed class Animator
{
    private readonly IReadOnlyList<TimeWarpEffectFrame> _frames;
    private readonly bool _loop;
    private int _ticks;
    public int Index { get; private set; }
    public bool Finished { get; private set; }
    public Texture2D Texture => _frames[Index].Texture;
    public Vector2 Offset => _frames[Index].Offset;

    public Animator(IReadOnlyList<TimeWarpEffectFrame> frames, bool loop = false)
    {
        if (frames.Count == 0)
            throw new ArgumentException("A time-warp animation cannot be empty.", nameof(frames));
        _frames = frames;
        _loop = loop;
        _ticks = Math.Max(1, frames[0].Duration);
    }

    public bool Advance()
    {
        if (Finished)
            return false;
        _ticks--;
        if (_ticks > 0)
            return false;
        if (Index + 1 < _frames.Count)
        {
            Index++;
            _ticks = Math.Max(1, _frames[Index].Duration);
            return true;
        }

        if (_loop)
        {
            Index = 0;
            _ticks = Math.Max(1, _frames[0].Duration);
            return true;
        }

        Finished = true;
        return false;
    }
}
