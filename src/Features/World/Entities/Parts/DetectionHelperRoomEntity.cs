
namespace Oracles;

// PART_DETECTION_HELPER $0e uses the actual shared PART allocation order.
// Invisible rays test Link before moving, then test one collision quadrant.
internal sealed class DetectionHelperRoomEntity : IRoomEntity, IFixedRoomEntity,
    IRoomEntityLifetime, IUpdatesDuringRoomEntityFreeze, IUpdatesDuringDialogueRoomEntity,
    IScreenTransitionPreloadRoomEntity
{
    private readonly RoomEntityManager _owner;
    private readonly IRoomEntity _parent;
    private readonly int _subid, _variation;
    private int _angle, _counter1, _counter2, _cycle, _speedY, _speedX;
    internal int State { get; private set; }
    internal int Health { get; set; } = 1;
    public Node2D Node { get; } = new() { Visible = false };
    public bool Finished { get; private set; }
    public bool UpdatesDuringRoomEntityFreeze => State == 0;
    public bool UpdatesDuringDialogue => State == 0;
    public void SetTransitionDrawOffset(Vector2 offset) { }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        InitializeStateZero();
        return ScreenTransitionPresentation.Hidden;
    }
    internal DetectionHelperRoomEntity(RoomEntityManager owner, IRoomEntity parent,
        int subid = 0, Vector2 position = default, int angle = 0, int variation = 0)
    { _owner = owner; _parent = parent; _subid = subid; _variation = variation; Node.Position = position; _angle = angle; }
    private void InitializeStateZero()
    {
        if (State != 0) return;
        State = 1; Health = 1; // partCommon state0 reloads its source properties.
        if (_subid == 0) { _counter1 = _counter2 = 1; return; }
        if (_subid >= 2) { _counter1 = 4; _angle = (_angle + (_subid == 2 ? -1 : 1) * 2 * (_variation + 1)) & 31; }
        var velocity = NativeObjectMovement.Velocity(_owner.RuntimeState, 0x64, _angle);
        _speedY = unchecked((short)(velocity.YFixed * 4)); _speedX = unchecked((short)(velocity.XFixed * 4));
        _owner.RuntimeState.SetField("wTmpcec0", unchecked((byte)_speedY));
        _owner.RuntimeState.SetField("wTmpcec0", unchecked((byte)(_speedY >> 8)), 1);
        _owner.RuntimeState.SetField("wTmpcec0", unchecked((byte)_speedX), 2);
        _owner.RuntimeState.SetField("wTmpcec0", unchecked((byte)(_speedX >> 8)), 3);
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (State == 0)
        {
            InitializeStateZero();
            return;
        }
        if (Health == 0) { Finished = true; return; }
        var guard = _parent is AmbiGuardRoomEntity original ? _owner.ResolveEnemySlot(original.Slot) as AmbiGuardRoomEntity : null;
        var interaction = _parent as NativeNpcRoomEntity;
        if (_subid == 0)
        {
            if (guard is null && interaction is not { Finished: false }) { Finished = true; return; }
            Node.Position = guard?.Actor.Position ?? interaction!.Npc.Position;
            _angle = guard?.Actor.Angle ?? interaction!.ReadByte(0x49);
            if (--_counter1 == 0) { _counter1 = 15; _owner.TryCreateDetectionRay(_parent, 1, Node.Position, _angle, 0); }
            if (--_counter2 == 0)
            {
                _counter2 = 6; _cycle = (_cycle + 1) & 3;
                _owner.TryCreateDetectionRay(_parent, 2, Node.Position, _angle, _cycle);
                _owner.TryCreateDetectionRay(_parent, 3, Node.Position, _angle, _cycle);
            }
            return;
        }
        if (_subid >= 2 && --_counter1 == 0) { Finished = true; return; }
        Vector2 radius = _subid == 1 ? (((_angle + 4) & 8) == 0 ? new(1, 2) : new(2, 1)) : new(2, 2);
        if (Player.EnemyCollisionOverlaps(frame.Player.Position.Floor(), new Rect2(Node.Position.Floor() - radius, radius * 2)))
        {
            if (guard is not null) guard.Actor.DetectionSignal = 0xff;
            else if (interaction is { Finished: false }) interaction.WriteByte(0x7b, 0xff);
            Finished = true; return;
        }
        Node.Position = OracleObjectPosition.FromPixels(Node.Position).Add(_speedY, _speedX).PrecisePosition;
        Vector2 pixel = Node.Position.Floor();
        byte collision = _owner.ActiveRoom.GetTerrainInfo(pixel).Collision;
        int quadrant = (((int)pixel.Y & 8) == 0 ? 2 : 0) + (((int)pixel.X & 8) == 0 ? 1 : 0);
        if ((collision & (1 << quadrant)) != 0) Finished = true;
    }
}
