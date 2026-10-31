using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedHudCurrency(FrontendValidationRoot root, bool batched)
    {
        var engine = root._gameplaySession!;
        var rooms = engine.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var memory = rooms.RuntimeState;
        var inventory = root._scene.Player.Inventory;
        var hud = root._scene.Hud;
        var status = engine.StatusBar;
        var game = rooms.SaveData.Game;
        bool ages = game == OracleGame.Ages;
        int group = rooms.ActiveGroup, room = rooms.CurrentRoom.Id;
        int originalRupees = inventory.Rupees, originalOre = inventory.OreChunks;
        int originalA = inventory.EquippedA, originalB = inventory.EquippedB;
        var buildMap = typeof(Hud).GetMethod("BuildStatusMap", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var romBytes = ValidationRom.LoadCleanUs(game);
        var app = new ApplicationValidationFixture(root);
        void Travel(int targetGroup, int targetRoom)
        {
            if (ages) root.LoadDebugRoom(targetGroup,targetRoom);
            else root._seasonsOpening!.DebugTravel(targetGroup,targetRoom);
            entities.Clear();
            var player = root._scene.Player;
            Vector2 floor = Enumerable.Range(0,80).Select(index => new Vector2(index % 10 * 16 + 8,index / 10 * 16 + 8))
                .First(point => point.X is >= 24 and <= 136 && point.Y is >= 24 and <= 104 &&
                    !player.World.Collides(point) && player.World.GetActiveTerrain(point).Terrain.Hazard == HazardType.None);
            player.WarpTo(floor);
        }
        void Money(string field, int value)
        {
            if (field == "wNumRupees") { inventory.AddRupees(value-inventory.Rupees); return; }
            if (field == "wNumOreChunks")
            {
                rooms.SaveData.WriteField(field,(byte)(value % 10 | value / 10 % 10 << 4));
                rooms.SaveData.WriteField(field,(byte)(value / 100),1);
                return;
            }
            memory.SetField(field,(byte)(value % 10 | value / 10 % 10 << 4));
            memory.SetField(field,(byte)(value / 100),1);
        }
        try
        {
            Travel(0,ages ? 0x45 : 0xb6);
            inventory.EquipA(0); inventory.EquipB(0);
            inventory.SetScriptedHealth(inventory.MaxHealthQuarters); status.SynchronizeHealth();
            Money("wNumRupees",128);
            if (!ages) Money("wNumOreChunks",465);
            status.SynchronizeRupees();
            var sounds = root._sound.AttachPlayRequestAudit();
            var native = new BraceletRom(rooms.CurrentRoom,root._scene.Player.Position,0,0xff,game:game);
            native.LoadSave(rooms.SaveData); native.InitializeHud(inventory.HealthQuarters,128);
            native.UpdateHud(memory.FrameCounter);
            sounds.Clear(); native.ClearSounds();
            Money("wNumRupees",137); native.LoadSave(rooms.SaveData);
            app.Step(9,Vector2.Zero,batched:batched,afterUpdate:() =>
            {
                native.UpdateHud(memory.FrameCounter);
                int address = ages ? 0xcbe5 : 0xcbe6;
                FailIf(memory.ReadField("wDisplayedRupees") != native[address] ||
                    memory.ReadField("wDisplayedRupees",1) != native[address+1] ||
                    !sounds.Requests.SequenceEqual(native.Sounds),
                    $"{game}: live HUD BCD ${memory.ReadField("wDisplayedRupees",1):x2}{memory.ReadField("wDisplayedRupees"):x2} " +
                    $"!= native ${native[address+1]:x2}{native[address]:x2}; cues {string.Join(',',sounds.Requests)} != {string.Join(',',native.Sounds)}.");
            });
            // Both typed display and rasterization must observe raw native writes.
            Money("wDisplayedRupees",312);
            Money("wNumRupees",312);
            app.Step(1,Vector2.Zero,batched:batched);
            FailIf(status.DisplayedRupees != 312 || hud.Rupees != 312,
                $"{game}: a raw displayed-money write must reach the authoritative HUD view.");

            if (!ages)
            {
                Travel(1,0x00);
                FailIf((rooms.CurrentRoom.TilesetFlags & 0x80) == 0 || memory.ReadWramByte(0xcbe5) != 0xa7 ||
                    status.DisplayedRupees != 465 || hud.Rupees != 465,
                    "Seasons: native Subrosia room initialization must select ore and snap only the changed money source.");
                native = new BraceletRom(rooms.CurrentRoom,root._scene.Player.Position,0,0xff,game:game);
                native.LoadSave(rooms.SaveData); native.InitializeHud(inventory.HealthQuarters,312);
                native.LoadCommonGraphics();
                FailIf(native[0xcbe5] != 0xa7 || native.Word(0xcbe6) != 0x0465,
                    "Seasons native loadCommonGraphics_body must select $c6a7 and copy ore BCD.");
                Money("wNumOreChunks",469); native.LoadSave(rooms.SaveData);
                sounds.Clear(); native.ClearSounds();
                app.Step(4,Vector2.Zero,batched:batched,afterUpdate:() =>
                {
                    native.UpdateHud(memory.FrameCounter);
                    FailIf(memory.ReadField("wDisplayedRupees") != native[0xcbe6] ||
                        !sounds.Requests.SequenceEqual(native.Sounds),
                        $"Seasons: ore BCD ${memory.ReadField("wDisplayedRupees",1):x2}{memory.ReadField("wDisplayedRupees"):x2} != " +
                        $"native ${native[0xcbe7]:x2}{native[0xcbe6]:x2}, cues {string.Join(',',sounds.Requests)} != {string.Join(',',native.Sounds)}, " +
                        $"room ${rooms.ActiveGroup:x2}:${rooms.CurrentRoom.Id:x2}, selector ${memory.ReadWramByte(0xcbe5):x2}, ore {inventory.OreChunks}.");
                });
                Money("wNumOreChunks",480);
                engine.Menus.Inventory.OpenImmediatelyForValidation();
                app.Step(1,Vector2.Zero,batched:batched);
                FailIf(status.DisplayedRupees != 471,
                    "Seasons: inventory initialization must update once in common graphics and once at the menu tail without snapping.");
                using (Image menu = root._scene.InventoryScreen.ComposeImage())
                using (Image strip = hud.ComposeImage())
                    for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 160; x++)
                        FailIf(menu.GetPixel(x,y) != strip.GetPixel(x,y),
                            $"Seasons: inventory must retain the actual selected ore HUD at {x},{y}.");
                engine.Menus.Inventory.CloseImmediatelyForValidation();
                Travel(0,0xb6);
                FailIf(memory.ReadWramByte(0xcbe5) != 0xa5 || status.DisplayedRupees != 312,
                    "Seasons: returning from Subrosia must restore rupees through the common room loader.");
            }

            // Native table, key/large-indoor gates, extra-heart offsets and every icon pixel.
            foreach (int maximum in new[] { 12,60 })
            foreach (byte flags in new byte[] { 0x00,0x08,0x18,0x80,0x88,0x98 })
            {
                var save = OracleSaveData.CreateStandardGame(game);
                save.WriteField("wLinkMaxHealth",(byte)maximum); save.WriteField("wLinkHealth",(byte)maximum);
                save.WriteField("wDungeonSmallKeys",7,1);
                var ownInventory = new InventoryState(TreasureDatabase.For(rooms.AssetRoot),save);
                var probe = new Hud { AssetRoot = rooms.AssetRoot, TilesetFlags = flags, DungeonIndex = 1 };
                root.AddChild(probe);
                try
                {
                    probe.Initialize(TreasureDatabase.For(rooms.AssetRoot),ownInventory);
                    probe.HealthQuarters = probe.MaxHealthQuarters = maximum;
                    probe.Rupees = 128; probe.LoadCommonGraphics(flags);
                    var expected = new BraceletRom(rooms.CurrentRoom,Vector2.Zero,0,0xff,game:game);
                    expected.LoadSave(save); expected.InitializeHud(maximum,128);
                    expected[0xcc34] = flags; expected[0xcc39] = 1;
                    expected.UpdateHud(0);
                    var map = (byte[])buildMap.Invoke(probe,null)!;
                    int offset = maximum >= 57 ? -1 : 0;
                    for (int column = 10 + offset; column <= 12 + offset; column++)
                        FailIf(map[column] != expected.HudTile(column),
                            $"{game}: HUD keys flags=${flags:x2}, maximum=${maximum:x2}, tile=${column:x2} differs from native.");
                    if (!ages && (flags & 0x88) != 0)
                    {
                        using Image image = probe.ComposeImage();
                        int graphics = 0x6fc20 + ((flags & 8) != 0 ? 0 : 16);
                        int palette = 0x16 * 0x4000 + 0x830;
                        for (int y = 0; y < 8; y++)
                        for (int x = 0; x < 8; x++)
                        {
                            int shade = (romBytes.Span[graphics+y*2] >> (7-x) & 1) |
                                (romBytes.Span[graphics+y*2+1] >> (7-x) & 1) << 1;
                            int color = romBytes.Span[palette+shade*2] | romBytes.Span[palette+shade*2+1] << 8;
                            Color actual = image.GetPixel((10+offset)*8+x,y);
                            int actual555 = Mathf.RoundToInt(actual.R*31) | Mathf.RoundToInt(actual.G*31) << 5 |
                                Mathf.RoundToInt(actual.B*31) << 10;
                            FailIf(actual.A != 1 || actual555 != color,
                                $"Seasons: native $1b:$7c20 key/ore icon differs at {x},{y}, flags=${flags:x2}, maximum=${maximum:x2}.");
                        }
                        probe.TilesetFlags = 0; probe.Refresh();
                        using Image retained = probe.ComposeImage();
                        for (int y = 0; y < 8; y++)
                        for (int x = 0; x < 8; x++)
                            FailIf(retained.GetPixel((10+offset)*8+x,y) != image.GetPixel((10+offset)*8+x,y),
                                "Seasons: live room context changes must not implicitly replace retained common VRAM.");
                        probe.LoadCommonGraphics(0);
                        using Image restored = probe.ComposeImage();
                        bool differs = false;
                        for (int y = 0; y < 8; y++)
                        for (int x = 0; x < 8; x++)
                            differs |= restored.GetPixel((10+offset)*8+x,y) != image.GetPixel((10+offset)*8+x,y);
                        FailIf(!differs,"Seasons: reloading overworld graphics must replace the retained key/ore tile with the rupee graphic.");
                    }
                }
                finally { probe.Free(); }
            }
        }
        finally
        {
            if (engine.Menus.Inventory.IsActive) engine.Menus.Inventory.CloseImmediatelyForValidation();
            Money("wNumRupees",originalRupees);
            if (!ages) Money("wNumOreChunks",originalOre);
            inventory.EquipA(originalA); inventory.EquipB(originalB);
            Travel(group,room);
            status.SynchronizeHealth(); status.SynchronizeRupees();
        }
    }
}
