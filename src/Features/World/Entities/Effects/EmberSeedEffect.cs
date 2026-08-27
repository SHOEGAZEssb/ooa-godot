using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Shared Satchel-thrown seed child for ITEM_EMBER_SEED ($20),
/// ITEM_SCENT_SEED ($21), and ITEM_MYSTERY_SEED ($24), subid $00. State and
/// animation advances happen on original 60 Hz updates, including the
/// setup-only first update.
/// </summary>
public partial class EmberSeedEffect : TransitionOffsetNode2D
{

    private SeedRecord _record;
    private OracleRoomData _room = null!;
    private BreakableTileDatabase _breakables = null!;
    private Action<int> _playSound = null!;
    private Action<Vector2, HazardType> _enteredHazard = null!;
    private Action<ObjectFellInHoleKind> _objectFellInHole = null!;
    private Action _roomTileChanged = null!;
    private Func<long> _animationTick = null!;
    private Func<int, int?> _decideBreakableDrop = null!;
    private Func<Vector2I, int?>? _linkedRoomNeighbor;
    private OracleSaveData? _saveData;
    private int _group;
    private AnimationFrameDefinition[] _frames = null!;
    private AnimationFrameDefinition[] _flyingFrames = null!;
    private AnimationFrameDefinition[] _effectFrames = null!;
    private Texture2D[] _flyingTextures = null!;
    private Texture2D[] _effectTextures = null!;
    private Texture2D[] _collisionEffectTextures = null!;
    private Vector2 _precisePosition;
    private Vector2I _direction;
    private EmberState _state;
    private int _zFixed;
    private int _speedZ;
    private int _flameCounter;
    private int _frameIndex;
    private int _frameCounter;
    private int _loopStart;
    private int _effectLoopStart;
    private int _mysteryEffect;
    private bool _collisionEnabled;
    private bool _scentPublished;
    private ISeedBurnTarget? _burnTarget;
    private SeedLaunchKind _launchKind;
    private int _angle;
    private int _bouncesRemaining;
    private SeedShooterRecord _shooter;
    private ISeedBounceTarget? _lastBounceTarget;
    private bool _skipShooterTerrainCollision;

    public bool Finished => _state == EmberState.Finished;
    internal EmberState State => _state;
    internal int ElapsedFrames { get; private set; }
    internal int ZFixed => _zFixed;
    internal int SpeedZ => _speedZ;
    internal int FlameCounter => _flameCounter;
    internal int AnimationFrame => _frameIndex;
    internal Vector2 PrecisePosition => _precisePosition;
    internal bool CollisionEnabled =>
        _collisionEnabled && !Finished && _state != EmberState.Initializing;
    internal SeedLaunchKind LaunchKind => _launchKind;
    internal int Angle => _angle;
    internal int BouncesRemaining => _bouncesRemaining;
    internal int SeedItem => _record.SeedItem;
    internal Vector2? ScentTarget =>
        _state == EmberState.Scent && _scentPublished
            ? _precisePosition
            : null;
    internal int MysteryEffect => _mysteryEffect;
    internal ulong FlameTextureHashForValidation(int frame) =>
        OracleGraphicsCache.PixelHash(_effectTextures[frame].GetImage());
    internal ulong FlyingTextureHashForValidation(int frame) =>
        OracleGraphicsCache.PixelHash(_flyingTextures[frame].GetImage());
    internal Rect2 CollisionBounds => new(
        Position - new Vector2(_record.CollisionRadiusX, _record.CollisionRadiusY),
        new Vector2(_record.CollisionRadiusX * 2, _record.CollisionRadiusY * 2));

