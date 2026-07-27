using Godot;
using System;

namespace oracleofages;

public partial class OctorokRockProjectile
    : TransitionOffsetNode2D, IHostileProjectile
{
    private Texture2D _normalTexture = null!;
    private Texture2D _bounceTexture = null!;
    private HostileProjectileLifecycle _lifecycle = null!;

    public bool Finished => _lifecycle.Finished;
    internal HostileProjectileState State => _lifecycle.State;
    internal int Angle => _lifecycle.Angle;
    internal int Counter => _lifecycle.Counter;
    internal int ZFixed => _lifecycle.ZFixed;
    internal int ElapsedFrames => _lifecycle.ElapsedFrames;
    public Rect2 CollisionBounds => _lifecycle.CollisionBounds;

    internal void Initialize(
        OctorokProjectileRecord record,
        OracleRoomData room,
        Vector2 position,
        int angle)
    {
        Position = position;
        _lifecycle = new HostileProjectileLifecycle(
            this,
            room,
            new HostileProjectileProfile(
                "object_code/common/parts/octorokProjectile.s:partCode18",
                record.DamageQuarters,
                record.SpeedRaw,
                RingDamageSource.OctorokProjectile,
                new Vector2(
                    record.CollisionRadiusX,
                    record.CollisionRadiusY),
                HostileProjectileTileProbe.DestinationWithPendingBounce,
                HostileProjectileSwordWindow.BeforeBounce,
                ClearCollisionOnBounce: false,
                ResetZOnBounce: true),
            angle);
        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{record.SpriteName}.png");
        _normalTexture = BuildFirstFrame(
            source, record.NormalAnimation, record.TileBase, record.Palette);
        _bounceTexture = BuildFirstFrame(
            source, record.BounceAnimation, record.TileBase, record.Palette);
        QueueRedraw();
    }

    internal void UpdateFrame(Player player)
    {
        _lifecycle.UpdateFrame(player);
    }

    internal bool DeflectWithSword() => _lifecycle.DeflectWithSword();

    void IHostileProjectile.UpdateFrame(Player player) =>
        UpdateFrame(player);
    bool IHostileProjectile.DeflectWithSword() =>
        DeflectWithSword();

    public override void _Draw()
    {
        if (Finished)
            return;
        Texture2D texture =
            State == HostileProjectileState.Bouncing
                ? _bounceTexture
                : _normalTexture;
        DrawTexture(texture,
            new Vector2(-16, -16 + (ZFixed >> 8)) +
            TransitionDrawOffset);
    }

    private static Texture2D BuildFirstFrame(
        Image source,
        string animation,
        int tileBase,
        int palette)
    {
        AnimationFrameDefinition[] frames =
            OracleGraphicsCache.GetAnimationDefinition(animation).Frames;
        if (frames.Length == 0)
            throw new InvalidOperationException("Malformed Octorok projectile animation.");
        return NpcCharacter.BuildOamTexture(
            source, frames[0].EncodedOam, tileBase, palette);
    }
}
