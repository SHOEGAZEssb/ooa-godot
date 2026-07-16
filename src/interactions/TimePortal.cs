using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public partial class TimePortal : Node2D
{
    private sealed record AnimationFrame(Texture2D Texture, int Duration);

    // objectSetCollideRadius($02) is combined with Link's 6-pixel radius by
    // objectCheckCollidedWithLink_notDeadAndNotGrabbing.
    internal const float ContactRadius = 8.0f;

    private readonly List<AnimationFrame> _frames = new();
    private int _frame;
    private int _frameTicks;
    private int _loopStart;
    private Vector2 _transitionDrawOffset;
    private OracleRoomData _room = null!;
    private bool _linkWasOutside;

    public TimePortalDatabase.PortalRecord Record { get; private set; }
    public bool Entered { get; private set; }
    public bool Active { get; private set; }
    internal int CurrentFrame => _frame;

    public void Initialize(TimePortalDatabase.PortalRecord record, OracleRoomData room)
    {
        Record = record;
        _room = room;
        Position = new Vector2(record.X, record.Y);
        _loopStart = record.LoopStart;

        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{record.SpriteName}.png");
        foreach (OracleGraphicsCache.AnimationFrameDefinition frame in
            OracleGraphicsCache.GetAnimationDefinition(record.Animation).Frames)
        {
            _frames.Add(new AnimationFrame(
                NpcCharacter.BuildOamTexture(
                    source, frame.EncodedOam, record.TileBase, record.Palette),
                frame.Duration));
        }
        if (_frames.Count == 0 || _loopStart < 0 || _loopStart >= _frames.Count)
            throw new InvalidOperationException("Time portal has invalid animation data.");
        Active = room.GetMetatile(Position) == 0xd7;
        Visible = false;
        QueueRedraw();
    }

    internal void UpdateFrame(int frameCounter)
    {
        if (!Active)
        {
            // State 0 retries commonInit every update, so uncovering the
            // portal's `$d7 tile activates the already-placed interaction.
            Active = _room.GetMetatile(Position) == 0xd7;
            Visible = false;
            return;
        }
        // INTERAC_TIMEPORTAL_SPAWNER uses objectFlickerVisibility with b=$01.
        Visible = (frameCounter & 1) != 0;
        _frameTicks++;
        if (_frameTicks < _frames[_frame].Duration)
            return;
        _frameTicks = 0;
        _frame++;
        if (_frame >= _frames.Count)
            _frame = _loopStart;
        QueueRedraw();
    }

    internal bool CheckLinkContact(Vector2 linkPosition)
    {
        if (!Active || Entered)
            return false;
        Vector2 delta = linkPosition - Position;
        bool overlaps = Mathf.Abs(delta.X) < ContactRadius && Mathf.Abs(delta.Y) < ContactRadius;
        if (!overlaps)
        {
            _linkWasOutside = true;
            return false;
        }
        // Ordinary `$e1:$00 spawners normally wait for a fresh Tune of Echoes
        // activation after room load. The no-harp fallback keeps exposed spots
        // usable, but still requires Link to leave a destination spot before
        // re-entering so paired same-position portals cannot bounce forever.
        if (!_linkWasOutside)
            return false;
        Entered = true;
        // interactionBeginTimewarp deletes the portal interaction before the
        // cutscene trigger is serviced on the following update.
        Visible = false;
        return true;
    }

    internal void SetTransitionDrawOffset(Vector2 offset)
    {
        _transitionDrawOffset = offset;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_frames.Count > 0)
            DrawTexture(_frames[_frame].Texture, new Vector2(-16, -16) + _transitionDrawOffset);
    }
}
