using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

/// <summary>
/// INTERAC_SHOVELDEBRIS ($0a): initialization, a 14-update directional frame moving at
/// SPEED_80 with speedZ=-$240 and gravity $60.
/// </summary>
internal partial class ShovelDebrisEffect : FixedEffectNode2D
{
    private int _lifetimeFrames;
    private int _gravity;
    private int _speed;

    private Texture2D _texture = null!;
    private Vector2 _precisePosition;
    private Vector2I _direction;
    private int _zFixed;
    private int _speedZ;
    private int _elapsedFrames;
    private bool _initialized;
    private OracleRuntimeState? _movementMemory;

    internal void BindMovementMemory(OracleRuntimeState memory) => _movementMemory = memory;

    internal override bool Finished { get; private protected set; }
    internal int ElapsedFrames => _elapsedFrames;
    internal bool Initialized => _initialized;
    internal int CurrentParameter => _elapsedFrames < _lifetimeFrames ? 0 : 0xff;
    internal int ZFixed => _zFixed;
    internal int SpeedZ => _speedZ;
    internal Vector2 PrecisePosition => _precisePosition;

    internal void Initialize(string assetRoot, Vector2 position, Vector2I direction)
    {
        AssetRoot = assetRoot;
        int index = DirectionIndex(direction);
        GeneratedTable table = GeneratedTable.Load(assetRoot + "effects/shovel_debris.tsv",
            new("native Shovel debris", GeneratedTableKeySemantics.Unique,
                ["direction", "tile-base", "palette", "speed-raw", "initial-speed-z", "gravity", "animation", "source"],
                ["direction"], headerRequired: true));
        if (table.Rows.Count != 4) throw new InvalidOperationException($"{table.Path}: expected four native Shovel directions.");
        GeneratedTableRow row = table.Rows[index];
        if (row.UnsignedDecimal(0) != index || row.UnsignedDecimal(1) != 0x42 || row.UnsignedDecimal(2) != 3 ||
            row.UnsignedDecimal(3) != 0x14 || row.Decimal(4) != -0x240 || row.UnsignedDecimal(5) != 0x60)
            throw row.Invalid(0, "native Shovel debris direction/graphics/motion");
        var frames = OracleGraphicsCache.GetAnimationDefinition(row.RequiredString(6)).Frames;
        if (frames.Length != 2 || frames[0].Duration != 14 || frames[0].Parameter != 0 || frames[1].Parameter != 0xff ||
            frames[0].EncodedOam != frames[1].EncodedOam)
            throw row.Invalid(6, "one 14-update native debris frame followed by its terminal marker");
        _lifetimeFrames = frames[0].Duration;
        _speed = row.UnsignedDecimal(3); _speedZ = row.Decimal(4); _gravity = row.UnsignedDecimal(5);
        Image source = OracleGraphicsCache.LoadImage(
            assetRoot + "gfx/spr_common_sprites.png");
        _texture = OracleOamCompositor.BuildOamTexture(
            source, frames[0].EncodedOam, tileBase: row.UnsignedDecimal(1), basePalette: row.UnsignedDecimal(2));
        _precisePosition = position;
        _direction = direction;
        Position = OracleObjectMath.ToPixelPosition(position);
        QueueRedraw();
    }

    internal override void UpdateFrame()
    {
        if (Finished)
            return;
        // breakTileDebris.s state0 loads graphics/speed and returns. State1
        // observes the animation terminator before movement on a later update.
        if (!_initialized)
        {
            _initialized = true;
            return;
        }
        _elapsedFrames++;
        if (_elapsedFrames > _lifetimeFrames)
        {
            Finished = true;
            Visible = false;
            return;
        }

        OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, _gravity);
        Position = NativeObjectMovement.ApplySpeed(
            _movementMemory, ref _precisePosition, _speed, DirectionIndex(_direction) * 8);
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
