using Godot;
using System;

namespace oracleofages;

/// <summary>
/// INTERAC_EXPLOSION $56 with var03=$01 (Patch and Tingle's balloon).
/// The creator supplies imported graphics; the shared handler owns lifetime.
/// </summary>
internal sealed partial class InteractionExplosionEffect : FixedEffectNode2D
{
    private EnemyAnimationPlayer _animation = null!;
    private Action<int> _playSound = null!;
    private int _zOffset;
    private bool _initialized;

    internal override bool Finished { get; private protected set; }
    internal int ElapsedUpdates { get; private set; }
    internal int AnimationFrame => _animation.FrameIndex;
    internal int AnimationParameter => _animation.CurrentParameter;
    internal int ZOffset => _zOffset;
    internal Vector2 ObjectScreenPosition =>
        Position + new Vector2(0, _zOffset);
    internal Vector2 RenderedTextureOrigin =>
        Position + _animation.CurrentOffset + new Vector2(0, _zOffset);
    internal Vector2 TextureSize => _animation.CurrentTexture.GetSize();
    internal ulong TexturePixelHash => OracleGraphicsCache.PixelHash(
        _animation.CurrentTexture.GetImage());

    internal void Initialize(
        Vector2 position,
        int zOffset,
        InteractionExplosionVisual visual,
        Action<int> playSound)
    {
        Position = position;
        _zOffset = zOffset;
        _playSound = playSound;
        _animation = new EnemyAnimationPlayer(this, 1);
        _animation.Load(
            OracleGraphicsCache.LoadImage(
                $"res://assets/oracle/gfx/{visual.Sprite}.png"),
            [visual.Animation],
            visual.TileBase,
            visual.Palette);
        _animation.SetAnimation(0);
        QueueRedraw();
    }

    internal override void UpdateFrame()
    {
        if (Finished)
            return;

        ElapsedUpdates++;
        if (!_initialized)
        {
            _initialized = true;
            _playSound(OracleSoundEngine.SndExplosion);
            return;
        }
        if (_animation.CurrentParameter == 0xff)
        {
            Finished = true;
            Visible = false;
            return;
        }
        _animation.Advance();
    }

    public override void _Draw()
    {
        if (Visible && !Finished)
        {
            DrawTexture(
                _animation.CurrentTexture,
                _animation.CurrentOffset + new Vector2(0, _zOffset) +
                    TransitionDrawOffset);
        }
    }
}

internal sealed record InteractionExplosionSpawn(
    Vector2 Position,
    int ZOffset,
    InteractionExplosionVisual Visual)
    : RoomEntitySpawn(UpdateThisFrame: true);

internal readonly record struct InteractionExplosionVisual(string Sprite, int TileBase, int Palette, string Animation);
