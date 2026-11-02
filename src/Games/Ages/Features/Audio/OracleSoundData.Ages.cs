using System.Security.Cryptography;

namespace Oracles;

public sealed partial class OracleSoundData
{

    private static ConditionalRoomMusicRule[] LoadAgesConditionalRoomMusic()
    {
        GeneratedTable table = GeneratedTable.Load(
            AgesGameContent.AssetRoot + "audio/conditional_room_music.tsv",
            new GeneratedTableSchema(
                "conditional room music",
                GeneratedTableKeySemantics.Unique,
                [
                    "group", "room", "music", "required-global-flag",
                    "clear-room-flag-mask", "source"
                ],
                ["group", "room"],
                headerRequired: true));
        var rules = new List<ConditionalRoomMusicRule>(table.Rows.Count);
        foreach (GeneratedTableRow row in table.Rows)
        {
            rules.Add(new ConditionalRoomMusicRule(
                row.Decimal(0, 0, 7),
                row.HexByte(1),
                row.HexByte(2),
                row.HexByte(3),
                (byte)row.HexByte(4),
                row.RequiredString(5)));
        }
        if (rules.Count != 1 || rules[0] is not
            {
                Group: 1,
                Room: 0x97,
                Music: 0x35,
                RequiredGlobalFlag: 0x15,
                ClearRoomFlagMask: OracleSaveData.RoomFlag40,
                Source: "code/ages/roomSpecificCode.s:roomSpecificCode7"
            })
        {
            throw new InvalidOperationException(
                "Imported roomSpecificCode7 music contract diverged from room 1:97.");
        }
        return rules.ToArray();
    }

    private static RoomFlagMusicRule[] LoadAgesRoomFlagMusic()
    {
        GeneratedTable table = GeneratedTable.Load(
            AgesGameContent.AssetRoot + "audio/room_flag_music.tsv",
            new GeneratedTableSchema("room-flag music overrides", GeneratedTableKeySemantics.Unique,
                ["group", "assigned-music", "flag-group", "flag-room", "clear-flag-mask", "music", "source"],
                ["group", "assigned-music"], headerRequired: true));
        var rules = new List<RoomFlagMusicRule>();
        foreach (GeneratedTableRow row in table.Rows)
            rules.Add(new RoomFlagMusicRule(row.Decimal(0, 0, 7), row.HexByte(1),
                row.Decimal(2, 0, 7), row.HexByte(3), (byte)row.HexByte(4), row.HexByte(5), row.RequiredString(6)));
        return rules.ToArray();
    }
}
