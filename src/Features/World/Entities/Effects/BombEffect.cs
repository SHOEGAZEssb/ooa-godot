using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// ITEM_BOMB ($03) child actor. Fuse animation, weight-0 throwing, bounce,
/// hazard deletion, explosion collision, and nine ordered tile probes all
/// advance on the original 60 Hz room-entity update.
/// </summary>
public partial class BombEffect : TransitionOffsetNode2D
{
    private BombRecord _record = null!;
    private OracleRoomData _room = null!;
    private BreakableTileDatabase _breakables = null!;
    private InventoryState _inventory = null!;
    private OracleSaveData? _saveData;
    private Action<int> _playSound = null!;
    private Action<Vector2, HazardType, ObjectFellInHoleKind> _enteredHazard =
        null!;
    private Action _roomTileChanged = null!;
    private Func<long> _animationTick = null!;
    private Func<int, int?> _decideBreakableDrop = null!;
    private Func<Vector2I, int?>? _linkedRoomNeighbor;
    private Player? _heldBy;
    private Action<BombEffect>? _heldExplosion;
    private AnimationFrameDefinition[] _fuseFrames = null!;
    private AnimationFrameDefinition[] _explosionFrames = null!;
    private Texture2D[] _fuseTextures = null!;
    private Texture2D[] _explosionTextures = null!;
    private Vector2 _precisePosition;
    private Vector2I _throwDirection;
    private BombState _state;
    private int _group;
    private int _zFixed;
    private int _speedZ;
    private int _speedRaw;
    private int _frameIndex;
    private int _frameCounter;
    private int _breakProbe;
    private int _damage;
    private bool _setupPending;
    private bool _linkHit;
    private bool _explosionCollisionEnabled;

    public bool Finished => _state == BombState.Finished;
    internal BombState State => _state;
    internal int ZFixed => _zFixed;
    internal int SpeedZ => _speedZ;
    internal int SpeedRaw => _speedRaw;
    internal Vector2I ThrowDirection => _throwDirection;
    internal int AnimationFrame => _frameIndex;
    internal int AnimationCounter => _frameCounter;
    internal int BreakProbe => _breakProbe;
    internal int ElapsedFrames { get; private set; }
    internal Vector2 PrecisePosition => _precisePosition;
    internal bool CanBePickedUp =>
        !Finished && !_setupPending &&
        _state is BombState.Thrown or BombState.Grounded &&
        !_explosionCollisionEnabled;
    internal bool CanMaplePull =>
        !Finished &&
        _state is BombState.Grounded or BombState.MaplePulling;
    internal bool ExplosionCollisionEnabled =>
        _state == BombState.Exploding &&
        _explosionCollisionEnabled &&
        (_explosionFrames[_frameIndex].Parameter & 0xc0) == 0;
    internal int ExplosionRadius =>
        _state == BombState.Exploding
            ? _explosionFrames[_frameIndex].Parameter & 0x1f
            : 0;
    internal int Damage => _damage;
    internal int CollisionZ => _zFixed >> 8;
    internal Player? HeldPlayer => _heldBy;
    internal Rect2 ExplosionBounds
    {
        get
        {
            int radius = ExplosionRadius;
            return new Rect2(
                Position - new Vector2(radius, radius),
                new Vector2(radius * 2, radius * 2));
        }
    }

