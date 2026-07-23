using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>PART_PUMPKIN_HEAD_PROJECTILE $42.</summary>
internal sealed partial class PumpkinHeadProjectile : TransitionOffsetNode2D
{
    private const int CollisionRadiusY = 4;
    private const int CollisionRadiusX = 2;
    private const int LinkCollisionRadius = 6;
    private readonly EnemyAnimationPlayer _animation;
    private readonly OracleRoomData _room;
    private readonly int _angle;
    private int _delay = 8;

    internal PumpkinHeadProjectile(
        VisualRecord visual,
        OracleRoomData room,
        Vector2 position,
        int angle)
    {
        _room = room;
        _angle = angle & 0x1f;
        Position = position;
        Name = "PumpkinHeadProjectile";
        ZIndex = 11;
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
        Position - new Vector2(CollisionRadiusX, CollisionRadiusY),
        new Vector2(CollisionRadiusX * 2, CollisionRadiusY * 2));

    internal void UpdateFrame(Player player)
    {
        if (Finished)
            return;
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

        Position += OracleObjectMath.VectorFromAngle32(_angle) * 1.5f;
        if (!OracleObjectMath.IsInsideOriginalScreenBoundary(Position) ||
            _room.IsSolid(Position))
        {
            Finished = true;
            Visible = false;
            return;
        }
        if (Mathf.Abs(player.Position.X - Position.X) <
                CollisionRadiusX + LinkCollisionRadius &&
            Mathf.Abs(player.Position.Y - Position.Y) <
                CollisionRadiusY + LinkCollisionRadius)
        {
            player.ApplyEnemyContactDamage(Position, 2);
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
