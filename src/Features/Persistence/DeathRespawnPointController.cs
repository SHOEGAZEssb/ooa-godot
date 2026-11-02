
namespace Oracles;

/// <summary>
/// Maintains the original wDeathRespawnBuffer checkpoint. Saving copies this
/// already-maintained state; it does not snapshot Link's arbitrary position.
/// </summary>
public sealed class DeathRespawnPointController
{
    private readonly RoomSession _rooms;
    private readonly Player _player;
    private readonly OracleRuntimeState _runtimeState;
    private readonly HashSet<(int Group, int Room)> _initializationRooms = new();

    public DeathRespawnPointController(
        RoomSession rooms,
        Player player)
    {
        _rooms = rooms;
        _player = player;
        _runtimeState = rooms.RuntimeState;

        GeneratedTable table = GeneratedTable.Load(
            rooms.AssetRoot + "metadata/continuous_death_respawn_rooms.tsv",
            new GeneratedTableSchema(
                "room-initialization death-respawn rooms",
                GeneratedTableKeySemantics.Unique,
                ["group", "room"],
                ["group", "room"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            _initializationRooms.Add((row.Decimal(0, 0, 7), row.HexByte(1)));
        }
        int expected = _runtimeState.Game == OracleGame.Ages ? 2 : 0;
        if (_initializationRooms.Count != expected)
            throw new InvalidOperationException(
                $"{rooms.AssetRoot}: expected {expected} native room-initialization checkpoint rooms, loaded {_initializationRooms.Count}.");
    }

    internal void RecordRoomInitialization() =>
        RecordRoomInitialization(_player.PrecisePosition, FacingIndex(_player.FacingVector));

    internal void RecordRoomInitialization(Vector2 position, int facing)
    {
        // bank0.s:initializeRoom calls roomSpecificCode $06 once, before
        // object parsing. forceLoadRoom and ordinary object updates omit it.
        if (_initializationRooms.Contains((_rooms.ActiveGroup, _rooms.CurrentRoom.Id)))
            RecordPoint(position, facing);
    }

    public void RecordWarpDestination(int destinationTransition)
    {
        // warpTransition1, warpTransition3's destination completion,
        // warpTransitionB, and warpTransitionE call setDeathRespawnPoint.
        // warpUpdateRespawnPoint suppresses $01/$03/$0e in sidescroll groups.
        if (destinationTransition == WarpDestinationTransition.SlowFall ||
            (_rooms.ActiveGroup < 0x06 && destinationTransition is WarpDestinationTransition.SetRespawn or WarpDestinationTransition.EnterScreen or WarpDestinationTransition.XShifted))
            RecordCurrentPoint();
    }

    internal bool RecordsRoomInitialization(int group, int room) =>
        _initializationRooms.Contains((group, room));

    internal void RecordCurrentPoint() =>
        RecordPoint(_player.PrecisePosition, FacingIndex(_player.FacingVector));

    private void RecordPoint(Vector2 position, int facing)
    {
        int y = unchecked((byte)Mathf.FloorToInt(position.Y));
        int x = unchecked((byte)Mathf.FloorToInt(position.X));
        _rooms.SaveData.SetDeathRespawnPoint(
            _rooms.ActiveGroup,
            _rooms.CurrentRoom.Id,
            _runtimeState.ReadField("wRoomStateModifier"),
            facing,
            y,
            x,
            CompanionRuntimeState.ReadRemembered(_runtimeState),
            _runtimeState.ReadField("wLinkObjectIndex"));
    }

    private static int FacingIndex(Vector2I facing) => facing == Vector2I.Up ? 0
        : facing == Vector2I.Right ? 1
        : facing == Vector2I.Down ? 2
        : 3;
}