    internal void Initialize(
        BombRecord record,
        OracleRoomData room,
        BreakableTileDatabase breakables,
        Player player,
        int group,
        Action<BombEffect> heldExplosion,
        Action<int> playSound,
        Action<Vector2, HazardType, ObjectFellInHoleKind> enteredHazard,
        Action roomTileChanged,
        Func<long> animationTick,
        Func<int, int?> decideBreakableDrop,
        OracleSaveData? saveData,
        Func<Vector2I, int?>? linkedRoomNeighbor)
    {
        _record = record;
        _room = room;
        _breakables = breakables;
        _inventory = player.Inventory;
        _heldBy = player;
        _group = group;
        _heldExplosion = heldExplosion;
        _playSound = playSound;
        _enteredHazard = enteredHazard;
        _roomTileChanged = roomTileChanged;
        _animationTick = animationTick;
        _decideBreakableDrop = decideBreakableDrop;
        _saveData = saveData;
        _linkedRoomNeighbor = linkedRoomNeighbor;

        _fuseFrames = OracleGraphicsCache.GetAnimationDefinition(
            record.FuseAnimation).Frames;
        _explosionFrames = OracleGraphicsCache.GetAnimationDefinition(
            record.ExplosionAnimation).Frames;
        Image fuseSource = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{record.Sprite}.png");
        Image explosionSource = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{record.ExplosionSprite}.png");
        _fuseTextures = BuildTextures(
            fuseSource, _fuseFrames, record.TileBase, record.Palette);
        _explosionTextures = BuildTextures(
            explosionSource,
            _explosionFrames,
            record.ExplosionTileBase,
            record.ExplosionPalette);
        _precisePosition = player.Position;
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        ResetFuseAnimation();
        _state = BombState.Held;
        _setupPending = true;
        Visible = false;
        QueueRedraw();
    }

    internal void BeginHeld(
        Player player,
        Action<BombEffect> heldExplosion)
    {
        if (!CanBePickedUp)
            throw new InvalidOperationException(
                "Only a live unexploded Bomb can be picked up.");
        _heldBy = player;
        _heldExplosion = heldExplosion;
        _throwDirection = Vector2I.Zero;
        _speedRaw = 0;
        _speedZ = 0;
        _state = BombState.Held;
        QueueRedraw();
    }

    internal void SetHeldOffset(Player player, Vector2I offset)
    {
        if (_state != BombState.Held || !ReferenceEquals(_heldBy, player))
            return;
        _precisePosition =
            player.Position + new Vector2(offset.X, 0);
        _zFixed = offset.Y << 8;
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        QueueRedraw();
    }

    internal void Throw(
        Player player,
        Vector2I heldOffset,
        Vector2I direction,
        int speedZ,
        int speedRaw)
    {
        if (_state != BombState.Held || !ReferenceEquals(_heldBy, player))
            return;
        _precisePosition =
            player.Position + new Vector2(heldOffset.X, 0) +
            player.FacingVector;
        _zFixed = heldOffset.Y << 8;
        _speedZ = speedZ;
        _speedRaw = speedRaw;
        _throwDirection = direction;
        _heldBy = null;
        _heldExplosion = null;
        _state = BombState.Thrown;
        SyncPosition();
    }

    internal void ReleaseExploding(Player player, Vector2I heldOffset)
    {
        if (_state != BombState.Exploding ||
            !ReferenceEquals(_heldBy, player))
        {
            return;
        }
        _precisePosition =
            player.Position + new Vector2(heldOffset.X, 0);
        _zFixed = heldOffset.Y << 8;
        _heldBy = null;
        _heldExplosion = null;
        SyncPosition();
    }

    internal bool OverlapsForPickup(Player player)
    {
        if (!CanBePickedUp)
            return false;
        return Mathf.Abs(player.Position.X - Position.X) <
                _record.RadiusX + 6 &&
            Mathf.Abs(player.Position.Y - Position.Y) <
                _record.RadiusY + 6;
    }

    internal void Discard() => Finish();

    /// <summary>
    /// Head Thwomp marks a bomb as consumed when it crosses the mouth window.
    /// The original item continues only long enough to raise the boss's
    /// pending-bomb bit; it can no longer explode or be picked up.
    /// </summary>
    internal bool ConsumeByBoss()
    {
        if (_state is BombState.Held or BombState.MaplePulling or
            BombState.Finished)
        {
            return false;
        }
        Finish();
        return true;
    }

