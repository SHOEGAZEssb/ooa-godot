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
                    "completion", "utf8-base64", "source"
                ],
                ["group", "room"],
                headerRequired: true));

        int count = 0;
        foreach (GeneratedTableRow row in table.Rows)
        {
            CompanionTutorialCompletion completion = row.RequiredString(11) switch
            {
                "companion-right" => CompanionTutorialCompletion.CompanionRight,
                _ => throw row.Invalid(11, "companion-right")
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
                row.Base64Utf8(12),
                row.RequiredString(13));
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
        if (Count != 1 || room05b.Count != 1 || room05b[0] is not
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
            } || string.IsNullOrWhiteSpace(room05b[0].Message) ||
            room05b[0].Source !=
                "mainData.s:group0Map5bObjectData;companionTutorial.s:interactionCoded0")
        {
            throw new InvalidOperationException(
                "Imported room 0:5b INTERAC_COMPANION_TUTORIAL `$d0:$04 " +
                "contract is incomplete.");
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
    string Message,
    string Source);

internal enum CompanionTutorialCompletion
{
    CompanionRight
}
