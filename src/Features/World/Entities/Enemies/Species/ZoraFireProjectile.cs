using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed partial class ZoraFireProjectile : TransitionOffsetNode2D
{
    private readonly ZoraFireDatabase _data;
    private readonly EnemyAnimationPlayer _animation;
    private readonly Func<Vector2, Vector2> _worldToScreen;
    private int _palette;
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int Angle { get; private set; }
    internal bool Finished { get; private set; }
    internal void SetPaletteOverride(IReadOnlyDictionary<int, Color[]>? palette) => _animation.SetPaletteOverride(palette);
    internal Rect2 CollisionBounds => new(Position - new Vector2(_data.RadiusX, _data.RadiusY),
        new Vector2(_data.RadiusX * 2, _data.RadiusY * 2));

    internal ZoraFireProjectile(ZoraFireSpawn spawn, ZoraFireDatabase data,
        Func<Vector2, Vector2> worldToScreen)
    {
        Position = spawn.Position;
        _data = data;
        _palette = data.Palette;
        _worldToScreen = worldToScreen;
        _animation = new EnemyAnimationPlayer(this, 1);
        _animation.Load(OracleGraphicsCache.LoadImage($"res://assets/oracle/gfx/{data.Sprite}.png"),
            [data.Animation], data.TileBase, data.Palette, paletteVariants: [data.Palette ^ 7]);
        _animation.SetAnimation(0);
        Visible = false;
    }

    internal void UpdateFrame(RoomEntityFrame frame)
    {
        if (Finished) return;
        if (State == 0)
        {
            State = 1;
            Counter = 8;
            Visible = true;
            return;
        }
        // Part.collisionType starts enabled: even the eight stationary
        // updates retain the ordinary part collision window.
        if (frame.Player.TryBlockWithShield(CollisionBounds) ||
            frame.Player.AcceptsRoomEntityContact &&
            frame.Player.OverlapsEnemyCollision(CollisionBounds) &&
            frame.Player.ApplyEnemyContactDamage(Position, _data.Damage, RingDamageSource.OctorokProjectile))
        {
            Finished = true;
            Visible = false;
            return;
        }
        if (State == 1)
        {
            if (--Counter != 0) return;
            State = 2;
            Angle = OracleObjectMovement.Shared.RelativeAngle(Position,
                frame.ScentSeedTarget ?? frame.Player.Position);
            return;
        }
        if ((frame.Counter & 3) == 0) _palette ^= 7;
        Position += OracleObjectMovement.Shared.Delta(_data.Speed, Angle);
        Vector2 screen = _worldToScreen(Position) - Vector2.Down * OracleRoomData.StatusBarHeight;
        if (!OracleObjectMath.IsInsideOriginalScreenBoundary(screen))
        {
            Finished = true;
            Visible = false;
            return;
        }
        _animation.Advance();
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Finished) DrawTexture(_animation.CurrentTextureForPalette(_palette),
            new Vector2(-16, -16) + TransitionDrawOffset);
    }

    internal bool Strike()
    {
        if (Finished) return false;
        Finished = true;
        Visible = false;
        return true;
    }
}

internal sealed record ZoraFireSpawn(Vector2 Position) : RoomEntitySpawn;
