using Godot;
using System;

namespace oracleofages;

/// <summary>
/// INTERAC_TREASURE $60 using the imported ground-item spawn and grab modes.
/// State 0 initializes graphics, state 1 performs the selected spawn, and
/// collection holds the object above Link until its textbox closes.
/// </summary>
public partial class GroundTreasurePickup : TransitionOffsetNode2D
{

    private const float CombinedCollisionRadius = 12.0f;
    private Texture2D _texture = null!;
    private Vector2 _textureOffset;
    private Action<int> _soundRequested = static _ => { };
    private PickupState _state;
    private int _spawnSubstate;
    private int _spawnCounter;
    private int _zFixed;
    private int _speedZ;
    private int _bouncesRemaining;
    private OracleRoomData? _room;
    private Vector2 _precisePosition;
    private int _buriedAngle;
    private bool _buriedTileDug;
    private Func<Vector2, Vector2> _worldToScreen = static position => position;

    internal GroundTreasureDatabaseRecord Record { get; private set; }
    internal PickupState State => _state;
    internal bool Held { get; private set; }
    internal bool Finished { get; private set; }
    internal ulong PixelHash { get; private set; }
    internal int ZFixed => _zFixed;
    internal int SpeedZ => _speedZ;
    internal int SpawnCounter => _spawnCounter;
    internal int SpawnSubstate => _spawnSubstate;
    internal int BuriedAngle => _buriedAngle;
    internal bool UpdatesDuringDialogue =>
        _state == PickupState.Collected ||
        Record.SpawnMode == 2 && _state == PickupState.Waiting;

    internal void Initialize(
        GroundTreasureDatabaseRecord record,
        Action<int> soundRequested,
        Func<Vector2, Vector2>? worldToScreen = null,
        OracleRoomData? room = null)
    {
        Record = record;
        if (record.SpawnMode is not (0 or 2 or 5) ||
            record.GrabMode is not (1 or 2))
        {
            throw new InvalidOperationException(
                $"Ground treasure from {record.Source} uses unsupported " +
                $"spawn/grab mode ${record.SpawnMode:x2}/${record.GrabMode:x2}.");
        }
        if (record.SpawnMode == 2 &&
            (record.SpawnDelayFrames <= 0 ||
             record.InitialZAboveScreen &&
                 (record.AboveScreenMargin < 0 || record.AboveScreenFallback >= 0) ||
             !record.InitialZAboveScreen && record.InitialZPixels >= 0 ||
             record.BounceCount <= 0 || record.Gravity <= 0 ||
             record.BounceSpeed >= 0))
        {
            throw new InvalidOperationException(
                $"Falling ground treasure from {record.Source} has invalid motion metadata.");
        }
        if (record.SpawnMode == 5 && room is null)
        {
            throw new InvalidOperationException(
                $"Buried ground treasure from {record.Source} requires its room.");
        }
        Position = new Vector2(record.X, record.Y);
        _precisePosition = Position;
        _soundRequested = soundRequested;
        _worldToScreen = worldToScreen ?? (static position => position);
        _room = room;
        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{record.Sprite}.png");
        AnimationDefinition definition =
            OracleGraphicsCache.GetAnimationDefinition(record.Animation);
        if (definition.Frames.Length != 1)
        {
            throw new InvalidOperationException(
                $"Ground treasure from {record.Source} must have one static frame.");
        }
        (_texture, _textureOffset) = NpcCharacter.BuildPositionedOamTexture(
            source,
            definition.Frames[0].EncodedOam,
            record.TileBase,
            record.Palette,
            paletteOverride: null,
            sourceGrayscaleInverted: true);
        PixelHash = HashImage(_texture.GetImage());
        _state = PickupState.Initializing;
        _spawnSubstate = 0;
        _spawnCounter = 0;
        _zFixed = 0;
        _speedZ = 0;
        _bouncesRemaining = 0;
        _buriedAngle = 0;
        _buriedTileDug = false;
        // parseObjectData initializes a static treasure's graphics before the
        // destination room begins scrolling. Keep state 0 pending, but expose
        // spawn-mode $00 at the incoming room's transition draw offset.
        Visible = record.SpawnMode == 0;
        QueueRedraw();
    }

