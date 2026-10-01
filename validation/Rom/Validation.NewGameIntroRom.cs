using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateNewGameIntroRom()
    {
        uint[] clocks = [0, 5, 7, 0xff, 0xfffffffe];
        foreach (bool batched in new[] { false, true })
        for (int speed = 0; speed < 5; speed++)
            RunNewGameIntroRom(batched, speed, clocks[speed], 1, accelerate: false, arrival: true);
        GD.Print("Validated clean-US pregame initialization, shared-clock oscillation/flicker, Link/sparkle OAM and graphics, all five dialogue speeds, sound/RNG/save boundaries and arrival through split/batched application updates.");
    }

    private void ValidateNewGameIntroInputRom()
    {
        foreach (bool batched in new[] { false, true })
        for (int bit = 0; bit < 8; bit++)
            RunNewGameIntroRom(batched, 2, (uint)bit, 1 << bit, accelerate: true, arrival: false);
        GD.Print("Validated clean-US pregame input gates, held A/B versus press acceleration, all eight final-text buttons, two-update text close and unskippable vanish/post-vanish holds.");
    }

    private void RunNewGameIntroRom(bool batched, int speed, uint playtime, int closeButton, bool accelerate, bool arrival)
    {
        using var root = new FrontendValidationRoot();
        AddChild(root);
        try
        {
            var save = OracleSaveData.CreateStandardGame();
            save.SetLinkName("Zelda");
            save.SetTextSpeed(speed);
            for (int index = 0; index < 4; index++) save.WriteWramByte(0xc622 + index, (byte)(playtime >> (index * 8)));
            root.Initialize(save);
            root.Step(241, batched);
            root.Step(1, batched, "inventory");
            root.Step(1, batched);
            root.Step(1, batched, "inventory");
            root.Step(32, batched);
            root.Step(1, batched, "attack");
            root.Step(1, batched, "attack");
            byte[] stored = root.FileSlotSnapshot(0);
            var seed = root._random.CaptureState();
            var sounds = root._sound.AttachPlayRequestAudit();
            var rom = new NewGameIntroRom(save, seed);
            root.Step(32, batched); // File fade completes and initializes the live file.
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            T? Read<T>(string name) where T : class => (T?)typeof(GameRoot).GetField(name, fields)!.GetValue(root);
            var intro = Read<NewGameIntroController>("_newGameIntro")!;
            var screen = Read<NewGameIntroScreen>("_newGameIntroScreen")!;
            var live = root._saveData;
            FailIf(intro is null || !intro.GraphicsLoadPending, "File selection did not initialize the pregame intro.");
            var application = new ApplicationValidationFixture(root);
            int update = 0;
            bool handoff = false;
            void Compare()
            {
                string context = $"Pregame batch={batched} speed=${speed:x2} clock=${playtime:x8} button=${closeButton:x2} update={update} " +
                    $"native=${rom[0xc2ee]:x2}/${rom[0xcc03]:x2}/${rom[0xd005]:x2} runtime={intro.CurrentStage}/{intro.StageFrame}";
                Stage expected = rom[0xc2ee] switch
                {
                    0 => Stage.RestartGame,
                    3 when rom[0xc2ef] == 0 => Stage.LoadingArrival,
                    3 => Stage.Complete,
                    2 when rom[0xc2ef] != 0x0d => Stage.Complete,
                    2 when rom[0xd000] == 0 => Stage.PostVanish,
                    2 => rom[0xd005] switch { 0 or 1 => Stage.WaitingForVoice, 2 => Stage.Dialogue, _ => Stage.Vanishing },
                    _ => throw new InvalidOperationException($"{context}: unexpected game-state dispatch.")
                };
                FailIf(intro.CurrentStage != expected, $"{context}: stage != {expected}.");
                if (!handoff)
                {
                    FailIf(screen.Dialogue.IsOpen != (rom[0xcba0] != 0), $"{context}: text lifetime differs.");
                    if (rom[0xc2ee] == 2 && rom[0xd000] != 0)
                    {
                        FailIf(screen.LinkZ != rom[0xd00f] || screen.Record.LinkX != rom[0xd00d] || screen.Record.LinkY != rom[0xd00b],
                            $"{context}: Link position/Z runtime=${screen.LinkZ:x2}, native=${rom[0xd00f]:x2}.");
                        FailIf(screen.LinkVisible != ((rom[0xd01a] & 0x80) != 0), $"{context}: Link visibility differs.");
                        if (screen.LinkVisible)
                            FailIf(screen.LinkFrame.SourceOffset != rom.LinkSourceOffset ||
                                !screen.LinkFrame.Parts.SequenceEqual(rom.OamParts(0xd000)),
                                $"{context}: Link graphic/OAM runtime=${screen.LinkFrame.SourceOffset:x4}, native=${rom.LinkSourceOffset:x4}.");
                        int orb = Enumerable.Range(0xd0, 0x10).Select(page => page * 0x100 + 0x40)
                            .FirstOrDefault(address => rom[address] != 0 && rom[address + 1] == 0x84);
                        FailIf(screen.OrbVisible != (orb != 0 && (rom[orb + 0x1a] & 0x80) != 0), $"{context}: sparkle visibility differs.");
                        if (screen.OrbVisible)
                            FailIf(!screen.OrbFrame.Parts.SequenceEqual(rom.OamParts(orb)) ||
                                screen.OrbFrame.BasePalette != rom[orb + 0x1c], $"{context}: sparkle OAM/palette differs.");
                    }
                    FailIf(screen.Dialogue.Visible != (rom[0xcba0] != 0),
                        $"{context}: text closing presentation differs.");
                    if (screen.Dialogue.Visible)
                    {
                        // State $0f restores the RAM map, but its DMA and
                        // wTextIsActive clear belong to the next $10 update.
                        FailIf(rom.TextByte(0xd0c0) != 0x10 && screen.Dialogue.VisibleGlyphCount != rom.GlyphCount,
                            $"{context}: glyph count runtime={screen.Dialogue.VisibleGlyphCount}, native={rom.GlyphCount}, text state=${rom.TextByte(0xd0c0):x2}.");
                        FailIf(screen.Dialogue.Position.Y != 80 || rom[0xcbac] != 2 ||
                            screen.Dialogue.CurrentMessage != "Accept our\nquest, hero!" || rom[0xcba4] != 0x16,
                            $"{context}: TX_1213 content/position runtime='{screen.Dialogue.CurrentMessage}'/Y={screen.Dialogue.Position.Y}, native position=${rom[0xcbac]:x2}/index=${rom.Word(0xcba2):x4}.");
                        // Text dictionary calls replace the live text index.
                        // Compare the actually expanded native line instead.
                        int textState = rom.TextByte(0xd0c0);
                        string? line = textState is 2 or 3 ? "Accept our" : textState is 4 or 0x0f ? "quest, hero!" : null;
                        FailIf(line is not null && rom.TextLine != line, $"{context}: native expanded line '{rom.TextLine}' != '{line}'.");
                    }
                    if (expected == Stage.WaitingForVoice)
                    {
                        int remaining = rom[0xd005] == 0 ? rom.Word(0xd006) + 60 : rom[0xd006];
                        FailIf(intro.StageFrame != 360 - remaining, $"{context}: voice countdown differs.");
                    }
                    if (expected == Stage.PostVanish)
                        FailIf(intro.StageFrame != 60 - rom[0xcbb3] || screen.LinkVisible || screen.OrbVisible,
                            $"{context}: post-vanish hold differs.");
                }
                if (handoff && rom[0xc2ee] == 3)
                {
                    FailIf(root._player.Position != new Vector2(rom[0xd00d], rom[0xd00b]) ||
                        root._player.ObjectZHigh != rom[0xd00f], $"{context}: arrival Link position/Z differs.");
                    bool falling = rom[0xd000] != 0 && rom[0xd004] == 0x0a && rom[0xd005] == 1;
                    FailIf(root._player.IsNewGameSlowFalling != falling ||
                        root._player.Visible != (rom[0xd000] != 0 && (rom[0xd01a] & 0x80) != 0),
                        $"{context}: arrival falling/visible runtime={root._player.IsNewGameSlowFalling}/{root._player.Visible}, native=${rom[0xd000]:x2}/${rom[0xd004]:x2}/${rom[0xd005]:x2}/${rom[0xd01a]:x2}.");
                    if (falling)
                    {
                        var frames = (IntroSpriteFrame[])typeof(Player).GetField("_newGameFallFrames", fields)!.GetValue(root._player)!;
                        IntroSpriteFrame frame = frames[root._player.NewGameSlowFallFrame];
                        FailIf(frame.SourceOffset != rom.LinkSourceOffset || !frame.Parts.SequenceEqual(rom.OamParts(0xd000)),
                            $"{context}: arrival Link animation differs.");
                    }
                    FailIf(root._inventoryMenu.IsActive || root._mapMenu.IsActive || root._dialogue.IsOpen,
                        $"{context}: arrival leaked input into a gameplay menu.");
                }
                for (int index = 0; index < 4; index++)
                    FailIf(live.ReadWramByte(0xc622 + index) != rom[0xc622 + index], $"{context}: main clock byte {index} differs.");
                for (int address = 0xc6d0; address < 0xc6e0; address++)
                    FailIf(live.ReadWramByte(address) != rom[address], $"{context}: global flag byte ${address:x4} differs.");
                FailIf(!stored.SequenceEqual(root.FileSlotSnapshot(0)), $"{context}: the cutscene wrote live state to disk.");
                OracleRandomState rng = root._random.CaptureState();
                FailIf(rng.Rng1 != rom[0xff94] || rng.Rng2 != rom[0xff95] || rng.Calls - seed.Calls != rom.RandomCalls,
                    $"{context}: shared RNG differs.");
                FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),
                    $"{context}: sound requests runtime={string.Join(',', sounds.Requests)}, native={string.Join(',', rom.Sounds)}.");
            }
            void Step(int count = 1, int pressed = 0, int? held = null)
            {
                int edge = pressed;
                string[] Actions(int mask) => new[] { "attack", "item", "map", "inventory", "move_right", "move_left", "move_up", "move_down" }
                    .Where((_, bit) => (mask & (1 << bit)) != 0).ToArray();
                application.Step(count, Vector2.Zero, Actions(held ?? pressed), Actions(pressed), batched, () =>
                {
                    rom.Update(edge, held ?? pressed); edge = 0; update++;
                    handoff = Read<NewGameIntroController>("_newGameIntro") is null;
                    Compare();
                });
            }
            Compare();
            byte[] beforeLoad = live.Serialize();
            root.Step(1, batched, "attack", "inventory", "move_down");
            FailIf(intro.GraphicsLoadPending || !beforeLoad.SequenceEqual(live.Serialize()),
                "Pregame graphics-load completion accepted input or advanced the main clock.");
            Compare();
            Step(298, 0xff);
            Step(1, 0xff); // Original $012c lead-in reaches its separate $3c voice wait.
            Step(59, 0xff);
            FailIf(screen.Dialogue.IsOpen, "TX_1213 opened before 300+60 main updates.");
            Step(1, 0xff); // Opening input is consumed by standard text state 0.
            Step(1, 0xff); // State 1 prepares the first line without revealing it.
            Step(8, held: accelerate ? 3 : 0); // Held A/B alone must not accelerate.
            if (accelerate) { Step(pressed: 1); Step(pressed: 2); Step(pressed: 3); }
            Step(200);
            FailIf(!screen.Dialogue.IsPageComplete || rom.TextByte(0xd0c0) != 0x0f,
                "TX_1213 did not finish both lines at the selected text speed.");
            Step(pressed: closeButton);
            Step(pressed: 0xff); // Text state $10 still owns this original update.
            FailIf(intro.CurrentStage != Stage.Dialogue || screen.Dialogue.IsOpen,
                "Pregame Link observed text completion before the text thread cleared it.");
            Step(pressed: 0xff);
            FailIf(intro.CurrentStage != Stage.Vanishing || intro.StageFrame != 0,
                "Pregame text release did not create the glowing orb on the next object pass.");
            Step(61, 0xff);
            FailIf(intro.CurrentStage != Stage.Vanishing || rom[0xcbb9] != 7,
                "Pregame vanish lost its terminal-parameter/object-handler boundary.");
            Step(pressed: 0xff);
            Step(59, 0xff);
            FailIf(live.HasGlobalFlag(GlobalFlag.LinkSummoned), "GLOBALFLAG_3d was set before the final $3c hold update.");
            Step(pressed: 0xff);
            FailIf(!live.HasGlobalFlag(GlobalFlag.LinkSummoned), "Pregame completion did not set GLOBALFLAG_3d.");
            Step(pressed: 0xff); // initializeGame observes the flag; no second orb intro.
            Step(pressed: 0xff); // Arrival graphics and SND_WARP_START.
            FailIf(!handoff || live.HasGlobalFlag(GlobalFlag.PregameIntroDone), "Pregame handoff completed arrival early.");
            if (arrival)
            {
                Step(65, 0xff);
                Step(128, 0xff);
                FailIf(live.HasGlobalFlag(GlobalFlag.PregameIntroDone), "GLOBALFLAG_PREGAME_INTRO_DONE was set before the final wave boundary.");
                Step(pressed: 0xff);
                FailIf(!live.HasGlobalFlag(GlobalFlag.PregameIntroDone), "Arrival completion did not set GLOBALFLAG_PREGAME_INTRO_DONE.");
            }
        }
        finally { root.CompleteScenePreload(); root.Free(); }
    }
}
