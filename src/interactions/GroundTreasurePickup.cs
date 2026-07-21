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
    internal enum PickupState
    {
        Initializing,
        Spawning,
        Waiting,
        Collected
    }

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

    internal GroundTreasureDatabase.Record Record { get; private set; }
    internal PickupState State => _state;
    internal bool Held { get; private set; }
    internal bool Finished { get; private set; }
    internal ulong PixelHash { get; private set; }
    internal int ZFixed => _zFixed;
    internal int SpawnCounter => _spawnCounter;

    internal void Initialize(
        GroundTreasureDatabase.Record record,
        Action<int> soundRequested)
    {
        Record = record;
        if (record.SpawnMode is not (0 or 2) || record.GrabMode is not (1 or 2))
        {
            throw new InvalidOperationException(
                $"Ground treasure from {record.Source} uses unsupported " +
                $"spawn/grab mode ${record.SpawnMode:x2}/${record.GrabMode:x2}.");
        }
        if (record.SpawnMode == 2 &&
            (record.SpawnDelayFrames <= 0 || record.InitialZPixels >= 0 ||
             record.BounceCount <= 0 || record.Gravity <= 0 ||
             record.BounceSpeed >= 0))
        {
            throw new InvalidOperationException(
                $"Falling ground treasure from {record.Source} has invalid motion metadata.");
        }
        Position = new Vector2(record.X, record.Y);
        _soundRequested = soundRequested;
        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{record.Sprite}.png");
        OracleGraphicsCache.AnimationDefinition definition =
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
        Visible = false;
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
                else
                    UpdateFallingSpawn();
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

    internal bool TryCollect(Player player)
    {
        bool collectible = _state == PickupState.Waiting ||
            (_state == PickupState.Spawning && _spawnSubstate == 2 &&
             Math.Abs(_zFixed >> 8) < 7);
        if (!collectible || Finished || player.CutsceneControlled ||
            player.IsHoldingItemOneHand || player.IsHoldingItemTwoHands)
        {
            return false;
        }
        Vector2 delta = player.Position - Position;
        if (Mathf.Abs(delta.X) >= CombinedCollisionRadius ||
            Mathf.Abs(delta.Y) >= CombinedCollisionRadius)
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
                _zFixed = Record.InitialZPixels << 8;
                _speedZ = 0;
                _bouncesRemaining = Record.BounceCount;
                UpdateAirborneVisibility();
                QueueRedraw();
                return;
        }

        bool landed = OracleObjectMath.UpdateSpeedZ(
            ref _zFixed, ref _speedZ, Record.Gravity);
        UpdateAirborneVisibility();
        QueueRedraw();
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

    private void UpdateAirborneVisibility()
    {
        Visible = OracleObjectMath.IsInsideOriginalScreenBoundary(
            Position + new Vector2(0, _zFixed >> 8));
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
