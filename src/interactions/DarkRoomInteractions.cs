using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// The room-local wNumTorchesLit count and palette-thread state shared by one
/// PART_DARK_ROOM_HANDLER and its generated PART_LIGHTABLE_TORCH children.
/// </summary>
internal sealed class DarkRoomState
{
    private readonly OracleRoomData _room;
    private readonly DarkRoomDatabase _data;
    private int _fadeOffset;
    private int _fadeDirection;
    private bool _torchTotalInitialized;

    internal int LitCount { get; private set; }
    internal int TotalTorches { get; private set; }
    internal int Parameter { get; private set; }
    internal int RenderedOffset => _fadeOffset;
    internal bool FadeActive { get; private set; }

    internal DarkRoomState(OracleRoomData room, DarkRoomDatabase data)
    {
        _room = room;
        _data = data;
        Parameter = data.FullDarkParameter;
        _fadeOffset = SignedParameter(Parameter);
        _room.SetTemporaryBackgroundPaletteOffset(_fadeOffset);
    }

    internal void SetTotalTorches(int count)
    {
        if (count < 0 || _torchTotalInitialized)
            throw new InvalidOperationException("The dark-room torch total can only be initialized once.");
        TotalTorches = count;
        _torchTotalInitialized = true;
    }

    internal void IncrementLitCount()
    {
        if (LitCount >= TotalTorches)
            throw new InvalidOperationException("The dark-room lit count exceeded its torch total.");
        LitCount++;
    }

    internal void BeginBrighten(int targetParameter) => BeginFade(targetParameter, 1);
    internal void BeginDarken(int targetParameter) => BeginFade(targetParameter, -1);

    internal void AdvanceFade()
    {
        if (!FadeActive)
            return;
        int target = SignedParameter(Parameter);
        int candidate = _fadeOffset + _fadeDirection * _data.FadeSpeed;
        bool finished = _fadeDirection > 0
            ? candidate >= target
            : candidate < target;
        if (finished)
        {
            FadeActive = false;
            return;
        }
        _fadeOffset = candidate;
        _room.SetTemporaryBackgroundPaletteOffset(_fadeOffset);
    }

    private void BeginFade(int targetParameter, int direction)
    {
        if (targetParameter is < 0 or > 0xff || direction is not (-1 or 1))
            throw new ArgumentOutOfRangeException(nameof(targetParameter));
        // _setDarkeningVariables starts from the previous parameter, not the
        // last rendered offset, then immediately stores the new target.
        _fadeOffset = SignedParameter(Parameter);
        Parameter = targetParameter;
        _fadeDirection = direction;
        FadeActive = true;
    }

    private static int SignedParameter(int parameter) => unchecked((sbyte)(byte)parameter);
}

/// <summary>
/// PART_DARK_ROOM_HANDLER $08. State 0 scans all 176 wRoomLayout bytes in
/// address order; state 1 reacts only when its shared lit count changes.
/// </summary>
internal sealed partial class DarkRoomHandlerRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity
{
    private readonly DarkRoomDatabase.Record _record;
    private readonly OracleRoomData _room;
    private readonly DarkRoomDatabase _data;
    private readonly DarkRoomState _state;
    private bool _initialized;
    private int _lastLitCount;

    public Node2D Node => this;
    internal bool Initialized => _initialized;
    internal int LastLitCount => _lastLitCount;
    internal DarkRoomState State => _state;

    internal DarkRoomHandlerRoomEntity(
        DarkRoomDatabase.Record record,
        OracleRoomData room,
        DarkRoomDatabase data,
        DarkRoomState state)
    {
        if (record is not
            { Kind: DarkRoomDatabase.ObjectKind.Handler, Id: 0x08, SubId: 0x00 })
        {
            throw new ArgumentOutOfRangeException(nameof(record));
        }
        Name = $"DarkRoomHandler_{record.Order}";
        _record = record;
        _room = room;
        _data = data;
        _state = state;
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (_state.FadeActive)
        {
            _state.AdvanceFade();
            return;
        }
        if (!_initialized)
        {
            InitializeTorches(spawns);
            _initialized = true;
        }

        int lit = _state.LitCount;
        int previous = _lastLitCount;
        if (lit == previous)
            return;
        _lastLitCount = lit;
        if (lit == 0)
        {
            _state.BeginDarken(_data.FullDarkParameter);
            return;
        }
        if (lit == _state.TotalTorches)
        {
            _state.BeginBrighten(0);
            return;
        }
        if (_state.Parameter == _data.PartialDarkParameter)
            return;
        if (lit >= previous)
            _state.BeginBrighten(_data.PartialDarkParameter);
        else
            _state.BeginDarken(_data.PartialDarkParameter);
    }

    private void InitializeTorches(ICollection<RoomEntitySpawn> spawns)
    {
        if (_room.Layout.Length != 176)
        {
            throw new InvalidOperationException(
                $"PART_DARK_ROOM_HANDLER $08 in room {_record.Group:x1}:" +
                $"{_record.Room:x2} requires the 176-byte large-room layout.");
        }
        int count = 0;
        for (int index = 0; index < _room.Layout.Length; index++)
        {
            if (_room.Layout[index] != _data.UnlitTile)
                continue;
            int packedPosition = (index / 16 << 4) | index % 16;
            spawns.Add(new LightableTorchSpawn(_state, packedPosition));
            count++;
        }
        _state.SetTotalTorches(count);
    }
}

