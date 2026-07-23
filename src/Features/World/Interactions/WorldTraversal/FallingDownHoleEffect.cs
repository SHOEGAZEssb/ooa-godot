using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_FALLDOWNHOLE $0f:$00. It moves at SPEED_60 toward the metatile
/// center while playing the imported 8/12/12-update terminal animation.
/// </summary>
internal partial class FallingDownHoleEffect : TransitionOffsetNode2D
{

    private static FallingDownHoleEffectDefinition? _definition;
    private FallingDownHoleEffectDefinition _activeDefinition = null!;
    private Vector2 _precisePosition;
    private int _animationFrame;
    private int _animationCounter;

    internal bool Finished { get; private set; }
    internal int ElapsedUpdates { get; private set; }
    internal int AnimationFrame => Math.Min(
        _animationFrame, _activeDefinition.Frames.Length - 1);
    internal int CurrentParameter =>
        _activeDefinition.Frames[AnimationFrame].Parameter;
    internal Vector2 PrecisePosition => _precisePosition;

    internal void Initialize(Vector2 position)
    {
        _activeDefinition = _definition ??= LoadDefinition();
        _precisePosition = position;
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        _animationFrame = 0;
        _animationCounter = _activeDefinition.Frames[0].Duration;
        QueueRedraw();
    }

    internal void UpdateFrame()
    {
        if (Finished)
            return;

        ElapsedUpdates++;
        if ((CurrentParameter & 0x80) != 0)
        {
            Finished = true;
            Visible = false;
            return;
        }

        MoveTowardHoleCenter();
        _animationCounter--;
        if (_animationCounter == 0)
        {
            _animationFrame++;
            if (_animationFrame >= _activeDefinition.Frames.Length)
            {
                Finished = true;
                Visible = false;
                return;
            }
            _animationCounter = _activeDefinition.Frames[_animationFrame].Duration;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Finished)
            return;
        FallingDownHoleEffectFrameRecord frame = _activeDefinition.Frames[AnimationFrame];
        DrawTexture(frame.Texture, frame.TextureOffset + TransitionDrawOffset);
    }

    private void MoveTowardHoleCenter()
    {
        Vector2 pixels = OracleObjectMath.ToPixelPosition(_precisePosition);
        var target = new Vector2(
            (Mathf.FloorToInt(pixels.X) & 0xf0) + 8,
            ((Mathf.FloorToInt(pixels.Y) + 5) & 0xf0) + 8);
        if (pixels.IsEqualApprox(target))
            return;
        int angle = OracleObjectMath.AngleToward(_precisePosition, target);
        _precisePosition += OracleObjectMath.VectorFromAngle32(angle) *
            _activeDefinition.Speed;
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
    }

    private static FallingDownHoleEffectDefinition LoadDefinition()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/effects/fall_down_hole.tsv",
            new GeneratedTableSchema(
                "fall-down-hole effect",
                GeneratedTableKeySemantics.Ordered,
                ["tile-base", "palette", "speed-raw", "animation"],
                headerRequired: true));
        if (table.Rows.Count != 1)
        {
            throw new InvalidOperationException(
                $"INTERAC_FALLDOWNHOLE should have one row, got {table.Rows.Count}.");
        }
        GeneratedTableRow row = table.Rows[0];
        int tileBase = row.UnsignedDecimal(0);
        int palette = row.UnsignedDecimal(1);
        int speedRaw = row.UnsignedDecimal(2);
        if (tileBase != 0x16 || palette != 3 || speedRaw != 0x0f)
        {
            throw new InvalidOperationException(
                "Imported INTERAC_FALLDOWNHOLE constants do not match the supported handler.");
        }

        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_common_sprites.png");
        AnimationDefinition animation =
            OracleGraphicsCache.GetAnimationDefinition(row.RequiredString(3));
        var frames = new List<FallingDownHoleEffectFrameRecord>();
        foreach (AnimationFrameDefinition frame in animation.Frames)
        {
            (Texture2D texture, Vector2 offset) =
                NpcCharacter.BuildPositionedOamTexture(
                    source,
                    frame.EncodedOam,
                    tileBase,
                    palette,
                    paletteOverride: null,
                    sourceGrayscaleInverted: true);
            frames.Add(new FallingDownHoleEffectFrameRecord(
                texture, offset, frame.Duration, frame.Parameter));
        }
        if (frames.Count != 4 ||
            frames[0].Duration != 8 || frames[1].Duration != 12 ||
            frames[2].Duration != 12 ||
            (frames[^1].Parameter & 0x80) == 0)
        {
            throw new InvalidOperationException(
                "INTERAC_FALLDOWNHOLE animation 0 no longer has its 8/12/12 terminal sequence.");
        }
        return new FallingDownHoleEffectDefinition(frames.ToArray(), speedRaw / 40.0f);
    }
}

internal sealed record FallingDownHoleEffectFrameRecord(Texture2D Texture, Vector2 TextureOffset, int Duration, int Parameter);

internal sealed record FallingDownHoleEffectDefinition(FallingDownHoleEffectFrameRecord[] Frames, float Speed);
