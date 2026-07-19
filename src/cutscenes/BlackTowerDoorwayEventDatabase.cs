using Godot;
using System;

namespace oracleofages;

/// <summary>Imported INTERAC_MISCELLANEOUS_2 $dc:$10 data for room $1:$76.</summary>
internal sealed class BlackTowerDoorwayEventDatabase
{
    private const string Path =
        "res://assets/oracle/cutscenes/black_tower_doorway_event.tsv";

    public Record Data { get; }

    public BlackTowerDoorwayEventDatabase()
    {
        string row = FirstDataRow();
        string[] fields = row.Split('\t');
        if (fields.Length != 22)
        {
            throw new InvalidOperationException(
                $"Black Tower doorway row should have 22 fields, got {fields.Length}.");
        }

        Data = new Record(
            int.Parse(fields[0]), Hex(fields[1]), Hex(fields[2]), Hex(fields[3]),
            Hex(fields[4]), Hex(fields[5]), Hex(fields[6]), Hex(fields[7]),
            Hex(fields[8]), Hex(fields[9]), Hex(fields[10]), Hex(fields[11]),
            Hex(fields[12]), Hex(fields[13]), Hex(fields[14]), Hex(fields[15]),
            Hex(fields[16]), Hex(fields[17]), Hex(fields[18]), Hex(fields[19]),
            Hex(fields[20]), fields[21]);
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

    private static string FirstDataRow()
    {
        foreach (string raw in FileAccess.GetFileAsString(Path).Split(
            '\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string line = raw.TrimEnd('\r');
            if (!line.StartsWith('#'))
                return line;
        }
        throw new InvalidOperationException($"{Path} is empty.");
    }

    private static int Hex(string value) => Convert.ToInt32(value, 16);

    internal readonly record struct Record(
        int Group,
        int Room,
        int InteractionId,
        int SubId,
        int Y,
        int X,
        int ClearPositionA,
        int ClearPositionB,
        int ObjectRadiusY,
        int ObjectRadiusX,
        int LinkRadiusY,
        int LinkRadiusX,
        int RoomFlagMask,
        int ClearDestinationGroup,
        int ClearDestinationRoom,
        int SetDestinationGroup,
        int SetDestinationRoom,
        int WarpTransition,
        int DestinationPosition,
        int WarpTransition2,
        int Sound,
        string Source);
}