    internal void Initialize(
        SeedRecord record,
        OracleRoomData room,
        BreakableTileDatabase breakables,
        Vector2 linkPosition,
        Vector2I direction,
        Action<int> playSound,
        Action<Vector2, HazardType> enteredHazard,
        Action roomTileChanged,
        Func<long> animationTick,
        Func<int, int?> decideBreakableDrop,
        OracleSaveData? saveData,
        int group,
        Func<Vector2I, int?>? linkedRoomNeighbor = null,
        int mysteryEffect = 0,
        Action<ObjectFellInHoleKind>? objectFellInHole = null,
        SeedLaunchKind launchKind = SeedLaunchKind.Satchel,
        int angle = 0)
    {
        _record = record;
        _room = room;
        _breakables = breakables;
        _playSound = playSound;
        _enteredHazard = enteredHazard;
        _objectFellInHole = objectFellInHole ?? (_ => { });
        _roomTileChanged = roomTileChanged;
        _animationTick = animationTick;
        _decideBreakableDrop = decideBreakableDrop;
        _linkedRoomNeighbor = linkedRoomNeighbor;
        _saveData = saveData;
        _group = group;
        _launchKind = launchKind;
        _angle = angle;
        _shooter = SeedShooterRecord.Load();
        if (launchKind == SeedLaunchKind.Shooter && angle is < 0 or > 7)
            throw new ArgumentOutOfRangeException(nameof(angle));
        _bouncesRemaining = launchKind == SeedLaunchKind.Shooter
            ? _shooter.Bounces
            : 0;
        if (record.SeedItem == 0x24 && mysteryEffect is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(mysteryEffect));
        _mysteryEffect = record.SeedItem == 0x24 ? mysteryEffect : -1;
        _direction = direction;
        _precisePosition = linkPosition +
            (launchKind == SeedLaunchKind.Shooter
                ? _shooter.Offsets[angle]
                : record.Offset(direction));
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        _zFixed = record.InitialZ << 8;
        _speedZ = launchKind == SeedLaunchKind.Shooter ? 0 : record.SpeedZ;
        _collisionEnabled = true;

        AnimationDefinition animation =
            OracleGraphicsCache.GetAnimationDefinition(record.Animation);
        AnimationDefinition effectAnimation =
            OracleGraphicsCache.GetAnimationDefinition(record.EffectAnimation);
        _flyingFrames = animation.Frames;
        _effectFrames = effectAnimation.Frames;
        _frames = _flyingFrames;
        _loopStart = animation.LoopStart;
        _effectLoopStart = effectAnimation.LoopStart;
        if (_flyingFrames.Length == 0 || _effectFrames.Length == 0)
            throw new InvalidOperationException(
                $"{record.Source} imported an empty active-seed animation.");
        _frameCounter = _frames[0].Duration;

        Image flyingSource = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{record.Sprite}.png");
        Image flameSource = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{record.FlameSprite}.png");
        _flyingTextures = BuildTextures(
            flyingSource, record.TileBase, record.Palette, _flyingFrames);
        _effectTextures = BuildTextures(
            flameSource, record.FlameTileBase, record.FlamePalette,
            _effectFrames);
        _collisionEffectTextures = BuildTextures(
            flameSource,
            record.CollisionEffectTileBase,
            record.CollisionEffectPalette,
            _flyingFrames);
        Visible = false;
        QueueRedraw();
    }

    internal void UpdateFrame(ICollection<RoomEntitySpawn> spawns) =>
        UpdateFrame(ElapsedFrames + 1, spawns);

    internal void UpdateFrame(
        int globalFrameCounter,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        ElapsedFrames++;
        if (_state == EmberState.Initializing)
        {
            _state = EmberState.Flying;
            Visible = true;
            QueueRedraw();
            return;
        }
        if (_state == EmberState.Burning)
        {
            UpdateBurning(spawns);
            return;
        }
        if (_state == EmberState.Mystery)
        {
            UpdateDissipating();
            return;
        }
        if (_state == EmberState.Dissipating)
        {
            UpdateDissipating();
            return;
        }
        if (_state == EmberState.Scent)
        {
            UpdateScent(globalFrameCounter);
            return;
        }

        if (_launchKind == SeedLaunchKind.Shooter)
        {
            UpdateShooter(spawns);
            return;
        }

        if (!WithinRoomBoundary(_precisePosition))
        {
            Finish();
            return;
        }

        Position = OracleObjectMovement.Shared.ApplySpeed(
            ref _precisePosition,
            _record.SpeedRaw,
            DirectionAngle(_direction));
        bool landed = OracleObjectMath.UpdateSpeedZ(
            ref _zFixed, ref _speedZ, _record.Gravity);
        if (!landed)
        {
            QueueRedraw();
            return;
        }

        HazardType hazard = _room.GetTerrainInfo(Position).Hazard;
        if (hazard != HazardType.None)
        {
            if (hazard is HazardType.Water or HazardType.Lava)
                _enteredHazard(Position, hazard);
            else if (hazard == HazardType.Hole)
                _objectFellInHole(SeedHoleKind());
            Finish();
            return;
        }

        _playSound(_record.LandingSound);
        AdvanceAnimation();
        switch (_record.SeedItem)
        {
            case 0x20:
                BeginBurning();
                break;
            case 0x21:
                BeginScent();
                break;
            case 0x24:
                BeginMystery();
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported active seed ITEM ${_record.SeedItem:x2} landed.");
        }
    }