    /// <summary>
    /// SPECIALOBJECT_MAPLE's vacuum moves a grounded live Bomb one source
    /// pixel per axis, then raises it by $0040 until zh reaches $f8.
    /// </summary>
    internal bool PullTowardMaple(Vector2 maplePosition)
    {
        if (_state == BombState.Grounded)
            _state = BombState.MaplePulling;
        if (_state != BombState.MaplePulling)
            return false;

        ResetFuseAnimation();
        Vector2 target = OracleObjectMath.ToPixelPosition(maplePosition);
        Vector2 current = OracleObjectMath.ToPixelPosition(_precisePosition);
        bool moved = false;
        if (current.Y < target.Y)
        {
            _precisePosition.Y += 1;
            moved = true;
        }
        else if (current.Y > target.Y)
        {
            _precisePosition.Y -= 1;
            moved = true;
        }
        if (current.X < target.X)
        {
            _precisePosition.X += 1;
            moved = true;
        }
        else if (current.X > target.X)
        {
            _precisePosition.X -= 1;
            moved = true;
        }
        if (!moved)
        {
            _zFixed -= 0x40;
            if ((_zFixed >> 8) == -8)
            {
                Finish();
                return true;
            }
        }
        SyncPosition();
        return false;
    }

    internal void UpdateFrame(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        ElapsedFrames++;
        if (_setupPending)
        {
            _setupPending = false;
            Visible = true;
            QueueRedraw();
            return;
        }

        switch (_state)
        {
            case BombState.Held:
                UpdateHeld();
                break;
            case BombState.Thrown:
                UpdateThrown(spawns);
                break;
            case BombState.Grounded:
                UpdateGrounded(spawns);
                break;
            case BombState.MaplePulling:
                ResetFuseAnimation();
                QueueRedraw();
                break;
            case BombState.Exploding:
                UpdateExplosion(player, spawns);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported Bomb state {_state}.");
        }
    }

    public override void _Draw()
    {
        if (Finished || !Visible)
            return;
        Texture2D texture = _state == BombState.Exploding
            ? _explosionTextures[_frameIndex]
            : _fuseTextures[_frameIndex];
        DrawTexture(
            texture,
            new Vector2(-16, -16 + (_zFixed >> 8)) +
                TransitionDrawOffset);
    }

    private void UpdateHeld()
    {
        if (_heldBy is null)
        {
            throw new InvalidOperationException(
                "A held Bomb lost its Link owner.");
        }
        if (!RingEffects.BombsExplode(_heldBy.Inventory))
        {
            ResetFuseAnimation();
            Visible = true;
            QueueRedraw();
            return;
        }
        if (!AdvanceFuse())
            return;

        Action<BombEffect>? release = _heldExplosion;
        release?.Invoke(this);
    }

    private void UpdateThrown(ICollection<RoomEntitySpawn> spawns)
    {
        if (!WithinRoomBoundary(_precisePosition))
        {
            Finish();
            return;
        }

        if (_throwDirection != Vector2I.Zero)
        {
            Vector2 edge = _precisePosition +
                _record.EdgeOffset(_throwDirection);
            if (WithinRoomBoundary(edge) && _room.IsSolid(edge))
            {
                _throwDirection = Vector2I.Zero;
                _speedRaw = 0;
            }
            else if (!IsSideScrolling() || _throwDirection.X != 0)
            {
                Position = OracleObjectMovement.Shared.ApplySpeed(
                    ref _precisePosition,
                    _speedRaw,
                    DirectionAngle(_throwDirection));
            }
        }

        if (!OracleObjectMath.UpdateSpeedZ(
                ref _zFixed, ref _speedZ, _record.Gravity))
        {
            SyncPosition();
            AdvanceFuse();
            return;
        }

        SyncPosition();
        if (TryEnterHazard(spawns))
            return;

        _playSound(_record.LandingSound);
        int rebound = (-_speedZ) >> 1;
        if (rebound > -0x80)
        {
            _speedZ = 0;
            _speedRaw = 0;
            _throwDirection = Vector2I.Zero;
            _state = BombState.Grounded;
        }
        else
        {
            _speedZ = rebound;
            _speedRaw = _record.ReducedBounceSpeed(_speedRaw);
            if (_speedRaw == 0)
                _throwDirection = Vector2I.Zero;
        }
        AdvanceFuse();
    }

