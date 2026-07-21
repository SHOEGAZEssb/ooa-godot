using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class FallingDownHoleRoomEntity(FallingDownHoleEffect effect)
    : RoomEntityAdapter<FallingDownHoleEffect>(effect, effect.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame();

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}

/// <summary>
/// INTERAC_FALLDOWNHOLE $0f:$00. It moves at SPEED_60 toward the metatile
/// center while playing the imported 8/12/12-update terminal animation.
/// </summary>
internal partial class FallingDownHoleEffect : TransitionOffsetNode2D
{
    private sealed record FrameRecord(
        Texture2D Texture,
        Vector2 TextureOffset,
        int Duration,
        int Parameter);

    private sealed record Definition(FrameRecord[] Frames, float Speed);

    private static Definition? _definition;
    private Definition _activeDefinition = null!;
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
        FrameRecord frame = _activeDefinition.Frames[AnimationFrame];
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

    private static Definition LoadDefinition()
    {
        foreach (string raw in FileAccess.GetFileAsString(
            "res://assets/oracle/effects/fall_down_hole.tsv")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = raw.TrimEnd('\r');
            if (line.StartsWith('#'))
                continue;
            string[] fields = line.Split('\t');
            if (fields.Length != 4 ||
                !int.TryParse(fields[0], out int tileBase) ||
                !int.TryParse(fields[1], out int palette) ||
                !int.TryParse(fields[2], out int speedRaw) ||
                tileBase != 0x16 || palette != 3 || speedRaw != 0x0f)
            {
                throw new InvalidOperationException(
                    $"Malformed INTERAC_FALLDOWNHOLE data row: {line}");
            }

            Image source = OracleGraphicsCache.LoadImage(
                "res://assets/oracle/gfx/spr_common_sprites.png");
            OracleGraphicsCache.AnimationDefinition animation =
                OracleGraphicsCache.GetAnimationDefinition(fields[3]);
            var frames = new List<FrameRecord>();
            foreach (OracleGraphicsCache.AnimationFrameDefinition frame in animation.Frames)
            {
                (Texture2D texture, Vector2 offset) =
                    NpcCharacter.BuildPositionedOamTexture(
                        source,
                        frame.EncodedOam,
                        tileBase,
                        palette,
                        paletteOverride: null,
                        sourceGrayscaleInverted: true);
                frames.Add(new FrameRecord(
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
            return new Definition(frames.ToArray(), speedRaw / 40.0f);
        }
        throw new InvalidOperationException("INTERAC_FALLDOWNHOLE data is empty.");
    }
}
