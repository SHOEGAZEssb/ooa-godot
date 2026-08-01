using Godot;
using System;

namespace oracleofages;

/// <summary>
/// PART_3b $3b subid 0, created by Head Thwomp's purple face. It chooses one
/// source X coordinate at the top of the camera, falls with side-view gravity,
/// then changes to fixed-bank common-sprite impact graphics on solid terrain.
/// </summary>
internal sealed partial class HeadThwompBoulder
    : TransitionOffsetNode2D, IHostileProjectile
{
    private readonly OracleRoomData _room;
    private readonly OracleRandom _random;
    private readonly Action<int> _playSound;
    private readonly EnemyAnimationPlayer _fallAnimation;
    private readonly EnemyAnimationPlayer _impactAnimation;
    private int _yFixed;
    private int _speedYFixed;
    private int _radiusY = 5;
    private int _radiusX = 5;
    private bool _initialized;
    private bool _breaking;

    internal HeadThwompBoulder(
        OracleRoomData room,
        OracleRandom random,
        DungeonInteractionVisual fallVisual,
        DungeonInteractionVisual impactVisual,
        Action<int> playSound)
    {
        _room = room;
        _random = random;
        _playSound = playSound;
        Name = "HeadThwompBoulder";
        ZIndex = 11;
        Visible = false;

        _fallAnimation = LoadAnimation(fallVisual);
        _impactAnimation = LoadAnimation(impactVisual);
    }

    public bool Finished { get; private set; }
    public Rect2 CollisionBounds
    {
        get
        {
            Vector2 radii = new(_radiusX, _radiusY);
            return new Rect2(Position - radii, radii * 2.0f);
        }
    }

    internal bool Breaking => _breaking;
    internal int SpeedYFixed => _speedYFixed;
    internal int AnimationFrameIndex => ActiveAnimation.FrameIndex;
    internal Texture2D CurrentTexture => ActiveAnimation.CurrentTexture;

    private EnemyAnimationPlayer ActiveAnimation =>
        _breaking ? _impactAnimation : _fallAnimation;

    public void UpdateFrame(Player player)
    {
        if (Finished)
            return;
        if (!_initialized)
        {
            _initialized = true;
            OracleRandomResult result = _random.Next();
            Position = new Vector2(result.Value & 0x7c, 0);
            _yFixed = 0;
            _speedYFixed = 0x0200;
            Visible = true;
            _playSound(OracleSoundEngine.SndFallInHole);
            QueueRedraw();
            return;
        }

        if (_breaking)
        {
            if ((_impactAnimation.CurrentParameter & 0x80) != 0)
            {
                Finish();
                return;
            }
            SetImpactRadii(_impactAnimation.CurrentParameter);
            _impactAnimation.Advance();
            QueueRedraw();
            return;
        }

        if (_speedYFixed >= 0 && IsOnSolidFloor())
        {
            _breaking = true;
            _impactAnimation.SetAnimation(0);
            _playSound(OracleSoundEngine.SndBreakRock);
            QueueRedraw();
            return;
        }

        _yFixed = unchecked((ushort)(_yFixed + _speedYFixed));
        _speedYFixed = unchecked((short)(_speedYFixed + 0x20));
        Position = new Vector2(Position.X, _yFixed / 256.0f);
        if ((_yFixed >> 8) >= 0xb0)
        {
            Finish();
            return;
        }
        _fallAnimation.Advance();
        QueueRedraw();
    }

    public bool DeflectWithSword() => false;

    public override void _Draw()
    {
        if (!Finished)
        {
            DrawTexture(
                ActiveAnimation.CurrentTexture,
                new Vector2(-16, -16) + TransitionDrawOffset);
        }
    }

    private bool IsOnSolidFloor()
    {
        int y = unchecked((byte)((_yFixed >> 8) + 6));
        int x = Mathf.RoundToInt(Position.X);
        return IsSolidExceptHole(unchecked((byte)(x - 4)), y) ||
            IsSolidExceptHole(unchecked((byte)(x + 3)), y);
    }

    private bool IsSolidExceptHole(int x, int y)
    {
        var point = new Vector2(x, y);
        return _room.IsSolid(point) &&
            _room.GetTerrainInfo(point).Hazard != HazardType.Hole;
    }

    private void SetImpactRadii(int parameter)
    {
        ReadOnlySpan<(int Y, int X)> radii =
        [
            (0x04, 0x09),
            (0x06, 0x0b),
            (0x09, 0x0c),
            (0x0a, 0x0d),
            (0x0b, 0x0e)
        ];
        int index = parameter >> 1;
        if ((uint)index >= (uint)radii.Length)
        {
            throw new InvalidOperationException(
                $"PART_3b impact parameter ${parameter:x2} is invalid.");
        }
        (_radiusY, _radiusX) = radii[index];
    }

    private EnemyAnimationPlayer LoadAnimation(
        DungeonInteractionVisual visual)
    {
        var animation = new EnemyAnimationPlayer(this, visual.Animations.Length);
        animation.Load(
            EnemyVisualSource.LoadComposite(visual.Sprites),
            visual.Animations,
            visual.TileBase,
            visual.Palette,
            sourceGrayscaleInverted: visual.SourceGrayscaleInverted);
        animation.SetAnimation(0);
        return animation;
    }

    private void Finish()
    {
        Finished = true;
        Visible = false;
    }
}