    private void UpdateGrounded(ICollection<RoomEntitySpawn> spawns)
    {
        if (!WithinRoomBoundary(_precisePosition))
        {
            Finish();
            return;
        }
        if (TryEnterHazard(spawns))
            return;

        TerrainType terrain =
            _room.GetTerrainInfo(Position + new Vector2(0, 5)).Type;
        Vector2I direction = terrain switch
        {
            TerrainType.UpConveyor => Vector2I.Up,
            TerrainType.RightConveyor => Vector2I.Right,
            TerrainType.DownConveyor => Vector2I.Down,
            TerrainType.LeftConveyor => Vector2I.Left,
            _ => Vector2I.Zero
        };
        if (direction != Vector2I.Zero)
        {
            Vector2 edge = _precisePosition + _record.EdgeOffset(direction);
            if (WithinRoomBoundary(edge) && !_room.IsSolid(edge))
            {
                Position = OracleObjectMovement.Shared.ApplySpeed(
                    ref _precisePosition,
                    _record.ConveyorSpeedRaw,
                    DirectionAngle(direction));
            }
        }
        AdvanceFuse();
    }

    private bool TryEnterHazard(ICollection<RoomEntitySpawn> spawns)
    {
        HazardType hazard = _room.GetTerrainInfo(Position).Hazard;
        if (hazard == HazardType.None)
            return false;

        Vector2 position = Position;
        _enteredHazard(position, hazard, ObjectFellInHoleKind.Bomb);
        if (hazard == HazardType.Hole)
            spawns.Add(new FallingDownHoleSpawn(position));
        Finish();
        return true;
    }

    private bool AdvanceFuse()
    {
        _frameCounter--;
        if (_frameCounter > 0)
        {
            QueueRedraw();
            return false;
        }
        _frameIndex++;
        if (_frameIndex >= _fuseFrames.Length)
        {
            throw new InvalidOperationException(
                $"{_record.Source} fuse animation ended without its explosion marker.");
        }
        _frameCounter = _fuseFrames[_frameIndex].Duration;
        QueueRedraw();
        if (_fuseFrames[_frameIndex].Parameter == 0)
            return false;

        InitializeExplosion();
        return true;
    }

    private void InitializeExplosion()
    {
        _state = BombState.Exploding;
        _frameIndex = 0;
        _frameCounter = _explosionFrames[0].Duration;
        _breakProbe = 8;
        _damage = RingEffects.BombDamage(
            _record.BaseDamage,
            _inventory);
        _explosionCollisionEnabled = true;
        _linkHit = false;
        _playSound(_record.ExplosionSound);
        Visible = true;
        QueueRedraw();
    }

    private void UpdateExplosion(
        Player player,
        ICollection<RoomEntitySpawn> spawns)
    {
        int parameter = _explosionFrames[_frameIndex].Parameter;
        if ((parameter & 0x80) != 0)
        {
            Finish();
            return;
        }
        if ((parameter & 0x40) != 0)
            _explosionCollisionEnabled = false;

        int radius = parameter & 0x1f;
        if (_explosionCollisionEnabled && !_linkHit &&
            ZOverlaps(_zFixed >> 8, 0, radius) &&
            Mathf.Abs(player.Position.X - Position.X) < radius + 6 &&
            Mathf.Abs(player.Position.Y - Position.Y) < radius + 6 &&
            player.ApplyEnemyContactDamage(
                Position, _damage, RingDamageSource.OwnBomb))
        {
            _linkHit = true;
        }

        if (_breakProbe >= 0)
        {
            TryBreakTile(_record.BreakProbes[_breakProbe], spawns);
            _breakProbe--;
        }
        AdvanceExplosionAnimation();
    }

