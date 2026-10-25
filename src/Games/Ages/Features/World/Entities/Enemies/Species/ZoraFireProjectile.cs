using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed partial class ZoraFireProjectile : TransitionOffsetNode2D
{
    private readonly ZoraFireDatabase _data;
    private readonly EnemyAnimationPlayer _animation;
    private readonly Func<Vector2, Vector2> _worldToScreen;
    private readonly OracleRuntimeState _memory;
    private int _palette;
    private bool _healthCleared;
    private bool _pendingCollision;
    private OracleObjectPosition _position;
    private readonly RingDamageSource _damageSource;
    internal int PartId { get; }
    internal int SubId { get; }
    internal void ClearHealthAndCollision() => _healthCleared = true;
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int Angle { get; private set; }
    internal bool Finished { get; private set; }
    internal void SetPaletteOverride(IReadOnlyDictionary<int, Color[]>? palette) => _animation.SetPaletteOverride(palette);
    internal Rect2 CollisionBounds => Finished || _healthCleared || _pendingCollision ? new(Position, Vector2.Zero) :
        new(Position - new Vector2(_data.RadiusX, _data.RadiusY), new Vector2(_data.RadiusX * 2, _data.RadiusY * 2));

    internal ZoraFireProjectile(ZoraFireSpawn spawn, ZoraFireDatabase data,
        Func<Vector2, Vector2> worldToScreen, OracleRuntimeState memory)
    {
        _memory = memory;
        if (spawn.PartId is not (Oracles.PartId.ZoraFire or Oracles.PartId.GopongaProjectile)) throw new ArgumentOutOfRangeException(nameof(spawn), "Expected PART $19 or $31.");
        PartId = spawn.PartId;
        SubId = spawn.SubId;
        _damageSource = PartId == Oracles.PartId.ZoraFire ? RingDamageSource.ZoraFire : RingDamageSource.Generic;
        _position = OracleObjectMovement.Shared.PositionFromPixels(spawn.Position);
        Position = _position.PixelPosition;
        _data = data;
        _palette = data.Palette;
        _worldToScreen = worldToScreen;
        _animation = new EnemyAnimationPlayer(this, 1);
        _animation.Load(OracleGraphicsCache.LoadImage(AgesGameContent.AssetRoot + $"gfx/{data.Sprite}.png"),
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
            ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex;
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
            // common/parts/fireProjectiles.s: subid bit0 selects hFFB2/3
            // (Scent-or-Link); even subids use hEnemyTargetY/X (always Link).
            Vector2 target = frame.LinkTarget ?? frame.Player.EnemyContactPosition;
            if ((SubId & 1) != 0) target = frame.ScentSeedTarget ?? target;
            Angle = OracleObjectMovement.Shared.RelativeAngle(Position,
                target);
            return;
        }
        if ((frame.Counter & 3) == 0) _palette ^= 7;
        _position = NativeObjectMovement.ApplySpeed(_memory, _position, _data.Speed, Angle);
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
        if (!player.PatchCollisionsEnabled || player.InvincibilityFrames != 0 ||
            !player.OverlapsEnemyCollision(CollisionBounds)) return;
        if (RingEffects.PreventsDamage(player.Inventory, _damageSource))
            _healthCleared = true; // collisionEffect3c destroys $19 for Blue Holy Ring.
        else if (player.ApplyEnemyContactDamage(Position, _data.Damage, _damageSource))
            _pendingCollision = true;
    }
}

internal sealed record ZoraFireSpawn(Vector2 Position, int PartId = Oracles.PartId.ZoraFire, int SubId = 0) : RoomEntitySpawn;
