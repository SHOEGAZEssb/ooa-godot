using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>INTERAC_SPARKLE $84:$04: stationary, timed, animated flickering glow.</summary>
internal sealed partial class TimedSparkleRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity
{
    private readonly EnemyAnimationPlayer _animation;
    public Node2D Node => this;
    public bool Finished => RemainingUpdates == 0;
    internal int RemainingUpdates { get; private set; }
    internal int AnimationFrame => _animation.FrameIndex;
    internal Texture2D CurrentTexture => _animation.CurrentTexture;
    internal Vector2 TextureOffset => _animation.CurrentOffset;

    internal TimedSparkleRoomEntity(TimedSparkleSpawn spawn)
    {
        if (spawn.Lifetime is < 1 or > 255)
            throw new ArgumentOutOfRangeException(nameof(spawn), "INTERAC_SPARKLE $84:$04 counter1 must be a nonzero byte.");
        Name = "TimedSparkle";
        Position = spawn.Position;
        RemainingUpdates = spawn.Lifetime;
        _animation = new EnemyAnimationPlayer(this, 1);
        int frameCount = OracleGraphicsCache.GetAnimationDefinition(spawn.Visual.Animation).Frames.Length;
        _animation.Load(OracleGraphicsCache.LoadImage(
                $"res://assets/oracle/gfx/{spawn.Visual.Sprite}.png"),
            [spawn.Visual.Animation], spawn.Visual.TileBase, spawn.Visual.Palette,
            positionedOam: true,
            animationSourceOffsets: [Enumerable.Repeat(spawn.Visual.SourceOffset, frameCount).ToArray()]);
        _animation.SetAnimation(0);
        // State 0 initializes graphics and objectSetVisible80, then returns
        // without consuming counter1. The event creates this in its script pass.
        ZIndex = NpcCharacter.InFrontOfLinkZIndex;
        Visible = true;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        if (--RemainingUpdates == 0)
        {
            Visible = false;
            return;
        }
        _animation.Advance();
        Visible = (frame.Counter & 1) == 0;
        QueueRedraw();
    }

    internal void Cancel()
    {
        RemainingUpdates = 0;
        Visible = false;
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);

    public override void _Draw()
    {
        if (Visible && !Finished)
            DrawTexture(CurrentTexture, TextureOffset + TransitionDrawOffset);
    }
}

internal readonly record struct TimedSparkleVisual(string Sprite, int SourceOffset, int TileBase, int Palette, string Animation);
internal sealed record TimedSparkleSpawn(Vector2 Position, int Lifetime, TimedSparkleVisual Visual) : RoomEntitySpawn;
