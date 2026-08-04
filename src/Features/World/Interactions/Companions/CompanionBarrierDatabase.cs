using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Positioned INTERAC_COMPANION_SCRIPTS $71:$02 records. Once Link mounts,
/// the interaction clamps the live companion to its lower-Y boundary and
/// selects dialogue by SPECIALOBJECT_RICKY-relative companion index.
/// </summary>
internal sealed class CompanionBarrierDatabase
{
    private readonly Dictionary<int, CompanionBarrierRecord> _records = new();

    internal int Count => _records.Count;

    internal CompanionBarrierDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/companion_barriers.tsv",
            new GeneratedTableSchema(
                "companion barriers",
                GeneratedTableKeySemantics.Unique,
                [
                    "group", "room", "order", "id", "subid", "y", "x",
                    "ricky-state-address", "dimitri-state-address",
                    "moosh-state-address", "ricky-text-id", "dimitri-text-id",
                    "moosh-text-id", "ricky-utf8-base64",
                    "dimitri-utf8-base64", "moosh-utf8-base64", "source"
                ],
                ["group", "room"],
                headerRequired: true));

        foreach (GeneratedTableRow row in table.Rows)
        {
            CompanionBarrierRecord record = new(
                row.Decimal(0, 0, 7),
                row.HexByte(1),
                row.UnsignedDecimal(2),
                row.HexByte(3),
                row.HexByte(4),
                row.HexByte(5),
                row.HexByte(6),
                [row.HexWord(7), row.HexWord(8), row.HexWord(9)],
                [row.HexWord(10), row.HexWord(11), row.HexWord(12)],
                [row.Base64Utf8(13), row.Base64Utf8(14), row.Base64Utf8(15)],
                row.RequiredString(16));
            if (!_records.TryAdd(MakeKey(record.Group, record.Room), record))
            {
                throw new InvalidOperationException(
                    $"Duplicate companion barrier in {record.Source}.");
            }
        }

        if (Count != 2 ||
            !TryGet(0, 0x6c, out CompanionBarrierRecord room06c) ||
            room06c is not
            {
                Order: 4, Id: 0x71, SubId: 0x02, Y: 0x6d, X: 0x38
            } ||
            !TryGet(0, 0x89, out CompanionBarrierRecord room089) ||
            room089 is not
            {
                Order: 1, Id: 0x71, SubId: 0x02, Y: 0x6d, X: 0x38
            } ||
            room089.TextId(CompanionRuntimeState.RickyId) != 0x2007 ||
            room089.TextId(CompanionRuntimeState.MooshId) != 0x2209)
        {
            throw new InvalidOperationException(
                "Imported Ages INTERAC_COMPANION_SCRIPTS `$71:$02 contract is incomplete.");
        }
    }

    internal bool TryGet(
        int group,
        int room,
        out CompanionBarrierRecord record) =>
        _records.TryGetValue(MakeKey(group, room), out record);

    private static int MakeKey(int group, int room) => (group << 8) | room;
}

internal readonly record struct CompanionBarrierRecord(
    int Group,
    int Room,
    int Order,
    int Id,
    int SubId,
    int Y,
    int X,
    int[] StateAddresses,
    int[] TextIds,
    string[] Messages,
    string Source)
{
    internal int StateAddress(int companionId) =>
        StateAddresses[CompanionIndex(companionId)];

    internal int TextId(int companionId) =>
        TextIds[CompanionIndex(companionId)];

    internal string Message(int companionId) =>
        Messages[CompanionIndex(companionId)];

    private static int CompanionIndex(int companionId)
    {
        int index = companionId - CompanionRuntimeState.RickyId;
        if (index is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(companionId));
        return index;
    }
}