/// <summary>
/// Permanent PART_LIGHTABLE_TORCH $06:$00. A seed collision selects state 2;
/// the following object update increments the room count, changes tile $08 to
/// $09, plays SND_LIGHTTORCH, and deletes the part.
/// </summary>
internal sealed partial class LightableTorchRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, ISeedHittableRoomEntity, IRoomEntityLifetime
{
    private readonly DarkRoomState _state;
    private readonly OracleRoomData _room;
    private readonly DarkRoomDatabase _data;
    private readonly Action<int> _playSound;
    private readonly Action _roomTileChanged;
    private readonly Func<long> _animationTick;
    private bool _initialized;
    private bool _hit;

    public Node2D Node => this;
    public bool Finished { get; private set; }
    internal int PackedPosition { get; }
    internal bool HitPending => _hit;
    internal Rect2 CollisionBounds => new(
        Position - new Vector2(_data.TorchRadiusX, _data.TorchRadiusY),
        new Vector2(_data.TorchRadiusX * 2, _data.TorchRadiusY * 2));

    internal LightableTorchRoomEntity(
        DarkRoomState state,
        int packedPosition,
        OracleRoomData room,
        DarkRoomDatabase data,
        Action<int> playSound,
        Action roomTileChanged,
        Func<long> animationTick)
    {
        _state = state;
        _room = room;
        _data = data;
        _playSound = playSound;
        _roomTileChanged = roomTileChanged;
        _animationTick = animationTick;
        PackedPosition = packedPosition;
        Position = PositionFromPacked(packedPosition);
        Name = $"LightableTorch_{packedPosition:x2}";
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized)
        {
            _initialized = true;
            return;
        }
        if (!_hit || Finished)
            return;

        _state.IncrementLitCount();
        _playSound(_data.LightSound);
        _room.SetPositionTileAndCollision(
            Position, (byte)_data.LitTile, null, _animationTick());
        _roomTileChanged();
        Finished = true;
    }

    public bool ApplySeedHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized || _hit || Finished || !hitbox.Intersects(CollisionBounds))
            return false;
        _hit = true;
        return true;
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }

    private static Vector2 PositionFromPacked(int packedPosition) => new(
        (packedPosition & 0x0f) * OracleRoomData.MetatileSize + 8,
        (packedPosition >> 4) * OracleRoomData.MetatileSize + 8);
}

/// <summary>
/// INTERAC_MISCELLANEOUS_2 $dc:$00. Its ROOMFLAG_ITEM predicate is checked
/// before the exact two-torch count, and it deletes itself after creating the
/// falling Graveyard Key interaction.
/// </summary>
internal sealed partial class DarkRoomRewardRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly DarkRoomDatabase.Record _record;
    private readonly DarkRoomDatabase _data;
    private readonly DarkRoomState _state;
    private readonly OracleSaveData? _save;
    private readonly TreasureDatabase _treasures;

    public Node2D Node => this;
    public bool Finished { get; private set; }

    internal DarkRoomRewardRoomEntity(
        DarkRoomDatabase.Record record,
        DarkRoomDatabase data,
        DarkRoomState state,
        OracleSaveData? save,
        TreasureDatabase treasures)
    {
        if (record is not
            { Kind: DarkRoomDatabase.ObjectKind.Reward, Id: 0xdc, SubId: 0x00 })
        {
            throw new ArgumentOutOfRangeException(nameof(record));
        }
        _record = record;
        _data = data;
        _state = state;
        _save = save;
        _treasures = treasures;
        Name = $"DarkRoomReward_{record.Order}";
        Position = new Vector2(record.X, record.Y);
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        if (_save?.HasRoomFlag(
            _record.Group, _record.Room, OracleSaveData.RoomFlagItem) == true)
        {
            Finished = true;
            return;
        }
        if (_state.LitCount != _record.RequiredCount)
            return;

        TreasureDatabase.TreasureObjectRecord treasure =
            _treasures.GetObject(_record.TreasureObject);
        TreasureDatabase.TreasureObjectVisualRecord visual =
            _treasures.GetObjectVisual(treasure.Graphic);
        spawns.Add(new GroundTreasureSpawn(new GroundTreasureDatabase.Record(
            _record.Group,
            _record.Room,
            _record.Order,
            _record.Y,
            _record.X,
            treasure.Name,
            visual.Sprite,
            visual.TileBase,
            visual.Palette,
            visual.Animation,
            0,
            string.Empty,
            _record.Source,
            SpawnMode: _data.RewardSpawnMode,
            GrabMode: _data.RewardGrabMode,
            SpawnDelayFrames: _data.SpawnDelay,
            InitialZPixels: _data.AboveScreenFallback,
            BounceCount: _data.BounceCount,
            Gravity: _data.Gravity,
            BounceSpeed: _data.BounceSpeed,
            SpawnSound: _data.SpawnSound,
            LandingSound: _data.LandingSound,
            InitialZAboveScreen: true,
            AboveScreenMargin: _data.AboveScreenMargin,
            AboveScreenFallback: _data.AboveScreenFallback)));
        Finished = true;
    }

    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}
