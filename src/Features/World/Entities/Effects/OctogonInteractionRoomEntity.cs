using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// INTERAC$8e splash; $91:$01 attached producer; $91:$00 top-down bubbles.
internal sealed partial class OctogonInteractionRoomEntity : TransitionOffsetNode2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IScreenTransitionPreloadRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly OracleRandom _random;
    private readonly OracleRuntimeState _memory;
    private readonly Func<int,OctogonCharacter?> _parent;
    private readonly Func<Vector2,int,int,int,bool> _spawn;
    private readonly EnemyAnimationPlayer _animation;
    private readonly int _id;
    private readonly int _parameter;
    private readonly int _parentSlot;
    private int _zHigh;
    internal int State { get; private set; }
    internal int Counter1 { get; private set; }
    internal int Counter2 { get; private set; }
    internal int TurnStep { get; private set; }
    internal int Turns { get; private set; }
    internal int Angle { get; private set; }
    public bool Finished { get; private set; }
    public Node2D Node => this;
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;
    internal OctogonInteractionRoomEntity(Vector2 point,int id,int parameter,int parentSlot,OracleRandom random,
        OracleRuntimeState memory,Func<int,OctogonCharacter?> parent,Func<Vector2,int,int,int,bool> spawn,int zHigh = 0)
    {
        _id = id; _parameter = parameter; _parentSlot = parentSlot; _random = random; _memory = memory; _parent = parent; _spawn = spawn;
        Position = point.Floor(); _zHigh = zHigh; Visible = false;
        var visual = OctogonEffectsDatabase.Shared.Visual(id);
        _animation = new(this,visual.Animations.Length);
        _animation.Load(EnemyVisualSource.LoadComposite(visual.Sprites),visual.Animations,visual.TileBase,visual.Palette,
            sourceGrayscaleInverted:visual.SourceGrayscaleInverted);
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { UpdateNative(); return Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden; }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns) => UpdateNative();
    private void UpdateNative()
    {
        if (Finished) return;
        if (_id == 0x8e)
        {
            if (State == 0) { State = 1; _animation.SetAnimation(_parameter>>2); Visible = true; ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex; }
            else if (_animation.CurrentParameter == 0xff) Delete(); else _animation.Advance();
            QueueRedraw(); return;
        }
        if (_parameter == 1)
        {
            if (State == 0) { State = 1; Counter1 = 30; return; }
            var actor = _parent(_parentSlot);
            if (actor is null || !actor.CollisionEnabled) { Delete(); return; }
            CopyHigh(actor.Position); _zHigh = actor.ZFixed>>8;
            if (Counter1 != 0) Counter1--;
            if (Counter1 == 0) { Counter1 = 90; _spawn(Position.Floor(),0x91,0,_zHigh); }
            return;
        }
        Counter2 = (Counter2-1)&255;
        if (Counter2 == 0) { Delete(); return; }
        if (Counter2 < 60) Visible = !Visible;
        if (State == 0)
        {
            State = 1; Counter1 = 4; Counter2 = 180; Turns = 5;
            TurnStep = (_random.Next().Value&1) == 0 ? -1 : 1;
            int direction;
            do { direction = _random.Next().Value&7; } while (direction >= 5);
            Angle = (direction-2)&31; Visible = true; ZIndex = ObjectDrawPriority.InFrontOfLinkZIndex; QueueRedraw(); return;
        }
        var velocity = NativeObjectMovement.Velocity(_memory,0x14,Angle);
        Position = OracleObjectPosition.FromPixels(Position).Add(velocity.YFixed,velocity.XFixed).PrecisePosition;
        if ((byte)OracleObjectPosition.HighByte(Position.Y) >= 0xf0) { Delete(); return; }
        Counter1 = (Counter1-1)&255;
        if (Counter1 == 0)
        {
            Counter1 = 4; Turns = (Turns-1)&255;
            if (Turns == 0) { Turns = 8; TurnStep = -TurnStep; }
            Angle = (Angle+TurnStep)&31;
        }
        QueueRedraw();
    }
    private void CopyHigh(Vector2 point) => Position = new((byte)OracleObjectPosition.HighByte(point.X)+Position.X-Position.Floor().X,
        (byte)OracleObjectPosition.HighByte(point.Y)+Position.Y-Position.Floor().Y);
    private void Delete() { Finished = true; Visible = false; }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
    public override void _Draw()
    { if (Visible && !Finished) DrawTexture(_animation.CurrentTexture,_animation.CurrentOffset+Vector2.Down*_zHigh+TransitionDrawOffset); }
}
