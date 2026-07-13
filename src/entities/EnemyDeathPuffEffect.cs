using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Common PART_ENEMY_DESTROYED ($02) effect used when an enemy is defeated.
/// The room entity manager advances it on the same 60 Hz counter as enemies.
/// </summary>
public partial class EnemyDeathPuffEffect : Node2D
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
    private Vector2 _transitionDrawOffset;

    public bool HighKnockback { get; private set; }
    public bool Finished { get; private set; }
    internal int AnimationFrame => Math.Min(_animationFrame, _animation.Count - 1);
    internal int DurationFrames { get; private set; }
    internal int ElapsedFrames { get; private set; }
    internal int CurrentPalette => _palettes[_paletteIndex];
    internal Vector2 TransitionDrawOffset => _transitionDrawOffset;

    internal void Initialize(Vector2 position, bool highKnockback = false)
    {
        Definition definition = _definition ??= LoadDefinition();
        Position = position;
        HighKnockback = highKnockback;
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

    internal void SetTransitionDrawOffset(Vector2 offset)
    {
        if (_transitionDrawOffset.IsEqualApprox(offset))
            return;
        _transitionDrawOffset = offset;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Finished || _animation.Count == 0)
            return;
        DrawTexture(
            _animation[_animationFrame].PaletteTextures[_paletteIndex],
            new Vector2(-16, -16) + _transitionDrawOffset);
    }

    private static Definition LoadDefinition()
    {
        string[] lines = FileAccess.GetFileAsString(
                "res://assets/oracle/effects/enemy_death_puff.tsv")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (string rawLine in lines)
        {
            string line = rawLine.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;

            string[] fields = line.Split('\t');
            if (fields.Length != 5 ||
                !int.TryParse(fields[0], out int tileBase) ||
                !int.TryParse(fields[1], out int paletteA) ||
                !int.TryParse(fields[2], out int paletteB))
            {
                throw new InvalidOperationException(
                    $"Malformed PART_ENEMY_DESTROYED data row: {line}");
            }

            byte[] bytes = FileAccess.GetFileAsBytes(
                "res://assets/oracle/gfx/spr_common_sprites.png");
            Image source = new();
            Error loadError = source.LoadPngFromBuffer(bytes);
            if (loadError != Error.Ok)
                throw new InvalidOperationException(
                    $"Could not load common sprite graphics for enemy death puff: {loadError}.");

            int[] palettes = { paletteA, paletteB };
            List<FrameRecord> normal = BuildAnimation(source, fields[3], tileBase, palettes);
            List<FrameRecord> highKnockback = BuildAnimation(source, fields[4], tileBase, palettes);
            if (normal.Count == 0 || highKnockback.Count == 0)
                throw new InvalidOperationException("PART_ENEMY_DESTROYED has no animation frames.");
            return new Definition(palettes, normal, highKnockback);
        }

        throw new InvalidOperationException("PART_ENEMY_DESTROYED data is empty.");
    }

    private static List<FrameRecord> BuildAnimation(
        Image source,
        string encodedAnimation,
        int tileBase,
        int[] palettes)
    {
        var animation = new List<FrameRecord>();
        foreach (string encodedFrame in encodedAnimation.Split(
            '|', StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = encodedFrame.IndexOf('@');
            if (separator < 0 ||
                !int.TryParse(encodedFrame[..separator], out int duration))
                continue;

            string encodedOam = encodedFrame[(separator + 1)..];
            var textures = new Texture2D[palettes.Length];
            for (int palette = 0; palette < palettes.Length; palette++)
            {
                textures[palette] = NpcCharacter.BuildOamTexture(
                    source, encodedOam, tileBase, palettes[palette]);
            }
            animation.Add(new FrameRecord(textures, Math.Max(1, duration)));
        }
        return animation;
    }
}