    internal void UpdateFrame(Player player)
    {
        if (Finished)
            return;
        switch (_state)
        {
            case PickupState.Initializing:
                _state = PickupState.Spawning;
                Visible = Record.SpawnMode == 0;
                QueueRedraw();
                return;
            case PickupState.Spawning:
                if (Record.SpawnMode == 0)
                    _state = PickupState.Waiting;
                else if (Record.SpawnMode == 2)
                    UpdateFallingSpawn();
                else
                    UpdateBuriedSpawn(player);
                return;
            case PickupState.Collected when !Held:
                Held = true;
                _zFixed = 0;
                Position = player.Position + new Vector2(
                    Record.GrabMode == 1 ? -4 : 0, -14);
                if (Record.GrabMode == 1)
                    player.BeginGetItemOneHandPose();
                else
                    player.BeginGetItemTwoHandPose();
                _soundRequested(OracleSoundEngine.SndGetItem);
                Visible = true;
                QueueRedraw();
                return;
        }
    }

    internal bool TryCollect(Player player) =>
        TryCollectCore(player, checkDefaultDistance: true);

    /// <summary>
    /// Used by source interactions which perform their own collection check
    /// before entering the shared INTERAC_TREASURE held-item path.
    /// </summary>
    internal bool TryCollectAfterSourceCheck(Player player) =>
        TryCollectCore(player, checkDefaultDistance: false);

    private bool TryCollectCore(Player player, bool checkDefaultDistance)
    {
        bool collectible = _state == PickupState.Waiting ||
            (Record.SpawnMode == 2 &&
             _state == PickupState.Spawning && _spawnSubstate == 2 &&
             Math.Abs(_zFixed >> 8) < 7);
        if (!collectible || Finished || player.CutsceneControlled ||
            player.IsHoldingItemOneHand || player.IsHoldingItemTwoHands ||
            player.IsCarryingObject)
        {
            return false;
        }
        Vector2 delta = player.Position - Position;
        if (checkDefaultDistance &&
            (Mathf.Abs(delta.X) >= CombinedCollisionRadius ||
             Mathf.Abs(delta.Y) >= CombinedCollisionRadius))
        {
            return false;
        }
        _state = PickupState.Collected;
        return true;
    }

    /// <summary>
    /// Starts the same grab-mode-$02 state used by a touched ground treasure
    /// for script opcode giveitem, whose treasure is already assigned to Link.
    /// </summary>
    internal void BeginGranted(Player player)
    {
        if (Finished || Held)
            throw new InvalidOperationException(
                "A granted treasure cannot be started twice.");
        _state = PickupState.Collected;
        Visible = true;
        UpdateFrame(player);
    }

    internal void NotifyTileDug(int packedPosition)
    {
        if (Record.SpawnMode == 5 &&
            packedPosition == ((Record.Y >> 4) << 4 | (Record.X >> 4)))
        {
            _buriedTileDug = true;
        }
    }

