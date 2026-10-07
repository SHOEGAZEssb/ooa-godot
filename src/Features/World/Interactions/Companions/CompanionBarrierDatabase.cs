using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Positioned INTERAC_COMPANION_SCRIPTS $71:$01/$02/$04/$05 records. Once Link mounts,
/// the interaction clamps the live companion to its cardinal boundary and
/// selects dialogue by SPECIALOBJECT_RICKY-relative companion index.
/// </summary>
internal sealed class CompanionBarrierDatabase
{
    private readonly Lookup<int, CompanionBarrierRecord> _records = new();

    internal int Count { get; private set; }

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
                ["group", "room", "order"],
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
            if (record.Id != 0x71 || record.SubId is not (1 or 2 or 4 or 5))
                throw row.Invalid(3, "INTERAC_COMPANION_SCRIPTS $71:$01/$02/$04/$05");
            _records.Add(MakeKey(record.Group, record.Room), record);
            Count++;
        }

        if (Count != 10 || GetRoomRecords(0, 0x6a).Count != 2)
        {
            throw new InvalidOperationException(
                "Imported Ages INTERAC_COMPANION_SCRIPTS `$71:$01/$02/$04/$05 contract is incomplete.");
        }
    }

    internal IReadOnlyList<CompanionBarrierRecord> GetRoomRecords(int group, int room) =>
        _records.ValuesOrEmpty(MakeKey(group, room));

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
