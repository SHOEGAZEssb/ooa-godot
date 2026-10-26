using Godot;
using System;

namespace Oracles;

// bank1.s: screenTransitionState3-5 and transitionUpdateScrollAndLinkPosition.
internal sealed class RoomScrollController
{
    private Vector2 _start, _linkStep;
    private int _setupRemaining, _cleanupRemaining;
    internal Vector2I Direction { get; private set; }
    internal Vector2 FinishOffset { get; private set; }
    internal int Frame { get; private set; }
    internal int Frames { get; private set; }
    internal int TotalFrames { get; private set; }
    internal int Distance { get; private set; }
    internal bool Active { get; private set; }
    internal float Pixels => Frame * 4;
    internal Vector2 LinkPosition => _start + _linkStep * Frame;
    internal Vector2 DestinationPosition => _start + _linkStep * Frames + FinishOffset;

    internal void Begin(Vector2 start, Vector2I direction, int width, int height, int setupFrames, int cleanupFrames)
    {
        if (Math.Abs(direction.X) + Math.Abs(direction.Y) != 1 ||
            width < 160 || height < 128 || setupFrames < 0 || cleanupFrames < 1)
            throw new ArgumentException("A room scroll requires a cardinal direction, native room dimensions and valid phase counters.");
        Direction = direction; _start = start;
        Distance = direction.X != 0 ? OracleRoomData.ViewportWidth : OracleRoomData.ViewportHeight;
        Frames = Distance / 4; Frame = 0;
        _setupRemaining = setupFrames; _cleanupRemaining = cleanupFrames;
        TotalFrames = setupFrames + Frames + cleanupFrames;
        _linkStep = direction.X != 0 ? new Vector2(direction.X * 0.375f, 0) : new Vector2(0, direction.Y * 0.5f);
        FinishOffset = new(-direction.X * width, -direction.Y * height);
        Active = true;
    }

    internal void SetStart(Vector2 start) => _start = start;

    internal bool Advance()
    {
        if (!Active) return false;
        if (_setupRemaining > 0) { _setupRemaining--; return false; }
        if (Frame == Frames)
        {
            if (--_cleanupRemaining > 0) return false;
            Active = false;
        }
        else Frame++;
        return true;
    }
}
