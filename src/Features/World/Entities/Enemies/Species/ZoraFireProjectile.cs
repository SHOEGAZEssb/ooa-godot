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
    private bool _healthCleared;
    private bool _pendingCollision;
    private OracleObjectPosition _position;
    private readonly RingDamageSource _damageSource;
    internal int PartId { get; }
    internal void ClearHealthAndCollision() => _healthCleared = true;
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int Angle { get; private set; }
    internal bool Finished { get; private set; }
    internal void SetPaletteOverride(IReadOnlyDictionary<int, Color[]>? palette) => _animation.SetPaletteOverride(palette);
    internal Rect2 CollisionBounds => Finished || _healthCleared || _pendingCollision ? new(Position, Vector2.Zero) :
        new(Position - new Vector2(_data.RadiusX, _data.RadiusY), new Vector2(_data.RadiusX * 2, _data.RadiusY * 2));

    internal ZoraFireProjectile(ZoraFireSpawn spawn, ZoraFireDatabase data,
        Func<Vector2, Vector2> worldToScreen)
    {
        if (spawn.PartId is not (oracleofages.PartId.ZoraFire or oracleofages.PartId.GopongaProjectile)) throw new ArgumentOutOfRangeException(nameof(spawn), "Expected PART $19 or $31.");
        PartId = spawn.PartId;
        _damageSource = PartId == oracleofages.PartId.ZoraFire ? RingDamageSource.ZoraFire : RingDamageSource.Generic;
        _position = OracleObjectMovement.Shared.PositionFromPixels(spawn.Position);
        Position = _position.PixelPosition;
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
            _healthCleared = false; // partLoadGraphicsAndProperties precedes state zero.
            State = 1;
            Counter = 8;
            Visible = true;
            ZIndex = NpcCharacter.InFrontOfLinkZIndex;
            return;
        }
        if (_healthCleared || _pendingCollision)
        {
            Finished = true; // partCode19: jp nz,partDelete.
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
        _position = OracleObjectMovement.Shared.ApplySpeed(_position, _data.Speed, Angle);
        Position = _position.PixelPosition;
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

    internal void HandleLinkContact(Player player)
    {
        if (State == 0 || Finished || _healthCleared || _pendingCollision || !player.EnemyContactHeightOverlaps(0)) return;
        // Both partActiveCollisions rows are $0d: Link, L2 and L3 shield.
        if (player.CanAcceptShieldCollision && player.TryBlockWithShield(CollisionBounds, minimumLevel: 2))
        {
            _pendingCollision = true;
            return;
        }
        if (!player.AcceptsRoomEntityContact || player.InvincibilityFrames != 0 ||
            !player.OverlapsEnemyCollision(CollisionBounds)) return;
        if (RingEffects.PreventsDamage(player.Inventory, _damageSource))
            _healthCleared = true; // collisionEffect3c destroys $19 for Blue Holy Ring.
        else if (player.ApplyEnemyContactDamage(Position, _data.Damage, _damageSource))
            _pendingCollision = true;
    }
}

internal sealed record ZoraFireSpawn(Vector2 Position, int PartId = oracleofages.PartId.ZoraFire) : RoomEntitySpawn;
