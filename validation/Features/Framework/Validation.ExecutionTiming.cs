using System;
using System.Collections.Generic;
using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateOriginalExecutionTiming()
    {
        // Clean-US instruction traces, with complete timer-interrupt service
        // intervals removed. $ba/$a2 decode into banked WRAM: keeping the
        // importer's stack in $dxxx would silently terminate these early.
        OracleLoadingWork work = OracleLoadingWork.Shared;
        // Native update 1065's draw prefix is 11116 clocks. Remove the
        // objectQueueDraw work beyond its 64 enabled checks (972), Link
        // drawing (1212), and the taken draw CALL's twelve extra clocks.
        OracleGameplayDispatchWork dispatch = OracleGameplayDispatchWork.Shared;
        // Source instruction accounting: main-loop shared988, resumed main
        // thread128+4 taken-JR clocks, main-thread shared counter/yield424.
        FailIf(dispatch.Frame != 1544 || dispatch.Objects != 948 || dispatch.Sprites(true, false, false) != 8920 ||
            dispatch.Sprites(false, false, false) != 8704 ||
            dispatch.Sprites(true, true, false) != 8924 ||
            dispatch.Sprites(false, false, true) != 8328 ||
            dispatch.Sprites(true, false, true) != 8544 ||
            dispatch.Sprites(true, true, true) != 8548,
            "Shared object/sprite traversal differs from native work or its scroll/textbox gates.");
        // Source context switches use LD SP,HL and LD (a16),SP. Check the
        // restored return address and little-endian bus writes independently
        // of the imported table: 12+8+20+16 clocks, writes at clocks36/40.
        var cpuMemory = new byte[65536];
        byte[] switchProgram = [0x21, 0xee, 0xc1, 0xf9, 0x08, 0x80, 0xc0, 0xc9];
        switchProgram.CopyTo(cpuMemory, 0x100);
        cpuMemory[0xc1ee] = 0x34; cpuMemory[0xc1ef] = 0x12;
        long busClocks = 0;
        var stackWrites = new List<long>();
        var cpu = new OracleCpu(address => cpuMemory[address], (address, value) => {
            cpuMemory[address] = (byte)value;
            if (address is 0xc080 or 0xc081) stackWrites.Add(busClocks);
        }, clocks => busClocks += clocks, (pc, detail) => new InvalidOperationException($"${pc:x4}: {detail}"));
        cpu.BeginCall(0x100, stack: 0xc1f0);
        for (int instruction = 0; instruction < 4; instruction++) cpu.Step();
        FailIf(cpu.ProgramCounter != 0x1234 || cpu.StackPointer != 0xc1f0 || cpu.Cycles != 56 ||
            cpuMemory[0xc080] != 0xee || cpuMemory[0xc081] != 0xc1 ||
            stackWrites.Count != 2 || stackWrites[0] != 36 || stackWrites[1] != 40,
            "Source thread stack switching lost its return address, byte order, or bus timing.");
        // Native empty loops: items956, enemies1316, parts1288,
        // interactions1476, items-post760. JR's extra four clocks for each
        // empty slot are outside the shared portion; conditional CALL's
        // untaken twelve-clock fetch is already common work.
        FailIf(dispatch.SpecialObjects(false) != 1560 || dispatch.SpecialObjects(true) != 1672 ||
            dispatch.NormalItems != 916 || dispatch.PostItems != 760 ||
            dispatch.NormalObjectPass(0) != 1252 || dispatch.NormalObjectPass(1) != 1224 ||
            dispatch.NormalObjectPass(2) != 1476,
            "Normal object slot-loop work differs from source instruction traces.");
        // Native first north scroll $00:$8a -> $00:$7a, with interrupt
        // intervals removed from each decoder's entry/return trace.
        OracleRoomLoadingWork roomWork = OracleRoomLoadingWork.Shared;
        FailIf(roomWork.Tileset(0x14, 0, 0, 0x7a) != 425304 ||
            roomWork.Get("room", 0x007a, 0) != 24404 ||
            roomWork.Get("collisions", 0, 0) != 11172 ||
            roomWork.Get("collisions", 0, 1) != 10668 ||
            roomWork.Get("vram", 0, 0) != 96000 ||
            roomWork.Get("layout-wrapper", 0, 0) != 8020 ||
            roomWork.Get("layout-wrapper", 0, 1) != 8032 ||
            roomWork.Get("vram-wrapper", 0, 0) != 384 ||
            roomWork.Get("scroll-clear", 0, 0) != 2188 ||
            // Native refreshObjectGfx is 14964 here, excluding the
            // query returns (58*12 + 16*12 + 10*12) and extra-header
            // tail (56). Their common prefixes are included.
            roomWork.Get("graphics-traversal", 0, 0) != 13900 ||
            roomWork.Get("initialization", 0, 0) != 1932 ||
            roomWork.Get("room-state", 0, 0) != 380 ||
            roomWork.Get("room-specific", 0x007a, 0) != 408 ||
            roomWork.Get("vram-specific", 0x007a, 0) != 892,
            "Room decoders differ from the clean-US instruction trace.");
        // setPastCliffPalettesToRed: collisions != 0 exits first; only
        // outdoor past layouts scan all attributes, except room byte $38.
        FailIf(roomWork.Tileset(0x14, 1, 0x80, 0x7a) != 425272 ||
            roomWork.Tileset(0x14, 0, 0x80, 0x7a) != 446504 ||
            roomWork.Tileset(0x14, 0, 0x80, 0x38) != 425312,
            "Past cliff palette work lost its source gates.");
        OracleRoomData from = _rooms.GetRoom(0, 0x8a);
        OracleRoomData to = _rooms.GetRoom(0, 0x7a);
        OracleSaveData scrollSave = OracleSaveData.CreateStandardGame();
        FailIf(from.TilesetLayoutId != 0 || to.TilesetLayoutId != 0x14 || to.LayoutGroup != 0 ||
            roomWork.Scroll(from, to, scrollSave) != 608168 || roomWork.Scroll(to, to, scrollSave) != 182852,
            "Scrolling must decode changed tileset layouts and reuse an unchanged layout.");
        // Native first scroll: getNextActiveRoom1192 = ordered lookup/
        // dispatch772 + ordinary map advance420; equal room packs116.
        // Pirate load exits at the present-era gate420; portal group miss332.
        FailIf(roomWork.Get("next-room", 0x8a, 0) != 772 ||
            roomWork.Get("room-advance", 0, 0) != 420 ||
            roomWork.Get("room-pack", 0, 0) != 116 ||
            roomWork.PirateLoad(to, scrollSave) != 420 ||
            roomWork.Get("portal-spawn", 0, 0) != 332 ||
            roomWork.Get("portal-spawn", 0, 1) != 368,
            "Scroll selection or room-spawn checks differ from the native first scroll.");
        // Native update1065: music selection440, tileset selection1816,
        // single-tile changes1324, room-specific tile lookup1332,
        // standard flags448, chest gate248 and pollution gate284.
        FailIf(roomWork.ScreenData(0, 0x7a, scrollSave) != 2256 ||
            roomWork.SingleTiles(0, 0x7a, scrollSave) != 1324 ||
            roomWork.Get("tile-specific", 0x7a, 0) != 1332 ||
            roomWork.Get("standard-tiles", 0, 0) != 448 ||
            roomWork.Get("opened-chest", 0x7a, 0) != 248 ||
            roomWork.Get("pollution-gate", 0, 1) != 284 ||
            roomWork.Get("tile-dispatch", 0, 0) != 244,
            "Screen selection or tile-substitution work differs from the native first scroll.");
        // Native freeze traverses all sixty slots with zero conversions:
        // 3904 clocks less sixty taken-JR extras (4 each) is shared3664.
        // The parser clears32 bytes and calls history/RNG before its
        // 416-clock banked pointer lookup: 912+416 clocks excluding history/RNG.
        FailIf(roomWork.Get("object-freeze", 0, 0) != 3664 ||
            roomWork.Get("object-parse", 0, 0) != 1328,
            "Room entry lost its shared object traversal or parser scratch clear.");
        var history = new RecentEnemyDefeats();
        FailIf(history.BeginRoom(0) != 368 || history.BeginRoom(0x7a) != 764,
            "Enemy history must find zero in a cleared list and scan all eight entries for a new room.");
        history.MarkKilled(1);
        FailIf(history.BeginRoom(0x7b) != 764 || history.BeginRoom(0x7b) != 408 ||
            history.BeginRoom(0x7a) != 368 || !history.WasKilled(1),
            "Enemy-history timing lost ordered forty-clock misses or retained defeat bits.");
        // getAdjustedRoomGroup's SET replaces a taken JR (+4); unrelated
        // room bits must not choose another timing row. Single-tile $f2
        // returns early if unfinished, unlike the flag-mask rows that scan on.
        scrollSave.SetRoomFlag(0, 0x7a, 0x81);
        FailIf(roomWork.ScreenData(0, 0x7a, scrollSave) != 2260 ||
            roomWork.SingleTiles(0, 0x7a, scrollSave) != 1324 ||
            roomWork.SingleTiles(0, 0x47, scrollSave) != 1260,
            "Room-work selectors lost relevant flag bits or retained irrelevant ones.");
        scrollSave.SetGlobalFlag(GlobalFlag.FinishedGame);
        FailIf(roomWork.SingleTiles(0, 0x47, scrollSave) != 1684,
            "Single-tile finished-game branch must continue through the remaining source rows.");
        // checkMakuTreeSaved reloads the tileset after the past $48 bit0
        // changes. Global MakuTreeSaved is a different input.
        FailIf(roomWork.ScreenData(0, 0x38, scrollSave) != 2276,
            "Maku tileset timing changed before its source room flag.");
        scrollSave.SetRoomFlag(1, 0x48, 1);
        FailIf(roomWork.ScreenData(0, 0x38, scrollSave) != 2712,
            "Maku tileset timing omitted the source override reload.");
        // Source group-0 companion handling adds a 36-clock fallback when
        // no companion is chosen, or returns in 132 clocks when one is.
        // Other groups bypass that check (28 fewer clocks). Underwater and
        // layout-swap INC instructions balance the untaken JR clocks.
        FailIf(roomWork.Get("room-state", 0, 3) != 380 ||
            roomWork.Get("room-state", 0, 4) != 416 ||
            roomWork.Get("room-state", 0, 12) != 132 ||
            roomWork.Get("room-state", 1, 15) != 352 ||
            roomWork.Get("room-specific", 0x0093, 0) != 248 ||
            roomWork.Get("room-specific", 0x0038, 0) != 292 ||
            roomWork.Get("vram-specific", 0x0005, 0) != 248,
            "Room initialization lost companion gates or ordered first-match handler selection.");
        OracleSeaEffectSearchWork seaWork = OracleSeaEffectSearchWork.Shared;
        var smallLayout = new byte[80];
        FailIf(seaWork.Search(0, smallLayout) != 12620 || seaWork.Search(3, smallLayout) != 156,
            "Room entry must search both sea-effect tiles, while side-view lists terminate immediately.");
        smallLayout[0] = 0xeb;
        smallLayout[79] = 0xe9;
        // The tile list has priority over position: pollution at $00 wins
        // over whirlpool at $79. findTileInRoom's last comparison at $00
        // has a distinct return path; all earlier misses cost 32 clocks.
        FailIf(seaWork.Search(0, smallLayout) != 6352,
            "Sea-effect search reordered the source tile list.");
        smallLayout[0] = 0;
        FailIf(seaWork.Search(0, smallLayout) != 8720,
            "Sea-effect search lost the small room's sixteen-byte WRAM row stride.");
        var largeLayout = new byte[176];
        largeLayout[175] = 0xeb;
        FailIf(seaWork.Search(0, largeLayout) != 760,
            "Sea-effect search must include stored padding in a large room.");
        // Native first-scroll checkScreenEdgeWarps includes the ordered
        // group-0 source scan and costs 6256. getLinkWarpQuadrant's right
        // half adds eight clocks; it splits at $58 (small) or $80 (large).
        FailIf(roomWork.EdgeWarp(0, 0x8a, false, 0x57, false) != 6256 ||
            roomWork.EdgeWarp(0, 0x8a, false, 0x58, false) != 6264 ||
            roomWork.EdgeWarp(0, 0x8a, true, 0x57, true) != 6256 ||
            roomWork.EdgeWarp(4, 0, false, 0x7f, false) != 5204 ||
            roomWork.EdgeWarp(4, 0, false, 0x80, false) != 5212,
            "Edge-warp timing lost the ordered no-match scan or source quadrant boundaries.");
        // group0WarpSources starts with two $38 records, then the $48
        // north-left and north-right records. The latter scans one extra
        // record (108 clocks), plus the right-quadrant increment (8).
        // A mounted match adds the conditional CALL's twelve clocks; the
        // actual dismount routine remains separate from lookup work.
        FailIf(roomWork.EdgeWarp(0, 0x48, false, 0, false) != 1176 ||
            roomWork.EdgeWarp(0, 0x48, false, 0x58, false) != 1292 ||
            roomWork.EdgeWarp(0, 0x48, false, 0, true) != 1188,
            "Edge-warp timing must stop at the first matching source and retain the mounted call gate.");
        // Native $00:$3215 on arrival: 189720 elapsed clocks, less timer
        // interrupts (1756 each), short VBlank (468), and STAT (756 + 636),
        // is 184348. The source's zero seed takes 180200 clocks; each set bit in the
        // remaining 255 random bytes costs four more in multiplyAByC.
        var random = new OracleRandom();
        random.RestoreState(random.CaptureState() with { Rng1 = 0x5c, Rng2 = 0x88 });
        int permutationWork = 0;
        random.BlockingWork += clocks => permutationWork += clocks;
        random.BeginRoomParse();
        FailIf(permutationWork != 184348 || random.Calls != 256,
            "generateRandomBuffer lost its native RNG-dependent blocking work.");
        random.RestoreState(random.CaptureState() with { Rng1 = 0, Rng2 = 0 });
        random.GeneratePermutation();
        FailIf(permutationWork != 184348 + 180200 || random.Calls != 512,
            "Repeated buffer generation must charge the source work for its current seed.");
        // Independent instruction trace at the first Impa textbox:
        // showText is 700 clocks; textThreadStart through its first yield is
        // 106464, excluding timer/VBlank interrupts. Its queued DMA is 1884.
        IReadOnlyList<LoadingStep> textbox = OracleTextboxLoadingWork.Shared.Plan(0x0102, 0);
        FailIf(textbox.Count != 2 || textbox[0] != new LoadingStep("cpu", 107164) ||
            textbox[1] != new LoadingStep("vblank-work", 1884),
            "TX_0102 opening work differs from the clean-US instruction trace.");
        FailIf(work.Graphics(0xa0) != 610584 || work.Graphics(0xba) != 440144 ||
            work.Graphics(0xa2) != 158412,
            "loadGfxHeader CPU work differs from executed clean-US $a0/$ba/$a2 decoder paths.");
        FailIf(work.Plan("files")[10].Value != 1_272_492,
            "loadFileDisplayVariables/font copy lost the cold SRAM verification or untouched WRAM-bank-4 name bytes.");

        using var fixture = new SoundValidationFixture(new OracleSoundData());
        OracleSoundEngine sound = fixture.Sound;
        sound.PlaySound(0x01);
        for (int tick = 0; tick < 10; tick++) sound.Tick();
        sound.PlaySound(0x56);
        sound.PlaySound(0xfa);
        for (int tick = 0; tick < 32; tick++) sound.Tick();
        FailIf(sound.Driver.ReadState(0xc014) != 32 || sound.Channel(0).WaitFrames != 2,
            "Title fade setup differs from the clean-US timer boundary.");

        // Original $0050 entry through RETI. Interrupt dispatch itself adds
        // another 20 clocks. These measured costs vary with channel bytecode.
        foreach (int expected in new[] { 15400, 8856, 12120, 8344, 11492 })
            FailIf(sound.RunTimerInterrupt(correction: false) != expected,
                $"timerInterrupt CPU service cost differs from clean-US {expected} clocks.");
        FailIf(sound.Driver.ReadState(0xc014) != 37,
            "The fade must continue while foreground loading is blocked.");
        sound.PlaySound(0x11);
        FailIf(sound.RunTimerInterrupt(correction: true) != 32580 ||
            sound.Channel(0).WaitFrames != 111,
            "Music queue drain, hFFB8 correction and file-select sequencing lost their source order or CPU work.");

        var clock = new OracleExecutionClock(_ => 424);
        clock.EnableTimer();
        clock.WaitUntil(140291);
        FailIf(clock.TimerTicks != 0, "$77 TIMA must not overflow before the 137th divider edge plus four clocks.");
        clock.ConsumeCpuWork(1);
        FailIf(clock.TimerTicks != 1 || clock.Clocks != 140736 ||
            clock.TimerCorrectionCounter != 0xff,
            "timerInterrupt must suspend foreground work and wrap the initial hFFB8=$00 to $ff.");

        var corrections = new List<bool>();
        var phased = new OracleExecutionClock(correction =>
        {
            corrections.Add(correction);
            return correction ? 460 : 424;
        }, clocks: 500000, correctionCounter: 1);
        phased.EnableTimer();
        FailIf(phased.NextTimer != 640004,
            "enableTimer must preserve DIV's phase rather than start a new 1024-clock divider.");
        phased.WaitUntil(640004);
        FailIf(phased.NextTimer != 781316 || phased.TimerCorrectionCounter != 7,
            "hFFB8 zero must reload TIMA with $76 and retain a seven-interrupt correction period.");
        for (int tick = 0; tick < 7; tick++) phased.WaitUntil(phased.NextTimer);
        FailIf(corrections.Count != 8 || !corrections[0] || !corrections[7] ||
            corrections.GetRange(1, 6).Contains(true),
            "The timer correction is tied to interrupt count, independently of application updates.");
        byte retainedCorrection = phased.TimerCorrectionCounter;
        phased.DisableTimer();
        long retainedTicks = phased.TimerTicks;
        phased.ConsumeCpuWork(500000);
        FailIf(phased.TimerTicks != retainedTicks, "disableTimer must suppress sequencing during source work.");
        phased.EnableTimer();
        FailIf(phased.TimerCorrectionCounter != retainedCorrection,
            "restartSound/enableTimer must preserve hFFB8.");

        var whole = new OracleExecutionClock(correction => correction ? 460 : 424);
        var chunks = new OracleExecutionClock(correction => correction ? 460 : 424);
        whole.EnableTimer();
        chunks.EnableTimer();
        whole.ConsumeCpuWork(40_000_000);
        for (int index = 0; index < 1000; index++) chunks.ConsumeCpuWork(40_000);
        FailIf(whole.Clocks != chunks.Clocks || whole.TimerTicks != chunks.TimerTicks ||
            whole.NextTimer != chunks.NextTimer ||
            whole.TimerCorrectionCounter != chunks.TimerCorrectionCounter,
            "Splitting foreground work across host frames must retain every audio interrupt and its phase.");

        ValidateLoadingApplicationLoop();
        ValidateScrollLoadingWorkLifecycle();
    }

    private void ValidateScrollLoadingWorkLifecycle()
    {
        var charged = new List<int>();
        _transitions.BlockingWork += charged.Add;
        try
        {
            foreach (bool batched in new[] { false, true })
            {
                for (int visit = 0; visit < 2; visit++)
                {
                    LoadValidationRoom(0, 0x8a);
                    _player.WarpTo(new(72, 32));
                    charged.Clear();
                    FailIf(_currentRoom.IsSolid(_player.Position),
                        "Scroll work regression must approach the real north exit on passable terrain.");
                    StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                    FailIf(charged.Count != 0, "Stationary gameplay charged room-loading work.");
                    for (int update = 0; !_transitions.ScrollActive && update < 40; update += 2)
                        StepGameplayUpdates(2, Vector2.Up, batched: batched);
                    FailIf(!_transitions.ScrollActive || _currentRoom.Id != 0x7a ||
                        charged.FindAll(clocks => clocks == 608168).Count != 1 ||
                        charged.FindAll(clocks => clocks == 1308).Count != 1,
                        "North exit must charge its source room setup exactly once on each visit.");
                    int count = charged.Count;
                    // A committed source scroll cannot be cancelled by
                    // reversing input. It must retain its one setup charge.
                    StepGameplayUpdates(4, visit == 0 ? Vector2.Zero : Vector2.Down, batched: batched);
                    FailIf(!_transitions.ScrollActive || charged.Count != count,
                        "A pending scroll repeated room setup work.");
                    for (int update = 0; _transitions.ScrollActive && update < 100; update += 2)
                        StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                    StepGameplayUpdates(4, Vector2.Zero, batched: batched);
                    FailIf(_transitions.ScrollActive || charged.Count != count,
                        "Scroll completion or the following gameplay repeated room setup.");
                }
            }
        }
        finally { _transitions.BlockingWork -= charged.Add; }
    }

    private void ValidateLoadingApplicationLoop()
    {
        using var split = new ExecutionTimingValidationRoot();
        using var batched = new ExecutionTimingValidationRoot();
        AddChild(split);
        AddChild(batched);
        try
        {
            foreach (ExecutionTimingValidationRoot root in new[] { split, batched })
            {
                root.Initialize();
                root.Step(241);
                if (root == split)
                {
                    const double titleLoad = (37_499_532 - 36_017_044) / (double)OracleExecutionClock.CpuClocksPerSecond;
                    const double beforeLcdOff = 256.0 / OracleExecutionClock.CpuClocksPerSecond;
                    root.Sample("inventory");
                    root.AdvanceTimedApplication(beforeLcdOff);
                    FailIf(!root._originalTiming!.Busy || !root._frontendIntroScreen!.Visible ||
                        root._mainMenuScreen!.Visible || !root._frontendIntroScreen.OriginalLcdEnabled,
                        "Title loading exposed its destination before the original disableLcd call.");
                    root.AdvanceTimedApplication(titleLoad / 2 - beforeLcdOff);
                    FailIf(root._frontendIntroScreen.Visible || !root._mainMenuScreen.Visible ||
                        root._mainMenuScreen.OriginalLcdEnabled,
                        "Title graphics must be installed behind the source LCD-off white interval.");
                    root._originalTiming.CompleteUpdate();
                    FailIf(root._originalTiming.Busy || root._originalTiming.CompletedUpdates != 242 ||
                        !root._mainMenuScreen.OriginalLcdEnabled,
                        "Title presentation did not resume after its source graphics load.");
                }
                else root.Step(1, "inventory");
                root.Step(9);
                root.Step(1, "inventory");
                root.Step(31);
                FailIf(root._originalTiming!.Clocks != 43_249_800 ||
                    root._originalTiming.TimerTicks != 299 || root._sound.Driver.ReadState(0xc014) != 32,
                    "Cold-start/title timing did not reach the independently measured file-loading boundary.");
            }
            const double loadingTime = (45_749_364 - 43_249_800) / (double)OracleExecutionClock.CpuClocksPerSecond;
            var completed = new List<long>();
            split._originalTiming!.UpdateCompleted += () => {
                FailIf(split._originalTiming.Busy,
                    "Completed-update observers saw a still-running source load.");
                completed.Add(split._originalTiming.CompletedUpdates);
            };
            // A press and release wholly inside the blocked load must never
            // become the next menu input. Timer sequencing must still run.
            split.Sample();
            const double dispatchTime = 256.0 / OracleExecutionClock.CpuClocksPerSecond;
            split.AdvanceTimedApplication(dispatchTime);
            // Native frames 304-325 stay white across this handoff. The file
            // controller exists before disableLcd, but its palettes must not
            // clear the title's white fade and expose the destination early.
            FailIf(split._mainMenu?.CurrentPage != Page.FileSelect ||
                split._mainMenuScreen!.CurrentPage != Page.Title ||
                split._mainMenuScreen.WhiteFadeOffset != 31 || !split._mainMenuScreen.OriginalLcdEnabled,
                "File loading briefly exposed the unfaded file screen before LCD blanking.");
            split.AdvanceTimedApplication(loadingTime / 4 - dispatchTime);
            FailIf(!split._originalTiming!.Busy || split._originalTiming.CompletedUpdates != 283 ||
                split._sound.Driver.ReadState(0xc014) <= 32 || completed.Count != 0,
                "Loading failed to block foreground updates while continuing audio.");
            FailIf(split._mainMenuScreen.CurrentPage != Page.FileSelect || split._mainMenuScreen.OriginalLcdEnabled,
                "File graphics must replace the title only behind the original LCD-off white interval.");
            split.Sample("attack");
            split.AdvanceTimedApplication(loadingTime / 4);
            split.Sample();
            split.AdvanceTimedApplication(loadingTime / 2);
            FailIf(completed.Count != 1 || completed[0] != 284,
                "A blocked load failed to publish exactly one completed update after its last work operation.");
            batched.Sample();
            batched.AdvanceTimedApplication(loadingTime);
            foreach (ExecutionTimingValidationRoot root in new[] { split, batched })
            {
                FailIf(root._originalTiming!.Busy || root._originalTiming.CompletedUpdates != 284 ||
                    root._sound.Driver.ReadState(0xc014) != 37 || root._sound.Channel(0).WaitFrames != 100,
                    "Completed source loading differs from clean-US fade $25 / channel-0 wait $64.");
                FailIf(root._mainMenu?.CurrentPage != Page.FileSelect,
                    "A host input edge was consumed before loading completed.");
                FailIf(root._mainMenuScreen!.CurrentPage != Page.FileSelect ||
                    !root._mainMenuScreen.OriginalLcdEnabled || root._mainMenuScreen.WhiteFadeOffset != 0,
                    "Single/batched loading ended with a stale display or retained white override.");
                root.Step(1);
                FailIf(root._mainMenu?.CurrentPage != Page.FileSelect,
                    "A press released during loading leaked into the next original input poll.");
            }
            FailIf(split._originalTiming!.Clocks != batched._originalTiming!.Clocks ||
                split._sound.Apu.Clocks != batched._sound.Apu.Clocks ||
                split._sound.Channel(0).WaitFrames != batched._sound.Channel(0).WaitFrames,
                "Host-frame partitioning changed loading completion, APU time or audio state.");
        }
        finally { split.Free(); batched.Free(); }
    }
}
