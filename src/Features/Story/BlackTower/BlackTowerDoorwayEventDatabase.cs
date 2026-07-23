using System;

namespace oracleofages;

/// <summary>Imported INTERAC_MISCELLANEOUS_2 $dc:$10 data for room $1:$76.</summary>
internal sealed class BlackTowerDoorwayEventDatabase
{
    private const string Path =
        "res://assets/oracle/cutscenes/black_tower_doorway_event.tsv";

    public BlackTowerDoorwayEventDatabaseRecord Data { get; }

    public BlackTowerDoorwayEventDatabase()
    {
        GeneratedTableRow row = GeneratedTable.Load(
            Path,
            new GeneratedTableSchema(
                "Black Tower doorway event",
                GeneratedTableKeySemantics.Ordered,
                [
                    "group", "room", "id", "subid", "y", "x", "clear-position-a",
                    "clear-position-b", "object-radius-y", "object-radius-x", "link-radius-y",
                    "link-radius-x", "room-flag-mask", "clear-dest-group", "clear-dest-room",
                    "set-dest-group", "set-dest-room", "warp-transition", "dest-position",
                    "warp-transition2", "sound", "source"
                ],
                headerRequired: true)).SingleRow();

        Data = new BlackTowerDoorwayEventDatabaseRecord(
            row.Decimal(0, 0, 7), row.HexByte(1), row.HexByte(2), row.HexByte(3),
            row.HexByte(4), row.HexByte(5), row.HexByte(6), row.HexByte(7),
            row.HexByte(8), row.HexByte(9), row.HexByte(10), row.HexByte(11),
            row.HexByte(12), row.HexByte(13), row.HexByte(14), row.HexByte(15),
            row.HexByte(16), row.HexByte(17), row.HexByte(18), row.HexByte(19),
            row.HexByte(20), row.RequiredString(21));
        Validate();
    }

    private void Validate()
    {
        if (Data is not
            { Group: 1, Room: 0x76, InteractionId: 0xdc, SubId: 0x10,
              Y: 0x42, X: 0x50, ClearPositionA: 0x44, ClearPositionB: 0x45,
              ObjectRadiusY: 0x04, ObjectRadiusX: 0x10,
              LinkRadiusY: 0x06, LinkRadiusX: 0x06,
              RoomFlagMask: OracleSaveData.RoomFlagLayoutSwap,
              ClearDestinationGroup: 4, ClearDestinationRoom: 0xe7,
              SetDestinationGroup: 4, SetDestinationRoom: 0xf3,
              WarpTransition: 0x93, DestinationPosition: 0xff,
              WarpTransition2: 0x01, Sound: OracleSoundEngine.SndEnterCave } ||
            string.IsNullOrWhiteSpace(Data.Source))
        {
            throw new InvalidOperationException(
                "Room 1:76 doorway data diverges from interactiondc_subid10.");
        }
    }
}

internal readonly record struct BlackTowerDoorwayEventDatabaseRecord(int Group, int Room, int InteractionId, int SubId, int Y, int X, int ClearPositionA, int ClearPositionB, int ObjectRadiusY, int ObjectRadiusX, int LinkRadiusY, int LinkRadiusX, int RoomFlagMask, int ClearDestinationGroup, int ClearDestinationRoom, int SetDestinationGroup, int SetDestinationRoom, int WarpTransition, int DestinationPosition, int WarpTransition2, int Sound, string Source);
