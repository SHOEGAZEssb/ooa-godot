using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>PART_PUMPKIN_HEAD_PROJECTILE $42.</summary>
internal sealed partial class PumpkinHeadProjectile : TransitionOffsetNode2D
{
    private readonly PumpkinProjectileBehaviorProfile _behavior =
        EnemyBehaviorTables.Shared.PumpkinProjectile;
    private readonly EnemyAnimationPlayer _animation;
    private readonly OracleRoomData _room;
    private readonly OracleRuntimeState _memory;
    private int _angle;
    private int _zHigh;
    private int _delay;
    private readonly int _subId;
    private readonly Func<RoomEntitySpawn, bool> _createPart;
    private bool _initialized;

    internal PumpkinHeadProjectile(
        DungeonInteractionVisual visual,
        OracleRoomData room,
        Vector2 position,
        int angle, int subId, Func<RoomEntitySpawn, bool> createPart,
        int zHigh, OracleRuntimeState memory)
    {
        if (subId is < 0 or > 2)
            throw new NotSupportedException($"pumpkinHeadProjectile.s: unsupported subid${subId:x2}.");
        _room = room;
        _memory = memory;
        _angle = angle & 255;
        _zHigh = unchecked((sbyte)zHigh);
        _subId = subId;
        _createPart = createPart;
        Position = position.Floor(); // Fresh objectCopyPosition retains zero lows.
        Visible = false;
        Name = "PumpkinHeadProjectile";
        ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex;
        _animation = new EnemyAnimationPlayer(this, visual.Animations.Length);
        _animation.Load(
            EnemyVisualSource.LoadComposite(visual.Sprites),
            visual.Animations,
            visual.TileBase,
            visual.Palette);
        _animation.SetAnimation(0);
    }

    internal bool Finished { get; private set; }
    internal bool InitializationPending => !_initialized;
    internal int State => !_initialized ? 0 : _delay == 0 ? 2 : 1;
    internal int SubId => _subId;
    internal int ZHigh => _zHigh;
    internal int Delay => _delay;
    internal int Angle => _angle;
    internal Rect2 CollisionBounds => new(
        Position - new Vector2(
            _behavior.CollisionRadiusX,
            _behavior.CollisionRadiusY),
        new Vector2(
            _behavior.CollisionRadiusX * 2,
            _behavior.CollisionRadiusY * 2));

    internal void UpdateFrame(Player player)
    {
        if (Finished)
            return;
        if (!_initialized)
        {
            _initialized = true;
            _delay = _behavior.DelayFrames;
            Position = new Vector2(OracleObjectPosition.HighByte(Position.X),
                (OracleObjectPosition.HighByte(Position.Y) + _zHigh) & 255) + Position - Position.Floor();
            _zHigh = 0;
            int offsetIndex = _subId == 0 ? 0 : 3 - _subId;
            _angle = (_angle + EnemyBehaviorTables.Shared.PumpkinHeadProjectileAngleOffsets[offsetIndex].Value) & 31;
            _animation.SetAnimation(0); // partLoadGraphicsAndProperties precedes state0.
            // PART$42 state0 admits siblings in source subid order $02,$01,
            // stopping on the first failure while the primary retains its slot.
            if (_subId == 0)
            {
                if ((_angle & 7) != 0)
                    throw new NotSupportedException($"pumpkinHeadProjectile.s: root mouth table requires a cardinal producer angle, got${_angle:x2}.");
                var origin = EnemyBehaviorTables.Shared.PumpkinHeadProjectileOriginOffsets[_angle >> 3];
                Position = new Vector2((OracleObjectPosition.HighByte(Position.X) + origin.Second) & 255,
                    (OracleObjectPosition.HighByte(Position.Y) + origin.First) & 255) + Position - Position.Floor();
                for (int subId = 2; subId >= 1; subId--)
                {
                    // The child receives the root angle, then applies its
                    // own offset when its physical state0 visit is admitted.
                    if (!_createPart(new PumpkinHeadProjectileSpawn(Position.Floor(), _angle, subId))) break;
                }
            }
            ZIndex = _angle == 0 ? ObjectDrawPriority.BehindLinkZIndex : ObjectDrawPriority.InFrontOfLinkZIndex;
            Visible = true;
            QueueRedraw();
            return;
        }
        if (_delay > 0)
        {
            _delay--;
            if (_delay == 0)
                ZIndex = ObjectDrawPriority.BehindLinkZIndex;
        }
        // State1 falls through to state2 motion on every counter visit.
        _animation.Advance();
        Position = NativeObjectMovement.ApplySpeed(_memory, OracleObjectPosition.FromPixels(Position),
            _behavior.SpeedRaw, _angle).PrecisePosition;
        if (NativePartBoundary.HasTileCollisionOrBoundary(_room, Position))
        {
            Finished = true;
            Visible = false;
            return;
        }
        if (player.OverlapsEnemyCollision(CollisionBounds))
        {
            player.ApplyEnemyContactDamage(
                Position, _behavior.DamageQuarters);
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Finished)
        {
            DrawTexture(
                _animation.CurrentTexture,
                new Vector2(-16, -16) + TransitionDrawOffset);
        }
    }
}
