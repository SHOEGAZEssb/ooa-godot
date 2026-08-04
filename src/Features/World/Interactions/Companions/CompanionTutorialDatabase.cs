using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Source-owned INTERAC_COMPANION_TUTORIAL placements whose invisible marker
/// shows a mounted-companion explanation and retires after the companion
/// crosses its completion boundary.
/// </summary>
internal sealed class CompanionTutorialDatabase
{
    private readonly Lookup<int, CompanionTutorialRecord> _recordsByRoom = new();

    internal int Count { get; }

    internal CompanionTutorialDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/companion_tutorials.tsv",
            new GeneratedTableSchema(
                "companion tutorials",
                GeneratedTableKeySemantics.Grouped,
                [
                    "group", "room", "order", "id", "subid", "y", "x",
                    "required-companion", "text-id", "flag-address", "flag-bit",
                    "completion", "link-x-min", "link-x-max", "utf8-base64",
                    "source"
                ],
                ["group", "room"],
                headerRequired: true));

        int count = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            CompanionTutorialCompletion completion = row.RequiredString(11) switch
            {
                "companion-right" => CompanionTutorialCompletion.CompanionRight,
                "companion-above" => CompanionTutorialCompletion.CompanionAbove,
                "companion-below-or-left" =>
                    CompanionTutorialCompletion.CompanionBelowOrLeft,
                "above-link-range" =>
                    CompanionTutorialCompletion.CompanionAboveWithLinkXRange,
                _ => throw row.Invalid(
                    11,
                    "companion-right, companion-above, companion-below-or-left, " +
                    "or above-link-range")
            };
            CompanionTutorialRecord record = new(
                row.Decimal(0, 0, 7),
                row.HexByte(1),
                row.UnsignedDecimal(2),
                row.HexByte(3),
                row.HexByte(4),
                row.HexByte(5),
                row.HexByte(6),
                row.HexByte(7),
                row.HexWord(8),
                row.HexWord(9),
                row.Decimal(10, 0, 7),
                completion,
                row.HexByte(12),
                row.HexByte(13),
                row.Base64Utf8(14),
                row.RequiredString(15));
            List<CompanionTutorialRecord> records =
                _recordsByRoom.GetOrAdd(MakeKey(record.Group, record.Room));
            if (records.Count > 0 && records[^1].Order >= record.Order)
            {
                throw new InvalidOperationException(
                    $"Room {record.Group:x1}:{record.Room:x2} companion-tutorial " +
                    $"order did not increase at source object {record.Order}.");
            }
            records.Add(record);
            count++;
        }
        Count = count;

        IReadOnlyList<CompanionTutorialRecord> room05b = GetRoomRecords(0, 0x5b);
        IReadOnlyList<CompanionTutorialRecord> room079 = GetRoomRecords(0, 0x79);
        IReadOnlyList<CompanionTutorialRecord> room089 = GetRoomRecords(0, 0x89);
        if (Count != 6 || room05b.Count != 1 || room05b[0] is not
            {
                Order: 0,
                Id: 0xd0,
                SubId: 0x04,
                Y: 0x68,
                X: 0x60,
                RequiredCompanion: CompanionRuntimeState.MooshId,
                TextId: 0x2207,
                FlagAddress: 0xc649,
                FlagBit: 4,
                Completion: CompanionTutorialCompletion.CompanionRight
            } || room079.Count != 1 || room079[0] is not
            {
                Order: 0,
                Id: 0xd0,
                SubId: 0x01,
                Y: 0x38,
                X: 0x78,
                RequiredCompanion: CompanionRuntimeState.RickyId,
                TextId: 0x2009,
                FlagAddress: 0xc649,
                FlagBit: 1,
                Completion: CompanionTutorialCompletion.CompanionAbove
            } || room089.Count != 1 || room089[0] is not
            {
                Order: 0,
                Id: 0xd0,
                SubId: 0x00,
                Y: 0x38,
                X: 0x30,
                RequiredCompanion: CompanionRuntimeState.RickyId,
                TextId: 0x2008,
                FlagAddress: 0xc649,
                FlagBit: 0,
                Completion: CompanionTutorialCompletion.CompanionBelowOrLeft
            } || string.IsNullOrWhiteSpace(room05b[0].Message) ||
            string.IsNullOrWhiteSpace(room079[0].Message) ||
            string.IsNullOrWhiteSpace(room089[0].Message))
        {
            throw new InvalidOperationException(
                "Imported Ages INTERAC_COMPANION_TUTORIAL `$d0 contract is incomplete.");
        }
    }

    internal IReadOnlyList<CompanionTutorialRecord> GetRoomRecords(
        int group,
        int room) =>
        _recordsByRoom.ValuesOrEmpty(MakeKey(group, room));

    private static int MakeKey(int group, int room) => (group << 8) | room;
}

internal readonly record struct CompanionTutorialRecord(
    int Group,
    int Room,
    int Order,
    int Id,
    int SubId,
    int Y,
    int X,
    int RequiredCompanion,
    int TextId,
    int FlagAddress,
    int FlagBit,
    CompanionTutorialCompletion Completion,
    int LinkXMin,
    int LinkXMax,
    string Message,
    string Source);

internal enum CompanionTutorialCompletion
{
    CompanionRight,
    CompanionAbove,
    CompanionBelowOrLeft,
    CompanionAboveWithLinkXRange
}
