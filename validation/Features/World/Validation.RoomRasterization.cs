using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private async Task ValidateRoomRasterization()
    {
        if (Array.IndexOf(OS.GetCmdlineUserArgs(), "--profile-lynna-entry") >= 0)
        {
            ProfileLynnaFirstEntry();
            return;
        }
        // Independent, pixel-by-pixel GBC BG reference: signed tile addressing,
        if (DisplayServer.GetName() != "headless") await ValidateClearTilePresentation();
        // attribute palette/flips, and the live VRAM bytes at this boundary.
        void Verify(OracleRoomData room, bool firstPublication = false)
        {
            using Image vram = room.CaptureLiveGraphics();
            using Image expected = Image.CreateEmpty(room.Width, room.Height, false, Image.Format.Rgba8);
            for (int y = 0; y < room.Height; y++)
            for (int x = 0; x < room.Width; x++)
            {
                int tile = room.GetBackgroundSubtileForValidation(x / 8, y / 8) ^ 0x80;
                int attribute = room.GetBackgroundAttributeForValidation(x / 8, y / 8);
                int px = (attribute & 0x20) != 0 ? 7 - x % 8 : x % 8;
                int py = (attribute & 0x40) != 0 ? 7 - y % 8 : y % 8;
                float red = vram.GetPixel(tile % 16 * 8 + px, tile / 16 * 8 + py).R;
                int shade = Mathf.Clamp(Mathf.RoundToInt((1 - red) * 3), 0, 3);
                expected.SetPixel(x, y, room.BackgroundPalettes.Resolve(attribute & 7, shade));
            }
            // Inspect the CPU result directly: the dummy rendering backend
            // may retain a prior readback within one synchronous host frame.
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var headers = (int[])typeof(OracleRoomData).GetField("_activeAnimationHeaders", flags)!.GetValue(room)!;
            using Image actual = (Image)typeof(OracleRoomData).GetMethod("RenderRoom", flags)!.Invoke(room, [headers, 0])!;
            FailIf(!expected.GetData().SequenceEqual(actual.GetData()),
                $"Room {room.Group:x1}:{room.Id:x2} raster differs from signed BG tile/attribute reference.");
            if (firstPublication)
            {
                using Image published = room.Texture.GetImage();
                FailIf(!expected.GetData().SequenceEqual(published.GetData()),
                    $"Room {room.Group:x1}:{room.Id:x2} first texture publication used stale preparation state.");
            }
            var clearTile = (RoomClearTile)typeof(OracleRoomData).GetField("_clearTile", flags)!.GetValue(room)!;
            using Image clear = clearTile.Render();
            Image hud = OracleGraphicsCache.LoadImage("res://assets/oracle/gfx/gfx_hud.png");
            using Image expectedClear = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                int shade = Mathf.RoundToInt((1 - hud.GetPixel(x % 8, y % 8).R) * 3);
                expectedClear.SetPixel(x, y, room.BackgroundPalettes.Resolve(0, shade));
            }
            FailIf(!expectedClear.GetData().SequenceEqual(clear.GetData()),
                $"Room {room.Group:x1}:{room.Id:x2} cleared BG tilemap changed.");
            if (firstPublication)
            {
                using Image publishedClear = room.ClearedTilemapTexture.GetImage();
                FailIf(!expectedClear.GetData().SequenceEqual(publishedClear.GetData()),
                    $"Room {room.Group:x1}:{room.Id:x2} first clear texture used a stale BG0 palette.");
            }
        }

        // Room preparation may change palette, layout, animation and shared
        // VRAM before any view asks for a texture. Verify that the first GPU
        // publication uses their final state against the per-pixel reference.
        const System.Reflection.BindingFlags privateFields =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        foreach (var id in new[] { (2, 0x5e), (4, 0x07) })
        {
            var world = new OracleWorldData();
            OracleRoomData prepared = world.LoadRoom(id.Item1, id.Item2);
            world.SetCurrentPaletteRoom(prepared);
            prepared.SetPositionTileAndCollision(new Vector2(8, 8), prepared.Layout[1], 0, 0);
            prepared.UpdateAnimation(16);
            using Image live = prepared.CaptureLiveGraphics();
            live.Fill(Colors.Black);
            prepared.SetLiveGraphics(live, true);
            Color[,] palette = world.BackgroundPalettes.Capture();
            palette[0, 3] = Color.Color8(255, 0, 0);
            palette[3, 3] = Color.Color8(0, 255, 0);
            world.BackgroundPalettes.Restore(palette);
            FailIf(typeof(OracleRoomData).GetField("_texture", privateFields)!.GetValue(prepared) is not null ||
                typeof(OracleRoomData).GetField("_clearedTilemapTexture", privateFields)!.GetValue(prepared) is not null ||
                typeof(OracleRoomData).GetField("_renderPixels", privateFields)!.GetValue(prepared) is not null,
                $"Room {id.Item1:x1}:{id.Item2:x2} preparation eagerly rasterized or published a texture.");
            Verify(prepared, firstPublication: true);
            Texture2D retained = prepared.Texture;
            int publications = 0;
            void CountPublication() => publications++;
            retained.Changed += CountPublication;
            live.Fill(Colors.White);
            prepared.RedrawForPaletteChange();
            retained.Changed -= CountPublication;
            FailIf(!ReferenceEquals(retained, prepared.Texture) || publications != 1,
                "An acquired room texture lost its identity or immediate VRAM publication boundary.");
            publications = 0;
            retained.Changed += CountPublication;
            try
            {
                using (world.DeferTexturePublication())
                {
                    live.Fill(Colors.Black);
                    prepared.RedrawForPaletteChange();
                    using (world.DeferTexturePublication())
                    {
                        palette[3, 3] = Color.Color8(0, 0, 255);
                        world.BackgroundPalettes.Restore(palette);
                        prepared.SetPositionTileAndCollision(new Vector2(8, 8), 0, 0x0f, 0);
                    }
                    FailIf(publications != 0 || prepared.Layout[0] != 0 ||
                        world.BackgroundPalettes.Resolve(3, 3) != palette[3, 3],
                        "Nested presentation preparation delayed logical state or published an intermediate image.");
                }
                FailIf(publications != 1, "Prepared palette/layout/VRAM did not publish exactly once.");
                // The dummy backend caches a prior same-frame GetImage result.
                // Keep the CPU reference in headless checks; exercise repeated
                // texture readback with the real renderer.
                Verify(prepared, firstPublication: DisplayServer.GetName() != "headless");
                // Unwinding must publish the accepted state and restore ordinary
                // immediate publication, rather than leaving a stuck scope.
                try
                {
                    using var scope = world.DeferTexturePublication();
                    live.Fill(Colors.White);
                    prepared.RedrawForPaletteChange();
                    throw new OperationCanceledException();
                }
                catch (OperationCanceledException) { }
                FailIf(publications != 2, "Cancelled preparation failed to publish accepted VRAM state.");
                prepared.RedrawForPaletteChange();
                FailIf(publications != 3, "Preparation cancellation left texture publication suspended.");
            }
            finally { retained.Changed -= CountPublication; }
            prepared.SetLiveGraphics(null, false);
        }

        foreach (var id in new[] { (0, 0x6a), (0, 0x6b), (4, 0x07) })
        {
            LoadValidationRoom(id.Item1, id.Item2);
            Verify(_currentRoom);
            ValidateClearedTilemapDependencies(_currentRoom, room => Verify(room));
            for (int tick = 1; tick <= 32; tick++) _currentRoom.UpdateAnimation(tick);
            Verify(_currentRoom);
        }
        foreach (bool entering in new[] { true, false })
        {
            LoadValidationRoom(0, entering ? 0x6a : 0x6b);
            OracleRoomData source = _currentRoom;
            _transitions.BeginScroll(_player, entering ? Vector2I.Right : Vector2I.Left, entering ? 0x6b : 0x6a);
            OracleRoomData target = _currentRoom;
            int total = _transitions.ScrollTotalFrames;
            for (int tick = 1; tick <= total; tick++)
            {
                UpdateScrollingTransition(1.0 / 60);
                if (tick is 1 or 3 or 8 or 16 or 32 || tick == total)
                {
                    if (tick != total) Verify(source);
                    Verify(target);
                }
            }
            GD.Print($"Yoll {(entering ? "entry" : "exit")}: room and cleared-tilemap reference pixels match.");
        }
        ValidateRoomTextureInvalidation(room => Verify(room));
        ValidateUnderwaterPaletteInvalidation();
        ValidateShopPriceRasterization();
        MeasureRoomAndSpritePreparation();
    }

    private static void ValidateShopPriceRasterization()
    {
        var world = new OracleWorldData();
        OracleRoomData room = world.LoadRoom(2, 0x5e);
        world.SetCurrentPaletteRoom(room);
        Image source = OracleGraphicsCache.LoadImage("res://assets/oracle/inventory/gfx_inventory_hud_1.png");
        for (int palette = 0; palette < 8; palette++)
        for (int digit = 0; digit < 10; digit++)
        {
            using Texture2D texture = room.BuildBackgroundTileTexture(source, 0x10 + digit, palette);
            using Image actual = texture.GetImage();
            using Image expected = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
            // shopItem.s:shopItemGetTilesForRupeeDisplay selects $30 + BCD
            // digit; TREE_GFXH_03 maps that tile to inventory sheet $10.
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
            {
                float red = source.GetPixel(digit * 8 + x, 8 + y).R;
                int shade = Mathf.Clamp(Mathf.RoundToInt((1 - red) * 3), 0, 3);
                expected.SetPixel(x, y, room.BackgroundPalettes.Resolve(palette, shade));
            }
            FailIf(!expected.GetData().SequenceEqual(actual.GetData()),
                $"Shop digit {digit} palette {palette} differs from the BG tile reference.");
        }
        // Caller-owned images can change between requests and must not reuse
        // the immutable imported-image snapshot.
        using Image mutable = Image.CreateEmpty(128, 16, false, Image.Format.Rgba8);
        foreach (Color color in new[] { Colors.White, Colors.Black })
        {
            mutable.Fill(color);
            using Texture2D texture = room.BuildBackgroundTileTexture(mutable, 0x10, 6);
            using Image actual = texture.GetImage();
            using Image expected = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
            expected.Fill(room.BackgroundPalettes.Resolve(6, color == Colors.White ? 0 : 3));
            FailIf(!expected.GetData().SequenceEqual(actual.GetData()), "Mutable shop tile used a stale pixel snapshot.");
        }
        for (int run = 1; run <= 2; run++)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int repeat = 0; repeat < 128; repeat++)
            for (int digit = 0; digit < 10; digit++)
                using (room.BuildBackgroundTileTexture(source, 0x10 + digit, 6)) { }
            timer.Stop();
            GD.Print(FormattableString.Invariant($"SHOP_PRICE_RASTER run={run} tiles=1280 elapsed_ms={timer.Elapsed.TotalMilliseconds:F3} allocated_bytes={GC.GetAllocatedBytesForCurrentThread() - allocated}"));
        }
    }

    private void ProfileLynnaFirstEntry()
    {
        bool prepareConstructors = Array.IndexOf(OS.GetCmdlineUserArgs(), "--profile-prepared-constructors") >= 0;
        if (prepareConstructors)
            foreach (bool _ in new GameplayScriptResources().PrepareNativeConstructors()) { }
        GD.Print($"LYNNA_CONSTRUCTORS prepared={prepareConstructors}");
        // Run before raster/digit fixtures can warm the measured destination.
        // Direct transition entry isolates load work; this is not a movement
        // or collision-geometry regression and does not model Android GPU work.
        GD.Print($"LYNNA_PROFILE fixture=initialized script_retention={Array.IndexOf(OS.GetCmdlineUserArgs(), "--validate-no-script-retention") < 0} renderer={RenderingServer.GetCurrentRenderingMethod()} scope=cpu-not-presented-frame");
        foreach (int source in new[] { 0x69 })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
            _saveData.SetGlobalFlag(GlobalFlag.PregameIntroDone);
            LoadValidationRoom(0, source);
            for (int crossing = 0; crossing < 3; crossing++)
            {
                bool entering = crossing != 1;
                int destination = entering ? source - 1 : source;
                var timer = System.Diagnostics.Stopwatch.StartNew();
                _transitions.BeginScroll(_player, entering ? Vector2I.Left : Vector2I.Right, destination);
                timer.Stop();
                GD.Print(FormattableString.Invariant($"LYNNA_ENTRY destination=0:{destination:x2} crossing={crossing} setup_ms={timer.Elapsed.TotalMilliseconds:F3}"));
                int total = _transitions.ScrollTotalFrames;
                for (int tick = 0; tick < total; tick++) UpdateScrollingTransition(1.0 / 60);
                FailIf(_transitions.IsTransitioning || _rooms.ActiveGroup != 0 || _currentRoom.Id != destination,
                    $"Lynna diagnostic crossing did not finish at 0:{destination:x2}.");
                if (!entering) continue;
                Vector2 door = new(0x58, 0x28);
                FailIf(!WarpDatabase.Shared.TryGetTileWarp(0, _currentRoom, 0x25,
                    _currentRoom.GetMetatile(door), out Warp shop) ||
                    shop.DestinationGroup != 2 || shop.DestinationRoom != 0x5e,
                    "Lynna profile requires the imported $0:$68/$25 -> $2:$5e doorway.");
                MeasureWarp(shop, crossing == 0 ? "first" : "repeat");
                FailIf(!WarpDatabase.Shared.TryGetEdgeWarp(2, 0x5e, Vector2I.Down,
                    new Vector2(0x28, 0x78), new Vector2(160, 128), out Warp exit) ||
                    exit.DestinationGroup != 0 || exit.DestinationRoom != 0x68,
                    "Lynna profile requires the imported $2:$5e -> $0:$68 exit.");
                MeasureWarp(exit, "exit");
            }
        }

        void MeasureWarp(Warp warp, string pass)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            _transitions.ApplyWarp(_player, warp);
            double longest = timer.Elapsed.TotalMilliseconds;
            int updates = 0;
            while (_transitions.IsTransitioning && updates < 256)
            {
                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                _transitions.UpdateWarp(1.0 / 60);
                longest = Math.Max(longest, System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                updates++;
            }
            timer.Stop();
            FailIf(_transitions.IsTransitioning || _rooms.ActiveGroup != warp.DestinationGroup ||
                _currentRoom.Id != warp.DestinationRoom, "Lynna profile warp did not complete at its imported destination.");
            GD.Print(FormattableString.Invariant($"LYNNA_WARP pass={pass} destination={warp.DestinationGroup:x1}:{warp.DestinationRoom:x2} updates={updates} cpu_total_ms={timer.Elapsed.TotalMilliseconds:F3} max_call_ms={longest:F3}"));
        }
    }

    private static void MeasureRoomAndSpritePreparation()
    {
        var world = new OracleWorldData();
        OracleRoomData room = world.LoadRoom(2, 0xea);
        world.SetCurrentPaletteRoom(room);
        const System.Reflection.BindingFlags rasterFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var raster = typeof(OracleRoomData).GetMethod("RenderRoom", rasterFlags)!;
        var headers = typeof(OracleRoomData).GetField("_activeAnimationHeaders", rasterFlags)!;
        using (Image warmup = (Image)raster.Invoke(room, [headers.GetValue(room), 0])!) { }
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        var rasterTimer = System.Diagnostics.Stopwatch.StartNew();
        for (int repeat = 0; repeat < 256; repeat++)
            using ((Image)raster.Invoke(room, [headers.GetValue(room), 0])!) { }
        rasterTimer.Stop();
        GD.Print($"ROOM_RASTER frames=256 elapsed_ms={rasterTimer.Elapsed.TotalMilliseconds:F3} allocated_bytes={GC.GetAllocatedBytesForCurrentThread() - allocated}");
        var availabilityTimer = System.Diagnostics.Stopwatch.StartNew();
        for (int repeat = 0; repeat < 8192; repeat++)
            FailIf(!world.HasRoom(2, 0xea), "Room 2:ea disappeared from generated data.");
        availabilityTimer.Stop();
        GD.Print($"ROOM_AVAILABILITY checks=8192 elapsed_ms={availabilityTimer.Elapsed.TotalMilliseconds:F3}");
        int uploads = 0;
        void CountUpload() => uploads++;
        room.Texture.Changed += CountUpload;
        try
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            for (int repeat = 0; repeat < 256; repeat++)
                world.SetCurrentPaletteRoom(world.LoadRoom(2, 0xea));
            timer.Stop();
            GD.Print($"ROOM_PREPARATION cached_loads=256 elapsed_ms={timer.Elapsed.TotalMilliseconds:F3} uploads={uploads}");
            FailIf(uploads != 0, "Unchanged cached room 2:ea rebuilt its room texture.");
            uploads = 0;
            timer.Restart();
            for (int repeat = 0; repeat < 256; repeat++)
                world.BackgroundPalettes.LoadTextboxPalette((repeat & 1) == 0 ? 0 : 4);
            timer.Stop();
            GD.Print($"ROOM_PREPARATION textbox_palettes=256 elapsed_ms={timer.Elapsed.TotalMilliseconds:F3} uploads={uploads}");
            FailIf(uploads != 0, "Textbox BG1 changes rebuilt room 2:ea, which uses BG2-5 and BG7.");
        }
        finally
        {
            room.Texture.Changed -= CountUpload;
        }
        Image source = OracleGraphicsCache.LoadImage("res://assets/oracle/gfx/spr_impa.png");
        const string oam = "8,0,0,0;8,8,2,32";
        var spriteTimer = System.Diagnostics.Stopwatch.StartNew();
        for (int repeat = 0; repeat < 256; repeat++)
        {
            using Texture2D fixedFrame = NpcCharacter.BuildOamTextureUncachedForValidation(source, oam, 0, 1);
            var positioned = NpcCharacter.BuildPositionedOamTextureUncachedForValidation(source, oam, 0, 1, null, true);
            positioned.Texture.Dispose();
        }
        spriteTimer.Stop();
        GD.Print($"SPRITE_PREPARATION uncached_frames=512 elapsed_ms={spriteTimer.Elapsed.TotalMilliseconds:F3}");
    }

    private static void ValidateRoomTextureInvalidation(Action<OracleRoomData> verify)
    {
        var world = new OracleWorldData();
        OracleRoomData room = world.LoadRoom(2, 0xea);
        world.SetCurrentPaletteRoom(room);
        Color[,] original = world.BackgroundPalettes.Capture();
        Color[,] changed = (Color[,])original.Clone();
        int uploads = 0;
        void CountUpload() => uploads++;
        room.Texture.Changed += CountUpload;
        try
        {
            // initTextbox writes BG1; Blossom's room has no BG1 mappings.
            changed[1, 0] = new Color(1, 0, 1, 0.5f);
            world.BackgroundPalettes.Restore(changed);
            FailIf(uploads != 0, "Unused textbox BG1 redrew room 2:ea.");
            verify(room);
            changed[3, 0] = new Color(0, 1, 0, 0.5f);
            world.BackgroundPalettes.Restore(changed);
            FailIf(uploads != 1, "Used BG3 did not redraw room 2:ea exactly once.");
            verify(room);
            world.BackgroundPalettes.Restore(original);
            uploads = 0;
            world.SetCurrentPaletteRoom(world.LoadRoom(2, 0xea));
            FailIf(uploads != 0, "Cached room activation repeated an unchanged graphics/palette upload.");

            byte tile = room.GetBackgroundSubtileForValidation(0, 0);
            byte attribute = (byte)((room.GetBackgroundAttributeForValidation(0, 0) & ~7) | 1);
            room.SetBackgroundMappingRectangle(Vector2I.Zero, 1, new byte[] { tile, attribute }, 0);
            uploads = 0;
            world.BackgroundPalettes.Restore(changed);
            FailIf(uploads != 1, "A live mapping override using BG1 was omitted from palette invalidation.");
            verify(room);

            using Image vram = room.CaptureLiveGraphics();
            room.SetLiveGraphics(vram, true);
            uploads = 0;
            vram.Fill(Colors.White);
            room.RedrawForPaletteChange();
            FailIf(uploads != 1, "An in-place VRAM upload was skipped with an unchanged palette.");
            verify(room);
            uploads = 0;
            world.SetCurrentPaletteRoom(world.LoadRoom(2, 0xea));
            FailIf(uploads != 1, "Resetting cached live VRAM did not publish exactly one replacement texture.");
            verify(room);
        }
        finally
        {
            room.Texture.Changed -= CountUpload;
        }
    }

    private static void ValidateUnderwaterPaletteInvalidation()
    {
        // Isolate the native 16-byte row stride: only padding column 15 uses
        // BG1. It becomes visible through the 256-pixel underwater BG buffer.
        using Image source = Image.CreateEmpty(128, 128, false, Image.Format.Rgba8);
        source.Fill(Colors.White);
        Image hud = OracleGraphicsCache.LoadImage("res://assets/oracle/gfx/gfx_hud.png");
        var layout = new byte[176];
        layout[15] = 1;
        var mappings = new byte[2048];
        for (int quarter = 0; quarter < 4; quarter++)
        {
            mappings[4 + quarter] = 2;
            mappings[12 + quarter] = 1;
        }
        var palettes = new BackgroundPaletteState(new[] { Colors.Black, Colors.Black, Colors.Black, Colors.Black }, new Color[3, 4]);
        var room = new OracleRoomData(4, 0x07, 0, 0, 0, 0, layout, new byte[256],
            source, hud, mappings, new Color[6, 4], palettes, new OracleAnimationData());
        palettes.Changed += room.RefreshPaletteTextures;
        Texture2D underwater = room.UnderwaterBackgroundTexture;
        int uploads = 0;
        void CountUpload() => uploads++;
        underwater.Changed += CountUpload;
        try
        {
            Color[,] changed = palettes.Capture();
            changed[1, 0] = Colors.Red;
            palettes.Restore(changed);
            FailIf(uploads != 1, "BG1 used only by underwater padding did not invalidate its texture.");
            using Image actual = room.RenderUnderwaterBackground();
            FailIf(actual.GetPixel(240, 0) != Colors.Red || actual.GetPixel(0, 0) == Colors.Red,
                "Underwater padding lost its palette, row stride, or playable-column boundary.");
            room.SetStorageLayoutWithoutGraphics(15, 0);
            FailIf(uploads != 1, "A deferred padding write published graphics immediately.");
            changed[0, 0] = Colors.Blue;
            palettes.Restore(changed);
            FailIf(uploads != 2, "A palette publication omitted a deferred padding-layout write.");
        }
        finally
        {
            underwater.Changed -= CountUpload;
            palettes.Changed -= room.RefreshPaletteTextures;
        }
    }

    private async Task ValidateClearTilePresentation()
    {
        using Image pattern = Image.CreateEmpty(8, 8, false, Image.Format.Rgba8);
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++) pattern.SetPixel(x, y, Color.Color8((byte)(x * 32), (byte)(y * 32), 0));
        using Image background = Image.CreateEmpty(240, 176, false, Image.Format.Rgba8);
        background.Fill(Colors.Blue);
        using ImageTexture tile = ImageTexture.CreateFromImage(pattern);
        using ImageTexture texture = ImageTexture.CreateFromImage(background);
        var viewport = new SubViewport { Size = new Vector2I(240, 176), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            CanvasItemDefaultTextureFilter = Viewport.DefaultCanvasItemTextureFilter.Nearest };
        AddChild(viewport);
        var view = new RoomView();
        viewport.AddChild(view);
        view.SetRoom(texture);
        var overlay = new RoomLoadColumnRevealOverlay { Size = new Vector2(160, 128) };
        viewport.AddChild(overlay);
        try
        {
            foreach (bool screenOverlay in new[] { false, true })
            foreach (int columns in new[] { 0, 1, 5, 10 })
            {
                view.ClearRoomLoadColumnReveal();
                overlay.ClearReveal();
                if (screenOverlay) overlay.SetReveal(tile, columns);
                else view.SetRoomLoadColumnReveal(tile, columns);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using Image actual = viewport.GetTexture().GetImage();
                (int left, int right) = RoomTransitionController.RoomLoadRevealBounds(columns);
                for (int y = 0; y < 176; y++)
                for (int x = 0; x < 240; x++)
                {
                    bool cleared = (x < left || x >= right) && (!screenOverlay || x < 160 && y < 128);
                    Color expected = cleared ? pattern.GetPixel(x % 8, y % 8) : Colors.Blue;
                    FailIf(actual.GetPixel(x, y) != expected,
                        $"Clear tile repeat/coverage differs at ({x},{y}), columns={columns}, overlay={screenOverlay}.");
                }
            }
        }
        finally { viewport.Free(); }
    }

    private static void ValidateClearedTilemapDependencies(OracleRoomData room, Action<OracleRoomData> verify)
    {
        Color[,] original = room.BackgroundPalettes.Capture();
        Color[,] replacement = (Color[,])original.Clone();
        Texture2D texture = room.ClearedTilemapTexture;
        int changes = 0;
        void CountChange() => changes++;
        texture.Changed += CountChange;
        try
        {
            for (int palette = 1; palette < 8; palette++)
            for (int shade = 0; shade < 4; shade++)
            {
                Color old = replacement[palette, shade];
                replacement[palette, shade] = new Color(old.R, old.G, old.B, 1.0f - old.A);
            }
            room.BackgroundPalettes.Restore(replacement);
            room.RedrawForPaletteChange();
            using Image vram = room.CaptureLiveGraphics();
            room.SetLiveGraphics(vram, true);
            room.SetLiveGraphics(null, false);
            FailIf(changes != 0, "BG1-7 or live graphics changes rebuilt the BG0 cleared texture.");
            verify(room);
            for (int shade = 0; shade < 4; shade++)
            {
                Color old = replacement[0, shade];
                replacement[0, shade] = new Color(old.R, old.G, old.B, 1.0f - old.A);
                room.BackgroundPalettes.Restore(replacement);
                FailIf(changes != shade + 1 || !ReferenceEquals(texture, room.ClearedTilemapTexture),
                    $"BG0 color {shade} did not update the retained clear texture exactly once.");
                verify(room);
                room.RedrawForPaletteChange();
                FailIf(changes != shade + 1, "Repeated BG0 state rebuilt the cleared texture.");
            }
        }
        finally
        {
            texture.Changed -= CountChange;
            room.BackgroundPalettes.Restore(original);
        }
        verify(room);
    }
}
