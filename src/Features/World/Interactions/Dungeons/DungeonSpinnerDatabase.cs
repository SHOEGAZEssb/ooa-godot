using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Imported roomSpecificCode placements for INTERAC_SPINNER $7d. The
/// interaction behavior remains global; only its D3 crystal-gated room is
/// selected here.
/// </summary>
internal sealed class DungeonSpinnerDatabase
{
    private readonly Lookup<int, DungeonSpinnerPlacement> _recordsByRoom = new();

    internal int RecordCount { get; }

    internal DungeonSpinnerDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/dungeon_spinners.tsv",
            new GeneratedTableSchema(
                "room-specific dungeon spinners",
                GeneratedTableKeySemantics.Grouped,
                [
                    "group", "room", "position", "state-mask",
                    "required-global-flag", "required-global-state", "source"
                ],
                ["group", "room"],
                headerRequired: true));
        int count = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            DungeonSpinnerPlacement record = new(
                row.Decimal(0, 0, 7),
                row.HexByte(1),
                row.HexByte(2),
                row.HexByte(3),
                row.HexByte(4),
                row.Boolean01(5),
                row.RequiredString(6));
            _recordsByRoom.GetOrAdd(MakeKey(record.Group, record.Room)).Add(record);
            count++;
        }
        RecordCount = count;

        IReadOnlyList<DungeonSpinnerPlacement> room460 = GetRoomRecords(4, 0x60);
        IReadOnlyList<DungeonSpinnerPlacement> room452 = GetRoomRecords(4, 0x52);
        if (RecordCount != 2 ||
            room460 is not
            [
                {
                    PackedPosition: 0x57,
                    StateMask: 0x01,
                    RequiredGlobalFlag: 0x0f,
                    RequiredGlobalState: false
                }
            ] ||
            room452 is not
            [
                {
                    PackedPosition: 0x57,
                    StateMask: 0x01,
                    RequiredGlobalFlag: 0x0f,
                    RequiredGlobalState: true
                }
            ])
        {
            throw new InvalidOperationException(
                "Imported Moonlit Grotto spinner migration is incomplete.");
        }
    }

    internal IReadOnlyList<DungeonSpinnerPlacement> GetRoomRecords(
        int group,
        int room) => _recordsByRoom.ValuesOrEmpty(MakeKey(group, room));

    internal static bool IsEnabled(
        DungeonSpinnerPlacement record,
        OracleSaveData? saveData) =>
        (saveData?.HasGlobalFlag(record.RequiredGlobalFlag) ?? false) ==
            record.RequiredGlobalState;

    private static int MakeKey(int group, int room) => (group << 8) | room;
}

internal readonly record struct DungeonSpinnerPlacement(
    int Group,
    int Room,
    int PackedPosition,
    int StateMask,
    int RequiredGlobalFlag,
    bool RequiredGlobalState,
    string Source);
