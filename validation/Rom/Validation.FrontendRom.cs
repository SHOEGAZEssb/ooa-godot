using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateFrontendIntroRom()
    {
        int hostCase1 = 0;
        foreach (FrontendIntroStage? skipAt in new FrontendIntroStage?[]
            { FrontendIntroStage.Horse, FrontendIntroStage.Temple, FrontendIntroStage.PreTitle, null })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            using var root = new FrontendValidationRoot();
            AddChild(root);
            try
            {
                root.Initialize();
                var intro = root._frontendIntro!;
                var sounds = root._sound.AttachPlayRequestAudit();
                var application = new ApplicationValidationFixture(root);
                var rom = new FrontendRom();
                OracleRandomState seed = root._random.CaptureState();
                rom[0xff94] = seed.Rng1;
                rom[0xff95] = seed.Rng2;
                void Step(int updates, int pressed = 0)
                {
                    int left = pressed;
                    string[] keys = pressed == 0 ? [] : ["inventory"];
                    application.Step(updates, Vector2.Zero, keys, keys, batched, () =>
                    {
                        rom.UpdateIntro(left);
                        left = 0;
                        int stage = intro.Stage switch
                        {
                            FrontendIntroStage.Boot => 0, FrontendIntroStage.Capcom => 1,
                            FrontendIntroStage.Title => 3, FrontendIntroStage.Restart => 4, _ => 2
                        };
                        FailIf(stage != rom[0xc2e6] || intro.State != rom[0xc2e7] ||
                            intro.FrameCounter != rom[0xcbb7] || intro.InputsEnabled != (rom[0xffb9] != 0),
                            $"Frontend batch={batched} skip={skipAt} frame=${intro.FrameCounter:x2}/${rom[0xcbb7]:x2} active={intro.IsActive}: runtime={intro.Stage}/${intro.State:x2}/{intro.Counter}, native=${rom[0xc2e6]:x2}/${rom[0xc2e7]:x2}/{rom.Word(0xcbb3)}, input={intro.InputsEnabled}/${rom[0xffb9]:x2}.");
                        OracleRandomState actual = root._random.CaptureState();
                        FailIf(actual.Rng1 != rom[0xff94] || actual.Rng2 != rom[0xff95] ||
                            root._random.Calls - seed.Calls != rom.RandomCalls,
                            "Frontend intro changed native shared RNG state/order.");
                        if (intro.Stage is FrontendIntroStage.Capcom or FrontendIntroStage.Title)
                            FailIf(intro.Counter != rom.Word(0xcbb3),
                                $"Frontend {intro.Stage}: counter {intro.Counter} != ROM ${rom.Word(0xcbb3):x4}.");
                        if (rom[0xc4ab] is 1 or 2 or 9 or 10)
                            FailIf(root._mainMenuScreen!.WhiteFadeOffset != rom[0xc2ff],
                                "Frontend active fade differs from native $c2ff offset.");
                        if (stage == 2)
                            FailIf(rom[0xcbb5] != intro.Stage switch
                            {
                                FrontendIntroStage.Horse => 0, FrontendIntroStage.Temple => 1, _ => 2
                            }, "Frontend cinematic dispatch differs from native $cbb5.");
                        if (intro.Stage == FrontendIntroStage.Temple && intro.State != 0 && rom[0xd000] != 0)
                            FailIf(intro.TempleLinkY != rom[0xd00b] || intro.TempleLinkZ != rom[0xd00f] ||
                                intro.TempleCameraY != rom[0xffaa],
                                $"Temple ${intro.State:x2}: Link Y/Z/camera runtime=${intro.TempleLinkY:x2}/${intro.TempleLinkZ:x2}/${intro.TempleCameraY:x2}, ROM=${rom[0xd00b]:x2}/${rom[0xd00f]:x2}/${rom[0xffaa]:x2}.");
                        if (intro.Stage == FrontendIntroStage.Temple && intro.State == 6 ||
                            intro.Stage == FrontendIntroStage.PreTitle && intro.State == 3)
                            FailIf(intro.FlashWhite != (rom[0xcbba] == 0), "Frontend white flash differs from native threshold comparison.");
                        // The menu constructor starts its music at the transfer;
                        // the native thread's initialization is checked below.
                        if (!rom.FileSelectHandoff)
                            FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),
                                $"Frontend {intro.Stage}/${intro.State:x2} frame=${intro.FrameCounter:x2}: sound requests differ, runtime={string.Join(',', sounds.Requests)}, ROM={string.Join(',', rom.Sounds)}.");
                    });
                }
                Step(1, 8);
                Step(100, 8); // Start remains blocked during the logo hold.
                Step(108);
                Step(32, 8); // The logo fade blocks Start as well.
                FrontendIntroStage target = skipAt ?? FrontendIntroStage.Title;
                for (int chunk = 0; chunk < 180 && intro.Stage != target; chunk++)
                    Step(32);
                FailIf(intro.Stage != target,
                    $"ROM intro did not reach {target} within 6000 updates.");
                if (skipAt is not null) Step(1, 8);
                Step(8);
                // Exercise the native $0960 countdown, fade, restart dispatch,
                // and boot again before transferring to file selection.
                int titleRemaining = root._frontendIntro!.Counter;
                Step(titleRemaining);
                Step(33);
                Step(240);
                Step(1, 8);
                Step(8);
                Step(1, 8);
                Step(32);
                FailIf(!rom.FileSelectHandoff || root._mainMenu?.CurrentPage != Page.FileSelect,
                    "ROM/runtime title fade did not transfer ownership to file selection.");
                rom.InitializeMenu();
                FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),
                    "Frontend transfer changed the native file-select music request.");
            }
            finally { root.Free(); }
        }
        GD.Print("Validated clean-US boot/input gate, cinematic, title countdown/replay, sound/RNG order and file-thread transfer through split/batched application updates.");
    }

    private void ValidateMainMenuRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            using var root = new FrontendValidationRoot();
            AddChild(root);
            try
            {
                root.Initialize(persistSaveData: true);
                root.Step(241, batched);
                root.Step(1, batched, "inventory");
                root.Step(1, batched);
                root.Step(1, batched, "inventory");
                root.Step(32, batched);
                var application = new ApplicationValidationFixture(root);
                var rom = new FrontendRom();
                rom.InitializeMenu();
                var sounds = root._sound.AttachPlayRequestAudit();
                rom.Sounds.Clear();
                var menu = root._mainMenu!;
                var screen = root._mainMenuScreen!;
                int update = 0;
                string[] Actions(int keys) => new[] { "attack", "item", "map", "inventory",
                    "move_right", "move_left", "move_up", "move_down" }
                    .Where((_, bit) => (keys & (1 << bit)) != 0).ToArray();
                void Step(int updates = 1, int pressed = 0, int? held = null)
                {
                    int edge = pressed;
                    application.Step(updates, Vector2.Zero, Actions(held ?? pressed), Actions(pressed), batched, () =>
                    {
                        rom.UpdateMenu(edge, held ?? pressed);
                        edge = 0;
                        update++;
                        FailIf(menu.IsActive == rom.GameplayHandoff,
                            $"Menu update {update}: runtime/native gameplay handoff differs.");
                        Page expected = rom[0xcbb3] switch
                        {
                            1 => rom[0xcbb4] < 2 ? Page.FileSelect : Page.TextSpeed,
                            2 => Page.NameEntry,
                            3 => rom[0xcbb4] < 2 ? Page.CopySource : rom[0xcbb4] == 2 ? Page.CopyDestination : Page.CopyConfirm,
                            4 => rom[0xcbb4] < 2 ? Page.EraseSelect : Page.EraseConfirm,
                            5 => Page.NewFileOptions,
                            _ => throw new InvalidOperationException($"Unhandled menu ROM mode ${rom[0xcbb3]:x2}.")
                        };
                        FailIf(menu.CurrentPage != expected,
                            $"Menu update {update}: {menu.CurrentPage} != ROM ${rom[0xcbb3]:x2}/${rom[0xcbb4]:x2} ({expected}).");
                        if (rom[0xcbb4] != 0 && expected != Page.NameEntry)
                            FailIf(menu.Cursor != rom[0xcbbc],
                                $"Menu update {update} {expected}: cursor ${menu.Cursor:x2} != ROM ${rom[0xcbbc]:x2}.");
                        if (expected == Page.NameEntry && rom[0xcbb4] == 1)
                        {
                            FailIf(screen.NameCursor != rom[0xcbbc],
                                $"Name update {update}: cursor ${screen.NameCursor:x2} != ROM ${rom[0xcbbc]:x2}.");
                            for (int index = 0; index < 5; index++)
                                FailIf(screen.RawEnteredName[index] != (char)rom.NameByte(index),
                                    $"Name update {update}: glyph {index} differs from native $d7a0 buffer.");
                        }
                        if (expected == Page.TextSpeed)
                            FailIf(screen.TextSpeed != rom[0xc629], "Menu message speed differs from native $c629.");
                        if (rom[0xc4ab] == 1)
                            FailIf(screen.WhiteFadeOffset != rom[0xc2ff],
                                "File-start fade differs from native $c2ff offset.");
                        if (expected is Page.CopyConfirm or Page.EraseConfirm ||
                            expected == Page.FileSelect && rom[0xcbbc] == 3)
                            FailIf(screen.Choice != (rom[0xcbbd] & 1), "Menu confirmation choice differs from native $cbbd.");
                        if (expected == Page.EraseConfirm && rom[0xcbb4] == 3)
                            FailIf(screen.EraseHealth != rom.DisplayHearts(rom[0xff9a]),
                                "Erase countdown differs from native file-display hearts.");
                        // Stop at restartThisThread. New-game initialization
                        // and its music belong to the destination owner.
                        FailIf(!sounds.Requests.Take(rom.Sounds.Count).SequenceEqual(rom.Sounds) ||
                            !rom.GameplayHandoff && sounds.Requests.Count != rom.Sounds.Count,
                            $"Menu update {update} {expected}: sounds runtime={string.Join(',', sounds.Requests)}, ROM={string.Join(',', rom.Sounds)}.");
                    });
                }
                void Press(int keys) => Step(pressed: keys);
                void CompareSave(int slot)
                {
                    OracleSaveData.TryDeserialize(root.FileSlotSnapshot(slot), out var save);
                    for (int address = 0xc5b0; address < 0xcb00; address++)
                        FailIf(save!.ReadWramByte(address) != rom.SavedByte(slot, address) ||
                            rom.SavedByte(slot, address) != rom.SavedByte(slot, address, backup: true),
                            $"Menu slot ${slot:x2}: persisted byte ${address:x4} differs from native primary/backup.");
                }
                Press(0xc0); // Up takes priority over Down; wrap to bottom.
                Press(0x30); // Left before Right.
                Press(0x10);
                Press(0x10); // A clamped choice makes no extra sound.
                Press(0x40); Press(0x40); Press(0x40);
                Press(1); // Empty file -> new-file choices.
                Press(1); // Initialization must consume input.
                Press(0xc1); // Down before Up/A.
                Press(0x40);
                Press(0x06); // Back, then repeat the entry.
                Step(); Press(1); Step(); Press(1); Step();
                Press(0x11); // Right before A in the name dispatcher.
                Step(39, held: 0x10);
                Step(1, held: 0x10); // Autofire at $28.
                Step(8, held: 0x10); // Repeat every four original updates.
                Press(4); Press(1); Press(2); Press(1);
                Press(8); Press(8); // Select OK, then commit.
                Step(); Step();
                CompareSave(0);
                Press(1);
                Press(0x31); // Right before Left/A.
                Press(0x15); // Select backs out before movement/A.
                Press(1);
                Press(0x20); Press(0x20); Press(0x20); // Clamp at zero.
                Press(0x10); Press(0x10); Press(0x10); Press(0x10); Press(0x10);
                Press(2); // Cancel and reload to discard unsaved speed.
                Press(1); Step();
                FailIf(root._mainMenuScreen!.TextSpeed != 2, "Canceling message speed persisted unsaved settings.");
                Press(2);
                Press(0xc1); // Up to bottom still falls through to A: Copy.
                Press(1); Press(0x80); Press(1);
                Press(0x40); Press(0x80); // Destination skips the source.
                Press(1); Press(3); Press(2); // B wins; return to source.
                Press(1); Press(1); Press(0x10); Press(1);
                Step(); CompareSave(1);
                Press(0x40); Press(0x10); Press(1); // Erase.
                Press(1); Press(0x80); Press(0x80); Press(1);
                Press(3); Press(1); Press(0x10); Press(1);
                Step(25, pressed: 0xff); // Input is ignored during erase.
                FailIf(!root.FileSlotExists(1), "Erase deleted SRAM before its zero-heart completion update.");
                Step();
                FailIf(root.FileSlotExists(1), "Erase completion retained the destination file.");
                for (int address = 0xc5b0; address < 0xcb00; address++)
                    FailIf(rom.SavedByte(1, address) != 0 || rom.SavedByte(1, address, backup: true) != 0,
                        $"Erase retained native primary/backup byte ${address:x4}.");
                Step(); CompareSave(0);
                // Re-enter copy after erase; an empty source must reject A.
                Press(0x40); Press(1); Step(); Press(0x80); Press(0x80); Press(1);
                FailIf(menu.CurrentPage != Page.CopySource, "An erased copy source was accepted.");
                Press(0x80); Press(0x80); Press(1); Step();
                Press(1); Press(0x10); Press(0x10); Press(1);
                CompareSave(0); // Save the speed before the gameplay fade.
                Step(31, pressed: 0xff);
                FailIf(!menu.IsActive || rom.GameplayHandoff, "File start completed before the palette stop was observed.");
                Step();
                FailIf(menu.IsActive || !rom.GameplayHandoff || root._mainMenu is not null,
                    "File start did not release the menu on the native handoff update.");
                root.CompleteScenePreload();
            }
            finally { root.Free(); }
        }
        GD.Print("Validated clean-US file navigation, input/init gates, name autofire, persisted primary/backup bytes, message speed, copy/erase and gameplay fade handoff through split/batched application updates.");
    }
}
