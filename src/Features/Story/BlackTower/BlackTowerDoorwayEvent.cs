using Godot;

namespace oracleofages;

/// <summary>
/// Ports INTERAC_MISCELLANEOUS_2 $dc:$10: room $1:$76's invisible Black
/// Tower entrance rectangle and its room-bit-selected hardcoded destination.
/// </summary>
internal sealed class BlackTowerDoorwayEvent : IRoomEntryEvent
{

    private readonly RoomEventContext _context;
    private readonly BlackTowerDoorwayEventDatabase _database = new();
    private readonly BlackTowerDoorwayEventDatabaseRecord _record;
    private BlackTowerDoorwayEventEventStage _stage;

    public BlackTowerDoorwayEvent(RoomEventContext context)
    {
        _context = context;
        _record = _database.Data;
    }

    public bool HasState => _stage != BlackTowerDoorwayEventEventStage.Inactive;
    public bool BlocksGameplay => false;
    internal BlackTowerDoorwayEventEventStage Stage => _stage;
    internal BlackTowerDoorwayEventDatabase Database => _database;

    public bool Matches(int group, OracleRoomData room) =>
        group == _record.Group && room.Id == _record.Room;

    public void Start(OracleRoomData _)
    {
        Cancel();
        // RoomEntitiesLoaded fires before a destination warp places Link.
        // Defer state 0 so its initial overlap test observes the final spawn,
        // as the original interaction update does.
        _stage = BlackTowerDoorwayEventEventStage.Initialize;
    }

    public void UpdateFrame()
    {
        switch (_stage)
        {
            case BlackTowerDoorwayEventEventStage.Initialize:
                Initialize();
                break;
            case BlackTowerDoorwayEventEventStage.WaitForExit:
                if (!TouchesLink())
                    _stage = BlackTowerDoorwayEventEventStage.Armed;
                break;
            case BlackTowerDoorwayEventEventStage.Armed:
                if (TouchesLink() && LinkIsVulnerable())
                    EnterTower();
                break;
        }
    }

    public void Cancel() => _stage = BlackTowerDoorwayEventEventStage.Inactive;

    private void Initialize()
    {
        OracleRoomData room = _context.Rooms.CurrentRoom;
        room.SetPositionTileAndCollision(
            PointForPackedPosition(_record.ClearPositionA), 0x00, null,
            _context.AnimationTick(), preserveRenderedTile: true);
        room.SetPositionTileAndCollision(
            PointForPackedPosition(_record.ClearPositionB), 0x00, null,
            _context.AnimationTick(), preserveRenderedTile: true);

        // State 0 always increments once and increments a second time when
        // Link is not touching. A spawn already inside therefore waits for an
        // exit; any other entry starts armed.
        _stage = TouchesLink() ? BlackTowerDoorwayEventEventStage.WaitForExit : BlackTowerDoorwayEventEventStage.Armed;
    }

    private bool TouchesLink()
    {
        Vector2 delta = _context.Player.Position - new Vector2(_record.X, _record.Y);
        return Mathf.Abs(delta.Y) < _record.ObjectRadiusY + _record.LinkRadiusY &&
            Mathf.Abs(delta.X) < _record.ObjectRadiusX + _record.LinkRadiusX;
    }

    private bool LinkIsVulnerable() =>
        _context.Player.InvincibilityFrames <= 0.0f &&
        _context.Player.KnockbackFrames <= 0.0f &&
        !_context.Player.IsDrowning &&
        !_context.Player.IsFallingInHole &&
        !_context.DialogueOpen &&
        !_context.Player.CutsceneControlled;

    private void EnterTower()
    {
        bool flagSet = _context.Rooms.SaveData.HasRoomFlag(
            _record.Group, _record.Room, (byte)_record.RoomFlagMask);
        int destinationGroup = flagSet
            ? _record.SetDestinationGroup
            : _record.ClearDestinationGroup;
        int destinationRoom = flagSet
            ? _record.SetDestinationRoom
            : _record.ClearDestinationRoom;

        // m_HardcodedWarpA stores the destination transition in
        // wWarpTransition. $93 is destination transition 3 with an upward
        // middle-bottom entrance; wWarpTransition2=$01 loads it immediately.
        int destinationTransition = _record.WarpTransition & 0x0f;
        int destinationParameter = (_record.WarpTransition >> 4) & 0x07;
        Warp warp = new Warp(
            _record.Group,
            _record.Room,
            -1,
            0,
            0,
            destinationGroup,
            destinationRoom,
            _record.DestinationPosition,
            destinationParameter,
            destinationTransition);

        _stage = BlackTowerDoorwayEventEventStage.Inactive;
        _context.Sound.PlaySound(_record.Sound);
        _context.Transitions.ApplyWarp(_context.Player, warp);
    }

    private static Vector2 PointForPackedPosition(int position) => new(
        (position & 0x0f) * OracleRoomData.MetatileSize + 8,
        (position >> 4) * OracleRoomData.MetatileSize + 8);
}
