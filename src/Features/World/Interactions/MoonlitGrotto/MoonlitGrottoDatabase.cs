using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Source-ordered native Moonlit Grotto objects which are not represented by
/// the ordinary enemy pointer or shared dungeon-mechanic tables.
/// </summary>
internal sealed class MoonlitGrottoDatabase
{
    private readonly Lookup<int, DungeonObjectRecord> _records = new();
    private readonly Dictionary<int, string> _texts = new();

    internal MoonlitGrottoDatabase()
    {
        LoadObjects();
        LoadTexts();
        ValidateContract();
    }

    internal string SubterrorMessage => Text(0x2f03);

    internal IReadOnlyList<DungeonObjectRecord> GetRoomRecords(
        int group,
        int room) =>
        _records.ValuesOrEmpty((group << 8) | room);

    private void LoadObjects()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/moonlit_grotto_objects.tsv",
            new GeneratedTableSchema(
                "Moonlit Grotto native objects",
                GeneratedTableKeySemantics.Grouped,
                [
                    "group", "room", "order", "kind", "id", "subid",
                    "y", "x", "condition", "source"
                ],
                ["group", "room"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
        {
            DungeonObjectRecord record = new(
                row.Decimal(0, 0, 7),
                row.HexByte(1),
                row.UnsignedDecimal(2),
                row.RequiredString(3) switch
                {
                    "miniboss-reward" => DungeonObjectKind.MinibossReward,
                    "subterror" => DungeonObjectKind.Subterror,
                    _ => throw row.Invalid(
                        3, "miniboss-reward or subterror")
                },
                row.HexByte(4),
                row.HexByte(5),
                row.HexByte(6),
                row.HexByte(7),
                DungeonObjectData.ParseCondition(row, 8),
                row.RequiredString(9));
            List<DungeonObjectRecord> records =
                _records.GetOrAdd((record.Group << 8) | record.Room);
            if (records.Count > 0 && records[^1].Order >= record.Order)
            {
                throw new InvalidOperationException(
                    $"Room {record.Group:x1}:{record.Room:x2} Moonlit " +
                    "Grotto native object order did not increase.");
            }
            records.Add(record);
        }
    }

    private void LoadTexts()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/moonlit_grotto_text.tsv",
            new GeneratedTableSchema(
                "Moonlit Grotto native text",
                GeneratedTableKeySemantics.Unique,
                ["text-id", "message-base64"],
                ["text-id"],
                headerRequired: true));
        foreach (GeneratedTableRow row in table.Rows)
            _texts.Add(row.HexWord(0), row.Base64Utf8(1));
    }

    private void ValidateContract()
    {
        IReadOnlyList<DungeonObjectRecord> room4d =
            GetRoomRecords(4, 0x4d);
        if (room4d.Count != 2 ||
            room4d[0] is not
            {
                Order: 3,
                Kind: DungeonObjectKind.MinibossReward,
                Id: 0x20,
                SubId: 0x00,
                Y: 0x58,
                X: 0x78,
                Predicate: DungeonObjectCondition.Flag80Clear
            } ||
            room4d[1] is not
            {
                Order: 4,
                Kind: DungeonObjectKind.Subterror,
                Id: 0x72,
                SubId: 0x00,
                Y: 0x18,
                X: 0x78,
                Predicate: DungeonObjectCondition.Flag80Clear
            } ||
            _texts.Count != 1 ||
            string.IsNullOrWhiteSpace(SubterrorMessage))
        {
            throw new InvalidOperationException(
                "Imported Moonlit Grotto room 4:4d miniboss contract is incomplete.");
        }
    }

    private string Text(int id) =>
        _texts.TryGetValue(id, out string? value)
            ? value
            : throw new KeyNotFoundException(
                $"Moonlit Grotto text TX_{id:x4} was not imported.");
}
