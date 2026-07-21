using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Common PART_ENEMY_DESTROYED ($02) effect used when an enemy is defeated.
/// The room entity manager advances it on the same 60 Hz counter as enemies.
/// </summary>
public partial class EnemyDeathPuffEffect : TransitionOffsetNode2D
{
    private sealed record FrameRecord(Texture2D[] PaletteTextures, int Duration);

    private sealed record Definition(
        int[] Palettes,
        List<FrameRecord> NormalAnimation,
        List<FrameRecord> HighKnockbackAnimation);

    private static Definition? _definition;

    private List<FrameRecord> _animation = null!;
    private int[] _palettes = null!;
    private int _animationFrame;
    private int _animationCounter;
    private int _paletteIndex;

    public bool HighKnockback { get; private set; }
    public bool Finished { get; private set; }
    public int EnemyId { get; private set; } = -1;
    internal int AnimationFrame => Math.Min(_animationFrame, _animation.Count - 1);
    internal int DurationFrames { get; private set; }
    internal int ElapsedFrames { get; private set; }
    internal int CurrentPalette => _palettes[_paletteIndex];

    internal void Initialize(
        Vector2 position,
        bool highKnockback = false,
        int enemyId = -1)
    {
        Definition definition = _definition ??= LoadDefinition();
        Position = position;
        HighKnockback = highKnockback;
        EnemyId = enemyId;
        _palettes = definition.Palettes;
        _animation = highKnockback
            ? definition.HighKnockbackAnimation
            : definition.NormalAnimation;
        _animationFrame = 0;
        _animationCounter = _animation[0].Duration;
        _paletteIndex = 0;
        DurationFrames = 0;
        foreach (FrameRecord frame in _animation)
            DurationFrames += frame.Duration;
        QueueRedraw();
    }

    internal void UpdateFrame(int globalFrameCounter)
    {
        if (Finished)
            return;

        ElapsedFrames++;
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

        // partCode02 toggles bit 0 of Part.oamFlags on even values of
        // wFrameCounter, alternating standard sprite palettes $02 and $03.
        if ((globalFrameCounter & 1) == 0)
            _paletteIndex ^= 1;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Finished || _animation.Count == 0)
            return;
        DrawTexture(
            _animation[_animationFrame].PaletteTextures[_paletteIndex],
            new Vector2(-16, -16) + TransitionDrawOffset);
    }

    private static Definition LoadDefinition()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/effects/enemy_death_puff.tsv",
            new GeneratedTableSchema(
                "enemy death puff",
                GeneratedTableKeySemantics.Ordered,
                [
                    "tile-base", "palette-a", "palette-b", "normal-animation",
                    "high-knockback-animation"
                ],
                headerRequired: true));
        if (table.Rows.Count != 1)
        {
            throw new InvalidOperationException(
                $"PART_ENEMY_DESTROYED should have one row, got {table.Rows.Count}.");
        }
        GeneratedTableRow row = table.Rows[0];
        int tileBase = row.UnsignedDecimal(0);
        int paletteA = row.UnsignedDecimal(1);
        int paletteB = row.UnsignedDecimal(2);

        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_common_sprites.png");

        int[] palettes = { paletteA, paletteB };
        List<FrameRecord> normal = BuildAnimation(
            source, row.RequiredString(3), tileBase, palettes);
        List<FrameRecord> highKnockback = BuildAnimation(
            source, row.RequiredString(4), tileBase, palettes);
        if (normal.Count == 0 || highKnockback.Count == 0)
            throw new InvalidOperationException("PART_ENEMY_DESTROYED has no animation frames.");
        return new Definition(palettes, normal, highKnockback);
    }

    private static List<FrameRecord> BuildAnimation(
        Image source,
        string encodedAnimation,
        int tileBase,
        int[] palettes)
    {
        var animation = new List<FrameRecord>();
        foreach (OracleGraphicsCache.AnimationFrameDefinition frame in
            OracleGraphicsCache.GetAnimationDefinition(encodedAnimation).Frames)
        {
            var textures = new Texture2D[palettes.Length];
            for (int palette = 0; palette < palettes.Length; palette++)
            {
                textures[palette] = NpcCharacter.BuildOamTexture(
                    source, frame.EncodedOam, tileBase, palettes[palette]);
            }
            animation.Add(new FrameRecord(textures, frame.Duration));
        }
        return animation;
    }
}
