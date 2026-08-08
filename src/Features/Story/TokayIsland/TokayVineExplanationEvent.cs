namespace oracleofages;

/// <summary>
/// tokayExplainingVinesScript for INTERAC_TOKAY $48:$1e.
/// </summary>
internal sealed class TokayVineExplanationEvent : IRoomEvent
{
    private readonly RoomEventContext _context;
    private readonly TokayIslandDatabase _database;
    private bool _active;

    internal TokayVineExplanationEvent(
        RoomEventContext context,
        TokayIslandDatabase database)
    {
        _context = context;
        _database = database;
    }

    public bool HasState => _active;
    public bool BlocksGameplay => false;

    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (_active || !npc.Active || npc.Record is not { Id: 0x48, SubId: 0x1e })
            return false;

        bool explained = _context.Rooms.SaveData.HasRoomFlag(
            _context.Rooms.ActiveGroup,
            _context.Rooms.CurrentRoom.Id,
            OracleSaveData.RoomFlag40);
        if (!explained)
        {
            _context.Rooms.SaveData.SetRoomFlag(
                _context.Rooms.ActiveGroup,
                _context.Rooms.CurrentRoom.Id,
                OracleSaveData.RoomFlag40);
            _context.Sound.PlaySound(_database.SoundJump);
        }
        _context.ShowDialogue(_database.Text(explained ? 0x0a6b : 0x0a6a));
        _active = true;
        return true;
    }

    public void UpdateFrame()
    {
        if (_active && !_context.DialogueOpen)
            _active = false;
    }

    public void Cancel() => _active = false;
}