    private void TryBreakTile(
        BombBreakProbe probe,
        ICollection<RoomEntitySpawn> spawns)
    {
        Vector2I offset = probe.Offset;
        if (IsSideScrolling())
        {
            offset.Y += _zFixed >> 8;
        }
        else
        {
            int adjustedZ = unchecked((byte)((_zFixed >> 8) - 2));
            int threshold = unchecked((byte)probe.NecessaryZ);
            if (adjustedZ < threshold)
                return;
        }

        Vector2 point = Position + offset;
        if (!WithinRoomBoundary(point))
            return;
        byte tile = _room.GetMetatile(point);
        if (!_breakables.TryGet(
                _room.ActiveCollisions, tile,
                out BreakableTileRecord breakable) ||
            !breakable.AllowsSource(BreakableTileDatabase.SourceBomb))
        {
            return;
        }

        int packedPosition = _room.GetPackedPosition(point);
        Vector2 tileCenter = new(
            (packedPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
            (packedPosition >> 4) * OracleRoomData.MetatileSize + 8);
        byte replacement = breakable.ReplacementFor(_room, tileCenter);
        bool changed = breakable.Replacement == 0 ||
            _room.ReplaceMetatile(
                tileCenter,
                tile,
                replacement,
                _animationTick());
        if (!changed)
            return;

        breakable.ApplyPersistentEffects(
            _saveData,
            _group,
            _room.Id,
            _linkedRoomNeighbor);
        if ((breakable.Effect & 0x40) != 0)
            _playSound(OracleSoundEngine.SndSolvePuzzle);
        if (breakable.Drop != 0 &&
            _decideBreakableDrop(breakable.Drop) is int subId)
        {
            spawns.Add(new ItemDropSpawn(subId, tileCenter));
        }
        AddBreakEffect(spawns, tileCenter, breakable.Effect);
        _roomTileChanged();
    }

    private void AdvanceExplosionAnimation()
    {
        _frameCounter--;
        if (_frameCounter > 0)
        {
            QueueRedraw();
            return;
        }
        _frameIndex++;
        if (_frameIndex >= _explosionFrames.Length)
        {
            throw new InvalidOperationException(
                $"{_record.Source} explosion animation ended without parameter $ff.");
        }
        _frameCounter = _explosionFrames[_frameIndex].Duration;
        QueueRedraw();
    }

    private void ResetFuseAnimation()
    {
        _frameIndex = 0;
        _frameCounter = _fuseFrames?[0].Duration ?? 0x50;
        QueueRedraw();
    }

    private void Finish()
    {
        _state = BombState.Finished;
        _heldBy = null;
        _heldExplosion = null;
        Visible = false;
        QueueRedraw();
    }

    private void SyncPosition()
    {
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        QueueRedraw();
    }

    private bool WithinRoomBoundary(Vector2 point) =>
        point.X >= 0 && point.X < _room.Width &&
        point.Y >= 0 && point.Y < _room.Height;

    private bool IsSideScrolling() => (_room.TilesetFlags & 0x20) != 0;

    private static bool ZOverlaps(
        int sourceZ,
        int targetZ,
        int radius)
    {
        int doubled = radius * 2;
        return unchecked((byte)(targetZ - sourceZ + radius)) < doubled;
    }

    private static int DirectionAngle(Vector2I direction) =>
        direction == Vector2I.Up ? 0x00
        : direction == Vector2I.Right ? 0x08
        : direction == Vector2I.Down ? 0x10
        : direction == Vector2I.Left ? 0x18
        : throw new ArgumentOutOfRangeException(nameof(direction));

    private static Texture2D[] BuildTextures(
        Image source,
        AnimationFrameDefinition[] frames,
        int tileBase,
        int palette)
    {
        var result = new Texture2D[frames.Length];
        for (int index = 0; index < frames.Length; index++)
        {
            result[index] = NpcCharacter.BuildOamTexture(
                source, frames[index].EncodedOam, tileBase, palette);
        }
        return result;
    }

    private void AddBreakEffect(
        ICollection<RoomEntitySpawn> spawns,
        Vector2 position,
        int effect)
    {
        int interaction = effect & 0x0f;
        bool flickers = (effect & 0x10) != 0;
        if (interaction is 0x06 or 0x0c)
        {
            spawns.Add(new RockDebrisSpawn(position, interaction));
        }
        else if (interaction is 0x00 or 0x01)
        {
            spawns.Add(new GrassDebrisSpawn(
                position,
                interaction,
                flickers,
                (_room.TilesetFlags & 0x40) != 0));
        }
    }
}

internal enum BombState
{
    Held,
    Thrown,
    Grounded,
    MaplePulling,
    Exploding,
    Finished
}
