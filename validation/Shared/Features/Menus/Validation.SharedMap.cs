using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedMap()
    {
        // Independent clean-US ROM goldens: native $0d/$0e/$0f headers decoded
        // with disasm tools/common.py, and literal PALH_07/$08/$09 source spans.
        string[] vramHashes = [
            "15A3CBE7AC15CD218A8B8936267D28CEEDEE543918DBDCB00A0F99256679A51A",
            "22B9D7B5EEF8FD5DA2D82256368A594D49CBEC0200BC9819B8F72EB96E930AE3"
        ];
        string[] paletteHashes = [
            "B124CAB2D54730FAC281E890FC15188440BA163B3250BFEC33752AE7FBC6B85E",
            "D5888EA13B74E730F0B987098B52F4878EE1C07E00564F4B50B8105410B64861"
        ];
        string[][] backgrounds = [
            ["165647DCFF181BB1D3D0A041D538179B1AF07B90B25347EE450B832E14851685", "27572067ECE8BD08C67B83298B6853AEF8B34B14F35E94E407F56B28FCF2DC13"],
            ["7CA20959F09F24A4B2EA911CD0C3ECC1B3D7CC34AB49C8A0B55CA689B33645A2", "47E4F613D2BC3B17D435F4C8E2516EC644B74AC4C1745F608E581D92E9915C47"]
        ];
        foreach (OracleGame game in new[] { OracleGame.Ages, OracleGame.Seasons })
        {
            bool seasons = game == OracleGame.Seasons;
            int gameIndex = seasons ? 1 : 0;
            string root = seasons ? SeasonsGameContent.AssetRoot : AgesGameContent.AssetRoot;
            var graphics = new List<byte>(); var palettes = new List<byte>();
            foreach (string mode in new[] { "present", "past", "dungeon" })
            {
                graphics.AddRange(OracleAssetCache.ReadBytes(root + $"map/vram_{mode}.bin"));
                palettes.AddRange(OracleAssetCache.ReadBytes(root + $"map/bg_{mode}.bin"));
                palettes.AddRange(OracleAssetCache.ReadBytes(root + $"map/spr_{mode}.bin"));
            }
            FailIf(Convert.ToHexString(SHA256.HashData(graphics.ToArray())) != vramHashes[gameIndex] ||
                Convert.ToHexString(SHA256.HashData(palettes.ToArray())) != paletteHashes[gameIndex],
                $"{game}: native MENU_MAP VRAM banks or RGB5 palette uploads differ from clean US.");
            var save = OracleSaveData.CreateStandardGame(game);
            var memory = OracleRuntimeState.CreateInitialized(game);
            var inventory = new InventoryState(TreasureDatabase.For(root), save, runtimeState: memory);
            var data = new MapDataDatabase(root);
            (int Selector, int Height, int Width, int To, int From)[] copies = seasons
                ? [(0,4,5,0x0a8,0x0b6), (1,4,5,0x0a8,0x0bb), (2,2,13,0x207,0x213), (3,2,13,0x20d,0x213),
                   (5,2,2,0x0e2,0x0f4), (6,2,1,0x1e4,0x1fe), (6,1,1,0x1f0,0x1ff), (7,2,1,0x168,0x178)]
                : [(0,3,3,0x068,0x075), (1,3,3,0x068,0x078), (2,2,13,0x207,0x213), (3,2,13,0x20d,0x213),
                   (5,2,3,0x045,0x07b), (6,2,3,0x0c3,0x0d5)];
            for (int selector = 0; selector <= (seasons ? 7 : 6); selector++)
            {
                byte[] map = Enumerable.Range(0, 0x400).Select(index => (byte)index).ToArray();
                byte[] flags = Enumerable.Range(0, 0x400).Select(index => (byte)(index >> 2)).ToArray();
                byte[] expectedMap = (byte[])map.Clone(), expectedFlags = (byte[])flags.Clone();
                foreach (var copy in copies.Where(copy => copy.Selector == selector))
                    for (int y = 0; y < copy.Height; y++)
                    for (int x = 0; x < copy.Width; x++)
                    {
                        expectedMap[copy.To + y * 32 + x] = expectedMap[copy.From + y * 32 + x];
                        expectedFlags[copy.To + y * 32 + x] = expectedFlags[copy.From + y * 32 + x];
                    }
                data.ApplySubstitution(selector, map, flags);
                FailIf(!map.SequenceEqual(expectedMap) || !flags.SequenceEqual(expectedFlags),
                    $"{game}: native substitution ${selector:x2} lost copy order, rectangle boundaries, attributes or an empty/fallthrough selector.");
            }
            FailIf(data.Geometry(0) != (seasons ? new MapGeometry(16, 16, 2, 1, 8, 8) : new MapGeometry(14, 14, 3, 2, 8, 8)) ||
                data.Geometry(1) != (seasons ? new MapGeometry(11, 8, 4, 5, 5, 4) : new MapGeometry(14, 14, 3, 2, 8, 8)),
                $"{game}: native overworld/alternate-world geometry or popup boundaries changed.");
            RoomSession rooms = seasons
                ? new(new SeasonsRoomContent(new SeasonsWorldData()), 0, 0, () => 0, () => { }, save, memory, countAsRoomEntry: false)
                : AgesRoomContent.CreateSession(0, 0, () => 0, () => { }, save, runtimeState: memory);
            for (int group = 0; group < 2; group++)
            for (int room = 0; room < 256; room++) save.SetRoomFlag(group, room, OracleSaveData.RoomFlagVisited);
            Color[,] before = rooms.BackgroundPalettes.Capture();
            var screen = new MapScreen { AssetRoot = root, Visible = false };
            AddChild(screen); screen.Initialize(rooms, inventory, () => memory.FrameCounter);
            try
            {
                screen.Open(debugFastTravel: true);
                for (int group = 0; group < 2; group++)
                {
                    if (group == 1) screen.CycleDebugPage();
                    // Read the actual background texture without adding production
                    // tracing or including blinking cursor/popup OAM in this golden.
                    var texture = (Texture2D)typeof(MapScreen).GetField("_background", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(screen)!;
                    using Image image = texture.GetImage();
                    byte[] rgb = new byte[160 * 144 * 3];
                    for (int y = 0; y < 144; y++)
                    for (int x = 0; x < 160; x++)
                    {
                        Color color = image.GetPixel(x, y); int at = (y * 160 + x) * 3;
                        rgb[at] = (byte)Mathf.RoundToInt(color.R * 31);
                        rgb[at + 1] = (byte)Mathf.RoundToInt(color.G * 31);
                        rgb[at + 2] = (byte)Mathf.RoundToInt(color.B * 31);
                    }
                    string actual = Convert.ToHexString(SHA256.HashData(rgb));
                    FailIf(actual != backgrounds[gameIndex][group], $"{game} map group ${group:x2}: native background pixels changed: {actual}.");
                    screen.SelectGaleDestination(0, []);
                    screen.MoveOverworldCursor(Vector2I.Left); screen.MoveOverworldCursor(Vector2I.Up);
                    MapGeometry geometry = data.Geometry(group);
                    FailIf(screen.CursorRoom != ((geometry.Height - 1) << 4 | geometry.Width - 1),
                        $"{game}: map cursor did not wrap within native group ${group:x2} dimensions.");
                    screen.MoveOverworldCursor(Vector2I.Right); screen.MoveOverworldCursor(Vector2I.Down);
                    FailIf(screen.CursorRoom != 0 || screen.TextPosition != 3,
                        $"{game}: map cursor return/text position changed.");
                    if (seasons && group == 1)
                    {
                        FailIf(!screen.TryGetFastTravelTarget(out int targetGroup, out int targetRoom) || targetGroup != 1 || targetRoom != 0,
                            "Subrosia map debug travel must resolve the admitted native group $01 room $00.");
                        screen.SelectGaleDestination(0x40, []);
                        FailIf(screen.TextPosition != 0, "Subrosia's textbox must move above row $04, not Ages' row $08.");
                    }
                    using Image composed = screen.ComposeImage();
                    FailIf(composed.GetWidth() != 160 || composed.GetHeight() != 144, "MENU_MAP must use the shared native viewport.");
                }
                screen.Close();
                CheckRestored();
                if (seasons) CheckSeasonsData();

                var fade = new ColorRect(); AddChild(fade);
                var lifecycle = new OracleMenuLifecycle(fade, _gameplayPause, static () => { });
                var menu = new MapMenuController(screen, _dialogue, lifecycle, () => true, () => true,
                    (_, _) => { }, _ => { }, _ => { });
                try
                {
                    for (int entry = 0; entry < 2; entry++)
                    {
                        Tick("map");
                        FailIf(!menu.IsActive || !_gameplayPause.IsLeased || screen.Visible,
                            $"{game}: map input must acquire the shared pause before loading its screen.");
                        for (int frame = 0; frame < 40 && !menu.IsOpen; frame++) Tick();
                        FailIf(!menu.IsOpen || !screen.Visible, $"{game}: shared map lifecycle did not complete opening.");
                        Tick("map");
                        for (int frame = 0; frame < 40 && menu.IsActive; frame++) Tick();
                        FailIf(menu.IsActive || screen.Visible || _gameplayPause.IsLeased,
                            $"{game}: map closing/re-entry retained a screen or modal lease.");
                        CheckRestored();
                    }
                }
                finally { if (menu.IsActive) menu.CloseImmediatelyForValidation(); fade.Free(); }

                void Tick(string? pressed = null)
                {
                    string[] actions = pressed is null ? [] : [pressed];
                    Input.BeginOriginalUpdate(new ApplicationInputSnapshot(actions, actions, Vector2.Zero));
                    try { menu.Update(1.0 / 60); }
                    finally { Input.EndOriginalUpdate(); }
                }
                void CheckRestored()
                {
                    Color[,] after = rooms.BackgroundPalettes.Capture();
                    for (int slot = 0; slot < 8; slot++)
                    for (int shade = 0; shade < 4; shade++)
                        FailIf(after[slot, shade] != before[slot, shade], $"{game}: shared map close did not restore BG{slot}/{shade}.");
                }
                void CheckSeasonsData()
                {
                    var presentation = new MapPresentationState(save, inventory);
                    save.WriteWramByte(0xc6e6, 0xa7); save.SetMakuMapTextPresent(2);
                    FailIf(save.ReadWramByte(0xc6e5) != 2 || save.ReadWramByte(0xc6e6) != 0xa7 ||
                        presentation.ResolveMakuAdviceTextId(0) != 0x0317,
                        "Seasons Maku map state must use native $c6e5 and retain the unmet TX_0317 gate.");
                    save.SetGlobalFlag(0x18);
                    FailIf(presentation.ResolveMakuAdviceTextId(0) != 0x1702 ||
                        !data.TryResolveAreaText(rooms, presentation, 0, 0xc9, out MapText advice) || advice.TextId != 0x1702,
                        "Seasons map advice must resolve native TX_17XX, not Ages' TX_05XX.");
                    save.SetRoomFlag(1, 0xaf, OracleSaveData.RoomFlagVisited, false);
                    FailIf(screen.ResolvePopupTypeForValidation(2, 0, 0) != 0 || screen.ResolvePopupTypeForValidation(4, 0, 0) != 4 ||
                        screen.ResolvePopupTypeForValidation(0x0c, 0, 0x5e) != 0x0c || screen.ResolvePopupTypeForValidation(0x0c, 0, 0xe8) != 0x0d ||
                        screen.ResolvePopupTypeForValidation(0x0e, 1, 0x28) != 0x11 || screen.ResolvePopupTypeForValidation(0x0e, 1, 0x08) != 0x12 ||
                        screen.ResolvePopupTypeForValidation(0x0e, 1, 0x0a) != 0x13 || screen.ResolvePopupTypeForValidation(0x0e, 1, 0x18) != 0x14,
                        "Seasons popup dispatch must preserve the native shop and four temple types.");
                    save.SetGlobalFlag(0x16);
                    FailIf(screen.ResolvePopupTypeForValidation(0x0d, 0, 0x5b) != 0x10,
                        "Seasons Moblin popup must read native GLOBALFLAG $16.");
                    save.SetRoomFlag(1, 0xaf, OracleSaveData.RoomFlagVisited);
                    FailIf(screen.ResolvePopupTypeForValidation(2, 1, 0xaf) != 0x0e ||
                        !data.TryResolveAreaText(rooms, presentation, 0, 0xc5, out MapText shop) || shop.TextId != 0x032c,
                        "Seasons Advance Shop must use visited room $01:$af for popup and TX_032c.");
                    save.SetRoomFlag(1, 0xaf, OracleSaveData.RoomFlagVisited, false);
                    FailIf(!data.TryResolveAreaText(rooms, presentation, 0, 0xc5, out shop) || shop.TextId != 0x0321,
                        "Seasons Advance Shop must restore TX_0321 when its native visit flag is clear.");
                    FailIf(screen.ResolvePopupTypeForValidation(0x0b, 0, 0xee) != 0x0b ||
                        screen.ResolvePopupTypeForValidation(0x0b, 0, 0xe2) != 0,
                        "Seasons pirate popup must retain the ship's original locations before GLOBALFLAG $17.");
                    save.SetGlobalFlag(0x17);
                    FailIf(screen.ResolvePopupTypeForValidation(0x0b, 0, 0xee) != 0 ||
                        screen.ResolvePopupTypeForValidation(0x0b, 0, 0xe2) != 0x0b,
                        "Seasons pirate popup must move with native GLOBALFLAG $17.");
                    FailIf(data.JewelRooms(save).Any(), "Jewel markers require native TREASURE_TREASURE_MAP $4b.");
                    save.WriteField("wObtainedTreasureFlags", (byte)(save.ReadField("wObtainedTreasureFlags", 9) | 8), 9);
                    FailIf(!data.JewelRooms(save).SequenceEqual([0xb5, 0x1d, 0xc2, 0xf4]), "Unlinked Seasons jewel markers must use the native four-room table.");
                    save.SetLinkedGame(true);
                    FailIf(!data.JewelRooms(save).SequenceEqual([0xb5, 0x7e, 0xa7, 0xf4]), "Linked Seasons jewel markers must use the alternate four-room table.");
                    save.WriteField("wInsertedJewels", 1);
                    save.WriteField("wObtainedTreasureFlags", (byte)(save.ReadField("wObtainedTreasureFlags", 9) | 0x20), 9);
                    FailIf(!data.JewelRooms(save).SequenceEqual([0xa7, 0xf4]), "Inserted and owned jewels must disappear independently from the shared map.");
                    save.SetLinkedGame(false);
                    var trees = new GaleTreeWarpDatabase(root);
                    FailIf(trees.Get(rooms, 0) != new GaleTreeWarp(0x10, 0x34, 0x18) ||
                        trees.Get(rooms, 5) != new GaleTreeWarp(0xf8, 0x33, 0x15) || trees.Get(rooms, 6).Room != 0 || trees.Get(rooms, 7).Room != 0,
                        "Seasons must retain its six native Gale trees and two terminators without Ages' planting offset.");
                }
            }
            finally { screen.Free(); }
        }
        GD.Print("Validated both native map packs, independent clean-US VRAM/palette/background goldens, dimensions, wrapping, Seasons dispatch/advice/Gale data and repeated shared modal lifecycle.");
    }
}
