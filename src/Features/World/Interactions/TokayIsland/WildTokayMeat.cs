using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Bracelet-liftable INTERAC_TOKAY_MEAT `$8c used by Wild Tokay.</summary>
internal partial class WildTokayMeat : TransitionOffsetNode2D
{
    private readonly List<WildTokayMeatFrame> _frames = new();
    private Vector2 _velocity;
    private int _fallDelay;
    private int _dropLife;
    private int _z;
    private bool _lifted;

    internal bool Thrown { get; private set; }
    internal bool Finished { get; private set; }
    internal Rect2 CollisionBounds => new(Position - new Vector2(8, 8), new Vector2(16, 16));

    internal void Initialize(TokayIslandDatabase database)
    {
        Position = new Vector2(database.MeatStartX, database.MeatStartY);
        _z = database.MeatStartZ;
        _fallDelay = database.MeatFallDelay;
        _dropLife = database.Constant("meat-drop-life");
        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{database.MeatSprite}.png");
        AnimationDefinition animation =
            OracleGraphicsCache.GetAnimationDefinition(database.MeatAnimation);
        foreach (AnimationFrameDefinition frame in animation.Frames)
        {
            (Texture2D texture, Vector2 offset) =
                NpcCharacter.BuildPositionedOamTexture(
                    source, frame.EncodedOam, database.MeatTileBase,
                    database.MeatPalette, paletteOverride: null,
                    sourceGrayscaleInverted: true);
            _frames.Add(new WildTokayMeatFrame(texture, offset));
        }
        if (_frames.Count == 0)
            throw new InvalidOperationException("Wild Tokay meat has no animation frames.");
        QueueRedraw();
    }

    internal bool TryUseBracelet(Player player, Vector2I releaseDirection)
    {
        if (Finished)
            return false;
        if (_lifted)
        {
            _lifted = false;
            Thrown = true;
            _velocity = releaseDirection == Vector2I.Zero
                ? Vector2.Zero
                : (Vector2)releaseDirection * 2.0f;
            _z = -13;
            return true;
        }
        if (Thrown || _z != 0 || player.IsCarryingObject)
            return false;

        Vector2 point = OracleObjectMath.ToPixelPosition(player.Position) +
            player.FacingVector * NpcCharacter.AButtonPointOffset;
        if ((Position - point).LengthSquared() >= 14 * 14)
            return false;
        _lifted = true;
        return true;
    }

    internal void UpdateFrame(Player player)
    {
        if (Finished)
            return;
        if (_lifted)
        {
            Position = OracleObjectMath.ToPixelPosition(player.Position) +
                new Vector2(0, -13);
            _z = 0;
            QueueRedraw();
            return;
        }
        if (_fallDelay > 0)
        {
            _fallDelay--;
            return;
        }
        if (_z < 0 && !Thrown)
        {
            _z = Math.Min(0, _z + 4);
            QueueRedraw();
            return;
        }
        if (!Thrown)
            return;

        Position += _velocity;
        if (_velocity == Vector2.Zero ||
            Position.X < 0 || Position.X >= 160 || Position.Y < 16 || Position.Y >= 144)
        {
            if (--_dropLife <= 0)
                Finish();
        }
        QueueRedraw();
    }

    internal void Catch() => Finish();

    internal void Finish()
    {
        Finished = true;
        Visible = false;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Finished || _frames.Count == 0)
            return;
        WildTokayMeatFrame frame = _frames[0];
        DrawTexture(
            frame.Texture,
            frame.Offset + new Vector2(0, _z) + TransitionDrawOffset);
    }
}

internal sealed record WildTokayMeatFrame(Texture2D Texture, Vector2 Offset);
