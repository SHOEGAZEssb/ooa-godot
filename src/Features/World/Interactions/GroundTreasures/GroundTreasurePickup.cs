
namespace Oracles;

/// <summary>
/// INTERAC_TREASURE $60 using the imported ground-item spawn and grab modes.
/// State 0 initializes graphics, state 1 performs the selected spawn, and
/// collection holds the object above Link until its textbox closes.
/// </summary>
public partial class GroundTreasurePickup : TransitionOffsetNode2D, ITerrainShadowSource
{
    // treasure.s spawn states use visiblec2; the get-item presentation uses 80.
    int? ITerrainShadowSource.TerrainShadowZHigh =>
        _state == PickupState.Collected ? null : _zFixed >> 8;

    // treasure.s:@state0 sets both interaction collision radii to $06.
    private const int CollisionRadius = 6;
    private Texture2D _texture = null!;
    private Vector2 _textureOffset;
    private Action<int> _soundRequested = static _ => { };
    private Func<Vector2, bool>? _tryCreatePuff;
    private PickupState _state;
    private int _spawnSubstate;
    private int _spawnCounter;
    private int _swordGrabState, _swordGrabCounter;
    private int _zFixed;
    private int _speedZ;
    private int _bouncesRemaining;
    private OracleRoomData? _room;
    private Vector2 _precisePosition;
    private int _buriedAngle;
    private bool _buriedTileDug;
    private Func<bool>? _completionAllowed;
    private OracleRuntimeState? _movementMemory;

    internal void BindMovementMemory(OracleRuntimeState memory) => _movementMemory = memory;
    internal void SetNativeBirthZHigh(byte value) => _zFixed=unchecked((sbyte)value)<<8;
    private Func<Vector2, Vector2> _worldToScreen = static position => position;

    internal GroundTreasureDatabaseRecord Record { get; private set; }
    internal PickupState State => _state;
    internal bool Held { get; private set; }
    internal bool ScriptGrantPending { get; private set; }
    internal bool Finished { get; private set; }
    internal ulong PixelHash { get; private set; }
    internal int ZFixed => _zFixed;
    internal int SpeedZ => _speedZ;
    internal int SpawnCounter => _spawnCounter;
    internal int SpawnSubstate => _spawnSubstate;
    internal int BuriedAngle => _buriedAngle;
    internal bool UpdatesDuringDialogue =>
        _state is PickupState.Initializing or PickupState.Collected ||
        Record.SpawnMode == TreasureSpawnMode.FromScreenTop && _state == PickupState.Waiting;

