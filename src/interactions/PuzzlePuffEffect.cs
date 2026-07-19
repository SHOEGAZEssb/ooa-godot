using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class PuzzlePuffRoomEntity(PuzzlePuffEffect puff)
    : RoomEntityAdapter<PuzzlePuffEffect>(puff, puff.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame();

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}

/// <summary>
/// INTERAC_PUFF $05. State 0 initializes graphics and requests SND_POOF; later
/// updates run animation 0 and delete one update after its terminal bit-$80
/// animation parameter becomes active.
/// </summary>
internal partial class PuzzlePuffEffect : Node2D
{
    private sealed record FrameRecord(Texture2D Texture, int Duration, int Parameter);

    private static List<FrameRecord>? _definition;
    private List<FrameRecord> _animation = null!;
    private Action<int> _playSound = null!;
    private int _sound;
    private int _animationFrame;
    private int _animationCounter;
    private bool _initialized;
    private Vector2 _transitionDrawOffset;

    internal bool Finished { get; private set; }
    internal int ElapsedUpdates { get; private set; }
    internal int AnimationFrame => Math.Min(_animationFrame, _animation.Count - 1);
    internal int CurrentParameter => _animation[AnimationFrame].Parameter;

    internal void Initialize(Vector2 position, int sound, Action<int>? playSound = null)
    {
        Position = position;
        _animation = _definition ??= LoadDefinition();
        _sound = sound;
        _playSound = playSound ?? (static _ => { });
        _animationFrame = 0;
        _animationCounter = _animation[0].Duration;
        QueueRedraw();
    }

    internal void UpdateFrame()
    {
        if (Finished)
            return;

        ElapsedUpdates++;
        if (!_initialized)
        {
            _initialized = true;
            _playSound(_sound);
            return;
        }

        if ((CurrentParameter & 0x80) != 0)
        {
            Finished = true;
            Visible = false;
            return;
        }

        _animationCounter--;
        if (_animationCounter == 0)
        {
            _animationFrame++;
            if (_animationFrame >= _animation.Count)
            {
                Finished = true;
                Visible = false;
                return;
            }
            _animationCounter = _animation[_animationFrame].Duration;
        }
        QueueRedraw();
    }

    internal void SetTransitionDrawOffset(Vector2 offset)
    {
        if (_transitionDrawOffset.IsEqualApprox(offset))
            return;
        _transitionDrawOffset = offset;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Finished)
        {
            DrawTexture(
                _animation[AnimationFrame].Texture,
                new Vector2(-16, -16) + _transitionDrawOffset);
        }
    }

    private static List<FrameRecord> LoadDefinition()
    {
        foreach (string rawLine in FileAccess.GetFileAsString(
            "res://assets/oracle/effects/puzzle_puff.tsv")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;

            string[] fields = line.Split('\t');
            if (fields.Length != 3 ||
                !int.TryParse(fields[0], out int tileBase) ||
                !int.TryParse(fields[1], out int palette) ||
                tileBase != 0x16 || palette != 3)
            {
                throw new InvalidOperationException(
                    $"Malformed INTERAC_PUFF data row: {line}");
            }

            Image source = OracleGraphicsCache.LoadImage(
                "res://assets/oracle/gfx/spr_common_sprites.png");
            var animation = new List<FrameRecord>();
            foreach (OracleGraphicsCache.AnimationFrameDefinition frame in
                OracleGraphicsCache.GetAnimationDefinition(fields[2]).Frames)
            {
                animation.Add(new FrameRecord(
                    NpcCharacter.BuildOamTexture(
                        source, frame.EncodedOam, tileBase, palette),
                    frame.Duration,
                    frame.Parameter));
            }
            if (animation.Count < 2 ||
                (animation[^1].Parameter & 0x80) == 0)
            {
                throw new InvalidOperationException(
                    "INTERAC_PUFF animation 0 has no terminal parameter.");
            }
            return animation;
        }

        throw new InvalidOperationException("INTERAC_PUFF data is empty.");
    }
}