    internal void OnCollision(
        SeedHitResult result,
        ISeedBurnTarget? burnTarget = null,
        ISeedBounceTarget? bounceTarget = null)
    {
        if (!CollisionEnabled || result == SeedHitResult.None)
            return;
        if (result == SeedHitResult.Bounce)
        {
            if (bounceTarget is null)
                throw new ArgumentNullException(nameof(bounceTarget));
            if (_launchKind == SeedLaunchKind.Shooter)
            {
                BounceFrom(bounceTarget);
                return;
            }

            // The CROSSITEMS guard in func_50f4 only reflects shooter subid
            // $63. A Satchel seed takes the ordinary collided-with-wall path.
            result = SeedHitResult.Activate;
        }
        _collisionEnabled = false;
        if (result == SeedHitResult.Consume)
        {
            Finish();
            return;
        }
        if (_record.SeedItem == 0x24)
        {
            if (_state == EmberState.Flying)
                AdvanceAnimation();
            BeginMystery();
            return;
        }
        if (_record.SeedItem == 0x21)
        {
            if (_state == EmberState.Flying)
                AdvanceAnimation();
            BeginDissipating();
            return;
        }
        if (burnTarget is not null)
        {
            bool wasFlying = _state == EmberState.Flying;
            if (wasFlying)
            {
                // seedItemState1 performs one itemAnimate call before
                // COLLISIONEFFECT_BURN.
                AdvanceAnimation();
            }
            BeginEnemyBurn(burnTarget, playSound: wasFlying);
            return;
        }
        if (_state == EmberState.Flying)
        {
            // seedItemState1 branches to @seedCollidedWithEnemy before its
            // movement and performs this one itemAnimate call there.
            AdvanceAnimation();
            BeginBurning();
            return;
        }
        // emberSeedBurn already animated before itemUpdateDamageToApply. Its
        // parameter-$80 frame deletes the contacted flame immediately.
        if ((_frames[_frameIndex].Parameter & 0x80) != 0)
            Finish();
    }

    public override void _Draw()
    {
        if (Finished || !Visible)
            return;
        Texture2D texture = _state switch
        {
            EmberState.Burning or EmberState.Mystery or EmberState.Scent =>
                _effectTextures[_frameIndex],
            EmberState.Dissipating => _collisionEffectTextures[_frameIndex],
            _ => _flyingTextures[_frameIndex]
        };
        DrawTexture(texture,
            new Vector2(-16, -16 + (_zFixed >> 8)) + TransitionDrawOffset);
    }

    private void BeginBurning()
    {
        _state = EmberState.Burning;
        _flameCounter = _record.FlameCounter;
        _playSound(_record.FlameSound);
        QueueRedraw();
    }

    private void BeginMystery()
    {
        _state = EmberState.Mystery;
        _collisionEnabled = false;
        _playSound(_record.FlameSound);
        QueueRedraw();
    }

    private void BeginScent()
    {
        _state = EmberState.Scent;
        // @scentLanded calls objectSetVisible83. Source priority 3 is the
        // fixed low-priority group, below normal Link and priority-2 enemies.
        ZIndex = NpcCharacter.FixedLowPriorityZIndex;
        _scentPublished = false;
        _collisionEnabled = false;
        _flameCounter = _record.FlameCounter;
        _frames = _effectFrames;
        _loopStart = _effectLoopStart;
        _frameIndex = 0;
        _frameCounter = _frames[0].Duration;
        _playSound(_record.FlameSound);
        QueueRedraw();
    }

    private void BeginDissipating()
    {
        _state = EmberState.Dissipating;
        _collisionEnabled = false;
        _flameCounter = _record.CollisionEffectCounter;
        _playSound(_record.CollisionEffectSound);
        QueueRedraw();
    }

    private void UpdateDissipating()
    {
        AdvanceAnimation();
        if ((_frames[_frameIndex].Parameter & 0x80) != 0)
        {
            Finish();
            return;
        }
        QueueRedraw();
    }

    private void UpdateScent(int globalFrameCounter)
    {
        // scentSeedSmell decrements its $96 counter only on even global
        // updates. The zero update deletes before publishing
        // wScentSeedActive, so enemies stop following it immediately.
        if ((globalFrameCounter & 1) == 0)
        {
            _flameCounter--;
            if (_flameCounter == 0)
            {
                Finish();
                return;
            }
        }

        if (_flameCounter < 0x1e)
            Visible = !Visible;

        AdvanceAnimation();
        HazardType hazard = _room.GetTerrainInfo(Position).Hazard;
        if (hazard != HazardType.None)
        {
            if (hazard is HazardType.Water or HazardType.Lava)
                _enteredHazard(Position, hazard);
            else if (hazard == HazardType.Hole)
                _objectFellInHole(ObjectFellInHoleKind.ScentSeed);
            Finish();
            return;
        }
        _scentPublished = true;
        QueueRedraw();
    }

