using Godot;
using System;

namespace oracleofages;

/// <summary>
/// INTERAC_EXPLOSION $56 created by PART_TINGLE_BALLOON $44. The part sets
/// var03=$01, so this effect uses objectSetVisible81 priority.
/// </summary>
internal sealed partial class TingleBalloonExplosionEffect : FixedEffectNode2D
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
        TingleBalloonExplosionVisual visual,
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

internal sealed record TingleBalloonExplosionSpawn(
    Vector2 Position,
    int ZOffset)
    : RoomEntitySpawn(UpdateThisFrame: true);
