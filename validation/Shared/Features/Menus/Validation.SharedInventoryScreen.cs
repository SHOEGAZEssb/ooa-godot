using System.Security.Cryptography;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedInventoryScreen()
    {
        var treasures = new TreasureDatabase(SeasonsGameContent.AssetRoot);
        var save = OracleSaveData.CreateStandardGame(OracleGame.Seasons);
        save.WriteField("wInventoryStorage", 0x13);
        save.WriteField("wSlingshotLevel", 1);
        save.WriteField("wObtainedTreasureFlags", 3, 4); // Native Ember/Scent ownership bits.
        save.WriteField("wNumEmberSeeds", 0x23);
        save.WriteField("wNumEmberSeeds", 0x45, 1);
        save.WriteField("wRingBoxLevel", 1);
        save.WriteField("wRingBoxContents", 0);
        var memory = OracleRuntimeState.CreateInitialized(OracleGame.Seasons);
        var inventory = new InventoryState(treasures, save, runtimeState: memory);
        var context = new InventoryRoomContext(0, 0, 0, 0);
        var screen = new InventoryScreen { AssetRoot = SeasonsGameContent.AssetRoot, Visible = false };
        AddChild(screen);
        screen.Initialize(treasures, inventory, () => context);
        try
        {
            void NextPage()
            {
                screen.BeginNextSubscreen();
                screen.UpdatePageTransition(13.0 / 60);
                FailIf(screen.PageTransitionActive, "bank2.s: inventory page scrolling must finish on update $0d.");
            }
            // Independent RGB5 goldens from source spr_season_symbols.png and
            // the clean ROM's PALH_0a standard palettes, in native glyph order.
            string[] golden = [
                "502F8DB81624E9E67DA6A0F57A11FF804E5185105CF40A9784780DFBB76C6E6B",
                "04EB971BFDA327A5E6BF3C85C90B0959224031B0DA245D84CA9C06FE688E8F78",
                "4B662A22193B4098754C58C4A625DDFBC8D4B2E6CD781EE6F86B038CBD3C9F5E",
                "4960EE73CD16462C4A47ED5F636E5096BD4D9EB028CBDC860E8433AC9DBC0A53"
            ];
            void CheckBlurb(string expected)
            {
                using Image image = screen.ComposeImage();
                byte[] colors = new byte[32 * 16 * 3];
                for (int y = 0; y < 16; y++)
                for (int x = 0; x < 32; x++)
                {
                    Color pixel = image.GetPixel(112 + x, 24 + y);
                    int offset = (y * 32 + x) * 3;
                    colors[offset] = (byte)Mathf.RoundToInt(pixel.R * 31);
                    colors[offset + 1] = (byte)Mathf.RoundToInt(pixel.G * 31);
                    colors[offset + 2] = (byte)Mathf.RoundToInt(pixel.B * 31);
                }
                string actual = Convert.ToHexString(SHA256.HashData(colors));
                FailIf(actual != expected, $"Seasons native inventory blurb graphics/palette mismatch: {actual}.");
            }
            for (int season = 0; season < 4; season++)
            {
                context = new(0, 0, 0, season);
                screen.Open(); NextPage(); NextPage();
                if ((screen.ActiveCursor & 0x80) == 0) screen.MoveCursor(Vector2I.Right);
                screen.UpdateInventoryText(0);
                FailIf(screen.ActiveTextKey != 0x56 + season,
                    "Seasons' right-hand blurb must use TX_0956 plus the source season index.");
                CheckBlurb(golden[season]);
                screen.Close();
            }
            foreach (InventoryRoomContext forcedSpring in new[] { new InventoryRoomContext(0, 2, 0, 3), new InventoryRoomContext(0, 0, 0xff, 3) })
            {
                context = forcedSpring;
                screen.Open(); NextPage(); NextPage();
                CheckBlurb(golden[0]);
                screen.Close();
            }
            context = new(8, 4, 0, 3);
            screen.Open(); NextPage(); NextPage();
            screen.UpdateInventoryText(0);
            FailIf(screen.ActiveCursor != 0x81 || screen.ActiveTextKey != 0x52,
                "inventoryMenuState0 must start the hidden-blurb right cursor at heart pieces in Seasons.");
            CheckBlurb("91867EFEE6AFC013D5B32AE8F3177AFB3472D01509ECF3F4781151A79F9E2372");
            screen.MoveCursor(Vector2I.Up);
            FailIf(screen.ActiveCursor != 0x82 || !screen.SaveAndQuitSelected,
                "Seasons must skip its hidden blurb when cycling the right-hand cursor upward.");
            screen.MoveCursor(Vector2I.Down);
            FailIf(screen.ActiveCursor != 0x81, "The hidden blurb must remain unselectable after a full cursor cycle.");
            screen.Close();

            context = new(0, 0, 0, 0);
            foreach (bool batched in new[] { false, true })
            {
                // Keep the equipped Slingshot in storage for the next repeat.
                save.WriteField("wInventoryStorage", 0x13);
                save.WriteField("wInventoryA", 0);
                save.WriteField("wShooterSelectedSeeds", 0);
                inventory = new(treasures, save, runtimeState: memory);
                screen.Initialize(treasures, inventory, () => context);
                screen.Open();
                FailIf(screen.EquipToA() || !screen.ItemSubmenuActive || screen.ItemSubmenuReady,
                    "Two obtained seeds must select the shared Slingshot picker before swapping equipment.");
                if (batched) screen.UpdateItemSubmenu(14.0 / 60);
                else for (int update = 0; update < 14; update++) screen.UpdateItemSubmenu(1.0 / 60);
                FailIf(screen.ItemSubmenuReady, "The two-seed picker must retain its substate through source update $0e.");
                screen.UpdateItemSubmenu(1.0 / 60);
                FailIf(!screen.ItemSubmenuReady || screen.ItemSubmenuWidth != 8 || screen.ItemSubmenuHeight != 4,
                    "The two-seed picker must finish expansion at source update $0f in split and batched updates.");
                using (Image picker = screen.ComposeImage())
                    FailIf(picker.GetWidth() != 160 || picker.GetHeight() != 144, "The seed picker must render in the native screen-space viewport.");
                screen.MoveItemSubmenu(1);
                screen.UpdateInventoryText(0);
                FailIf(screen.ActiveTextKey != 0x38 || !screen.ConfirmItemSubmenu() || inventory.SlingshotSelectedSeeds != 1 ||
                    inventory.EquippedA != 0x13 || save.ReadField("wShooterSelectedSeeds") != 1,
                    "Confirming Scent Seeds must use native Slingshot text TX_0938 and persist the shared selection/equipment bytes.");
                screen.Close();
            }
            screen.Open(); NextPage();
            for (int cursor = 0; cursor < 16; cursor++) screen.MoveCursor(Vector2I.Right);
            memory.SetField("wInBoxingMatch", 1);
            FailIf(!screen.RingEquippingBlocked || screen.EquipSelectedRing() || inventory.ActiveRing != 0xff,
                "Seasons must reject ring equipment during a boxing match without changing its active ring.");
            memory.SetField("wInBoxingMatch", 0);
            FailIf(!screen.EquipSelectedRing() || inventory.ActiveRing != 0 || !screen.EquipSelectedRing() || inventory.ActiveRing != 0xff,
                "The shared ring equip action must resume and toggle repeatedly after the source boxing gate clears.");
        }
        finally { screen.Free(); }
    }
}
