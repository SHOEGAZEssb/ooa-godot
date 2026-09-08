using Godot;
using System.Linq;

namespace oracleofages;

/// <summary>Native INTERAC_TUNI_NUT $b1 slot, updated before the two sisters.</summary>
internal sealed partial class TuniNutRoomEntity : TransitionOffsetNode2D, IRoomEntity, IRoomBlocker
{
    private readonly TuniNutDatabase _data;
    private readonly SymmetryDatabase _npcs;
    private readonly OracleRoomData _room;
    private readonly EnemyAnimationPlayer _nut;
    private readonly EnemyAnimationPlayer _sparkle;
    private readonly Sprite2D _sparkleSprite;
    private Vector2 _precisePosition;
    private int _counter;
    private int _substate;
    private int _z;
    private int _speedZ;
    private int _palette;
    private int _paletteDirection;
    private int _movementDirection;
    private bool _sparkleActive;
    private bool _sparkleVisible;
    private bool _sparkleJustCreated;
    internal int State { get; private set; }
    internal int Substate => _substate;
    internal int Counter => _counter;
    internal int Height => _z;
    public Node2D Node => this;

    public TuniNutRoomEntity(TuniNutDatabase data, SymmetryDatabase npcs, InventoryState? inventory, OracleSaveData? save, OracleRoomData room)
    {
        _data = data;
        _npcs = npcs;
        _room = room;
        Name = "TuniNut";
        Position = new(data.Constant("x"), data.Constant("y"));
        _precisePosition = Position;
        _nut = Load(data.Visual("nut"));
        _sparkle = Load(data.Visual("sparkle"));
        _sparkleSprite = new Sprite2D
        {
            Name = "Sparkle", Centered = false, Visible = false,
            ZAsRelative = false, ZIndex = NpcCharacter.BehindLinkZIndex
        };
        AddChild(_sparkleSprite);
        ZIndex = NpcCharacter.BehindLinkZIndex;
        Visible = false;
        if (save?.HasGlobalFlag(npcs.Constant("placed-flag")) == true) FinishPosition();
        else State = inventory?.HasTreasure(TreasureDatabase.TreasureTuniNut) == true && inventory.TuniNutState == 2 ? 1 : -1;
    }
    private EnemyAnimationPlayer Load(TimedSparkleVisual visual)
    {
        var animation = new EnemyAnimationPlayer(this, 1);
        int count = OracleGraphicsCache.GetAnimationDefinition(visual.Animation).Frames.Length;
        animation.Load(OracleGraphicsCache.LoadImage($"res://assets/oracle/gfx/{visual.Sprite}.png"),
            [visual.Animation], visual.TileBase, visual.Palette, positionedOam: true,
            animationSourceOffsets: [Enumerable.Repeat(visual.SourceOffset, count).ToArray()]);
        animation.SetAnimation(0);
        return animation;
    }
    internal void AdvanceFrame(SymmetryEvent owner)
    {
        RoomEventContext context = owner.Context;
        // Palette thread runs independently of the interaction's own state.
        if (_paletteDirection != 0)
        {
            int target = _paletteDirection < 0 ? _data.Constant("darken") : 0;
            if (_palette == target) _paletteDirection = 0;
            else _palette += _paletteDirection;
            context.Rooms.CurrentRoom.SetTemporaryBackgroundPaletteOffset(_palette);
        }
        switch (State)
        {
            case 1:
                if (context.DialogueOpen || owner.BlocksGameplay || !context.Player.AcceptsGroundInteractionContact ||
                    context.Player.IsDying || context.Player.IsCarryingObject || context.Player.BraceletLiftCollisionsDisabled ||
                    !Player.EnemyCollisionOverlaps(context.Player.Position,
                        new Rect2(Position - new Vector2(16, 8), new Vector2(32, 16)))) break;
                owner.SetInputEnabled(false);
                context.Entities.ClearPhysicalPlayerItems();
                context.Player.PutOnGroundForScript();
                int distance = Mathf.FloorToInt(context.Player.Position.X) - _data.Constant("x");
                _counter = System.Math.Abs(distance);
                _movementDirection = distance > 0 ? -1 : 1;
                State = 2;
                if (distance == 0) BeginPlacement(context);
                break;
            case 2:
                context.Player.AdvanceCutsceneMovement(new Vector2(_movementDirection, 0), new Vector2I(_movementDirection, 0));
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
                        _precisePosition += OracleObjectMovement.Shared.Delta(_data.Constant("speed"), 0);
                        Position = OracleObjectMath.ToPixelPosition(_precisePosition);
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
                        if (--_counter == 0) { _paletteDirection = 1; _substate++; }
                        break;
                    case 5:
                        if (_paletteDirection != 0) break;
                        context.Rooms.SaveData.SetGlobalFlag(_npcs.Constant("placed-flag"));
                        context.Inventory.LoseTreasure(TreasureDatabase.TreasureTuniNut);
                        foreach (int room in new[] { 0x02, 0x03, 0x04, 0x12, 0x13, 0x14 })
                            context.Rooms.SaveData.SetRoomFlag(0, room, 1);
                        owner.SetInputEnabled(true);
                        context.Entities.RuntimeState.SetWramByte(0xcfc0,
                            (byte)(context.Entities.RuntimeState.ReadWramByte(0xcfc0) | 1));
                        context.Sound.PlayRoomMusic(context.Rooms.ActiveGroup, context.Rooms.CurrentRoom.Id);
                        FinishPosition();
                        break;
                }
                break;
        }
        // The spawned $84:$07 occupies a later slot and follows y/x/z each pass.
        if (_sparkleActive)
        {
            if (_sparkleJustCreated) { _sparkleJustCreated = false; _sparkleVisible = true; }
            else if ((context.Entities.RuntimeState.ReadWramByte(0xcfc0) & 1) != 0) _sparkleActive = false;
            else { _sparkle.Advance(); _sparkleVisible = (context.Entities.FrameCounter & 1) == 0; }
        }
        _sparkleSprite.Visible = _sparkleActive && _sparkleVisible;
        _sparkleSprite.Texture = _sparkle.CurrentTexture;
        _sparkleSprite.Position = _sparkle.CurrentOffset + new Vector2(0, _z >> 8) + TransitionDrawOffset;
        QueueRedraw();
    }
    private void BeginPlacement(RoomEventContext context)
    {
        context.Player.AdvanceCutsceneMovement(Vector2.Zero, Vector2I.Up);
        _counter = _data.Constant("rise-wait");
        _sparkleActive = true;
        _sparkleJustCreated = true;
        _paletteDirection = -1;
        context.Sound.PlaySound(_data.Constant("stop-music"));
        Visible = true;
        ZIndex = NpcCharacter.InFrontOfLinkZIndex; // objectSetVisiblec0
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
        ZIndex = NpcCharacter.BehindLinkZIndex; // objectSetVisible82
        State = 4;
        Visible = true;
    }
    internal void Cancel()
    {
        _room.SetTemporaryBackgroundPaletteOffset(0);
        _paletteDirection = 0;
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
