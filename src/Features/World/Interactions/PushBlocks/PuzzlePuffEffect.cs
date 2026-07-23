using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_PUFF $05. State 0 initializes graphics and requests SND_POOF; later
/// updates run animation 0 and delete one update after its terminal bit-$80
/// animation parameter becomes active.
/// </summary>
internal partial class PuzzlePuffEffect : TransitionOffsetNode2D
{

    private static List<PuzzlePuffEffectFrameRecord>? _definition;
    private List<PuzzlePuffEffectFrameRecord> _animation = null!;
    private Action<int> _playSound = null!;
    private int _sound;
    private int _animationFrame;
    private int _animationCounter;
    private bool _initialized;

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

    public override void _Draw()
    {
        if (!Finished)
        {
            DrawTexture(
                _animation[AnimationFrame].Texture,
                new Vector2(-16, -16) + TransitionDrawOffset);
        }
    }

    private static List<PuzzlePuffEffectFrameRecord> LoadDefinition()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/effects/puzzle_puff.tsv",
            new GeneratedTableSchema(
                "puzzle puff",
                GeneratedTableKeySemantics.Ordered,
                ["tile-base", "palette", "animation"],
                headerRequired: true));
        if (table.Rows.Count != 1)
        {
            throw new InvalidOperationException(
                $"INTERAC_PUFF should have one row, got {table.Rows.Count}.");
        }
        GeneratedTableRow row = table.Rows[0];
        int tileBase = row.UnsignedDecimal(0);
        int palette = row.UnsignedDecimal(1);
        if (tileBase != 0x16 || palette != 3)
            throw new InvalidOperationException("Imported INTERAC_PUFF constants are invalid.");

        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_common_sprites.png");
        var animation = new List<PuzzlePuffEffectFrameRecord>();
        foreach (AnimationFrameDefinition frame in
            OracleGraphicsCache.GetAnimationDefinition(row.RequiredString(2)).Frames)
        {
            animation.Add(new PuzzlePuffEffectFrameRecord(
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
}
