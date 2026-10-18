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
    private readonly int _angle;
    private int _delay;
    private readonly int _subId;
    private readonly Func<RoomEntitySpawn, bool> _createPart;
    private bool _initialized;

    internal PumpkinHeadProjectile(
        DungeonInteractionVisual visual,
        OracleRoomData room,
        Vector2 position,
        int angle, int subId, Func<RoomEntitySpawn, bool> createPart)
    {
        _room = room;
        _angle = angle & ObjectAngle.Mask;
        _subId = subId;
        _createPart = createPart;
        _delay = _behavior.DelayFrames;
        Position = position;
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
            // PART$42 state0 admits siblings in source subid order $02,$01,
            // stopping on the first failure while the primary retains its slot.
            if (_subId == 0)
                for (int subId = 2; subId >= 1; subId--)
                {
                    // Imported fan rows are center, minus, plus (source
                    // offsets $00,$02,$01), rather than subid order.
                    int offset = EnemyBehaviorTables.Shared.PumpkinHeadProjectileAngleOffsets[3 - subId].Value;
                    if (!_createPart(new PumpkinHeadProjectileSpawn(Position.Floor(), (_angle + offset) & 31, subId))) break;
                }
            return;
        }
        _animation.Advance();
        if (_delay > 0)
        {
            _delay--;
            if (_delay > 0)
            {
                QueueRedraw();
                return;
            }
        }

        Position += OracleObjectMovement.Shared.Delta(
            _behavior.SpeedRaw, _angle);
        if (!OracleObjectMath.IsInsideOriginalScreenBoundary(Position) ||
            _room.IsSolid(Position))
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
