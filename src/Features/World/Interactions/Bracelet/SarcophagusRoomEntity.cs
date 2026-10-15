using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// Globally dispatched INTERAC_SARCOPHAGUS $82. The room-layout buffer
// remains authoritative for the two tiles restored on its first held update.
internal sealed partial class SarcophagusRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, INativeBraceletRoomEntity,
    IBraceletChildRoomEntity, IReservedBraceletCollisionRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IScreenTransitionPreloadRoomEntity
{
    private readonly DungeonObjectRecord _record;
    private readonly OracleRoomData _room;
    private readonly OracleSaveData _save;
    private readonly Action<Vector2, byte> _setTile;
    private readonly Action<int> _sound;
    private readonly EnemyAnimationPlayer _idle;
    private readonly EnemyAnimationPlayer _break;
    private readonly NativeBraceletThrow _throw;
    private readonly BraceletGrabGeometry _grab = new();
    private readonly BraceletDatabaseRecord _bracelet = new BraceletDatabase().Data;
    private Action<INativeBraceletRoomEntity>? _publish;
    private Player? _holder;
    internal int State { get; private set; }
    internal int Substate { get; private set; }
    internal int Counter { get; private set; }
    internal int ZHigh { get; private set; }
    internal NativeBraceletThrow ReservedThrow => _throw;
    internal Rect2 CollisionBounds => new(Position.Floor() - new Vector2(8, 16), new(16, 32));
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;
    public bool ReservedBraceletChildActive => _throw.Active;

    internal SarcophagusRoomEntity(DungeonObjectRecord record, DungeonInteractionVisual idle,
        DungeonInteractionVisual breaking, OracleRoomData room, OracleSaveData save,
        OracleRuntimeState runtime, Action<Vector2, byte> setTile, Action<int> sound)
    {
        _record = record; _room = room; _save = save; _setTile = setTile; _sound = sound;
        Position = record.Position; Visible = false; Name = $"Sarcophagus_{record.SubId:x2}";
        _idle = Load(idle); _break = Load(breaking);
        _throw = new(room, 0, () => !Finished && State == 2 && Substate < 2,
            () => !Finished && State == 2 && Substate < 3, () => Position, () => ZHigh << 8,
            CopyPosition, _ => Substate = 2, () => Substate = 3,
            (position, speed, angle) => {
                var velocity = NativeObjectMovement.Velocity(runtime, speed, angle);
                return position.Add(velocity.YFixed, velocity.XFixed);
            }, BraceletWeightDatabase.Shared, new BombDatabase().Data);
        TreeExiting += Drop;
    }
    private EnemyAnimationPlayer Load(DungeonInteractionVisual visual)
    {
        var animation = new EnemyAnimationPlayer(this, visual.Animations.Length);
        animation.Load(EnemyVisualSource.LoadComposite(visual.Sprites), visual.Animations,
            visual.TileBase, visual.Palette, sourceGrayscaleInverted: visual.SourceGrayscaleInverted);
        animation.SetAnimation(0); return animation;
    }
    private void Initialize()
    {
        if ((_record.SubId & 0x80) != 0) { Break(); return; }
        if (_record.SubId != 0 && _save.HasRoomFlag(_record.Group, _record.Room, 0x40))
        { Finished = true; return; }
        State = 1; Visible = true; ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex;
        byte tile = (byte)_room.GetPackedPosition(Position);
        _room.SetPackedTileCollision(tile, 0x0f);
        _room.SetStorageLayoutWithoutGraphics(tile, 0);
        tile = (byte)(tile - 0x10);
        _room.SetStorageLayoutWithoutGraphics(tile, 0);
        _room.SetPackedTileCollision(tile, 0x0f);
    }
    public void BindGrabbablePublisher(Action<INativeBraceletRoomEntity> publish) => _publish = publish;
    public bool TryUseBracelet(Player player, Vector2I releaseDirection)
    {
        if (_holder == player)
        {
            _throw.Begin(CarriedObjectMotion.DirectionIndex(player.FacingVector),
                releaseDirection == Vector2I.Zero ? 0xff : CarriedObjectMotion.DirectionIndex(releaseDirection) * 8,
                RingEffects.UsesStrongThrow(player.Inventory));
            player.EndCarriedObjectPose(); _holder = null; return true;
        }
        if (Finished || State != 1 || player.Inventory.BraceletLevel < 2 ||
            player.IsCarryingObject || !player.IsOnGroundForBracelet) return false;
        if (!_grab.Overlaps(new(player.EnemyContactPosition - new Vector2(6,6), new(12,12)),
            player.EnemyContactZ, CarriedObjectMotion.DirectionIndex(player.FacingVector),
            CollisionBounds, ZHigh, false)) return false;
        State = 2; Substate = 0; _holder = player; player.BeginCarriedObjectPose();
        return true;
    }
    private void Drop()
    {
        if (_holder is null) return;
        if (State == 2 && Substate < 2) Substate = 3;
        _holder.EndCarriedObjectPose(); _holder = null;
    }
    public void UpdateBraceletChild(Player player)
    {
        if (_holder is not null && !_holder.IsCarryingObject) Drop();
        _throw.Update(_sound);
    }
    public void UpdateHeldPosition(Player player)
    {
        if (_holder != player || State != 2 || Substate >= 2) return;
        if (player.BraceletEntityOffset is Vector2I offset)
            CopyPosition(new((byte)((int)player.Position.X + offset.X), (byte)(int)player.Position.Y),
                unchecked((sbyte)(player.EnemyContactZ + offset.Y)));
        else _throw.Hold(player.Position, player.EnemyContactZ, player.CarriedObjectAnimationFrame == 0 ? 2 : 3,
            CarriedObjectMotion.DirectionIndex(player.FacingVector));
    }
    private void CopyPosition(Vector2 position, int zHigh)
    {
        Position = position.Floor() + Position - Position.Floor(); ZHigh = unchecked((sbyte)zHigh);
        QueueRedraw();
    }
    public bool TryGetReservedBraceletCollision(out ReservedBraceletCollision collision)
    {
        collision = default;
        if (!_throw.Active) return false;
        collision = new(new(_throw.Position.Floor() - new Vector2(8, 16), new(16, 32)),
            _throw.ZFixed >> 8, _bracelet.CollisionZRadius, _bracelet.Damage);
        return true;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (State == 0) { Initialize(); return; }
        if (State == 1)
        {
            if (frame.Player.Inventory.BraceletLevel >= 2) _publish?.Invoke(this);
            return;
        }
        if (State == 2)
        {
            switch (Substate)
            {
                case 0:
                    Substate = 1;
                    if (_record.SubId != 0)
                    {
                        if (_record.SubId == 1) _sound(SoundId.SndSolvePuzzle);
                        _save.SetRoomFlag(_record.Group, _record.Room, 0x40);
                    }
                    _setTile(Position, _room.GetUnderlyingMetatile(Position));
                    _setTile(Position - new Vector2(0, 16), _room.GetUnderlyingMetatile(Position - new Vector2(0, 16)));
                    frame.Player.BraceletObjectWeight = 0;
                    ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex;
                    return;
                case 1: return;
                case 2: if (ZHigh < 0) return; break;
                case 3: break;
                default: throw new InvalidOperationException($"INTERAC$82: unsupported held substate${Substate:x2}.");
            }
            Break(); return;
        }
        if (State != 3) throw new InvalidOperationException($"INTERAC$82: unsupported state${State:x2}.");
        Counter = (byte)(Counter - 1);
        if (Counter == 0) _sound(SoundId.SndKillEnemy);
        if (_break.CurrentParameter == 0xff) { Finished = true; Visible = false; return; }
        _break.Advance(); QueueRedraw();
    }
    private void Break()
    {
        State = 3; Counter = 2; Visible = true; ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex;
        _break.SetAnimation(0); QueueRedraw();
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { if (State == 0) Initialize(); return Finished ? ScreenTransitionPresentation.Hidden : ScreenTransitionPresentation.Visible; }
    public override void _Draw()
    {
        var animation = State == 3 ? _break : _idle;
        if (Visible && !Finished && animation.HasFrames)
            DrawTexture(animation.CurrentTexture, animation.CurrentOffset + new Vector2(0, ZHigh) + TransitionDrawOffset);
    }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
}
