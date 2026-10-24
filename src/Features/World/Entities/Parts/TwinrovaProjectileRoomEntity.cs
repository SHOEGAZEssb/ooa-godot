using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// PART $4b/$4d: only native active collision columns can reflect a shot.
// The pending collision survives frozen PART visits and is consumed once.
internal sealed partial class TwinrovaProjectileRoomEntity : EnemyCharacter, IRoomEntity, IFixedRoomEntity,
    IRoomEntityLifetime, IPostObjectLinkContactRoomEntity, IPostObjectMeleeCollisionRoomEntity,
    ILinkSwordStateAwareRoomEntity, ISwordHittableRoomEntity, IObjectCollisionHeightRoomEntity, INativePartHealthRoomEntity
{
    private readonly TwinrovaEnvironment _world;
    private readonly int _parentSlot;
    private int _targetSlot = -1, _pendingHit = -1, _sword = 4;
    internal int Id { get; }
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    internal int Angle { get; private set; }
    internal int Speed { get; private set; }
    internal int ZFixed { get; private set; }
    internal override bool InitializationPending => State == 0;
    internal override bool CollisionEnabled => !IsDead && State >= 2;
    protected override Vector2 AnimationDrawOffset => base.AnimationDrawOffset + Vector2.Down * (ZFixed >> 8);
    public Node2D Node => this;
    public bool Finished => IsDead;
    public bool MeleeReportsContact { get; private set; }
    public int CollisionZ => ZFixed >> 8;
    private TwinrovaCharacter Parent => Resolve(_parentSlot);
    private TwinrovaCharacter Resolve(int slot) => _world.Entities.ResolveEnemySlot(slot) is TwinrovaRoomEntity twin ? twin.Actor :
        throw new NotSupportedException($"twinrovaProjectile.s: PART ${Id:x2} related enemy slot${slot:x2} is not ENEMY $03.");
    internal TwinrovaProjectileRoomEntity(TwinrovaCharacter parent, int id)
    {
        _world = parent.World; _parentSlot = parent.Slot; Id = id; ZFixed = (parent.ZFixed >> 8) << 8;
        Angle = parent.Angle; // ecom_spawnProjectile copies this before the charge pass.
        InitializeEnemy(parent.Position.Floor(), EnemyCharacterConfiguration.FromImported(_world.Data.Actor(id)), positionedOam: true);
        Visible = false;
    }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
    public void ClearHealthAndCollision() => throw new NotSupportedException($"twinrovaProjectile.s: PART ${Id:x2} has no native health-clear collision column.");
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        try
        {
            if (_pendingHit >= 0)
            {
                int hit = _pendingHit; _pendingHit = -1;
                if (hit == 3) { Finish(); return; } // L3 shield deletes, before the parent-state gate.
                if (hit != 0)
                {
                    Angle = OracleObjectMovement.Shared.RelativeAngle(Position.Floor(), (frame.EnemyTarget ?? frame.Player.Position).Floor()) ^ 16;
                    State = 3; Speed = 0x64;
                }
            }
            if (Parent.State >= 0x0d) { _world.Entities.TryCreateVirePuff(Position, ZFixed >> 8, 0); Finish(); return; }
            if (State == 0)
            {
                State = 1; Counter = 30; Speed = 0x50;
                Position = new(Position.X, (((int)Position.Y + (ZFixed >> 8)) & 255) + Position.Y % 1); ZFixed &= 255;
                _targetSlot = Parent.TwinSlot;
                _world.Entities.OnSoundRequested(Id == 0x4b ? SoundId.SndBeam1 : SoundId.SndBeam2);
                Visible = true; ZIndex = ObjectDrawPriority.FromVisible(0x81);
            }
            if (State == 1)
            {
                if (Counter != 0) Counter--;
                if (Counter != 0)
                {
                    Position = new((int)Parent.Position.X + Position.X % 1, (((int)Parent.Position.Y - 22) & 255) + Position.Y % 1);
                    ZFixed &= 255; AdvanceAnimation(); return;
                }
                Angle = OracleObjectMovement.Shared.RelativeAngle(Position.Floor(), (frame.EnemyTarget ?? frame.Player.Position).Floor());
                State = 2;
            }
            if (State == 3)
            {
                var target = Resolve(_targetSlot);
                if (RoomEntityManager.ObjectCollisionZOverlaps(CollisionZ, target.ZFixed >> 8, 7) &&
                    RoomEntityManager.ObjectCollisionXYOverlaps(CollisionBounds, target.CollisionBounds))
                {
                    target.InvincibilityCounter = 20; target.Health = (target.Health - 1) & 255;
                    if (target.Health == 0) target.Flags |= 64;
                    Parent.Health = (Parent.Health - 1) & 255;
                    _world.Entities.OnSoundRequested(SoundId.SndBossDamage); Finish(); return;
                }
            }
            Position = ApplyMovementSpeed(OracleObjectPosition.FromPixels(Position), Speed, Angle).PrecisePosition;
            if (!NativePartBoundary.IsInside(_world.Entities.ActiveRoom, Position.Floor(), Angle, _world.Data.Bytes("part-boundaries"))) { Finish(); return; }
            AdvanceAnimation();
        }
        finally { AdvanceInvincibilityCounter(); }
    }
    public void SetLinkSwordState(SwordActionState state, int level, int? collision = null) => _sword = collision ?? SwordCollision.Type(state, level);
    public bool ApplySwordHit(Rect2 bounds, Vector2 origin, int damage, EnemyKnockbackStrength strength, ICollection<RoomEntitySpawn> spawns) => Hit(_sword, bounds);
    private bool Hit(int item, Rect2 bounds)
    {
        MeleeReportsContact = false;
        if (!CollisionEnabled || _pendingHit >= 0 || InvincibilityCounter != 0 || !_world.Data.Active(true, Id, item) ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(CollisionBounds, bounds)) return false;
        int effect = _world.Data.Effect(0x7a, item);
        if (effect == 0) return true;
        if (effect is not (0x1c or 0x20 or 0x1e or 0x1f))
            throw new NotSupportedException($"twinrovaProjectile.s: PART ${Id:x2}, item${item:x2}, effect${effect:x2}.");
        _pendingHit = item; MeleeReportsContact = true;
        if (effect is 0x1e or 0x1f)
        {
            if (item <= 3) _world.Entities.OnSoundRequested(SoundId.SndBombLand);
            InvincibilityCounter = -28; _world.Entities.OnSoundRequested(SoundId.SndClink2);
        }
        return true;
    }
    public void HandleLinkContact(Player player)
    {
        if (!CollisionEnabled || _pendingHit >= 0 || InvincibilityCounter != 0 || !player.EnemyContactHeightOverlaps(CollisionZ)) return;
        if (player.CanAcceptShieldCollision && player.IsUsingShield && Hit(player.Inventory.ShieldLevel, player.ShieldCollisionBounds))
        {
            if (_world.Data.Effect(0x7a, player.Inventory.ShieldLevel) == 0x1e) player.ApplyShieldCollisionRecoil(Position, 0, 16);
            return;
        }
        if (player.OverlapsEnemyCollision(CollisionBounds, CollisionZ) && player.ApplyEnemyContactDamage(Position, _world.Data.Actor(Id).DamageQuarters,
            RingDamageSource.Generic, 34, 15)) _pendingHit = 0;
    }
}
