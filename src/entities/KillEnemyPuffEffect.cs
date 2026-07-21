using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_KILLENEMYPUFF ($08), used by a red Zol before it splits.
/// Unlike PART_ENEMY_DESTROYED, this effect never resolves an item drop.
/// </summary>
public partial class KillEnemyPuffEffect : TransitionOffsetNode2D
{
    private sealed record FrameRecord(Texture2D Texture, int Duration);

    private static List<FrameRecord>? _definition;
    private List<FrameRecord> _animation = null!;
    private int _animationFrame;
    private int _animationCounter;

    public bool Finished { get; private set; }
    internal int ElapsedFrames { get; private set; }
    internal int DurationFrames { get; private set; }
    internal int AnimationFrame => Math.Min(_animationFrame, _animation.Count - 1);

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

    public override void _Draw()
    {
        if (!Finished && _animation.Count > 0)
        {
            DrawTexture(
                _animation[_animationFrame].Texture,
                new Vector2(-16, -16) + TransitionDrawOffset);
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

            Image source = OracleGraphicsCache.LoadImage(
                "res://assets/oracle/gfx/spr_common_sprites.png");

            var animation = new List<FrameRecord>();
            foreach (OracleGraphicsCache.AnimationFrameDefinition frame in
                OracleGraphicsCache.GetAnimationDefinition(fields[2]).Frames)
            {
                animation.Add(new FrameRecord(
                    NpcCharacter.BuildOamTexture(
                        source, frame.EncodedOam, tileBase, palette),
                    frame.Duration));
            }
            if (animation.Count == 0)
                throw new InvalidOperationException("INTERAC_KILLENEMYPUFF has no frames.");
            return animation;
        }

        throw new InvalidOperationException("INTERAC_KILLENEMYPUFF data is empty.");
    }
}