    private void BeginEnemyBurn(ISeedBurnTarget target, bool playSound)
    {
        _burnTarget = target;
        _state = EmberState.Burning;
        // PART_BURNING_ENEMY $12 initializes counter1 to 59. Its final
        // update restores the post-hit health and releases the target.
        _flameCounter = 59;
        _zFixed = 0;
        FollowBurnTarget();
        if (playSound)
            _playSound(_record.FlameSound);
        QueueRedraw();
    }

    private void UpdateBurning(ICollection<RoomEntitySpawn> spawns)
    {
        if (_burnTarget is not null)
        {
            if (!_burnTarget.IsSeedBurning)
            {
                Finish();
                return;
            }
            FollowBurnTarget();
        }
        _flameCounter--;
        if (_flameCounter == 0)
        {
            if (_burnTarget is null)
                TryBreakTile(spawns);
            else
                _burnTarget.CompleteSeedBurn(spawns);
            Finish();
            return;
        }
        AdvanceAnimation();
        if (_zFixed != 0)
        {
            OracleObjectMath.UpdateSpeedZ(
                ref _zFixed, ref _speedZ, _record.Gravity);
            QueueRedraw();
            return;
        }
        if ((_frames[_frameIndex].Parameter & 0x40) != 0 &&
            _room.GetTerrainInfo(Position).Hazard is
                HazardType.Water or HazardType.Lava)
        {
            Finish();
            return;
        }
        QueueRedraw();
    }

    private void FollowBurnTarget()
    {
        if (_burnTarget is null)
            return;
        _precisePosition = _burnTarget.SeedBurnPosition;
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
    }

