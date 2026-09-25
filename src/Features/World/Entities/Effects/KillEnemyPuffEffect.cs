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

    private static List<KillEnemyPuffEffectFrameRecord>? _definition;
    private List<KillEnemyPuffEffectFrameRecord> _animation = null!;
    private int _animationFrame;
    private int _animationCounter;
    private Action _initializeSound = static () => { };
    internal bool Initialized { get; private set; }
    internal int AnimationFrame => Math.Min(_animationFrame, _animation.Count - 1);
    internal int AnimationParameter => !Initialized ? 0
        : _animationFrame >= _animation.Count ? 0xff : _animation[AnimationFrame].Parameter;

    public bool Finished { get; private set; }
    internal int ElapsedFrames { get; private set; }
    internal int DurationFrames { get; private set; }

    internal void Initialize(Vector2 position, Action initializeSound)
    {
        Position = position;
        _initializeSound = initializeSound;
        Visible = false;
        _animation = _definition ??= LoadDefinition();
        _animationFrame = 0;
        _animationCounter = _animation[0].Duration;
        DurationFrames = 2; // State0 plus the state1 terminal-parameter check.
        foreach (KillEnemyPuffEffectFrameRecord frame in _animation)
            DurationFrames += frame.Duration;
        QueueRedraw();
    }

    internal void UpdateFrame()
    {
        if (Finished)
            return;

        ElapsedFrames++;
        if (!Initialized)
        {
            Initialized = true;
            Visible = true;
            _initializeSound();
            return;
        }
        // interactionAnimation5a122's final $ff is tested before animating.
        if (_animationFrame >= _animation.Count)
        {
            Finished = true;
            Visible = false;
            return;
        }
        _animationCounter--;
        if (_animationCounter <= 0)
        {
            _animationFrame++;
            if (_animationFrame >= _animation.Count)
            {
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
                _animation[AnimationFrame].Texture,
                new Vector2(-16, -16) + TransitionDrawOffset);
        }
    }

    private static List<KillEnemyPuffEffectFrameRecord> LoadDefinition()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/effects/kill_enemy_puff.tsv",
            new GeneratedTableSchema(
                "kill-enemy puff",
                GeneratedTableKeySemantics.Ordered,
                ["tile-base", "palette", "animation"],
                headerRequired: true));
        GeneratedTableRow row = table.SingleRow();
        int tileBase = row.UnsignedDecimal(0);
        int palette = row.UnsignedDecimal(1);

        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_common_sprites.png");

        var animation = new List<KillEnemyPuffEffectFrameRecord>();
        foreach (AnimationFrameDefinition frame in
            OracleGraphicsCache.GetAnimationDefinition(row.RequiredString(2)).Frames)
        {
            animation.Add(new KillEnemyPuffEffectFrameRecord(
                NpcCharacter.BuildOamTexture(
                    source, frame.EncodedOam, tileBase, palette),
                frame.Duration, frame.Parameter));
        }
        if (animation.Count == 0)
            throw new InvalidOperationException("INTERAC_KILLENEMYPUFF has no frames.");
        return animation;
    }
}

internal sealed record KillEnemyPuffEffectFrameRecord(Texture2D Texture, int Duration, int Parameter);
