using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC_BALL $95, including its exact 8.8 parabolic flight.</summary>
internal partial class Room149Ball : TransitionOffsetNode2D
{
    private const int InitialY = 0x4a;
    private const int BoyX = 0x75;
    private const int FatherX = 0x3c;
    private const int HorizontalSpeed = 2;
    private const int InitialSpeedZ = -0x1c0;
    private const int Gravity = 0x20;

    private Texture2D _texture = null!;
    private int _direction;
    private float _preciseX;
    private int _zFixed;
    private int _speedZ;
    private bool _active;

    internal bool Idle { get; private set; } = true;
    internal bool Stopped { get; private set; }
    internal int SubId { get; private set; }
    internal int ZFixed => _zFixed;
    internal int SpeedZ => _speedZ;
    internal bool Active => _active;

    internal void Initialize(Room149FamilyDatabaseVisualRecord visual)
    {
        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{visual.SpriteName}.png");
        AnimationFrameDefinition[] frames =
            OracleGraphicsCache.GetAnimationDefinition(visual.Animation).Frames;
        if (frames.Length == 0)
            throw new InvalidOperationException(
                "INTERAC_BALL $95 has malformed imported animation data.");
        _texture = NpcCharacter.BuildOamTexture(
            source,
            frames[0].EncodedOam,
            visual.TileBase,
            visual.Palette);
        Reset();
    }

    internal void SetActive(bool active)
    {
        _active = active;
        Visible = active;
        QueueRedraw();
    }

    internal void Reset()
    {
        Idle = true;
        Stopped = false;
        SubId = 0;
        _direction = 0;
        _preciseX = BoyX;
        _zFixed = 0;
        _speedZ = 0;
        Position = new Vector2(BoyX, InitialY);
        QueueRedraw();
    }
    internal void SetNativeInitialPosition(Vector2 position)
    {
        Reset();Position=position;_preciseX=position.X;
    }

    internal void Launch(int signal)
    {
        if (!_active || !Idle || Stopped || signal is < 1 or > 3)
            return;
        // cfd3=$01 is the father throwing right (ball subid $00); cfd3=$02
        // is the boy throwing left (ball subid $01).
        SubId = signal - 1;
        _direction = signal == 1 ? 1 : -1;
        _preciseX = Position.X;
        _zFixed = 0;
        _speedZ = InitialSpeedZ;
        if (signal == 3)
        {
            Position = new Vector2(BoyX, 0x50);
            _preciseX = BoyX;
            _zFixed = -0x600;
        }
        Idle = false;
        QueueRedraw();
    }

    internal void UpdateFrame()
    {
        if (!_active || Idle || Stopped)
            return;

        _preciseX = (_preciseX + _direction * HorizontalSpeed + 256) % 256;
        Position = new Vector2(Mathf.Floor(_preciseX), SubId == 2 ? 0x50 : InitialY);
        if (OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, Gravity))
        {
            if (SubId == 2)
            {
                // interactionCode95@subid2 negates the unsigned half of
                // the landing velocity, then compares it with signed -$80.
                _speedZ = -((ushort)_speedZ >> 1);
                Stopped = _speedZ >= -0x80;
                QueueRedraw();
                return;
            }
            Position = new Vector2(SubId == 0 ? BoyX : FatherX, InitialY);
            _preciseX = Position.X;
            Idle = true;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_active)
        {
            DrawTexture(
                _texture,
                new Vector2(-16, -16 + (_zFixed >> 8)) +
                    TransitionDrawOffset);
        }
    }
}