    internal void Finish(Player player)
    {
        if (Finished)
            return;
        if (Record.GrabMode == 1)
            player.EndGetItemOneHandPose();
        else
            player.EndGetItemTwoHandPose();
        Held = false;
        Finished = true;
        Visible = false;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Finished)
        {
            DrawTexture(
                _texture,
                _textureOffset + TransitionDrawOffset +
                new Vector2(0, _zFixed >> 8));
        }
    }

    private void UpdateFallingSpawn()
    {
        switch (_spawnSubstate)
        {
            case 0:
                _spawnSubstate = 1;
                _spawnCounter = Record.SpawnDelayFrames;
                _soundRequested(Record.SpawnSound);
                return;

            case 1:
                _spawnCounter--;
                if (_spawnCounter > 0)
                    return;
                _spawnSubstate = 2;
                _zFixed = (Record.InitialZAboveScreen
                    ? GetZAboveScreen(Position)
                    : Record.InitialZPixels) << 8;
                _speedZ = 0;
                _bouncesRemaining = Record.BounceCount;
                UpdateAirborneVisibility();
                QueueRedraw();
                return;
        }

        // INTERAC_TREASURE spawn mode $02 checks its current camera-relative
        // position before objectUpdateSpeedZ_paramC. This matters at the top
        // screen boundary: the newly moved position is not made visible until
        // the following update.
        UpdateAirborneVisibility();
        QueueRedraw();
        bool landed = OracleObjectMath.UpdateSpeedZ(
            ref _zFixed, ref _speedZ, Record.Gravity);
        if (!landed)
            return;

        _soundRequested(Record.LandingSound);
        _bouncesRemaining--;
        if (_bouncesRemaining == 0)
        {
            _state = PickupState.Waiting;
            Visible = true;
            return;
        }
        _speedZ = Record.BounceSpeed;
    }

    /// <summary>
    /// INTERAC_TREASURE spawn mode $05. State 1 first remembers its packed
    /// tile, then the matching successful break launches it in Link's facing
    /// direction. objectUpdateSpeedZAndBounce halves each impact speed and
    /// stops once the next bounce would be below one pixel per update.
    /// </summary>
    private void UpdateBuriedSpawn(Player player)
    {
        switch (_spawnSubstate)
        {
            case 0:
                _spawnSubstate = 1;
                return;

            case 1:
                if (!_buriedTileDug)
                    return;
                _spawnSubstate = 2;
                _speedZ = Record.BuriedInitialSpeedZ;
                _buriedAngle = player.FacingVector switch
                {
                    { X: 0, Y: -1 } => 0x00,
                    { X: 1, Y: 0 } => 0x08,
                    { X: 0, Y: 1 } => 0x10,
                    { X: -1, Y: 0 } => 0x18,
                    _ => throw new InvalidOperationException(
                        "Link has no cardinal facing for buried treasure.")
                };
                Visible = true;
                QueueRedraw();
                return;
        }

        OracleRoomData room = _room!;
        Vector2 current = OracleObjectMath.ToPixelPosition(_precisePosition);
        TerrainInfo terrain = room.GetTerrainInfo(current);
        bool holeOrLava = terrain.Hazard is HazardType.Hole or HazardType.Lava;
        bool outside = current.X < 0 || current.X >= room.Width ||
            current.Y < 0 || current.Y >= room.Height;
        if (!outside && (!room.IsSolid(current) || holeOrLava))
        {
            Position = OracleObjectMovement.Shared.ApplySpeed(
                ref _precisePosition, Record.BuriedMoveSpeed, _buriedAngle);
        }

        bool landed = OracleObjectMath.UpdateSpeedZ(
            ref _zFixed, ref _speedZ, Record.Gravity);
        QueueRedraw();
        if (!landed)
            return;

        HazardType hazard = room.GetTerrainInfo(Position).Hazard;
        if (hazard != HazardType.None)
        {
            Finished = true;
            Visible = false;
            QueueRedraw();
            return;
        }

        _soundRequested(OracleSoundEngine.SndDropEssence);
        int nextSpeedZ = -_speedZ / 2;
        if (nextSpeedZ > -0x80)
        {
            _state = PickupState.Waiting;
            return;
        }
        _speedZ = nextSpeedZ;
    }

    private void UpdateAirborneVisibility()
    {
        Vector2 screenPosition = _worldToScreen(Position) +
            new Vector2(0, _zFixed >> 8);
        Visible = OracleObjectMath.IsInsideOriginalScreenBoundary(
            screenPosition);
    }

    private int GetZAboveScreen(Vector2 worldPosition)
    {
        int screenY = Mathf.FloorToInt(_worldToScreen(worldPosition).Y);
        int candidate = -screenY - Record.AboveScreenMargin;
        return candidate >= 0
            ? Record.AboveScreenFallback
            : Math.Max(Record.AboveScreenFallback, candidate);
    }

    private static ulong HashImage(Image image)
    {
        ulong hash = 14695981039346656037UL;
        foreach (byte value in image.GetData())
        {
            hash ^= value;
            hash *= 1099511628211UL;
        }
        return hash;
    }
}

internal enum PickupState
{
    Initializing,
    Spawning,
    Waiting,
    Collected
}
