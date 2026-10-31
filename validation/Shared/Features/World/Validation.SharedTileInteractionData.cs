using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void ValidateSharedTileInteractionData()
    {
        foreach (OracleGame game in new[] { OracleGame.Ages, OracleGame.Seasons })
        {
            bool ages = game == OracleGame.Ages;
            var rom = ValidationRom.LoadCleanUs(game);
            int Byte(int address) => rom.Span[address];
            int Word(int address) => Byte(address) | Byte(address + 1) << 8;
            int Pointer(int table, int offset) => (table & ~0x3fff) + (Word(table + offset) & 0x3fff);
            string root = OracleAssetPack.ForGame(game);
            var signs = new SignDatabase(root);
            var chests = new ChestDatabase(root);
            var signRows = GeneratedTable.Load(root + "objects/signs.tsv",
                new GeneratedTableSchema("native sign expectations", GeneratedTableKeySemantics.Unique,
                    ["group", "room", "position", "text-id", "utf8-base64"],
                    ["group", "room", "position"], headerRequired: true)).Rows;
            int signTable = ages ? 0x1b784 : 0x1b552;
            int chestTable = ages ? 0x59108 : 0x54f6c;
            int treasures = ages ? 0x59332 : 0x55129;
            int rupees = ages ? 0x1791 : 0x176a;
            int[] signCounts = ages ? [15, 9, 7, 6, 4, 1, 0, 0] : [25, 3, 1, 25, 1, 1, 0, 0];
            int[] chestCounts = ages ? [7, 1, 9, 6, 53, 57, 0, 0] : [8, 2, 2, 2, 58, 36, 0, 0];
            int signIndex = 0;
            for (int group = 0; group < 8; group++)
            {
                int at = Pointer(signTable, group * 2);
                for (int index = 0; index < signCounts[group]; index++, at += 3)
                {
                    var row = signRows[signIndex++];
                    int position = Byte(at), room = Byte(at + 1), text = Byte(at + 2);
                    FailIf(row.Decimal(0, 0, 7) != group || row.HexByte(1) != room ||
                        row.HexByte(2) != position || row.HexByte(3) != text ||
                        !signs.TryGetMessage(group, room, position, out string message) ||
                        message.Length == 0 || message != row.Base64Utf8(4),
                        $"{game}: sign {group:x1}:{room:x2}/${position:x2} must retain native source order and TX_2e{text:x2}.");
                }
                FailIf(Byte(at) != 0, $"{game}: sign group ${group:x1} lost its native $00 boundary.");

                at = Pointer(chestTable, group * 2);
                int loaded = Enumerable.Range(0, 256).Sum(room => chests.GetRoomRecords(group, room).Count);
                FailIf(loaded != chestCounts[group], $"{game}: chest group ${group:x1} lost native rows or aliased pointers.");
                for (int index = 0; index < chestCounts[group]; index++, at += 4)
                {
                    int position = Byte(at), room = Byte(at + 1), id = Byte(at + 2), subid = Byte(at + 3);
                    FailIf(!chests.TryGetRecord(group, room, position, out ChestRecord chest),
                        $"{game}: native chest {group:x1}:{room:x2}/${position:x2} is missing.");
                    int property = treasures + id * 4;
                    if ((Byte(property) & 0x80) != 0) property = Pointer(property, 1) + subid * 4;
                    else FailIf(subid != 0, $"{game}: inline treasure ${id:x2} cannot have subid ${subid:x2}.");
                    int parameter = Byte(property + 1), text = Byte(property + 2), graphic = Byte(property + 3);
                    int bcd = rupees + parameter * 2;
                    int amount = id == 0x28 ? Byte(bcd + 1) * 100 + (Byte(bcd) >> 4) * 10 + (Byte(bcd) & 15) : 0;
                    FailIf(chest.Group != group || chest.Room != room || chest.Position != position ||
                        chest.TreasureId != id || chest.SubId != subid || chest.Parameter != parameter ||
                        chest.TextId != text || chest.Graphic != graphic || chest.Amount != amount ||
                        (text != 0xff && chest.Message.Length == 0),
                        $"{game}: chest {group:x1}:{room:x2}/${position:x2} must retain native treasure ${id:x2}:${subid:x2}, properties and BCD amount.");
                }
                FailIf(Byte(at) != 0xff, $"{game}: chest group ${group:x1} lost its native $ff boundary.");
            }
            FailIf(signIndex != signRows.Count, $"{game}: signs contain extra records beyond the native streams.");

            // Source text goldens independently cover shared suffixes, aliases and page commands.
            FailIf(!signs.TryGetMessage(0, ages ? 0x2a : 0xd9, ages ? 0x35 : 0x43, out string first) ||
                first != (ages ? "← Rolling Ridge\n↓ Lynna City" : "Maku Tree Gate\\stop\nShow courage to\ngain passage!"),
                $"{game}: sign text lost its selected-game content, symbols or page command.");
            if (!ages)
            {
                FailIf(!signs.TryGetMessage(3, 0xd9, 0x43, out string alias) || alias != first ||
                    !signs.TryGetMessage(0, 0x0b, 0x17, out string sharedText) ||
                    sharedText != "Flying Cucco\\stop\nFlaps when held.\nOtherwise,\nit's dangerous." ||
                    !signs.TryGetMessage(0, 0x1f, 0x53, out string fourthText) || fourthText != sharedText ||
                    !signs.TryGetMessage(2, 0x04, 0x17, out string secondText) || secondText != sharedText ||
                    !signs.TryGetMessage(4, 0x04, 0x17, out string fourthGroup) || fourthGroup != sharedText,
                    "Seasons sign group0/group3 pointer aliases or TX_2e02/$03/$04 text aliases were lost.");
            }
            var fallback = new TileInteractionFallbackDatabase(root, TreasureDatabase.For(root));
            FailIf(fallback.ChestWrongSide.TextId != 0x510d || fallback.SignWrongSide.TextId != 0x510e ||
                fallback.SignNoMatch.TextId != 0x0901 ||
                fallback.ChestWrongSide.Message != "It won't open\nfrom this side!" ||
                fallback.SignWrongSide.Message != "You can't read\nit from here!" ||
                fallback.SignNoMatch.Message != (ages ? "Eternal Spirit\nIt speaks across\ntime to the\nheart!" : "Fertile Soil\nScattered seeds\nare nourished.") ||
                fallback.ChestNoMatch.TreasureId != 0x28 || fallback.ChestNoMatch.SubId != 0 ||
                fallback.ChestNoMatch.Amount != 1,
                $"{game}: common sign/chest fallbacks must retain native IDs and selected-game text/reward data.");
            int defaultCode = ages ? 0x10f6 : 0x10c4;
            FailIf(Byte(defaultCode) != 0x01 || Word(defaultCode + 1) != 0x2800,
                $"{game}: getChestData's independently traced default changed from contents $2800.");
        }
    }
}
