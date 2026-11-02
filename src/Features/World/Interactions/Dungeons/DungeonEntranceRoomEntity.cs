
namespace Oracles;

/// <summary>Invisible INTERAC_DUNGEON_STUFF $12:$00.</summary>
internal sealed class DungeonEntranceRoomEntity : RoomEntityAdapter<Node2D>,
    IFixedRoomEntity, IRoomEntityLifetime,
    IScreenTransitionPreloadRoomEntity, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly EntryRecord _record;
    private readonly int _entryMinimumY, _entryRadius;
    private readonly OracleRuntimeState _runtimeState;
    private readonly Action<int, string> _triggered;
    private readonly Action<int> _initializeStaticObjects;
    private readonly bool _whiteoutEntry;
    private bool _initialized;

    internal DungeonEntranceRoomEntity(
        Vector2 position,
        EntryRecord record,
        int entryMinimumY, int entryRadius,
        OracleRuntimeState runtimeState,
        bool whiteoutEntry,
        Action<int> initializeStaticObjects,
        Action<int, string> triggered)
        : base(new Node2D { Name = "DungeonEntrance", Visible = false }, static _ => { })
    {
        Entity.Position = position;
        _record = record;
        _entryMinimumY = entryMinimumY; _entryRadius = entryRadius;
        _runtimeState = runtimeState;
        _whiteoutEntry = whiteoutEntry;
        _initializeStaticObjects = initializeStaticObjects;
        _triggered = triggered;
    }

    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;

    public ScreenTransitionPresentation PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns)
    {
        // INTERAC$12:$00 deletes in state0 when SCROLLMODE_02 is clear.
        // Ordinary scrolling must release this slot before the eye scanner.
        if (!_whiteoutEntry) Finished = true;
        return ScreenTransitionPresentation.Hidden;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished)
            return;
        if (!_initialized)
        {
            _initialized = true;
            if (!_whiteoutEntry || frame.Player.Position.Y < _entryMinimumY)
            {
                Finished = true;
                return;
            }

            // initializeDungeonStuff clears these three session-persistent
            // dungeon fields before the Ages table supplies wSpinnerState.
            _runtimeState.SetField("wToggleBlocksState", 0);
            _runtimeState.SetField("wSwitchState", 0);
            _runtimeState.SetField(
                "wSpinnerState", (byte)_record.SpinnerState);
            _initializeStaticObjects(_record.Dungeon);
        }

        // objectCheckCollidedWithLink_notDead includes the byte-height gate
        // and high-byte XY arithmetic (negative radius edge is inclusive).
        if (frame.Player.IsDying ||
            !RoomEntityManager.ObjectCollisionZOverlaps(frame.Player.ObjectZHigh, 0, 7) ||
            !RoomEntityManager.ObjectCollisionXYOverlaps(
                new Rect2(Entity.Position - Vector2.One * _entryRadius, Vector2.One * (_entryRadius * 2)),
                new Rect2(frame.Player.Position - Vector2.One * NpcCharacter.LinkCollisionRadius,
                    Vector2.One * (NpcCharacter.LinkCollisionRadius * 2))))
            return;
        Finished = true;
        _triggered(_record.TextId, _record.Message);
    }

}
