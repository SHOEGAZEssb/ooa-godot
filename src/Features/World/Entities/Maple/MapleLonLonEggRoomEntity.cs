namespace Oracles;

// Seasons ghastlyDoll.s: INTERAC$94 shares the book's flight, then grants
// the doll directly and waits $40 eligible visits; it has no always-update bit.
internal sealed partial class MapleLonLonEggRoomEntity : TransitionOffsetNode2D,
    INativeInteractionRoomEntity, IFixedRoomEntity, IRoomEntityLifetime
{
    private readonly MapleEncounter _maple;
    private readonly RoomEntityManager _owner;
    private readonly MapleVisualRecord _visual;
    private Texture2D _texture = null!;
    private Vector2 _precisePosition;
    private int _zFixed, _speedZ, _angle, _counter;
    public Node2D PresentationNode => this;
    public bool Finished { get; private set; }
    public bool NativeInitializationPending => State == 0 && !Finished;
    internal int State { get; private set; }

    internal MapleLonLonEggRoomEntity(MapleLonLonEggSpawn spawn, RoomEntityManager owner)
    {
        _maple = spawn.Maple; _visual = spawn.Visual; _owner = owner;
        AssetRoot = owner.AssetRoot;
        Name = "MapleLonLonEgg";
        Visible = false;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        var memory = _owner.RuntimeState;
        var player = frame.Player;
        if (Finished) return;
        switch (State)
        {
            case 0:
                memory.SetField("wMenuDisabled", 1);
                if (player.KnockbackFrames != 0) return;
                _precisePosition = player.Position.Floor();
                _zFixed = (player.ItemCreationZFixed >> 8) << 8;
                _angle = OracleObjectMovement.Shared.RelativeAngle(_precisePosition, new(0x50, 0x38)) & 0x1c;
                _speedZ = -0x100;
                SetVisual(2, _visual.TileBase, _visual.Palette);
                Visible = player.Visible; ZIndex = player.ZIndex;
                State = 1;
                _owner.OnSoundRequested(SoundId.SndGainHeart);
                break;
            case 1:
                if (OracleObjectMath.UpdateSpeedZ(ref _zFixed, ref _speedZ, 0x20)) State = 2;
                else NativeObjectMovement.ApplySpeed(memory, ref _precisePosition, 0x28, _angle);
                break;
            case 2:
                Vector2 target = _maple.BookTargetPosition.Floor();
                if (!_maple.BookTradeAccepted || _precisePosition.Floor() != target)
                {
                    _maple.SetBookMovementAngle(OracleObjectMovement.Shared.RelativeAngle(_precisePosition, target) ^ 0x10);
                    break;
                }
                _maple.SetBookMovementAngle(0xff);
                State = 3; Visible = false;
                _owner.OnSoundRequested(SoundId.SndGetSeed);
                _maple.ShowBookText(0x070a, player);
                break;
            case 3:
                memory.SetField("wLinkForceState", 4);
                memory.SetField("wcc50", 1);
                _precisePosition = player.Position.Floor() + new Vector2(0, -14);
                _zFixed = (player.ItemCreationZFixed >> 8) << 8;
                _counter = 0x40;
                SetVisual(3, _visual.TileBase + 2, 0);
                Visible = player.Visible; ZIndex = player.ZIndex;
                State = 4;
                _maple.ShowBookText(0x005c, player);
                (_owner.Inventory ?? throw new InvalidOperationException("INTERAC$94 requires live inventory.")).GiveTreasure(TreasureId.TradeItem, 2);
                _owner.OnSoundRequested(SoundId.SndGetItem);
                break;
            case 4:
                if (--_counter != 0) break;
                memory.SetField("wTextIsActive", 0);
                memory.SetField("wDisabledObjects", 0);
                _maple.SignalBookDeparture();
                Finished = true; Visible = false;
                break;
            default:
                throw new InvalidOperationException($"ghastlyDoll.s: INTERAC$94 unsupported state ${State:x2}.");
        }
        Position = _precisePosition.Floor();
        QueueRedraw();
    }

    private void SetVisual(int animation, int tile, int palette)
    {
        var frame = OracleGraphicsCache.GetAnimationDefinition(_visual.Animations[animation]).Frames[0];
        _texture = OracleOamCompositor.BuildOamTexture(OracleGraphicsCache.LoadImage(AssetRoot + $"gfx/{_visual.Sprite}.png"),
            frame.EncodedOam, tile, palette);
    }

    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
    public override void _Draw()
    {
        if (Visible && !Finished) DrawTexture(_texture, new Vector2(-16, -16 + (_zFixed >> 8)) + TransitionDrawOffset);
    }
}

internal sealed record MapleLonLonEggSpawn(MapleEncounter Maple, MapleVisualRecord Visual) : RoomEntitySpawn;
