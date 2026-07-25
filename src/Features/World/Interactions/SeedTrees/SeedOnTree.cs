using Godot;
using System;

namespace oracleofages;

/// <summary>
/// PART_SEED_ON_TREE ($10). A sword hit knocks the seed toward Link when the
/// Seed Satchel is owned; landing or Link contact gives six seeds.
/// </summary>
internal partial class SeedOnTree : TransitionOffsetNode2D
{
    private SeedTreeDatabase _database = null!;
    private SeedTreeController _controller = null!;
    private SeedTreeTypeRecord _type;
    private InventoryState? _inventory;
    private Action<int, string, Vector2> _messageRequested = null!;
    private Func<bool> _dialogueOpen = null!;
    private Action<int> _soundRequested = null!;
    private Texture2D _texture = null!;
    private Vector2 _textureOffset;
    private Vector2 _precisePosition;
    private Vector2 _lastLinkPosition;
    private int _zFixed;
    private int _speedZ;
    private int _angle;
    private int _collisionDelay;
    private bool _collisionEnabled;

    internal SeedOnTreeState State { get; private set; }
    internal int Index { get; private set; }
    internal int SeedType => _type.Type;
    internal int ZFixed => _zFixed;
    internal int SpeedZ => _speedZ;
    internal int Angle => _angle;
    internal bool CollisionEnabled => _collisionEnabled;
    internal bool Finished => State == SeedOnTreeState.Finished;
    internal Rect2 CollisionBounds => new(
        Position - new Vector2(
            _database.CollisionRadiusX,
            _database.CollisionRadiusY),
        new Vector2(
            _database.CollisionRadiusX * 2,
            _database.CollisionRadiusY * 2));

    internal void Initialize(
        SeedTreeDatabase database,
        SeedTreeController controller,
        SeedTreeTypeRecord type,
        Vector2 position,
        int index,
        InventoryState? inventory,
        Action<int, string, Vector2> messageRequested,
        Func<bool> dialogueOpen,
        Action<int> soundRequested)
    {
        _database = database;
        _controller = controller;
        _type = type;
        Index = index;
        _inventory = inventory;
        _messageRequested = messageRequested;
        _dialogueOpen = dialogueOpen;
        _soundRequested = soundRequested;
        _precisePosition = position;
        Position = OracleObjectMath.ToPixelPosition(position);
        State = SeedOnTreeState.Perched;
        _collisionEnabled = true;
        Visible = true;

        Image source = OracleGraphicsCache.LoadImage(
            $"res://assets/oracle/gfx/{database.Visual.Sprite}.png");
        AnimationDefinition animation =
            OracleGraphicsCache.GetAnimationDefinition(database.Visual.Animation);
        if (animation.Frames.Length == 0)
            throw new InvalidOperationException(
                "PART_SEED_ON_TREE has no imported animation frame.");
        (_texture, _textureOffset) = NpcCharacter.BuildPositionedOamTexture(
            source,
            animation.Frames[0].EncodedOam,
            type.TileBase,
            type.Palette,
            paletteOverride: null,
            sourceGrayscaleInverted: true);
    }

    internal void UpdateFrame(Player player)
    {
        _lastLinkPosition = player.Position;
        switch (State)
        {
            case SeedOnTreeState.Fallen:
                if (_collisionDelay > 0)
                    _collisionDelay--;
                if (_collisionDelay == 0 && LinkOverlaps(player))
                {
                    Collect();
                    return;
                }

                _precisePosition +=
                    OracleObjectMath.VectorFromAngle32(_angle) *
                    (_database.SpeedRaw / 40.0f);
                Position = OracleObjectMath.ToPixelPosition(_precisePosition);
                if (OracleObjectMath.UpdateSpeedZ(
                    ref _zFixed, ref _speedZ, _database.Gravity))
                {
                    // objectNegateAndHalveSpeedZ stops once downward speed is
                    // below one pixel/update; otherwise it performs one more
                    // half-height bounce.
                    if (_speedZ < 0x100)
                    {
                        Collect();
                        return;
                    }
                    _speedZ = -_speedZ / 2;
                }
                QueueRedraw();
                return;

            case SeedOnTreeState.AwaitingIntroText:
                if (!_dialogueOpen())
                    FinishCollection();
                return;
        }
    }

    internal bool TryCollectWithSword(Rect2 hitbox)
    {
        if (State != SeedOnTreeState.Perched ||
            !_collisionEnabled ||
            !hitbox.Intersects(CollisionBounds))
        {
            return false;
        }

        _collisionEnabled = false;
        if (_inventory?.HasTreasure(
                TreasureDatabase.TreasureSeedSatchel) != true)
        {
            // The original clears this part's collision before showing TX_0035.
            // It stays on the tree and is recreated on the next room parse.
            if (_controller.TryClaimNoSatchelMessage())
            {
                _messageRequested(
                    _database.NoSatchelTextId,
                    _database.Visual.NoSatchelMessage,
                    Position);
            }
            return true;
        }

        _zFixed = 0;
        _speedZ = _database.InitialSpeedZ;
        _angle = OracleObjectMath.AngleToward(
            Position, _lastLinkPosition);
        _collisionDelay = _database.CollisionDelay;
        State = SeedOnTreeState.Fallen;
        return true;
    }

    private bool LinkOverlaps(Player player)
    {
        if (!player.AcceptsRoomEntityContact ||
            player.HealthQuarters <= 0 ||
            player.IsCarryingObject ||
            player.IsHoldingItemOneHand ||
            player.IsHoldingItemTwoHands)
        {
            return false;
        }

        int zPixel = _zFixed >> 8;
        if (Mathf.Abs(zPixel) >= 7)
            return false;
        Vector2 delta =
            OracleObjectMath.ToPixelPosition(player.Position) - Position;
        return Mathf.Abs(delta.X) <
                _database.CollisionRadiusX + _database.LinkRadius &&
            Mathf.Abs(delta.Y) <
                _database.CollisionRadiusY + _database.LinkRadius;
    }

    private void Collect()
    {
        if (_inventory is null)
        {
            FinishCollection();
            return;
        }

        bool firstOfType = !_inventory.HasTreasure(_type.TreasureId);
        if (firstOfType)
        {
            State = SeedOnTreeState.AwaitingIntroText;
            _messageRequested(
                _type.IntroTextId,
                _type.IntroMessage,
                Position);
        }
        _inventory.GiveTreasure(
            _type.TreasureId,
            _database.TreasureParameter);
        _soundRequested(_database.CollectionSound);
        Visible = false;
        QueueRedraw();

        if (!firstOfType)
            FinishCollection();
    }

    private void FinishCollection()
    {
        if (Finished)
            return;
        Visible = false;
        State = SeedOnTreeState.Finished;
        _controller.NotifyChildCollected();
    }

    public override void _Draw()
    {
        if (!Visible)
            return;
        DrawTexture(
            _texture,
            _textureOffset +
                new Vector2(0, _zFixed >> 8) +
                TransitionDrawOffset);
    }
}

internal enum SeedOnTreeState
{
    Perched,
    Fallen,
    AwaitingIntroText,
    Finished
}
