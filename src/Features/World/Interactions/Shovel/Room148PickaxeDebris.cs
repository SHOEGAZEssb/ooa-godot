using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_FALLING_ROCK $92:$06, including its state-0 return and exact 8.8 Z
/// integration. OAM flag bit 3 addresses fixed VRAM bank 1, so tile base $02
/// comes from spr_common_sprites rather than the worker's dynamic sprite slot.
/// </summary>
internal partial class Room148PickaxeDebris : TransitionOffsetNode2D
{
    private Texture2D _texture = null!;
    private Vector2 _precisePosition;
    private int _speed;
    private int _gravity;
    private bool _stateInitialized;

    internal bool Finished { get; private set; }
    internal int Palette { get; private set; }
    internal int Angle { get; private set; }
    internal int ZFixed { get; private set; }
    internal int SpeedZ { get; private set; }
    internal Vector2 PrecisePosition => _precisePosition;
    internal bool StateInitialized => _stateInitialized;

    internal void Initialize(
        PickaxeRecord record,
        Room148DebrisSpawn spawn)
    {
        AnimationFrameDefinition[] frames =
            OracleGraphicsCache.GetAnimationDefinition(
                record.DebrisAnimation).Frames;
        if (frames.Length == 0)
            throw new InvalidOperationException(
                "INTERAC_FALLING_ROCK $92:$06 has no imported animation frame.");
        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{record.DebrisSpriteName}.png");
        _texture = NpcCharacter.BuildOamTexture(
            source,
            frames[0].EncodedOam,
            record.DebrisTileBase,
            spawn.Palette);
        Palette = spawn.Palette;
        Angle = spawn.Angle;
        ZIndex = spawn.DrawPriority;
        _speed = record.Speed;
        _gravity = record.Gravity;
        SpeedZ = record.InitialSpeedZ;
        ZFixed = 0;
        _precisePosition = spawn.Position;
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        QueueRedraw();
    }

    internal void UpdateFrame()
    {
        if (Finished)
            return;
        if (!_stateInitialized)
        {
            // fallingRock_subid06 state 0 installs angle/speed/Z and returns.
            _stateInitialized = true;
            return;
        }

        int z = ZFixed;
        int speedZ = SpeedZ;
        if (OracleObjectMath.UpdateSpeedZ(ref z, ref speedZ, _gravity))
        {
            ZFixed = z;
            SpeedZ = speedZ;
            Finished = true;
            Visible = false;
            return;
        }

        ZFixed = z;
        SpeedZ = speedZ;
        _precisePosition += OracleObjectMath.VectorFromAngle32(Angle) *
            (_speed / 40.0f);
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Finished)
        {
            DrawTexture(
                _texture,
                new Vector2(-16, -16 + (ZFixed >> 8)) +
                    TransitionDrawOffset);
        }
    }
}
