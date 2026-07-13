using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_KILLENEMYPUFF ($08), used by a red Zol before it splits.
/// Unlike PART_ENEMY_DESTROYED, this effect never resolves an item drop.
/// </summary>
public partial class KillEnemyPuffEffect : Node2D
{
    private sealed record FrameRecord(Texture2D Texture, int Duration);

    private static List<FrameRecord>? _definition;
    private List<FrameRecord> _animation = null!;
    private int _animationFrame;
    private int _animationCounter;
    private Vector2 _transitionDrawOffset;

    public bool Finished { get; private set; }
    internal int ElapsedFrames { get; private set; }
    internal int DurationFrames { get; private set; }
    internal int AnimationFrame => Math.Min(_animationFrame, _animation.Count - 1);
    internal Vector2 TransitionDrawOffset => _transitionDrawOffset;

    internal void Initialize(Vector2 position)
    {
        Position = position;
        _animation = _definition ??= LoadDefinition();
        _animationFrame = 0;
        _animationCounter = _animation[0].Duration;
        DurationFrames = 0;
        foreach (FrameRecord frame in _animation)
            DurationFrames += frame.Duration;
        QueueRedraw();
    }

    internal void UpdateFrame()
    {
        if (Finished)
            return;

        ElapsedFrames++;
        _animationCounter--;
        if (_animationCounter <= 0)
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
        if (!Finished && _animation.Count > 0)
        {
            DrawTexture(
                _animation[_animationFrame].Texture,
                new Vector2(-16, -16) + _transitionDrawOffset);
        }
    }

    private static List<FrameRecord> LoadDefinition()
    {
        foreach (string rawLine in FileAccess.GetFileAsString(
            "res://assets/oracle/effects/kill_enemy_puff.tsv")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;

            string[] fields = line.Split('\t');
            if (fields.Length != 3 ||
                !int.TryParse(fields[0], out int tileBase) ||
                !int.TryParse(fields[1], out int palette))
            {
                throw new InvalidOperationException(
                    $"Malformed INTERAC_KILLENEMYPUFF data row: {line}");
            }

            byte[] bytes = FileAccess.GetFileAsBytes(
                "res://assets/oracle/gfx/spr_common_sprites.png");
            Image source = new();
            Error error = source.LoadPngFromBuffer(bytes);
            if (error != Error.Ok)
                throw new InvalidOperationException(
                    $"Could not load kill-enemy-puff graphics: {error}.");

            var animation = new List<FrameRecord>();
            foreach (string encodedFrame in fields[2].Split(
                '|', StringSplitOptions.RemoveEmptyEntries))
            {
                int separator = encodedFrame.IndexOf('@');
                if (separator < 0 ||
                    !int.TryParse(encodedFrame[..separator], out int duration))
                    continue;
                animation.Add(new FrameRecord(
                    NpcCharacter.BuildOamTexture(
                        source, encodedFrame[(separator + 1)..], tileBase, palette),
                    Math.Max(1, duration)));
            }
            if (animation.Count == 0)
                throw new InvalidOperationException("INTERAC_KILLENEMYPUFF has no frames.");
            return animation;
        }

        throw new InvalidOperationException("INTERAC_KILLENEMYPUFF data is empty.");
    }
}
