using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// PART_BOSS_DEATH_EXPLOSION ($04). Unlike an ordinary death puff, this part
/// retains one live enemy count until its terminal animation parameter.
/// </summary>
public partial class BossDeathExplosionEffect : TransitionOffsetNode2D
{

    private static List<BossDeathExplosionEffectFrameRecord>? _definition;
    private List<BossDeathExplosionEffectFrameRecord> _animation = null!;
    private Action<int> _playSound = null!;
    private int _frame;
    private int _counter;
    private bool _initialized;
    private bool _terminalFrameReached;

    public bool Finished { get; private set; }
    internal int BossId { get; private set; }
    internal int AnimationFrame => Math.Min(_frame, _animation.Count - 1);
    internal int AnimationDuration { get; private set; }
    internal Vector2 CurrentTextureSize => _animation[_frame].Texture.GetSize();
    internal Vector2 CurrentDrawOffset => _animation[_frame].Offset;

    internal void Initialize(Vector2 position, int bossId, Action<int> playSound)
    {
        Position = position;
        BossId = bossId;
        _playSound = playSound;
        _animation = _definition ??= LoadDefinition();
        _counter = _animation[0].Duration;
        foreach (BossDeathExplosionEffectFrameRecord frame in _animation)
            AnimationDuration += frame.Duration;
        QueueRedraw();
    }

    internal void UpdateFrame()
    {
        if (Finished)
            return;
        if (!_initialized)
        {
            _initialized = true;
            if (BossId != 0)
                _playSound(OracleSoundEngine.SndBigExplosion);
            return;
        }
        if (_terminalFrameReached)
        {
            Finished = true;
            Visible = false;
            return;
        }
        if (--_counter != 0)
            return;
        _frame++;
        if (_frame >= _animation.Count)
        {
            // The source terminal record reuses offset $18 with parameter
            // $ff. It remains visible until state 1 observes that parameter
            // on the following object update.
            _frame = _animation.Count - 1;
            _terminalFrameReached = true;
        }
        else
        {
            _counter = _animation[_frame].Duration;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Finished)
        {
            DrawTexture(
                _animation[_frame].Texture,
                _animation[_frame].Offset + TransitionDrawOffset);
        }
    }

    private static List<BossDeathExplosionEffectFrameRecord> LoadDefinition()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/effects/boss_death_explosion.tsv",
            new GeneratedTableSchema(
                "boss death explosion",
                GeneratedTableKeySemantics.Ordered,
                ["tile-base", "palette", "animation"],
                headerRequired: true));
        if (table.Rows.Count != 1)
        {
            throw new InvalidOperationException(
                $"PART_BOSS_DEATH_EXPLOSION should have one row, got {table.Rows.Count}.");
        }
        GeneratedTableRow row = table.Rows[0];
        int tileBase = row.UnsignedDecimal(0);
        int palette = row.UnsignedDecimal(1);
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_common_sprites.png");
        var animation = new List<BossDeathExplosionEffectFrameRecord>();
        foreach (AnimationFrameDefinition frame in
            OracleGraphicsCache.GetAnimationDefinition(row.RequiredString(2)).Frames)
        {
            (Texture2D texture, Vector2 offset) =
                NpcCharacter.BuildPositionedOamTexture(
                    source, frame.EncodedOam, tileBase, palette,
                    paletteOverride: null, sourceGrayscaleInverted: true);
            animation.Add(new BossDeathExplosionEffectFrameRecord(
                texture,
                offset,
                frame.Duration));
        }
        if (animation.Count != 13)
        {
            throw new InvalidOperationException(
                $"PART_BOSS_DEATH_EXPLOSION expected 13 frames, got {animation.Count}.");
        }
        return animation;
    }
}

internal sealed record BossDeathExplosionEffectFrameRecord(
    Texture2D Texture,
    Vector2 Offset,
    int Duration);
