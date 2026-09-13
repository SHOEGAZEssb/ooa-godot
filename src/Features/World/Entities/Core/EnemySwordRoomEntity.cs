using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>Invisible PART_ENEMY_SWORD $1d. Its position and collision lifetime
/// belong to the part pass, after its parent's enemy pass.</summary>
internal sealed class EnemySwordRoomEntity : IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    ISwordHittableRoomEntity, ILinkSwordStateAwareRoomEntity, ISwordAttackerKnockbackRoomEntity,
    ILinkContactEntity, IItemCollisionHittableRoomEntity, IScreenTransitionPreloadRoomEntity,
    INativePartHealthRoomEntity
{
    private readonly SwordEnemyCharacter _parent;
    private readonly Action<int> _soundRequested;
    private readonly Func<bool> _parentCollisionAllowed;
    private readonly Node2D _node = new() { Name = "EnemySword_1d", Visible = false };
    private bool _initialized;
    private bool _enabled;
    private int _invincibility;
    private int _knockback;
    private int _knockbackAngle;
    private bool _healthCleared;
    private int _pendingRecoil;
    private Vector2? _pendingBumpSource;
    private SwordActionState _swordState;
    private int _swordLevel;
    private Rect2 _bounds;

    internal EnemySwordRoomEntity(SwordEnemyCharacter parent, Action<int> soundRequested, Func<bool> parentCollisionAllowed)
    { _parent = parent; _soundRequested = soundRequested; _parentCollisionAllowed = parentCollisionAllowed; }

    public Node2D Node => _node;
    public bool Finished => !GodotObject.IsInstanceValid(_parent) || _parent.IsDead;
    internal bool CollisionEnabled => _enabled && !Finished;
    internal int InvincibilityCounter => _invincibility;
    internal int KnockbackCounter => _knockback;
    internal int KnockbackAngle => _knockbackAngle;
    internal Rect2 CollisionBounds => _bounds;
    public void ClearHealthAndCollision() { _healthCleared = true; _enabled = false; }
    public void SetTransitionDrawOffset(Vector2 offset) { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => UpdatePart();

    private void UpdatePart()
    {
        if (Finished) return;
        if (!_initialized) _healthCleared = false; // Common state-zero property reload.
        else if (_invincibility != 0) _invincibility += _invincibility > 0 ? -1 : 1;
        if (_pendingBumpSource is not null || _healthCleared)
        {
            // partCode1d copies the raw bytes, after common invincibility
            // advances. A zero-health part repeats this on every dispatch.
            _parent.ApplyBladeBump(_invincibility, _knockbackAngle, _knockback);
            _pendingBumpSource = null;
        }
        // State 0 goes directly to position setup; subsequent updates check
        // relatedObj1.var30, health, stun and var3f before enabling collision.
        // The part reads parent health and var30; the body's collision-enable
        // bit is not a gate (the Switch Hook clears it while holding the body).
        _enabled = !_initialized || _parent.SwordBlocking && _parent.Health != 0 && _parentCollisionAllowed();
        _initialized = true;
        _node.Position = _parent.EnemySwordPosition;
        _bounds = _parent.EnemySwordCollisionBounds;
        // The part never decrements its knockback byte. Only the parent
        // advances the copy in its own ENEMY pass.
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { if (!_initialized) UpdatePart(); return ScreenTransitionPresentation.Hidden; }

    public void SetLinkSwordState(SwordActionState state, int swordLevel)
    { _swordState = state; _swordLevel = swordLevel; }

    public bool ApplySwordHit(Rect2 hitbox, Vector2 sourcePosition, int damage,
        EnemyKnockbackStrength strength, ICollection<RoomEntitySpawn> spawns)
    {
        if (!CollisionEnabled || _invincibility != 0 || !_bounds.Intersects(hitbox)) return false;
        int collision = _swordState == SwordActionState.Spin ? (_swordLevel >= 2 ? 8 : 7)
            : _swordState is SwordActionState.Held or SwordActionState.Charged ? 9
            : _swordLevel >= 3 ? 6 : _swordLevel >= 2 ? 5 : 4;
        int effect = EnemyBehaviorTables.Shared.EnemySwordCollisionEffects[collision].Value;
        if (effect == 0) return false;
        if (effect == 0x14)
        {
            _invincibility = -26;
            _knockback = 15;
        }
        else if (effect is 0x32 or 0x33)
        {
            _pendingRecoil = effect == 0x32 ? 6 : 8;
            _invincibility = -(_pendingRecoil + 3);
            _knockback = _pendingRecoil + 1;
        }
        else throw new InvalidOperationException($"PART_ENEMY_SWORD $1d: unsupported sword effect ${effect:x2}.");
        PublishBump(sourcePosition);
        Vector2 midpoint = OracleObjectMath.ToPixelPosition(
            (_node.Position + OracleObjectMath.ToPixelPosition(hitbox.GetCenter())) / 2);
        spawns.Add(new EnemyClinkSpawn(midpoint));
        return true;
    }

    public bool ApplyItemCollision(RoomEntityItemCollision collision, Rect2 hitbox,
        Vector2 sourcePosition, int damage, ICollection<RoomEntitySpawn> spawns) => false;

    public bool TryGetSwordAttackerKnockback(EnemyKnockbackStrength strength, out SwordAttackerKnockback response)
    {
        response = new SwordAttackerKnockback(_node.Position, _pendingRecoil);
        bool pending = _pendingRecoil != 0;
        _pendingRecoil = 0;
        return pending;
    }

    public void HandleLinkContact(Player player)
    {
        if (!CollisionEnabled || _invincibility != 0 || !player.EnemyContactHeightOverlaps(0)) return;
        if (player.IsUsingShield && player.ShieldCollisionBounds.Intersects(_bounds))
        {
            if (!player.CanAcceptShieldCollision) return;
            bool level1 = player.Inventory.ShieldLevel == 1;
            _invincibility = -(level1 ? 21 : 16);
            _knockback = level1 ? 11 : 8;
            PublishBump(player.ShieldCollisionBounds.GetCenter());
            player.ApplyShieldCollisionRecoil(_node.Position, level1 ? 15 : 8, level1 ? 19 : 11);
            _soundRequested(OracleSoundEngine.SndClink);
            return;
        }
        if (player.OverlapsEnemyCollision(_bounds))
            player.ApplyEnemyContactDamage(_node.Position, 2);
    }

    private void PublishBump(Vector2 sourcePosition)
    {
        _pendingBumpSource = sourcePosition;
        _knockbackAngle = OracleObjectMovement.Shared.RelativeAngle(
            OracleObjectMath.ToPixelPosition(_node.Position),
            OracleObjectMath.ToPixelPosition(sourcePosition)) ^ 0x10;
    }
}

internal sealed record EnemySwordSpawn(SwordEnemyCharacter Parent, Action<int> SoundRequested,
    Func<bool> ParentCollisionAllowed) : RoomEntitySpawn;
