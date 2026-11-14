
namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedRoomRuntime()
    {
        foreach (OracleGame game in new[] { OracleGame.Ages, OracleGame.Seasons })
        {
            var clockSave = OracleSaveData.CreateStandardGame(game);
            var clockState = OracleRuntimeState.CreateInitialized(game);
            // bank0.s:mainThreadStart increments the four little-endian bytes,
            // then publishes the low byte before runGameLogic.
            for (int index = 0; index < 4; index++) clockSave.WriteField("wPlaytimeCounter", 0xff, index);
            clockState.FrameCounter = 0x37;
            MainThreadClock.Advance(clockSave, clockState);
            FailIf(clockState.FrameCounter != 0 || clockState.ReadWramByte(0xcc00) != 0 ||
                Enumerable.Range(0, 4).Any(index => clockSave.ReadField("wPlaytimeCounter", index) != 0),
                $"{game}: mainThread must wrap the 32-bit playtime counter and publish $00 before gameplay.");
            clockState.FrameCounter = 0x91;
            MainThreadClock.Advance(clockSave, clockState);
            FailIf(clockState.FrameCounter != 1 || clockSave.ReadField("wPlaytimeCounter") != 1,
                $"{game}: the next mainThread update must derive its frame byte from playtime, not a previous native scratch modification.");
        }
        var data = new SeasonsWorldData();
        var save = OracleSaveData.CreateStandardGame(OracleGame.Seasons);
        var memory = OracleRuntimeState.CreateInitialized(OracleGame.Seasons);
        long tick = 0;
        var content = new SeasonsRoomContent(data);
        var rooms = new RoomSession(content, 0, 0xa7, () => tick, () => tick = 0, save, memory, countAsRoomEntry: false);
        FailIf(!ReferenceEquals(rooms.BackgroundPalettes, data.Palettes),
            "The shared room session must expose its selected content's authoritative palette owner.");
        FailIf(!rooms.HasRoom(0, 0x00) || !rooms.HasRoom(0, 0xff) || !rooms.HasRoom(1, 0x00) || !rooms.HasRoom(2, 0x00) ||
            !rooms.HasRoom(3, 0x83) || rooms.HasRoom(7, 0xff) || rooms.HasRoom(8, 0x00) ||
            rooms.HasRoom(0, -1) || rooms.HasRoom(0, 0x100) || rooms.GetDungeonIndex(0, 0xb6) != -1 || rooms.GetDungeonIndex(1, 0x00) != -1,
            "Room availability and dungeon queries must use the selected content's imported room boundary.");
        // Native roomPacks: $b6/$a6=$10; $c6=$00. Seasons compares all
        // eight bits and fades even though these records have no high bit.
        FailIf(!rooms.RequiresRoomPackFade(0, 0xb6, 0xc6) ||
            !rooms.RequiresRoomPackFade(0, 0xc6, 0xb6) ||
            rooms.RequiresRoomPackFade(0, 0xb6, 0xa6) ||
            rooms.RequiresRoomPackFade(1, 0xb6, 0xc6),
            "Room-pack policy must retain Seasons' group-zero/full-byte gate independently of Ages' fade bit.");
        FailIf(save.GetRoomFlags(0, 0xa7) != 0 || save.GashaMaturity != 0 || memory.ReadField("wScrollMode") != 0,
            "Preparing a shared Seasons room session must not enter the room or advance native maturity.");
        rooms.Load(0, 0xb6);
        FailIf(rooms.CurrentRoom.Id != 0xb6 || rooms.CurrentDungeonIndex != -1 ||
            save.GetRoomFlags(0, 0xb6) != 0x10 || save.GashaMaturity != 5 || save.MinimapRoom != 0xb6,
            "bank1 room admission must give Seasons room $0:$b6 the same visit/minimap owner and native $05 maturity increment.");
        OracleRoomData incoming = rooms.GetRoom(0, 0x98);
        FailIf(save.GetRoomFlags(0, 0x98) != 0 || save.GashaMaturity != 5 || rooms.CurrentRoom.Id != 0xb6,
            "Preparing the destination must not commit its room identity or visit state.");
        rooms.SetLoadedRoom(0, incoming, updateMinimap: false);
        FailIf(save.GetRoomFlags(0, 0x98) != 0x10 || save.GashaMaturity != 10 || save.MinimapRoom != 0xb6,
            "Scrolling admission must defer the minimap handoff until the native finish phase.");
        rooms.UpdateMinimapLocation();
        rooms.LoadCutsceneBackground(0, 0xc8);
        FailIf(save.MinimapRoom != 0x98 || save.GetRoomFlags(0, 0xc8) != 0 || save.GashaMaturity != 10,
            "A cutscene background load must preserve admission counters and the visited/minimap state.");
        Texture2D texture = rooms.CurrentRoom.Texture;
        Color[,] originalPalette = rooms.BackgroundPalettes.Capture();
        Color[,] replacement = (Color[,])originalPalette.Clone();
        for (int palette = 2; palette < 8; palette++)
        for (int shade = 0; shade < 4; shade++) replacement[palette, shade] = Colors.Magenta;
        int redraws = 0;
        void CountRedraw() => redraws++;
        texture.Changed += CountRedraw;
        try
        {
            using (rooms.DeferTexturePublication())
            {
                rooms.BackgroundPalettes.Restore(replacement);
                using (rooms.DeferTexturePublication()) rooms.CurrentRoom.RedrawForPaletteChange();
                FailIf(redraws != 0 || rooms.BackgroundPalettes.Resolve(2, 0) != Colors.Magenta,
                    "A room publication scope must defer textures while native palette bytes change synchronously.");
            }
            FailIf(redraws != 1 || !ReferenceEquals(texture, rooms.CurrentRoom.Texture),
                "Nested room publication must flush once through the selected content without replacing texture identity.");
        }
        finally
        {
            texture.Changed -= CountRedraw;
            rooms.BackgroundPalettes.Restore(originalPalette);
        }
        byte[] original = memory.CaptureState().Wram;
        bool rejected = false;
        try { _ = new RoomSession(content, 0, 0xa7, () => 0, () => { }, OracleSaveData.CreateStandardGame(OracleGame.Ages), memory); }
        catch (ArgumentException) { rejected = true; }
        FailIf(!rejected || !original.SequenceEqual(memory.CaptureState().Wram),
            "Cross-game room content must fail before changing live runtime bytes.");

        foreach (Vector2I direction in new[] { Vector2I.Up, Vector2I.Right, Vector2I.Down, Vector2I.Left })
        {
            var scroll = new RoomScrollController();
            var start = new Vector2(80.25f, 64.5f);
            scroll.Begin(start, direction, 160, 128, 5, 2);
            int movementFrames = direction.X == 0 ? 32 : 40;
            for (int update = 1; update <= 5 + movementFrames + 2; update++)
            {
                scroll.Advance();
                int movement = Math.Clamp(update - 5, 0, movementFrames);
                Vector2 step = direction.X == 0 ? new(0, direction.Y * 0.5f) : new(direction.X * 0.375f, 0);
                FailIf(scroll.Frame != movement || scroll.LinkPosition != start + step * movement ||
                    scroll.Pixels != movement * 4 || scroll.Active != (update < 5 + movementFrames + 2),
                    $"bank1 scrolling direction {direction}, update {update}: shared setup/movement/cleanup counters or fixed-point Link movement differ.");
            }
        }
        // Seasons uses descending bits $07..$00 for its eight source rows.
        var refills = new SeedTreeRefillData(SeasonsGameContent.AssetRoot, 8);
        FailIf(memory.ReadField("wSeedTreeRefilledBitset") != 0xfc,
            "Seasons initializeSeedTreeRefillData must initialize $fc, independently of Ages' $fff0.");
        memory.SetField("wSeedTreeRefilledBitset", 0);
        foreach (int room in Enumerable.Range(1, 8)) refills.UpdateRefillState(memory, 0, room);
        refills.UpdateRefillState(memory, 0, 0xf8);
        FailIf(memory.ReadField("wSeedTreeRefilledBitset") != 0x80 ||
            Enumerable.Range(0, 8).Any(slot => memory.ReadSeedTreeRefillRoom(0, slot) != 0),
            "Seasons tree room $f8 must set bit $07 and clear only its completed eight-room history.");
    }
}
