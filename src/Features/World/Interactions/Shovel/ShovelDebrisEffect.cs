using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_SHOVELDEBRIS ($0a): one 14-update directional frame moving at
/// SPEED_80 with speedZ=-$240 and gravity $60.
/// </summary>
internal partial class ShovelDebrisEffect : TransitionOffsetNode2D
{
    private const int LifetimeFrames = 14;
    private const int InitialSpeedZ = -0x240;
    private const int Gravity = 0x60;
    private const float Speed = 0.5f;

    private static readonly string[] DirectionalOam =
    {
        "8,0,0,0;16,8,0,32",
        "8,4,0,32;20,8,0,32",
        "16,0,0,0;8,8,0,32",
        "8,6,0,0;20,8,0,0"
    };

    private Texture2D _texture = null!;
    private Vector2 _precisePosition;
    private Vector2I _direction;
    private int _zFixed;
    private int _speedZ;
    private int _elapsedFrames;

    internal bool Finished { get; private set; }
    internal int ElapsedFrames => _elapsedFrames;
    internal int ZFixed => _zFixed;
    internal int SpeedZ => _speedZ;
    internal Vector2 PrecisePosition => _precisePosition;

    internal void Initialize(Vector2 position, Vector2I direction)
    {
        int index = DirectionIndex(direction);
        Image source = OracleGraphicsCache.LoadImage(
            "res://assets/oracle/gfx/spr_common_sprites.png");
        _texture = NpcCharacter.BuildOamTexture(
            source, DirectionalOam[index], tileBase: 0x42, basePalette: 3);
        _precisePosition = position;
        _direction = direction;
        _speedZ = InitialSpeedZ;
        Position = OracleObjectMath.ToPixelPosition(position);
        QueueRedraw();
    }

    internal void UpdateFrame()
    {
        if (Finished)
            return;
        _elapsedFrames++;
        if (_elapsedFrames >= LifetimeFrames)
        {
            Finished = true;
            Visible = false;
            return;
        }

        OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, Gravity);
        _precisePosition += (Vector2)_direction * Speed;
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Finished)
        {
            DrawTexture(
                _texture,
                new Vector2(-16, -16 + (_zFixed >> 8)) +
                    TransitionDrawOffset);
        }
    }

    private static int DirectionIndex(Vector2I direction) =>
        direction == Vector2I.Up ? 0
        : direction == Vector2I.Right ? 1
        : direction == Vector2I.Down ? 2
        : direction == Vector2I.Left ? 3
        : throw new ArgumentOutOfRangeException(nameof(direction));
}
