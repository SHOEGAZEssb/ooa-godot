using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Maintains the original wDeathRespawnBuffer checkpoint. Saving copies this
/// already-maintained state; it does not snapshot Link's arbitrary position.
/// </summary>
public sealed class DeathRespawnPointController
{
    private const byte TilesetFlagUnderwater = 0x40;
    private readonly RoomSession _rooms;
    private readonly Player _player;
    private readonly HashSet<(int Group, int Room)> _continuousRooms = new();

    public DeathRespawnPointController(RoomSession rooms, Player player)
    {
        _rooms = rooms;
        _player = player;

        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/continuous_death_respawn_rooms.tsv",
            new GeneratedTableSchema(
                "continuous death-respawn rooms",
                GeneratedTableKeySemantics.Unique,
                ["group", "room"],
                ["group", "room"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            _continuousRooms.Add((row.Decimal(0, 0, 7), row.HexByte(1)));
        }
        if (_continuousRooms.Count != 2)
            throw new InvalidOperationException(
                $"Expected 2 continuous death-respawn rooms, loaded {_continuousRooms.Count}.");
    }

    public void Update()
    {
        if (_continuousRooms.Contains((_rooms.ActiveGroup, _rooms.CurrentRoom.Id)))
            RecordCurrentPoint();
    }

    public void RecordWarpDestination(int destinationTransition)
    {
        // warpTransition1, warpTransition3's destination completion,
        // warpTransitionB, and warpTransitionE call setDeathRespawnPoint.
        // warpUpdateRespawnPoint suppresses $01/$03/$0e in sidescroll groups.
        if (destinationTransition == 0x0b ||
            (_rooms.ActiveGroup < 0x06 && destinationTransition is 0x01 or 0x03 or 0x0e))
            RecordCurrentPoint();
    }

    internal bool UpdatesContinuously(int group, int room) =>
        _continuousRooms.Contains((group, room));

    internal void RecordCurrentPoint()
    {
        int stateModifier = (_rooms.CurrentRoom.TilesetFlags & TilesetFlagUnderwater) != 0 ? 1 : 0;
        if (_rooms.SaveData.HasRoomFlag(
            _rooms.ActiveGroup, _rooms.CurrentRoom.Id, OracleSaveData.RoomFlagLayoutSwap))
        {
            stateModifier++;
        }

        Vector2 position = _player.Position;
        _rooms.SaveData.SetDeathRespawnPoint(
            _rooms.ActiveGroup,
            _rooms.CurrentRoom.Id,
            stateModifier,
            FacingIndex(_player.FacingVector),
            Mathf.Clamp(Mathf.FloorToInt(position.Y), 0, 0xff),
            Mathf.Clamp(Mathf.FloorToInt(position.X), 0, 0xff));
    }

    private static int FacingIndex(Vector2I facing) => facing == Vector2I.Up ? 0
        : facing == Vector2I.Right ? 1
        : facing == Vector2I.Down ? 2
        : 3;
}
