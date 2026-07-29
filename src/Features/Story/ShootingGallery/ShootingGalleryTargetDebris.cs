using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_FALLING_ROCK $92:$04/$05 emitted four-at-a-time when PART_BALL
/// replaces a target. The subid selects blue/fairy or red/imp palette.
/// </summary>
internal sealed partial class ShootingGalleryTargetDebris
    : TransitionOffsetNode2D
{
    private Texture2D _texture = null!;
    private Vector2 _precisePosition;
    private int _angle;
    private int _speed;
    private int _counter;
    private bool _initialized;

    internal bool Finished { get; private set; }
    internal int Counter => _counter;
    internal int Angle => _angle;
    internal int Palette { get; private set; }

    internal void Initialize(
        ShootingGalleryDebrisRecord record,
        ShootingGalleryTargetDebrisSpawn spawn)
    {
        if (spawn.AngleIndex is < 0 or >= 4 ||
            spawn.TargetType is < 0 or >= 4)
        {
            throw new ArgumentOutOfRangeException(nameof(spawn));
        }

        AnimationFrameDefinition[] frames =
            OracleGraphicsCache.GetAnimationDefinition(record.Animation).Frames;
        if (frames.Length != 1)
        {
            throw new InvalidOperationException(
                "INTERAC_FALLING_ROCK $92:$04/$05 requires one imported frame.");
        }
        Palette = spawn.TargetType < 2
            ? record.BluePalette
            : record.RedPalette;
        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{record.Sprite}.png");
        _texture = NpcCharacter.BuildOamTexture(
            source,
            frames[0].EncodedOam,
            record.TileBase,
            Palette);
        _precisePosition = spawn.Position;
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        _angle = record.Angle(spawn.AngleIndex);
        _speed = record.Speed;
        _counter = record.Lifetime;
        QueueRedraw();
    }

    internal void UpdateFrame()
    {
        if (Finished)
            return;
        if (!_initialized)
        {
            // State 0 initializes graphics/counter/angle and returns.
            _initialized = true;
            return;
        }

        _counter--;
        if (_counter == 0)
        {
            Finished = true;
            Visible = false;
            QueueRedraw();
            return;
        }

        Position = OracleObjectMovement.Shared.ApplySpeed(
            ref _precisePosition, _speed, _angle);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Finished)
        {
            DrawTexture(
                _texture,
                new Vector2(-16, -16) + TransitionDrawOffset);
        }
    }
}

internal sealed class ShootingGalleryTargetDebrisRoomEntity(
    ShootingGalleryTargetDebris debris)
    : RoomEntityAdapter<ShootingGalleryTargetDebris>(
        debris, debris.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime,
        IUpdatesDuringDialogueRoomEntity
{
    public bool Finished => Entity.Finished;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        _ = frame;
        _ = spawns;
        Entity.UpdateFrame();
    }
}

internal sealed record ShootingGalleryTargetDebrisSpawn(
    Vector2 Position,
    int TargetType,
    int AngleIndex)
    : RoomEntitySpawn(UpdateThisFrame: true);
