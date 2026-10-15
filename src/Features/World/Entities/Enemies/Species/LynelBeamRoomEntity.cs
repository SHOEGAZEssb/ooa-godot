using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// PART_LYNEL_BEAM $1b. This projectile has no tile-collision or sword response.
internal sealed partial class LynelBeamRoomEntity : TransitionOffsetNode2D, IRoomEntity,
    IFixedRoomEntity, IRoomEntityLifetime, IPostObjectLinkContactRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, INativePartHealthRoomEntity, IObjectCollisionHeightRoomEntity
{
    private readonly EnemyBehaviorTables _data = EnemyBehaviorTables.Shared;
    private readonly EnemyAnimationPlayer _animation;
    private readonly RoomEntityManager _owner;
    private bool _hit, _collisionEnabled = true;
    internal int State { get; private set; }
    internal int Angle { get; }
    public int CollisionZ { get; }
    internal int Palette { get; private set; }
    internal Rect2 CollisionBounds { get; private set; }
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
    internal LynelBeamRoomEntity(EnemyProjectileVisualRecord visual, LynelBeamSpawn spawn, RoomEntityManager owner)
    {
        _owner = owner; Position = spawn.Position.Floor(); Angle = spawn.Angle; CollisionZ = spawn.ZHigh;
        Palette = visual.Palette;
        _animation = new(this, visual.Animations.Length);
        _animation.Load(EnemyVisualSource.LoadComposite(visual.Sprites), visual.Animations, visual.TileBase,
            visual.Palette, sourceGrayscaleInverted: visual.SourceGrayscaleInverted,
            paletteVariants: [visual.Palette ^ _data.LynelBeamState[2].Value]);
        Visible = false;
    }
    public void ClearHealthAndCollision() { _collisionEnabled = false; _hit = true; }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (_hit) { Finished = true; Visible = false; return; }
        if (State == 0)
        {
            State = 1;
            int index = ((Angle + 4) & 0x18) >> 1;
            Position = new(((int)Position.X + _data.LynelBeamOffsets[index + 1].Value) & 255,
                ((int)Position.Y + _data.LynelBeamOffsets[index].Value) & 255);
            Vector2 radius = new(_data.LynelBeamOffsets[index + 3].Value, _data.LynelBeamOffsets[index + 2].Value);
            CollisionBounds = new(Position - radius, radius * 2);
            _animation.SetAnimation(Angle >> 3);
            Visible = true; ZIndex = ObjectDrawPriority.FromVisible(_data.LynelBeamState[3].Value);
            return;
        }
        // objectCheckWithinScreenBoundary performs byte subtraction/addition:
        // wrapped $f9..$ff coordinates retain the -7..-1 screen margin.
        Vector2 screen = _owner.ToScreen(Position);
        if ((((int)Mathf.Floor(screen.Y) + 7) & 255) >= 0x8f ||
            (((int)Mathf.Floor(screen.X) + 7) & 255) >= 0xaf)
        { Finished = true; Visible = false; return; }
        Vector2 before = Position;
        var velocity = NativeObjectMovement.Velocity(_owner.RuntimeState, _data.LynelBeamState[0].Value, Angle);
        Position = OracleObjectPosition.FromPixels(Position).Add(velocity.YFixed, velocity.XFixed).PrecisePosition;
        CollisionBounds = new(CollisionBounds.Position + Position - before, CollisionBounds.Size);
        if ((frame.Counter & _data.LynelBeamState[1].Value) == 0) Palette ^= _data.LynelBeamState[2].Value;
        QueueRedraw();
    }
    public void HandleLinkContact(Player player)
    {
        if (State == 0 || Finished || _hit || !_collisionEnabled || !player.EnemyContactHeightOverlaps(CollisionZ)) return;
        int shield = player.Inventory.ShieldLevel;
        if (player.IsUsingShield && shield is >= 1 and <= 3 && _data.LynelBeamActiveCollisions[shield].Value != 0 &&
            player.CanAcceptShieldCollision && RoomEntityManager.ObjectCollisionXYOverlaps(CollisionBounds, player.ShieldCollisionBounds))
        {
            if (_data.LynelBeamEffects[shield].Value != 0x1e) throw new NotSupportedException($"PART$1b shield${shield:x2} effect unsupported.");
            // collisionEffect1e: LINKDMG_28 leaves invincibility untouched
            // and writes a sixteen-update recoil, with no clink sound.
            player.ApplyShieldCollisionRecoil(Position, 0, 16);
            _hit = true; return;
        }
        if (player.NativeObjectVulnerable && player.OverlapsEnemyCollision(CollisionBounds, CollisionZ) &&
            player.ApplyEnemyContactDamage(Position, (0x100 - _data.LynelBeamData[3].Value) / 2,
                RingDamageSource.Generic, 42, 21)) _hit = true;
    }
    public override void _Draw()
    {
        if (Visible && !Finished) DrawTexture(_animation.CurrentTextureForPalette(Palette), _animation.CurrentOffset + Vector2.Down * CollisionZ + TransitionDrawOffset);
    }
}
