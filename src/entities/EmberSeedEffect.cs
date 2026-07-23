using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// ITEM_EMBER_SEED ($20), subid $00: the Satchel-thrown child item. State and
/// animation advances happen on original 60 Hz updates, including the state-0
/// setup update and the 58-update flame counter.
/// </summary>
public partial class EmberSeedEffect : TransitionOffsetNode2D
{
    internal enum EmberState { Initializing, Flying, Burning, Finished }

    private SeedSatchelDatabase.SeedRecord _record;
    private OracleRoomData _room = null!;
    private BreakableTileDatabase _breakables = null!;
    private Action<int> _playSound = null!;
    private Action<Vector2, OracleRoomData.HazardType> _enteredHazard = null!;
    private Action _roomTileChanged = null!;
    private Func<long> _animationTick = null!;
    private Func<int, int?> _decideBreakableDrop = null!;
    private Func<Vector2I, int?>? _linkedRoomNeighbor;
    private OracleSaveData? _saveData;
    private int _group;
    private OracleGraphicsCache.AnimationFrameDefinition[] _frames = null!;
    private Texture2D[] _flyingTextures = null!;
    private Texture2D[] _flameTextures = null!;
    private Vector2 _precisePosition;
    private Vector2I _direction;
    private EmberState _state;
    private int _zFixed;
    private int _speedZ;
    private int _flameCounter;
    private int _frameIndex;
    private int _frameCounter;
    private int _loopStart;
    private bool _collisionEnabled;
    private ISeedBurnTarget? _burnTarget;

    public bool Finished => _state == EmberState.Finished;
    internal EmberState State => _state;
    internal int ElapsedFrames { get; private set; }
    internal int ZFixed => _zFixed;
    internal int SpeedZ => _speedZ;
    internal int FlameCounter => _flameCounter;
    internal int AnimationFrame => _frameIndex;
    internal Vector2 PrecisePosition => _precisePosition;
    internal bool CollisionEnabled => _collisionEnabled && !Finished;
    internal ulong FlameTextureHashForValidation(int frame) =>
        OracleGraphicsCache.PixelHash(_flameTextures[frame].GetImage());
    internal Rect2 CollisionBounds => new(
        Position - new Vector2(_record.CollisionRadiusX, _record.CollisionRadiusY),
        new Vector2(_record.CollisionRadiusX * 2, _record.CollisionRadiusY * 2));

    internal void Initialize(
        SeedSatchelDatabase.SeedRecord record,
        OracleRoomData room,
        BreakableTileDatabase breakables,
        Vector2 linkPosition,
        Vector2I direction,
        Action<int> playSound,
        Action<Vector2, OracleRoomData.HazardType> enteredHazard,
        Action roomTileChanged,
        Func<long> animationTick,
        Func<int, int?> decideBreakableDrop,
        OracleSaveData? saveData,
        int group,
        Func<Vector2I, int?>? linkedRoomNeighbor = null)
    {
        _record = record;
        _room = room;
        _breakables = breakables;
        _playSound = playSound;
        _enteredHazard = enteredHazard;
        _roomTileChanged = roomTileChanged;
        _animationTick = animationTick;
        _decideBreakableDrop = decideBreakableDrop;
        _linkedRoomNeighbor = linkedRoomNeighbor;
        _saveData = saveData;
        _group = group;
        _direction = direction;
        _precisePosition = linkPosition + record.Offset(direction);
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        _zFixed = record.InitialZ << 8;
        _speedZ = record.SpeedZ;
        _collisionEnabled = true;

        OracleGraphicsCache.AnimationDefinition animation =
            OracleGraphicsCache.GetAnimationDefinition(record.Animation);
        _frames = animation.Frames;
        _loopStart = animation.LoopStart;
        if (_frames.Length == 0)
            throw new InvalidOperationException(
                $"{record.Source} imported an empty ITEM_EMBER_SEED animation.");
        _frameCounter = _frames[0].Duration;

        Image flyingSource = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{record.Sprite}.png");
        Image flameSource = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{record.FlameSprite}.png");
        _flyingTextures = BuildTextures(
            flyingSource, record.TileBase, record.Palette);
        _flameTextures = BuildTextures(
            flameSource, record.FlameTileBase, record.FlamePalette);
        Visible = false;
        QueueRedraw();
    }

    internal void UpdateFrame(ICollection<RoomEntitySpawn> spawns)
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

        if (!WithinRoomBoundary(_precisePosition))
        {
            Finish();
            return;
        }

        _precisePosition += (Vector2)_direction * (_record.SpeedRaw / 40.0f);
        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
        bool landed = OracleObjectMath.UpdateSpeedZ(
            ref _zFixed, ref _speedZ, _record.Gravity);
        if (!landed)
        {
            QueueRedraw();
            return;
        }

        OracleRoomData.HazardType hazard = _room.GetTerrainInfo(Position).Hazard;
        if (hazard != OracleRoomData.HazardType.None)
        {
            if (hazard is OracleRoomData.HazardType.Water or OracleRoomData.HazardType.Lava)
                _enteredHazard(Position, hazard);
            Finish();
            return;
        }

        _playSound(_record.LandingSound);
        AdvanceAnimation();
        BeginBurning();
    }

    internal void OnCollision(
        SeedHitResult result,
        ISeedBurnTarget? burnTarget = null)
    {
        if (!CollisionEnabled || result == SeedHitResult.None)
            return;
        _collisionEnabled = false;
        if (result == SeedHitResult.Consume)
        {
            Finish();
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
        Texture2D texture = _state == EmberState.Burning
            ? _flameTextures[_frameIndex]
            : _flyingTextures[_frameIndex];
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
                OracleRoomData.HazardType.Water or OracleRoomData.HazardType.Lava)
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
                out BreakableTileDatabase.BreakableTileRecord breakable) ||
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

    private Texture2D[] BuildTextures(Image source, int tileBase, int palette)
    {
        var result = new Texture2D[_frames.Length];
        for (int index = 0; index < _frames.Length; index++)
        {
            result[index] = NpcCharacter.BuildOamTexture(
                source, _frames[index].EncodedOam, tileBase, palette);
        }
        return result;
    }

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
        _collisionEnabled = false;
        Visible = false;
    }
}