    internal void Initialize(
        string assetRoot,
        GroundTreasureDatabaseRecord record,
        Action<int> soundRequested,
        Func<Vector2, Vector2>? worldToScreen = null,
        OracleRoomData? room = null,
        Func<Vector2, bool>? tryCreatePuff = null)
    {
        AssetRoot = assetRoot;
        Record = record;
        if (record.SpawnMode is not (TreasureSpawnMode.Instant or TreasureSpawnMode.Puff or TreasureSpawnMode.FromScreenTop or TreasureSpawnMode.Buried) ||
            record.GrabMode is not (TreasureGrabMode.NoChange or TreasureGrabMode.OneHand or TreasureGrabMode.TwoHands or TreasureGrabMode.SpinSlash))
        {
            throw new InvalidOperationException(
                $"Ground treasure from {record.Source} uses unsupported " +
                $"spawn/grab mode ${record.SpawnMode:x2}/${record.GrabMode:x2}.");
        }
        if (record.SpawnMode == TreasureSpawnMode.Puff && (tryCreatePuff is null || record.SpawnDelayFrames <= 0))
            throw new InvalidOperationException($"{record.Source}: puff treasure requires its native allocation owner and imported wait.");
        if (record.SpawnMode == TreasureSpawnMode.FromScreenTop &&
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
        if (record.SpawnMode == TreasureSpawnMode.Buried && room is null)
        {
            throw new InvalidOperationException(
                $"Buried ground treasure from {record.Source} requires its room.");
        }
        Position = new Vector2(record.X, record.Y);
        _precisePosition = Position;
        _soundRequested = soundRequested;
        _tryCreatePuff = tryCreatePuff;
        _worldToScreen = worldToScreen ?? (static position => position);
        _room = room;
        Image source = OracleGraphicsCache.LoadImage(
            AssetRoot + $"gfx/{record.Sprite}.png");
        AnimationDefinition definition =
            OracleGraphicsCache.GetAnimationDefinition(record.Animation);
        if (definition.Frames.Length != 1)
        {
            throw new InvalidOperationException(
                $"Ground treasure from {record.Source} must have one static frame.");
        }
        (_texture, _textureOffset) = OracleOamCompositor.BuildPositionedOamTexture(
            source,
            definition.Frames[0].EncodedOam,
            record.TileBase,
            record.Palette,
            paletteOverride: null,
            sourceGrayscaleInverted: record.SourceGrayscaleInverted);
        PixelHash = HashImage(_texture.GetImage());
        _state = PickupState.Initializing;
        _spawnSubstate = 0;
        _spawnCounter = 0;
        _zFixed = 0;
        _speedZ = 0;
        _bouncesRemaining = 0;
        _buriedAngle = 0;
        _buriedTileDug = false;
        _completionAllowed = null;
        // parseObjectData initializes a static treasure's graphics before the
        // destination room begins scrolling. Keep state 0 pending, but expose
        // spawn-mode $00 at the incoming room's transition draw offset.
        Visible = record.SpawnMode == TreasureSpawnMode.Instant;
        QueueRedraw();
    }

    internal void UpdateFrame(Player player)
    {
        if (Finished)
            return;
        if (_state == PickupState.Collected && Record.GrabMode == TreasureGrabMode.SpinSlash)
        {
            UpdateSwordGrab(player);
            return;
        }
        switch (_state)
        {
            case PickupState.Collected when Held && _completionAllowed?.Invoke() == true:
                Finish(player);
                return;
            // treasure.s:@state3 dispatches grab mode $00 directly to
            // interactionDelete on the update after the inventory grant.
            case PickupState.Collected when Record.GrabMode == TreasureGrabMode.NoChange:
                Finish(player);
                return;
            case PickupState.Initializing:
                InitializeGraphicsState();
                return;
            case PickupState.Spawning:
                if (Record.SpawnMode == TreasureSpawnMode.Instant)
                    _state = PickupState.Waiting;
                else if (Record.SpawnMode == TreasureSpawnMode.Puff)
                    UpdatePuffSpawn();
                else if (Record.SpawnMode == TreasureSpawnMode.FromScreenTop)
                    UpdateFallingSpawn();
                else
                    UpdateBuriedSpawn(player);
                return;
            case PickupState.Collected when !Held:
                Held = true;
                _zFixed = 0;
                Position = player.Position + new Vector2(
                    Record.GrabMode == TreasureGrabMode.OneHand ? -4 : 0, -14);
                // treasure.s queues state04 with bit7 set: text closure
                // releases Link regardless of the surrounding object mask.
                player.RequestGetItemState(
                    Record.GrabMode == TreasureGrabMode.OneHand ? (byte)0x80 : (byte)0x81);
                var memory = player.Inventory.RuntimeState;
                memory.SetField("wDisabledObjects", (byte)(memory.ReadField("wDisabledObjects") | 1));
                _soundRequested(SoundId.SndGetItem);
                Visible = true;
                QueueRedraw();
                // grabMode1/2 falls through to its text check even on the
                // initialization visit, including text closed by an earlier slot.
                if (_completionAllowed?.Invoke() == true) Finish(player);
                return;
        }
    }

    private void UpdatePuffSpawn()
    {
        // treasure.s:@spawnMode1 advances before checked puff allocation.
        // Success falls through into the first decrement on this same update.
        if (_spawnSubstate == 0)
        {
            _spawnSubstate = 1;
            _spawnCounter = Record.SpawnDelayFrames;
            if (!_tryCreatePuff!(Position.Floor())) return;
        }
        if (--_spawnCounter != 0) return;
        _state = PickupState.Waiting;
        _spawnSubstate = 0;
        Visible = true;
        QueueRedraw();
    }

    internal void InitializeGraphicsState()
    {
        if (_state != PickupState.Initializing) return;
        _state = PickupState.Spawning;
        Visible = Record.SpawnMode == TreasureSpawnMode.Instant;
        QueueRedraw();
    }

    internal bool TryCollect(Player player) =>
        TryCollectCore(player, checkDefaultDistance: true);

    /// <summary>
    /// Used by source interactions which perform their own collection check
    /// before entering the shared INTERAC_TREASURE held-item path.
    /// </summary>
    internal bool TryCollectAfterSourceCheck(Player player) =>
        TryCollectCore(player, checkDefaultDistance: false);

    internal void BeginScriptGrant() => ScriptGrantPending = true;

    internal void SetSpritePaletteOverrides(IReadOnlyDictionary<int, Color[]> palettes)
    {
        Image source = OracleGraphicsCache.LoadImage(AssetRoot + $"gfx/{Record.Sprite}.png");
        AnimationDefinition animation = OracleGraphicsCache.GetAnimationDefinition(Record.Animation);
        (_texture, _textureOffset) = OracleOamCompositor.BuildPositionedOamTextureWithPaletteOverrides(
            source, animation.Frames[0].EncodedOam, Record.TileBase, Record.Palette,
            palettes, Record.SourceGrayscaleInverted);
        PixelHash = HashImage(_texture.GetImage());
        QueueRedraw();
    }

    internal bool TryCollectScriptGrant()
    {
        if (!ScriptGrantPending || _state != PickupState.Waiting) return false;
        ScriptGrantPending = false;
        _state = PickupState.Collected;
        _zFixed = 0;
        return true;
    }

    private bool TryCollectCore(Player player, bool checkDefaultDistance)
    {
        bool collectible = _state == PickupState.Waiting ||
            (Record.SpawnMode == TreasureSpawnMode.FromScreenTop &&
             _state == PickupState.Spawning && _spawnSubstate == 2);
        if (!collectible || Finished || player.CutsceneControlled ||
            !checkDefaultDistance && (player.IsHoldingItemOneHand || player.IsHoldingItemTwoHands || player.IsCarryingObject))
        {
            return false;
        }
        if (checkDefaultDistance &&
            (!RoomEntityManager.ObjectCollisionZOverlaps(player.ItemCreationZFixed >> 8, _zFixed >> 8, 7) ||
            !Player.EnemyCollisionOverlaps(player.Position,
                new Rect2(Position - Vector2.One * CollisionRadius,
                    Vector2.One * (CollisionRadius * 2)))))
        {
            return false;
        }
        if (checkDefaultDistance)
        {
            // treasure.s:@checkLinkTouched runs inside this interaction's slot,
            // including ordinary Feather air; it does not use the global contact pass.
            var memory = player.Inventory.RuntimeState;
            if (memory.ReadField("wLinkForceState") != 0 || memory.ReadField("wLinkPlayingInstrument") != 0 ||
                (memory.ReadField("wLinkObjectIndex") & 1) != 0 || (memory.ReadField("wLinkGrabState") & 0xbe) != 0 ||
                memory.ReadField("wLinkDeathTrigger") != 0 ||
                !(player.NativeState08Active || player.NativeNormalStateForInteraction))
                return false;
        }
        _state = PickupState.Collected;
        _zFixed = 0;
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
        if (Record.SpawnMode == TreasureSpawnMode.Buried &&
            packedPosition == ((Record.Y >> 4) << 4 | (Record.X >> 4)))
        {
            _buriedTileDug = true;
        }
    }

    internal void BindCompletion(Func<bool> allowed) => _completionAllowed = allowed;

    internal void Finish(Player player)
    {
        if (Finished)
            return;
        // Ordinary state04 restores its own animation on Link's dispatch.
        // The sword-spin path instead changes a pose while state08 owns Link.
        if (Record.GrabMode == TreasureGrabMode.SpinSlash)
            player.EndGetItemOneHandPose();
        if (Record.GrabMode is TreasureGrabMode.OneHand or TreasureGrabMode.TwoHands)
        {
            var memory = player.Inventory.RuntimeState;
            memory.SetField("wDisabledObjects", (byte)(memory.ReadField("wDisabledObjects") & ~1));
            memory.SetField("wInstrumentsDisabledCounter", 0x0f);
        }
        Held = false;
        Finished = true;
        // A room/event owner may finish its retained grant after the entity
        // manager has retired and freed the presentation node.
        if (!GodotObject.IsInstanceValid(this)) return;
        Visible = false;
        QueueRedraw();
    }

    // treasure.s:@grabMode3: the silent second sword object owns a four-update
    // delay, the forced parent-item spin, then the raised sword until the
    // calling interaction clears wDisabledObjects.
    private void UpdateSwordGrab(Player player)
    {
        var memory = _movementMemory ?? throw new InvalidOperationException(
            $"treasure.s:@grabMode3 from {Record.Source} requires the shared runtime state.");
        switch (_swordGrabState)
        {
            case 0:
                _swordGrabState = 1; _swordGrabCounter = 4; Visible = false;
                memory.SetField("wDisabledObjects", 0x81);
                player.RequestNativeState08Control(this, 0xff);
                return;
            case 1:
                if (--_swordGrabCounter != 0) return;
                _swordGrabState = 2; memory.SetField("wcc63", 0xff); return;
            case 2:
                if (memory.ReadField("wcc63") != 0) return;
                _swordGrabState = 3; Held = true;
                Position = player.Position + new Vector2(-4, -14); Visible = true;
                memory.SetField("wcc50", 0x0e);
                _soundRequested(SoundId.SndSwordObtained); return;
            case 3:
                if (memory.ReadField("wDisabledObjects") == 0) Finish(player);
                return;
        }
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

        // objectCheckWithinScreenBoundary checks the ground xh/yh against
        // hCameraX/Y before objectUpdateSpeedZ_paramC. Z affects drawing, not
        // this visibility bit; the sprite may still be clipped above the field.
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
            Position = NativeObjectMovement.ApplySpeed(
                _movementMemory, ref _precisePosition, Record.BuriedMoveSpeed, _buriedAngle);
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

        _soundRequested(SoundId.SndDropEssence);
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
        Vector2 screenPosition = _worldToScreen(Position);
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
