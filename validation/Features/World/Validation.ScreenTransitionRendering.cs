using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoomPackTransitions()
    {
        // roomPacksPresent: $38=$80, $39=$01. checkRoomPack compares low
        // seven bits, then ORs bit 7. Interior groups bypass that check.
        FailIf(!_world.RequiresRoomPackFade(0, 0x38, 0x39) ||
            !_world.RequiresRoomPackFade(0, 0x39, 0x38) ||
            _world.RequiresRoomPackFade(0, 0x38, 0x38) ||
            _world.RequiresRoomPackFade(0, 0x6a, 0x6b) ||
            _world.RequiresRoomPackFade(4, 0x38, 0x39),
            "checkRoomPack lost its group, pack identity, or fade-bit gate.");

        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            ResetValidationInput();
            _saveData.SetMakuTreeState(3);
            _saveData.SetGlobalFlag(OracleSaveData.GlobalFlagMakuTreeDisappeared);
            _saveData.SetGlobalFlag(OracleSaveData.GlobalFlagMakuTreeSaved);
            LoadValidationRoom(0, 0x38);
            _player.WarpTo(new Vector2(0x88 + 0.625f, 0x30 + 0.19921875f));
            FailIf(_collision.Collides(_player.Position), "Room-pack fixture must approach through clear geometry.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                foreach (bool east in new[] { true, false })
                {
                    int source = east ? 0x38 : 0x39;
                    int target = east ? 0x39 : 0x38;
                    Vector2 movement = east ? Vector2.Right : Vector2.Left;
                    for (int update = 0; update < 40 && !IsTransitioning; update++)
                        StepGameplayUpdates(1, movement);
                    FailIf(!IsTransitioning || _transitions.ScrollActive || _currentRoom.Id != source,
                        $"Room 0:{source:x2} -> 0:{target:x2} did not begin CUTSCENE_05's fade through live movement: position={_player.PrecisePosition}, room=${_currentRoom.Id:x2}, transition={IsTransitioning}, scroll={_transitions.ScrollActive}.");
                    Vector2 sourcePosition = _player.PrecisePosition;
                    StepGameplayUpdates(31, Vector2.Zero, batched: batched);
                    FailIf(!IsTransitioning || _currentRoom.Id != source ||
                        _player.PrecisePosition != sourcePosition,
                        "Room-pack fade loaded its destination or moved Link before the fade completed.");
                    StepGameplayUpdates(1, Vector2.Zero);
                    Vector2 arrival = _player.PrecisePosition;
                    FailIf(_currentRoom.Id != target || !IsTransitioning || _transitions.ScrollActive ||
                        Mathf.FloorToInt(arrival.X) != (east ? 10 : 150) || arrival.Y != sourcePosition.Y ||
                        arrival.X - Mathf.Floor(arrival.X) != sourcePosition.X - Mathf.Floor(sourcePosition.X),
                        "Room-pack reload lost func_4493's wrapped coordinate or started scrolling.");
                    using Image live = _currentRoom.CaptureLiveGraphics();
                    Image atlas = OracleGraphicsCache.LoadImage(
                        $"res://assets/oracle/gfx/gfx_tileset{_currentRoom.TilesetId:x2}.png");
                    // $8c00 lies outside every uniqueGfxHeader upload. The
                    // full GFXH_TILESET load must replace it in both directions.
                    for (int y = 0; y < 8; y++)
                    for (int x = 0; x < 8; x++)
                        FailIf(live.GetPixel(x, 32 + y) != atlas.GetPixel(x, 32 + y),
                            $"Room 0:{target:x2} retained outgoing base graphics at VRAM $8c00.");
                    StepGameplayUpdates(31, Vector2.Zero, batched: batched);
                    FailIf(!IsTransitioning, "Room-pack fade-in completed before its terminal update.");
                    StepGameplayUpdates(1, Vector2.Zero);
                    FailIf(IsTransitioning || _player.PrecisePosition != arrival,
                        "Room-pack fade did not release Link at the preserved arrival coordinate.");
                    StepGameplayUpdates(1, Vector2.Zero);
                }
            }
        }
    }

    private void ValidateScreenTransitionRendering()
    {
        var routes = new ScreenTransitionPaletteDatabase();
        foreach (bool entering in new[] { true, false })
        {
            int sourceId = entering ? 0x6a : 0x6b;
            int targetId = entering ? 0x6b : 0x6a;
            Vector2I direction = entering ? Vector2I.Right : Vector2I.Left;
            LoadValidationRoom(0, sourceId);
            OracleRoomData source = _currentRoom;
            Color[,] original = source.BackgroundPalettes.Capture();
            using Image originalVram = source.CaptureLiveGraphics();
            ulong immutableHash = OracleGraphicsCache.PixelHash(OracleGraphicsCache.LoadImage(
                $"res://assets/oracle/gfx/gfx_tileset{source.TilesetId:x2}.png"));
            _transitions.BeginScroll(_player, direction, targetId);
            OracleRoomData target = _currentRoom;
            FailIf(!routes.TryGet(0, targetId, direction, _rooms.SaveData, out var fade),
                $"Missing Yoll palette route into 0:{targetId:x2}.");
            int setup = entering ? 4 : 7;
            int total = entering ? 48 : 49;
            int uploadStart = entering ? 46 : 2;
            FailIf(_transitions.ScrollTotalFrames != total,
                $"Yoll {(entering ? "entry" : "exit")} graphics schedule changed.");
            Image targetAtlas = OracleGraphicsCache.LoadImage(
                $"res://assets/oracle/gfx/gfx_tileset{target.TilesetId:x2}.png");
            byte[] finalPalette = Godot.FileAccess.GetFileAsBytes(
                $"res://assets/oracle/metadata/palette{target.TilesetId:x2}.bin");
            for (int tick = 0; tick <= total; tick++)
            {
                if (tick == setup)
                {
                    int targetRedraws = 0;
                    void CountRedraw() => targetRedraws++;
                    target.Texture.Changed += CountRedraw;
                    try { UpdateScrollingTransition(1.0 / 60.0); }
                    finally { target.Texture.Changed -= CountRedraw; }
                    FailIf(targetRedraws != 1,
                        $"Yoll palette update redrew the destination {targetRedraws} times instead of once.");
                }
                else if (tick != 0) UpdateScrollingTransition(1.0 / 60.0);
                int age = tick - (setup - 2);
                int weight = Math.Clamp(age / 2, 0, 15);
                for (int p = 0; p < 8; p++)
                for (int c = 0; c < 4; c++)
                {
                    Color actual = target.BackgroundPalettes.Resolve(p, c);
                    if (p < 2 || weight == 0)
                    {
                        FailIf(actual != original[p, c],
                            $"Yoll update {tick} prematurely changed BG{p}, color {c}.");
                        continue;
                    }
                    int index = ((p - 2) * 4 + c) * 3;
                    int Expected(int i) => tick == total ? finalPalette[i] :
                        (fade.Source[i] * (16 - weight) + fade.Destination[i] * weight) / 16;
                    FailIf(Mathf.RoundToInt(actual.R * 31) != Expected(index) ||
                        Mathf.RoundToInt(actual.G * 31) != Expected(index + 1) ||
                        Mathf.RoundToInt(actual.B * 31) != Expected(index + 2),
                        $"Yoll update {tick} BG{p}, color {c} did not use paletteFadeHandler08 RGB5 arithmetic.");
                }
                if (tick < total)
                {
                    // Unaddressed VRAM retains the source's animation/scripted
                    // bytes. Each header replaces only $9300/$9500/$9700.
                    using Image vram = target.CaptureLiveGraphics();
                    for (int tile = 0; tile < 256; tile++)
                    {
                        int entry = tile < 0xb0 ? -1 : tile < 0xd0 ? 0 : tile < 0xf0 ? 1 : 2;
                        Image expected = entry >= 0 && tick >= uploadStart + entry ? targetAtlas : originalVram;
                        for (int y = 0; y < 8; y++)
                        for (int x = 0; x < 8; x++)
                        {
                            int px = tile % 16 * 8 + x, py = tile / 16 * 8 + y;
                            FailIf(vram.GetPixel(px, py) != expected.GetPixel(px, py),
                                $"Yoll update {tick} VRAM tile ${tile:x2} pixel {x},{y} changed outside its upload.");
                        }
                    }
                    using Image displayed = source.Texture.GetImage();
                    ulong displayedHash = OracleGraphicsCache.PixelHash(displayed);
                    source.RedrawForPaletteChange();
                    using Image refreshed = source.Texture.GetImage();
                    FailIf(displayedHash != OracleGraphicsCache.PixelHash(refreshed),
                        $"Yoll update {tick} left the outgoing room with stale palette/VRAM pixels.");
                }
            }
            FailIf(OracleGraphicsCache.PixelHash(OracleGraphicsCache.LoadImage(
                $"res://assets/oracle/gfx/gfx_tileset{source.TilesetId:x2}.png")) != immutableHash,
                "Scrolling mutated the immutable source graphics cache.");
            using Image steppedImage = target.Texture.GetImage();
            ulong steppedHash = OracleGraphicsCache.PixelHash(steppedImage);
            LoadValidationRoom(0, sourceId);
            _transitions.BeginScroll(_player, direction, targetId);
            UpdateScrollingTransition(total / 60.0);
            using Image batchedImage = _currentRoom.Texture.GetImage();
            FailIf(_transitions.ScrollActive || OracleGraphicsCache.PixelHash(batchedImage) != steppedHash,
                "Batched scrolling produced different palette/VRAM pixels.");
        }

        foreach (bool repaired in new[] { false, true })
        {
            _rooms.SaveData.SetGlobalFlag(0x29, repaired);
            FailIf(routes.TryGet(0, 0x12, Vector2I.Up, _rooms.SaveData, out _) == repaired ||
                !routes.TryGet(1, 0x12, Vector2I.Up, _rooms.SaveData, out _) ||
                routes.TryGet(0, 0x12, Vector2I.Left, _rooms.SaveData, out _),
                "checkSymmetryCityPaletteTransition did not respect group, direction, and GLOBALFLAG_TUNI_NUT_PLACED $29.");
            LoadValidationRoom(0, 0x22);
            Color old = _currentRoom.BackgroundPalettes.Resolve(2, 0);
            _transitions.BeginScroll(_player, Vector2I.Up, 0x12);
            byte[] destination = Godot.FileAccess.GetFileAsBytes(
                $"res://assets/oracle/metadata/palette{_currentRoom.TilesetId:x2}.bin");
            for (int tick = 1; tick <= 6; tick++)
            {
                UpdateScrollingTransition(1.0 / 60.0);
                Color actual = _currentRoom.BackgroundPalettes.Resolve(2, 0);
                bool committed = repaired && tick >= 4;
                FailIf(committed ?
                    Mathf.RoundToInt(actual.R * 31) != destination[0] ||
                    Mathf.RoundToInt(actual.G * 31) != destination[1] ||
                    Mathf.RoundToInt(actual.B * 31) != destination[2] : actual != old,
                    $"Symmetry update {tick}, Tuni Nut repaired={repaired}: func_47fc palette boundary changed.");
            }
            FinishActiveScrollingTransitionForValidation();
        }

        LoadValidationRoom(5, 0x77);
        _transitions.BeginScroll(_player, Vector2I.Right, 0x78);
        UpdateScrollingTransition(_transitions.ScrollTotalFrames / 60.0);
        FailIf(_transitions.ScrollActive || _currentRoom.TilesetPaletteId != 0x63,
            "Room 5:78 did not execute uniqueGfxHeader14's terminal PALH_63 upload.");
    }

    private void ValidateScreenTransitionGraphicsPayloads()
    {
        var data = new ScreenTransitionGraphicsDatabase();
        ValidateBufferedTransitionUploads(data);
        for (int unique = 1; unique < 0x14; unique++)
        {
            int tileset = Enumerable.Range(0, 103).First(t => data.ForTileset(t).Unique == unique);
            Image atlas = OracleGraphicsCache.LoadImage($"res://assets/oracle/gfx/gfx_tileset{tileset:x2}.png");
            var uploads = data.Uploads(unique);
            FailIf(uploads.Count != 3, $"uniqueGfxHeader{unique:x2} must contain three uploads.");
            for (int order = 0; order < 3; order++)
            {
                ScreenGraphicsUpload upload = uploads[order];
                FailIf(upload.Address != 0x9300 + order * 0x200 || upload.Tiles != (order == 2 ? 16 : 32),
                    $"uniqueGfxHeader{unique:x2} entry {order} lost its destination or tile count.");
                // The expanded-tileset patch edits Symmetry's last block.
                // Its clean bytes are checked separately below.
                if (unique == 8 && order == 2) continue;
                for (int tile = 0; tile < upload.Tiles; tile++)
                for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    int index = (upload.Address - 0x8800) / 16 + tile;
                    int shade = Mathf.RoundToInt((1 - atlas.GetPixel(index % 16 * 8 + x, index / 16 * 8 + y).R) * 3);
                    int bit = 1 << (7 - x);
                    int offset = tile * 16 + y * 2;
                    FailIf(((upload.Data[offset] & bit) != 0) != ((shade & 1) != 0) ||
                        ((upload.Data[offset + 1] & bit) != 0) != ((shade & 2) != 0),
                        $"uniqueGfxHeader{unique:x2} entry {order} differs from extracted tileset ${tileset:x2}.");
                }
            }
        }
        ScreenGraphicsUpload palette = data.Uploads(0x14).Single();
        string symmetryHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            data.Uploads(8).SelectMany(u => u.Data).ToArray()));
        FailIf(symmetryHash != "D683C9D0EB3B785CB439EF4D04FA099968E520797232C77034B85B8F2FB54F5A",
            "Clean-US uniqueGfxHeader08 bytes changed to the patched Symmetry art.");
        FailIf(palette.Palette != 0x63 || palette.Tiles != 0 || palette.Data.Length != 0,
            "uniqueGfxHeader14 lost its terminal PALH_63 record.");
    }

    private static void ValidateBufferedTransitionUploads(ScreenTransitionGraphicsDatabase data)
    {
        byte[] initial = new byte[128 * 128 * 4];
        for (int i = 0; i < initial.Length; i++) initial[i] = (byte)(i * 17 + i / 257);
        using Image expected = Image.CreateFromData(128, 128, false, Image.Format.Rgba8, initial);
        using Image actual = Image.CreateFromData(128, 128, false, Image.Format.Rgba8, initial);
        ulong identity = actual.GetInstanceId();
        for (int unique = 0; unique < 0x15; unique++)
        foreach (ScreenGraphicsUpload upload in data.Uploads(unique))
        {
            if (upload.Palette >= 0) continue;
            Compare(upload);
        }
        // Also exercise the first/last VRAM tiles and a partial row crossing.
        foreach (int address in new[] { 0x8800, 0x88f0, 0x97e0 })
        {
            byte[] bytes = new byte[32];
            for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)(i * 37);
            Compare(new ScreenGraphicsUpload(address, 2, -1, bytes));
        }
        if (System.Environment.GetEnvironmentVariable("OOA_BENCHMARK_SCROLL_UPLOADS") == "1")
            BenchmarkTransitionUploads(data.Uploads(1)[0], expected, actual);

        void Compare(ScreenGraphicsUpload upload)
        {
            ApplyReferenceTransitionUpload(expected, upload);
            ScreenTransitionRenderer.ApplyUpload(actual, upload);
            FailIf(actual.GetInstanceId() != identity ||
                !actual.GetData().AsSpan().SequenceEqual(expected.GetData()),
                $"Buffered transition upload ${upload.Address:x4}, ${upload.Tiles:x2} tiles changed image identity or addressed/unaddressed pixels.");
        }
    }

    private static void ApplyReferenceTransitionUpload(Image vram, ScreenGraphicsUpload upload)
    {
        int startTile = (upload.Address - 0x8800) / 16;
        for (int tile = 0; tile < upload.Tiles; tile++)
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            int offset = tile * 16 + y * 2;
            int shade = ((upload.Data[offset] >> (7 - x)) & 1) |
                (((upload.Data[offset + 1] >> (7 - x)) & 1) << 1);
            byte intensity = (byte)(255 - shade * 85);
            int index = startTile + tile;
            vram.SetPixel(index % 16 * 8 + x, index / 16 * 8 + y,
                Color.Color8(intensity, intensity, intensity));
        }
    }

    private static void BenchmarkTransitionUploads(ScreenGraphicsUpload upload, Image beforeImage, Image afterImage)
    {
        Action before = () => ApplyReferenceTransitionUpload(beforeImage, upload);
        Action after = () => ScreenTransitionRenderer.ApplyUpload(afterImage, upload);
        for (int i = 0; i < 100; i++) { before(); after(); }
        double[] oldTimes = new double[7], newTimes = new double[7];
        long oldBytes = 0, newBytes = 0;
        for (int sample = 0; sample < 7; sample++)
        {
            if ((sample & 1) == 0)
            {
                (oldTimes[sample], oldBytes) = Measure(before);
                (newTimes[sample], newBytes) = Measure(after);
            }
            else
            {
                (newTimes[sample], newBytes) = Measure(after);
                (oldTimes[sample], oldBytes) = Measure(before);
            }
        }
        Array.Sort(oldTimes); Array.Sort(newTimes);
        GD.Print(FormattableString.Invariant(
            $"SCROLL_UPLOAD_BENCHMARK ${upload.Address:x4}, {upload.Tiles} tiles: median_us before={oldTimes[3]:F3} after={newTimes[3]:F3}; managed_bytes/op before={oldBytes} after={newBytes}; 7 samples, 1000 uploads/sample, headless; excludes room redraws."));

        static (double Microseconds, long Bytes) Measure(Action action)
        {
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int i = 0; i < 1000; i++) action();
            long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - start;
            return (elapsed * 1000.0 / System.Diagnostics.Stopwatch.Frequency,
                (GC.GetAllocatedBytesForCurrentThread() - allocated) / 1000);
        }
    }
}
