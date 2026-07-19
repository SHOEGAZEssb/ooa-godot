using Godot;
using System;

namespace oracleofages;

/// <summary>
/// INTERAC_TREASURE $60 using spawn mode $00 and grab mode $02. State 0
/// initializes graphics, state 1 exposes collision, and collection holds the
/// object fourteen pixels above Link until its textbox closes.
/// </summary>
public partial class GroundTreasurePickup : Node2D
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
    private Vector2 _transitionDrawOffset;
    private Action<int> _soundRequested = static _ => { };
    private PickupState _state;

    internal GroundTreasureDatabase.Record Record { get; private set; }
    internal PickupState State => _state;
    internal bool Held { get; private set; }
    internal bool Finished { get; private set; }
    internal ulong PixelHash { get; private set; }

    internal void Initialize(
        GroundTreasureDatabase.Record record,
        Action<int> soundRequested)
    {
        Record = record;
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
                Visible = true;
                QueueRedraw();
                return;
            case PickupState.Spawning:
                _state = PickupState.Waiting;
                return;
            case PickupState.Collected when !Held:
                Held = true;
                Position = player.Position + new Vector2(0, -14);
                player.BeginGetItemTwoHandPose();
                _soundRequested(OracleSoundEngine.SndGetItem);
                QueueRedraw();
                return;
        }
    }

    internal bool TryCollect(Player player)
    {
        if (_state != PickupState.Waiting || Finished ||
            player.CutsceneControlled || player.IsHoldingItemTwoHands)
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

    internal void Finish(Player player)
    {
        if (Finished)
            return;
        player.EndGetItemTwoHandPose();
        Held = false;
        Finished = true;
        Visible = false;
        QueueRedraw();
    }

    internal void SetTransitionDrawOffset(Vector2 offset)
    {
        if (_transitionDrawOffset.IsEqualApprox(offset))
            return;
        _transitionDrawOffset = offset;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!Finished)
            DrawTexture(_texture, _textureOffset + _transitionDrawOffset);
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
