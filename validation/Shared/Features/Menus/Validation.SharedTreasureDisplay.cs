using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedTreasureDisplay()
    {
        foreach (string pack in new[] { AgesGameContent.AssetRoot, SeasonsGameContent.AssetRoot })
        {
            TreasureDatabase catalog = TreasureDatabase.For(pack);
            // The suffix-free UPGRADE label is $1904 in Ages, $1901 in Seasons.
            TreasureObjectRecord satchel = catalog.GetObject("TREASURE_OBJECT_SEED_SATCHEL_UPGRADE");
            TreasureObjectRecord ringBox = catalog.GetObject("TREASURE_OBJECT_RING_BOX_04");
            bool agesPack = pack == AgesGameContent.AssetRoot;
            FailIf(satchel is not { TreasureId: 0x19, Parameter: 0, Graphic: 0x20 } ||
                satchel.SubId != (agesPack ? 4 : 1) || satchel.TextId != (agesPack ? 0x80 : 0x46) ||
                ringBox is not { TreasureId: 0x2c, SubId: 4, Parameter: 3, Graphic: 0x35 } ||
                catalog.Objects.Count(record => record.Name == "TREASURE_OBJECT_RING_BOX_04") != 1,
                "Treasure catalogs must follow native macro-assigned subids and the active ENABLE_US_BUGFIXES ring-box row in both packs.");
        }
        // Independent clean-US Seasons ROM $3f:$6da1-$714a, in native
        // treasureDisplayData2 order. The last pointer aliases sword.
        string[] names = ["standard", "satchel", "slingshot", "hyperSlingshot", "sword", "shield",
            "feather", "boomerang", "tradeItem", "pirateBell", "magnetGlove", "flute"];
        int[] counts = [89, 5, 5, 5, 3, 3, 2, 2, 12, 2, 2, 4];
        var table = GeneratedTable.Load(SeasonsGameContent.AssetRoot + "metadata/treasure_display.tsv",
            new("native Seasons display", GeneratedTableKeySemantics.Grouped,
                ["table", "index", "treasure-id", "left-sprite", "left-palette", "right-sprite", "right-palette", "extra-mode", "text-low"],
                ["table"], headerRequired: true));
        var bytes = new List<byte>();
        int cursor = 0;
        for (int group = 0; group < names.Length; group++)
        for (int index = 0; index < counts[group]; index++)
        {
            GeneratedTableRow row = table.Rows[cursor++];
            FailIf(row.RequiredString(0) != $"treasureDisplayData_{names[group]}" || row.UnsignedDecimal(1) != index,
                "Seasons treasureDisplayData2 table order and native row extents must retain all 134 source records.");
            for (int column = 2; column < 9; column++) bytes.Add((byte)row.HexByte(column));
        }
        FailIf(cursor != table.Rows.Count || bytes.Count != 938 ||
            Convert.ToHexString(SHA256.HashData(bytes.ToArray())) != "68443D9DF92E0FB75AB918E184B9F32A27A60167853A48C198B7F7D3FC228355",
            "Seasons' seven-byte display records must match the independent native-ROM golden.");

        var treasures = new TreasureDatabase(SeasonsGameContent.AssetRoot);
        var save = OracleSaveData.CreateStandardGame(OracleGame.Seasons);
        save.WriteField("wShooterSelectedSeeds", 4);
        save.WriteField("wSatchelSelectedSeeds", 2);
        save.WriteField("wSlingshotLevel", 1);
        save.WriteField("wFeatherLevel", 1);
        save.WriteField("wBoomerangLevel", 1);
        var inventory = new InventoryState(treasures, save);
        FailIf(treasures.GetButtonDisplay(5, inventory) is not { TreasureId: 0x24, LeftSprite: 0x82, LeftPalette: 5, RightSprite: 0x87, ExtraMode: 1, TextLow: 0x3d },
            "A zero-level Seasons Sword display must retain the source -7 pointer prefix instead of clamping to level $01.");
        DisplayRecord sling = treasures.GetButtonDisplay(0x13, inventory);
        FailIf(sling is not { TreasureId: 0x24, LeftSprite: 0x81, LeftPalette: 4, RightSprite: 0x87, ExtraMode: 1, TextLow: 0x3c } ||
            treasures.GetButtonDisplay(0x19, inventory) is not { TreasureId: 0x22, LeftSprite: 0x80, TextLow: 0x2d } ||
            treasures.GetButtonDisplay(0x17, inventory) is not { LeftSprite: 0x96, ExtraMode: 0, TextLow: 0x2c } ||
            treasures.GetButtonDisplay(0x06, inventory) is not { LeftSprite: 0x9c, ExtraMode: 0, TextLow: 0x27 },
            "Seasons display selection must use its own selector fields, seed rows and -7 level biases on a cold reader.");
        save.WriteField("wSlingshotLevel", 2);
        inventory = new(treasures, save);
        FailIf(treasures.GetButtonDisplay(0x13, inventory) is not { LeftSprite: 0x82, LeftPalette: 5, TextLow: 0x3d },
            "loadTreasureDisplayData@getTableIndices must select the second Slingshot row for level $02.");

        Span<TreasureExtraTile> extra = stackalloc TreasureExtraTile[4];
        (int X, int Y)[] positions = [(0, 0), (0, -8), (8, -8), (8, 0)];
        for (int season = 0; season < 4; season++)
        {
            save.WriteField("wObtainedSeasons", (byte)(1 << season));
            inventory = new(treasures, save);
            DisplayRecord rod = treasures.GetButtonDisplay(0x07, inventory);
            foreach (bool equipped in new[] { false, true })
            {
                int count = TreasureExtraTiles.Write(OracleGame.Seasons, rod, inventory, equipped, extra);
                FailIf(count != 1 || extra[0] != new TreasureExtraTile(0x1c + season, equipped ? 0x80 : 7, positions[season].X, positions[season].Y),
                    $"drawTreasureExtraTiles@val02: obtained season bit ${1 << season:x2} must write only its native Rod tile/position/attributes.");
            }
        }
        foreach (int polarity in new[] { 0, 1 })
        {
            save.WriteField("wMagnetGlovePolarity", (byte)polarity);
            inventory = new(treasures, save);
            DisplayRecord gloves = treasures.GetButtonDisplay(0x08, inventory);
            FailIf(gloves.ExtraMode != 3 || gloves.LeftPalette != (polarity == 0 ? 1 : 2) || gloves.RightSprite != (polarity == 0 ? 0x89 : 0),
                "Seasons' Magnet Gloves display must select its native polarity row.");
            int count = TreasureExtraTiles.Write(OracleGame.Seasons, gloves, inventory, false, extra);
            FailIf(count != polarity || polarity != 0 && extra[0] != new TreasureExtraTile(0x0a, 0x87, 0, 0),
                "drawTreasureExtraTiles@val03 must overwrite N with native tile $0a and retain the rotated high attribute bit.");
        }
        FailIf(treasures.GetInventoryText(0x56).NameTextId != 0x0956 || treasures.GetRingText(0).NameTextId != 0x3040,
            "Cold Seasons inventory text must resolve its own season blurb and ring text without treasure-object or Ages data.");
        var agesSave = OracleSaveData.CreateStandardGame(OracleGame.Ages);
        agesSave.WriteField("wSwordLevel", 1);
        var ages = new TreasureDatabase(AgesGameContent.AssetRoot);
        FailIf(ages.GetButtonDisplay(5, new InventoryState(ages, agesSave)).LeftSprite != 0x90,
            "A cold Ages display query must load its own selector/presentation data without eager object-catalog initialization.");
        agesSave.WriteField("wSwordLevel", 0);
        FailIf(ages.GetButtonDisplay(5, new InventoryState(ages, agesSave)) is not { TreasureId: 0x24, LeftSprite: 0x81, LeftPalette: 5, RightSprite: 0x87, ExtraMode: 1, TextLow: 0x40 },
            "Ages must preserve its own native -7 prefix when the selected level byte is $00.");
    }
}
