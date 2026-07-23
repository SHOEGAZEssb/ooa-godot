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
        foreach (KillEnemyPuffEffectFrameRecord frame in _animation)
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

    private static List<KillEnemyPuffEffectFrameRecord> LoadDefinition()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/effects/kill_enemy_puff.tsv",
            new GeneratedTableSchema(
                "kill-enemy puff",
                GeneratedTableKeySemantics.Ordered,
                ["tile-base", "palette", "animation"],
                headerRequired: true));
        if (table.Rows.Count != 1)
        {
            throw new InvalidOperationException(
                $"INTERAC_KILLENEMYPUFF should have one row, got {table.Rows.Count}.");
        }
        GeneratedTableRow row = table.Rows[0];
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
                frame.Duration));
        }
        if (animation.Count == 0)
            throw new InvalidOperationException("INTERAC_KILLENEMYPUFF has no frames.");
        return animation;
    }
}

internal sealed record KillEnemyPuffEffectFrameRecord(Texture2D Texture, int Duration);
