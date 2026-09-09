using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateInventoryIconFidelity()
    {
        // Independent clean-US goldens: decode ROM $064000-$0645ff directly,
        // and decompress $0a4a75/$0a5363/$0a5469 using the vanilla tools/common.py.
        // Hash color indices in image order, without PNG encoding/metadata.
        (string Path, bool Sprite, int Height, string Hash)[] sheets =
        [
            ("gfx/spr_item_icons_1", true, 16, "59E45B85BD207604B9C97713C0F26FC86D9AAA642E48EC0E6564244FBE4C39F9"),
            ("gfx/spr_item_icons_2", true, 16, "459CBCB0D33E812636EB274787CD3BC3121929E18AD12B745DAF05A99417DEA6"),
            ("gfx/spr_item_icons_3", true, 16, "DC7714CD8887986B0812930C2CE70C2E1ECE45B35FD0F9E983E12B0B3D9890A8"),
            ("gfx/spr_item_icons_1_spr", true, 16, "6AC5936F39EA13DC262B7373D25656CD615765CE92F58B9DF079FB2C1144E36D"),
            ("gfx/gfx_hud", false, 16, "24622F627493631CBDBF64E2DE82BF970AE61BBEA7B5A4A83B3440795C69E9B1"),
            ("inventory/gfx_inventory_hud_1", false, 24, "2B960C884E9B22593D326296F200F1F86AF533C863438283CB1DDE7544B15324")
        ];
        foreach (var sheet in sheets)
        {
            Image image = OracleGraphicsCache.LoadImage($"res://assets/oracle/{sheet.Path}.png");
            FailIf(image.GetWidth() != 128 || image.GetHeight() != sheet.Height,
                $"Clean-US {sheet.Path}: incorrect tile extent.");
            var shades = new byte[128 * sheet.Height];
            for (int y = 0; y < sheet.Height; y++)
            for (int x = 0; x < 128; x++)
            {
                int shade = Mathf.RoundToInt(image.GetPixel(x, y).R * 3);
                shades[y * 128 + x] = (byte)(sheet.Sprite ? shade : 3 - shade);
            }
            string hash = Convert.ToHexString(SHA256.HashData(shades));
            FailIf(hash != sheet.Hash, $"Clean-US {sheet.Path}: color-index hash {hash}.");
        }

        // The complete eleven tables, in treasureDisplayData2 pointer order;
        // this golden comes from ROM $3f:$6d62, including each -7 level bias.
        string[] names = ["standard", "satchel", "sword", "shield", "bracelet",
            "trade", "flute", "shooter", "harp", "tuniNut", "switchHook"];
        int[] counts = [96, 5, 3, 3, 2, 13, 4, 5, 4, 3, 2];
        string[][] rows = FileAccess.GetFileAsString(
                "res://assets/oracle/metadata/treasure_display.tsv")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !line.StartsWith('#')).Select(line => line.TrimEnd('\r').Split('\t')).ToArray();
        var bytes = new List<byte>();
        int cursor = 0;
        for (int table = 0; table < names.Length; table++)
        for (int index = 0; index < counts[table]; index++)
        {
            FailIf(cursor >= rows.Length || rows[cursor][0] != $"treasureDisplayData_{names[table]}" ||
                rows[cursor][1] != index.ToString(), "Clean-US treasure display table order/boundary changed.");
            foreach (string value in rows[cursor++].Skip(2)) bytes.Add(Convert.ToByte(value, 16));
        }
        FailIf(cursor != rows.Length || bytes.Count != 980 ||
            Convert.ToHexString(SHA256.HashData(bytes.ToArray())) !=
                "AB2A891271C13C302790F6B933557626D735A7354237A4F6E8F28480B9E41AD1",
            "Clean-US treasureDisplayData: expected all 140 original seven-byte rows.");

        var save = OracleSaveData.CreateStandardGame();
        var inventory = new InventoryState(_treasures, save);
        inventory.GiveTreasure(TreasureDatabase.TreasureBracelet, 2);
        FailIf(_treasures.GetButtonDisplay(0x04, inventory).LeftSprite != 0x97 ||
            _treasures.GetButtonDisplay(0x0f, inventory).LeftSprite != 0x81 ||
            _treasures.GetButtonDisplay(0x16, inventory).LeftSprite != 0x98 ||
            _treasures.GetButtonDisplay(0x06, inventory).ExtraMode != 0xff ||
            _treasures.GetButtonDisplay(0x17, inventory).ExtraMode != 0xff,
            "Clean-US Cane/Shooter/Power Glove selection or Boomerang/Feather label changed.");
        for (int sprite = 0x80; sprite < 0xa3; sprite++)
        for (int palette = 0; palette < 6; palette++)
            FailIf(ItemIconAtlas.EquippedSprite(sprite) != sprite ||
                ItemIconAtlas.EquippedLeftPalette(sprite, palette) !=
                    (sprite < 0x84 ? ((palette - 3) | 1) & 7 : palette),
                $"loadEquippedItemSpriteData: incorrect $84 boundary for ${sprite:x2}.");

        // Both live A/B renderers must select the small pair; the stored and
        // picker OAM retain the large pair. Goldens hash ROM color indices.
        int[] large = [0xa3, 0xa4, 0xa7, 0xa8, 0xab, 0xac];
        ulong[] smallHashes = [0xdb15da9e7ae861a1UL, 0x1b336aec473acf7dUL,
            0x0f1f31c6620da750UL, 0x9c94fd57b7fa4245UL,
            0x167e2e381569adbeUL, 0x65d7fdee20275a55UL];
        for (int index = 0; index < large.Length; index++)
            FailIf(_hud.ItemIconShadeHashForValidation(large[index]) != smallHashes[index] ||
                _inventoryScreen.EquippedItemIconShadeHashForValidation(large[index]) != smallHashes[index],
                $"loadItemIconGfx: equipped Harp ${large[index]:x2} did not select small pair +$02.");
        FailIf(ItemIconAtlas.EquippedSprite(0x02) != 0x82,
            "No-song equipped Harp must copy spr_item_icons_1+$40.");
        MenuPresentationDatabase layouts = MenuPresentationDatabase.Shared;
        int[] tiles = [0x0e, 0x46, 0x4e, 0x56];
        int[] attributes = [0x08, 0x08, 0x0b, 0x09];
        for (int song = 0; song < 4; song++)
        {
            IReadOnlyList<MenuOamPart> parts = layouts.InventoryHarp(song);
            FailIf(parts.Count != (song == 0 ? 1 : 2), $"Harp song ${song:x2} OAM count.");
            for (int part = 0; part < parts.Count; part++)
                FailIf(parts[part].Y != 0x14 || parts[part].X != (song == 0 ? 0x0c : 8 + part * 8) ||
                    parts[part].Tile != tiles[song] + part * 2 || parts[part].Attributes != attributes[song],
                    $"seedAndHarpSpriteTable+4: song ${song:x2}, part {part} differs from original OAM.");
        }

        var screen = new InventoryScreen { Visible = false };
        AddChild(screen);
        screen.Initialize(_treasures, inventory);
        for (int count = 1; count <= 4; count++)
        {
            inventory.GiveTreasure(0x4b, 1);
            var overlay = screen.QuantityOverlayForValidation(0x4b);
            FailIf(overlay is not { } value || value.TensTile != 0x1b || value.OnesTile != 0x10 + count ||
                value.Attributes != 7 || value.Offset != new Vector2(8, 8),
                $"drawTreasureExtraTiles @val04: slate count {count} must draw x{count} at right column +$20.");
        }
        screen.Free();
        ValidateComposedInventoryHud();
        GD.Print("Validated all 140 clean-US inventory display rows, six ROM graphics sheets, " +
            "equipped Harp small pairs, four stored/picker OAM selections, and slate counts.");
    }

    private void ValidateComposedInventoryHud()
    {
        // Independent RGB5 screen goldens composed from the clean ROM and
        // vanilla updateStatusBar/drawHeartDisplay/@val02 tile addresses.
        // This catches a correct sheet used with incorrect runtime tile IDs.
        string[] expected =
        [
            "B30884E88E930A36948B37906C21DFA34E8EA2BEE2BD4D4C2D651CC8A55BDB76",
            "949020EDA8DAA3C4B93F094DE50A2EE21667D5BB5E2861821AFC301EEC48C408",
            "12472C1083F097DE98BB6F7E8A2C85CAB9FC745C5A6154F0E1815075919BF334",
            "6E11522F12055596A67DBA25B97FAA9D33F0F17F0D6A130B8BB61B828640F23E",
            "41F07D92F2D3DB5A6D7ECA01491892291D7D58FD73F39781A6BB32EE4A04572A",
            "BCE5EF4CC0856EDE34A7D1359097262E724D967A93D3275A1CE99507C605BE5E",
            "4536875C2CD7CEDB6610BECE76A6307C7E83274AC12C1ACF217C6D815776D5BF",
            "FCDEEAA84C58A951874BCEBEFC6766C35FFF7669F62F1D58246BFD7D90589810"
        ];
        for (int layout = 0; layout < 2; layout++)
        for (int song = 0; song < 4; song++)
        {
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(0xc6b2, 1);
            save.WriteWramByte(0xc6b7, (byte)song);
            save.WriteWramByte(0xc672, 3);
            var inventory = new InventoryState(_treasures, save);
            var hud = new Hud
            {
                Visible = false, MaxHealthQuarters = layout == 0 ? 20 : 60,
                HealthQuarters = layout == 0 ? 17 : 57, Rupees = 3,
                EquippedB = 5, EquippedA = 0x11, DungeonIndex = layout == 0 ? -1 : 0
            };
            AddChild(hud);
            hud.Initialize(_treasures, inventory);
            using Image rendered = hud.ComposeImage();
            var rgb5 = new byte[160 * 16 * 3];
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 160; x++)
            {
                Color pixel = rendered.GetPixel(x, y);
                int offset = (y * 160 + x) * 3;
                rgb5[offset] = (byte)Mathf.RoundToInt(pixel.R * 31);
                rgb5[offset + 1] = (byte)Mathf.RoundToInt(pixel.G * 31);
                rgb5[offset + 2] = (byte)Mathf.RoundToInt(pixel.B * 31);
            }
            string hash = Convert.ToHexString(SHA256.HashData(rgb5));
            string output = OS.GetEnvironment("OOA_HUD_AUDIT_OUTPUT");
            if (!string.IsNullOrEmpty(output))
                rendered.SavePng(System.IO.Path.Combine(output, $"hud-rendered-{layout}-{song}.png"));
            FailIf(hash != expected[layout * 4 + song],
                $"Composed HUD layout {layout}, song ${song:x2}: {hash}; " +
                "check hearts $0b-$0f, rupee/key $09/$0a, Harp BG $1c-$1f and small song OAM.");
            hud.Free();
        }
    }
}