    private void TryBreakTile(ICollection<RoomEntitySpawn> spawns)
    {
        byte tile = _room.GetMetatile(Position);
        if (!_breakables.TryGet(
                _room.ActiveCollisions, tile,
                out BreakableTileRecord breakable) ||
            !breakable.AllowsSource(BreakableTileDatabase.SourceEmberSeed))
        {
            return;
        }
        int packedPosition = _room.GetPackedPosition(Position);
        Vector2 tileCenter = new(
            (packedPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
            (packedPosition >> 4) * OracleRoomData.MetatileSize + 8);
        bool changed = breakable.Replacement == 0 || _room.ReplaceMetatile(
            tileCenter, tile, (byte)breakable.Replacement, _animationTick());
        if (!changed)
            return;

        breakable.ApplyPersistentEffects(
            _saveData, _group, _room.Id, _linkedRoomNeighbor);
        if ((breakable.Effect & 0x40) != 0)
            _playSound(OracleSoundEngine.SndSolvePuzzle);
        if (breakable.Drop != 0 &&
            _decideBreakableDrop(breakable.Drop) is int subId)
        {
            spawns.Add(new ItemDropSpawn(
                subId, tileCenter, DirectionAngle(_direction)));
        }
        _roomTileChanged();
    }

    private void UpdateShooter(ICollection<RoomEntitySpawn> spawns)
    {
        if (!WithinRoomBoundary(_precisePosition))
        {
            Finish();
            return;
        }

        if (_skipShooterTerrainCollision)
        {
            _skipShooterTerrainCollision = false;
            MoveShooterSeed();
            ClearSeparatedBounceTarget();
            QueueRedraw();
            return;
        }

        // seedItemUpdateBouncing inspects the seed's current tile and
        // direction-edge probes before objectApplySpeed. The seed entered this
        // tile on the previous update, so itemUpdateDamageToApply gets the
        // first chance to hit an orb, switch, or seed reflector above.
        byte tile = _room.GetMetatile(_precisePosition);
        if (Array.IndexOf(_shooter.NonBounceDungeonTiles, tile) >= 0)
        {
            ActivateShooterSeed();
            return;
        }

        bool diagonal = (_angle & 1) != 0;
        bool hitX = false;
        bool hitY = false;
        if (diagonal)
        {
            int probeX = _angle is 1 or 3 ? 3 : -4;
            int probeY = _angle is 3 or 5 ? 3 : -4;
            hitY = ShooterTileBlocks(
                _precisePosition + new Vector2(0, probeY));
            hitX = ShooterTileBlocks(
                _precisePosition + new Vector2(probeX, 0));
        }
        else
        {
            // The cardinal branch calls objectCheckTileCollision_allowHoles,
            // which tests the object's current Y/X rather than an edge probe.
            bool hit = ShooterTileBlocks(_precisePosition);
            hitX = hit;
            hitY = hit;
        }
        if (!hitX && !hitY)
        {
            MoveShooterSeed();
            ClearSeparatedBounceTarget();
            QueueRedraw();
            return;
        }

        _bouncesRemaining--;
        if (_bouncesRemaining == 0)
        {
            ActivateShooterSeed();
            return;
        }
        if (hitX && hitY)
            _angle = (_angle + 4) & 7;
        else if (hitX)
            _angle = (8 - _angle) & 7;
        else
            _angle = (4 - _angle) & 7;
        ClearSeparatedBounceTarget();
        QueueRedraw();
    }

    private void MoveShooterSeed() => Position =
        OracleObjectMovement.Shared.ApplySpeed(
            ref _precisePosition, _shooter.SpeedRaw, _angle * 4);

    private void BounceFrom(ISeedBounceTarget target)
    {
        if (ReferenceEquals(_lastBounceTarget, target))
            return;
        _lastBounceTarget = target;

        int reflectedAngle =
            ((target.SeedBounceOrientation & 0x03) * 2 - _angle) & 0x07;
        if (reflectedAngle == _angle)
        {
            // func_50f4 clears knockbackCounter when the reflector returns
            // the seed's existing direction, allowing normal movement.
            _lastBounceTarget = null;
            _skipShooterTerrainCollision = true;
            return;
        }

        _bouncesRemaining--;
        if (_bouncesRemaining == 0)
        {
            ActivateShooterSeed();
            return;
        }

        _angle = reflectedAngle;
        _skipShooterTerrainCollision = true;
    }

    private void ClearSeparatedBounceTarget()
    {
        if (_lastBounceTarget is not null &&
            !_lastBounceTarget.IntersectsSeed(CollisionBounds))
        {
            _lastBounceTarget = null;
        }
    }

    private void ActivateShooterSeed()
    {
        _collisionEnabled = false;
        // @seedCollidedWithWall performs exactly one itemAnimate call. The
        // moving shooter path itself never animates the seed.
        AdvanceAnimation();
        switch (_record.SeedItem)
        {
            case 0x20:
                BeginBurning();
                break;
            case 0x21:
                BeginDissipating();
                break;
            case 0x24:
                BeginMystery();
                break;
            default:
                Finish();
                break;
        }
    }

    private bool ShooterTileBlocks(Vector2 point) =>
        _room.IsSolid(point) && !_shooter.CanPassSolidTile(_room, point);

    private void AdvanceAnimation()
    {
        _frameCounter--;
        if (_frameCounter > 0)
            return;
        _frameIndex++;
        if (_frameIndex >= _frames.Length)
            _frameIndex = Math.Clamp(_loopStart, 0, _frames.Length - 1);
        _frameCounter = _frames[_frameIndex].Duration;
    }

    private static Texture2D[] BuildTextures(
        Image source,
        int tileBase,
        int palette,
        AnimationFrameDefinition[] frames)
    {
        var result = new Texture2D[frames.Length];
        for (int index = 0; index < frames.Length; index++)
        {
            result[index] = NpcCharacter.BuildOamTexture(
                source, frames[index].EncodedOam, tileBase, palette);
        }
        return result;
    }

    private ObjectFellInHoleKind SeedHoleKind() => _record.SeedItem switch
    {
        0x20 => ObjectFellInHoleKind.EmberSeed,
        0x21 => ObjectFellInHoleKind.ScentSeed,
        0x24 => ObjectFellInHoleKind.MysterySeed,
        _ => throw new InvalidOperationException(
            $"Unsupported hole reaction for ITEM ${_record.SeedItem:x2}.")
    };

    private bool WithinRoomBoundary(Vector2 point) =>
        point.X >= 0 && point.X < _room.Width &&
        point.Y >= 0 && point.Y < _room.Height;

    private static int DirectionAngle(Vector2I direction) => direction == Vector2I.Up
        ? 0x00 : direction == Vector2I.Right ? 0x08
        : direction == Vector2I.Down ? 0x10
        : direction == Vector2I.Left ? 0x18
        : throw new ArgumentOutOfRangeException(nameof(direction));

    private void Finish()
    {
        _state = EmberState.Finished;
        _scentPublished = false;
        _collisionEnabled = false;
        Visible = false;
    }
}

internal enum EmberState
{
    Initializing,
    Flying,
    Burning,
    Mystery,
    Scent,
    Dissipating,
    Finished
}
