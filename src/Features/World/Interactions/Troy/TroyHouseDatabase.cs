using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>
/// Imported room/script contract for INTERAC_TROY $ca:$01 in room 3:fb.
/// Each row preserves one of the 16 equally likely wTextSubstitutions values.
/// </summary>
internal sealed class TroyHouseDatabase
{
    private readonly TroyHouseAnimalText[] _animalTexts;

    public TroyHouseRecord Record { get; }
    public IReadOnlyList<TroyHouseAnimalText> AnimalTexts => _animalTexts;

    public TroyHouseDatabase()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/objects/troy_house.tsv",
            new GeneratedTableSchema(
                "room 3:fb Troy house interaction",
                GeneratedTableKeySemantics.Unique,
                [
                    "group", "room", "id", "subid", "room-flag",
                    "random-mask", "choice", "first-text-id",
                    "repeat-text-id", "animal-text-id", "source",
                    "first-utf8-base64", "repeat-utf8-base64",
                    "animal-utf8-base64"
                ],
                ["group", "room", "choice"],
                headerRequired: true));
        if (table.Rows.Count != 16)
        {
            throw new InvalidOperationException(
                $"Room 3:fb Troy data should have 16 RNG rows, got {table.Rows.Count}.");
        }

        GeneratedTableRow first = table.Rows[0];
        Record = new TroyHouseRecord(
            first.Decimal(0, 0, 7),
            first.HexByte(1),
            first.HexByte(2),
            first.HexByte(3),
            first.HexByte(4),
            first.HexByte(5),
            first.HexWord(7),
            first.HexWord(8),
            first.Base64Utf8(11),
            first.Base64Utf8(12),
            first.RequiredString(10));

        _animalTexts = new TroyHouseAnimalText[table.Rows.Count];
        var choices = new HashSet<int>();
        foreach (GeneratedTableRow row in table.Rows)
        {
            int choice = row.HexByte(6);
            var rowRecord = new TroyHouseRecord(
                row.Decimal(0, 0, 7),
                row.HexByte(1),
                row.HexByte(2),
                row.HexByte(3),
                row.HexByte(4),
                row.HexByte(5),
                row.HexWord(7),
                row.HexWord(8),
                row.Base64Utf8(11),
                row.Base64Utf8(12),
                row.RequiredString(10));
            if (rowRecord != Record ||
                choice >= _animalTexts.Length ||
                !choices.Add(choice))
            {
                throw new InvalidOperationException(
                    $"Invalid room 3:fb Troy RNG row at {row.Path}:{row.LineNumber}.");
            }
            _animalTexts[choice] = new TroyHouseAnimalText(
                choice, row.HexWord(9), row.Base64Utf8(13));
        }

        if (Record is not
            {
                Group: 3, Room: 0xfb, InteractionId: 0xca, SubId: 0x01,
                FirstTalkFlag: 0x40, RandomMask: 0x0f,
                FirstTextId: 0x2c11, RepeatTextId: 0x2c12
            } ||
            !Record.FirstMessage.EndsWith("\\n", StringComparison.Ordinal) ||
            !Record.RepeatMessage.Contains(
                "\\call(0xff)", StringComparison.OrdinalIgnoreCase) ||
            _animalTexts.Where((text, index) =>
                text.Choice != index ||
                text.TextId != 0x2c13 + index ||
                string.IsNullOrEmpty(text.Message)).Any() ||
            string.IsNullOrWhiteSpace(Record.Source))
        {
            throw new InvalidOperationException(
                "Room 3:fb Troy data diverges from $ca:$01 and troySubid1Script.");
        }
    }

    public bool Matches(NpcRecord record) =>
        record.Group == Record.Group &&
        record.Room == Record.Room &&
        record.Id == Record.InteractionId &&
        record.SubId == Record.SubId;

    public int TextId(bool firstTalk) =>
        firstTalk ? Record.FirstTextId : Record.RepeatTextId;

    public string ComposeMessage(bool firstTalk, int choice)
    {
        if (choice < 0 || choice >= _animalTexts.Length)
            throw new ArgumentOutOfRangeException(nameof(choice));
        string repeated = Record.RepeatMessage.Replace(
            "\\call(0xff)",
            _animalTexts[choice].Message,
            StringComparison.OrdinalIgnoreCase);
        if (!firstTalk)
            return repeated;

        // TX_2c11 is unterminated and ends in the fixed \n text command.
        // Resolve it before adjoining TX_2c12 so the command scanner cannot
        // greedily read the fallthrough as one unknown "\nJust" command.
        string first = Record.FirstMessage[..^2] + '\n';
        return first + repeated;
    }
}

internal readonly record struct TroyHouseRecord(
    int Group,
    int Room,
    int InteractionId,
    int SubId,
    int FirstTalkFlag,
    int RandomMask,
    int FirstTextId,
    int RepeatTextId,
    string FirstMessage,
    string RepeatMessage,
    string Source);

internal readonly record struct TroyHouseAnimalText(
    int Choice,
    int TextId,
    string Message);
