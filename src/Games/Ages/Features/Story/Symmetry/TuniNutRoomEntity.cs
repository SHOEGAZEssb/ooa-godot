
namespace Oracles;

/// <summary>Native INTERAC_TUNI_NUT $b1 slot, updated before the two sisters.</summary>
internal sealed partial class TuniNutRoomEntity : TransitionOffsetNode2D, INativeInteractionRoomEntity, IRoomBlocker,
    IFixedRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity
{
    private readonly TuniNutDatabase _data;
    private readonly SymmetryDatabase _npcs;
    private readonly OracleRoomData _room;
    private readonly EnemyAnimationPlayer _nut;
    private readonly InventoryState? _inventory;
    private readonly OracleSaveData? _save;
    private SymmetryEvent? _owner;
    private TuniNutSparkleRoomEntity? _sparkle;
    private Vector2 _precisePosition;
    private int _counter;
    private int _substate;
    private int _z;
    private int _speedZ;
    private bool _ownsPalette;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;
    internal int State { get; private set; }
    internal int Substate => _substate;
    internal int Counter => _counter;
    internal int Height => _z;
    public Node2D Node => this;

    public TuniNutRoomEntity(TuniNutDatabase data, SymmetryDatabase npcs, InventoryState? inventory, OracleSaveData? save, OracleRoomData room)
    {
        AssetRoot = room.AssetRoot;
        _data = data;
        _npcs = npcs;
        _room = room;
        _inventory = inventory; _save = save;
        Name = "TuniNut";
        Position = new(data.Constant("x"), data.Constant("y"));
        _precisePosition = Position;
        _nut = Load(data.Visual("nut"));
        ZIndex = ObjectDrawPriority.BehindLinkZIndex;
        Visible = false;
    }
    private EnemyAnimationPlayer Load(TimedSparkleVisual visual)
    {
        var animation = new EnemyAnimationPlayer(this, 1);
        int count = OracleGraphicsCache.GetAnimationDefinition(visual.Animation).Frames.Length;
        animation.Load(OracleGraphicsCache.LoadImage(AgesGameContent.AssetRoot + $"gfx/{visual.Sprite}.png"),
            [visual.Animation], visual.TileBase, visual.Palette, positionedOam: true,
            animationSourceOffsets: [Enumerable.Repeat(visual.SourceOffset, count).ToArray()]);
        animation.SetAnimation(0);
        return animation;
    }
    internal void Bind(SymmetryEvent owner) => _owner = owner;
    internal void AdvancePalette()
    { if (_ownsPalette) _owner!.Context.Transitions.AdvanceNativeRoomColorFade(_room); }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (State == 0) { Initialize(); return; }
        AdvanceFrame(_owner ?? throw new System.InvalidOperationException("tuniNutMain.s:$b1 requires its Symmetry story binding."));
    }
    private void Initialize()
    {
        if (_save?.HasGlobalFlag(_npcs.Constant("placed-flag")) == true) FinishPosition();
        else if (_inventory?.HasTreasure(TreasureId.TuniNut) == true && _inventory.TuniNutState == 2) State = 1;
        else Finished = true;
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (State == 0 && !Finished) Initialize();
        return Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }
    private void AdvanceFrame(SymmetryEvent owner)
    {
        RoomEventContext context = owner.Context;
        switch (State)
        {
            case 1:
                if (context.DialogueOpen || owner.BlocksGameplay || !context.Player.AcceptsGroundInteractionContact ||
                    context.Player.IsDying || context.Player.IsCarryingObject || context.Player.BraceletLiftCollisionsDisabled ||
                    !Player.EnemyCollisionOverlaps(context.Player.Position,
                        new Rect2(Position - new Vector2(16, 8), new Vector2(32, 16)))) break;
                owner.BeginNutPlacement();
                context.Entities.ClearPhysicalPlayerItems();
                context.Player.PutOnGroundForScript();
                int distance = Mathf.FloorToInt(context.Player.Position.X) - _data.Constant("x");
                _counter = System.Math.Abs(distance);
                State = 2;
                if (distance == 0) BeginPlacement(context);
                else context.Player.RequestNativeForcedMovement(_counter,
                    distance > 0 ? 0x18 : 0x08, distance > 0 ? 3 : 1, owner);
                break;
            case 2:
                if (--_counter == 0) BeginPlacement(context);
                break;
            case 3:
                switch (_substate)
                {
                    case 0:
                        if (--_counter == 0) { _counter = _data.Constant("rise-distance"); _substate++; }
                        break;
                    case 1:
                        if ((context.Entities.FrameCounter & 1) != 0) break;
                        _z -= 0x100;
                        if (--_counter == 0) { CenterOnTile(); _substate++; }
                        break;
                    case 2:
                        Position = NativeObjectMovement.ApplySpeed(context.Entities.RuntimeState,
                            ref _precisePosition, _data.Constant("speed"), ObjectAngle.Up);
                        if (Mathf.FloorToInt(Position.Y) < _data.Constant("placed-y")) { CenterOnTile(); _substate++; }
                        break;
                    case 3:
                        if (OracleObjectMath.UpdateSpeedZ(ref _z, ref _speedZ, _data.Constant("gravity")))
                        {
                            context.Sound.PlaySound(_data.Constant("drop-sound"));
                            _counter = _data.Constant("land-wait");
                            context.Sound.PlaySound(_data.Constant("solve-sound"));
                            _substate++;
                        }
                        break;
                    case 4:
                        if (--_counter == 0) { context.Transitions.BeginNativeRoomBrightening(); _substate++; }
                        break;
                    case 5:
                        if (context.Transitions.NativePaletteFadeActive) break;
                        context.Rooms.SaveData.SetGlobalFlag(_npcs.Constant("placed-flag"));
                        context.Inventory.LoseTreasure(TreasureId.TuniNut);
                        foreach (int room in new[] { 0x02, 0x03, 0x04, 0x12, 0x13, 0x14 })
                            context.Rooms.SaveData.SetRoomFlag(0, room, 1);
                        owner.SetInputEnabled(true);
                        context.Entities.RuntimeState.SetWramByte(AgesWramAddress.wTmpcfc0,
                            (byte)(context.Entities.RuntimeState.ReadWramByte(AgesWramAddress.wTmpcfc0) | 1));
                        context.Sound.PlayRoomMusic(context.Rooms.ActiveGroup, context.Rooms.CurrentRoom.Id);
                        FinishPosition();
                        break;
                }
                break;
        }
        QueueRedraw();
    }
    private void BeginPlacement(RoomEventContext context)
    {
        context.Player.Face(Vector2I.Up);
        _counter = _data.Constant("rise-wait");
        _sparkle = context.Entities.TryAllocateInteraction(() => new TuniNutSparkleRoomEntity(context, this, _data.Visual("sparkle")));
        if (_sparkle is null)
        {
            // Unchecked creation retains HL=$e040; relatedObj1 aliases sound RAM.
            context.Entities.WriteSoundAllocationAlias(0xc056, 0x46);
            context.Entities.WriteSoundAllocationAlias(0xc057, (byte)(0xd0 + context.Entities.NativeInteractionSlot(this)));
        }
        context.Transitions.BeginNativeLightRoomDarkening(); _ownsPalette = true;
        context.Sound.PlaySound(_data.Constant("stop-music"));
        Visible = true;
        ZIndex = ObjectDrawPriority.FixedHighPriorityZIndex; // objectSetVisiblec0
        State = 3;
    }
    private void CenterOnTile()
    {
        Position = new((Mathf.FloorToInt(Position.X) & 0xf0) | 8,
            (Mathf.FloorToInt(Position.Y) & 0xf0) | 8);
        _precisePosition = Position;
    }
    private void FinishPosition()
    {
        Position = new(_data.Constant("x"), _data.Constant("placed-y"));
        _precisePosition = Position;
        ZIndex = ObjectDrawPriority.BehindLinkZIndex; // objectSetVisible82
        State = 4;
        Visible = true;
    }
    internal void Cancel(OracleRoomData currentRoom)
    {
        // loadTilesetGraphics installs the destination palette before room
        // events retire the outgoing $b1. Its cleanup must not write the old
        // tileset into those shared BG slots (5:F6 -> 1:13).
        if (ReferenceEquals(_room, currentRoom))
            _room.SetTemporaryBackgroundPaletteOffset(0);
        if (_ownsPalette) _owner?.Context.Transitions.CancelNativeRoomColorFade(_room);
        _ownsPalette = false;
        if (_sparkle is not null) _owner?.Context.Entities.RetireEntity(_sparkle);
        _sparkle = null; Finished = true; _owner = null;
    }
    public bool BlocksLink(Vector2 center) => State == 4 && Player.EnemyCollisionOverlaps(center,
        new Rect2(Position - new Vector2(6, 6), new Vector2(12, 12)));
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
    public override void _Draw()
    {
        if (!Visible) return;
        Vector2 offset = new(0, _z >> 8);
        DrawTexture(_nut.CurrentTexture, _nut.CurrentOffset + offset + TransitionDrawOffset);
    }
}
