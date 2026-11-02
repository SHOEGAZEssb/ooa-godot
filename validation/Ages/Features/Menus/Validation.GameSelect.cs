using System.IO;
using System.Threading.Tasks;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateGameSelect()
    {
        Vector2I originalSize = GetWindow().ContentScaleSize;
        var originalAspect = GetWindow().ContentScaleAspect;
        byte[] save = _saveData.Serialize();
        int rng = _random.Calls;
        foreach (bool batched in new[] { false, true })
        {
            var root = new FrontendValidationRoot();
            AddChild(root);
            try
            {
                GameSelectScreen screen = root.InitializeGameSelect();
                var ages = screen.Preview(OracleGame.Ages);
                var seasons = screen.Preview(OracleGame.Seasons);
                FailIf(screen.SelectedGame != OracleGame.Ages || !ages.IsSelected || seasons.IsSelected ||
                    GetWindow().ContentScaleSize != new Vector2I(480, 270) ||
                    GetWindow().ContentScaleAspect != Window.ContentScaleAspectEnum.Expand,
                    "Game select must start with Ages active on a full-width expanding canvas.");
                // ages.s:oamData_7249 and introCutscenes.s:seasonsOamData_03_7397.
                FailIf(ages.Panorama!.Sprites.Count != 39 || seasons.Panorama!.Sprites.Count != 36 ||
                    ages.Panorama.Sprites[0].Position != new Vector2(0x38 - 8, 0x38 - 16) ||
                    seasons.Panorama.Sprites[0].Position != new Vector2(0x3f - 8, 0x51 - 16) ||
                    seasons.Panorama.Sprites[^1].Position != new Vector2(0x3c - 8, 0x90 - 16),
                    "Game-select source close-ups lost their original OAM counts, order or hardware origins.");
                FailIf(seasons.GetNode<Label>("Hint").Text != "NEW GAME · OPENING",
                    "Seasons must identify the playable opening before confirmation.");
                foreach (var preview in new[] { ages, seasons })
                {
                    var title = preview.GetNode<TextureRect>("Title");
                    FailIf(title.Texture?.GetSize() != new Vector2(144, 90) || !title.UseParentMaterial ||
                        title.MouseFilter != Control.MouseFilterEnum.Ignore || preview.HasNode("Series"),
                        "Game names must use native graphics, inherit inactive grayscale and pass clicks to their preview.");
                    using Image logo = title.Texture!.GetImage();
                    byte[] pixels = logo.GetData();
                    int opaque = 0;
                    for (int offset = 3; offset < pixels.Length; offset += 4)
                        if (pixels[offset] != 0) opaque++;
                    // GFXH_TITLESCREEN bank-0 art in (12,20)-(156,110), with
                    // PALH_03 shade $00 outside subtitle palettes $04/$06,
                    // flame palette $01 and flame-edge colors in $05/$07 are transparent.
                    // The (45,75) subtitle pixel is shade $03 in Ages, $01 in Seasons.
                    Color subtitle = logo.GetPixel(45, 75);
                    Color expected = preview.Game == OracleGame.Ages
                        ? new Color(0x0b / 31f, 0, 0x11 / 31f)
                        : new Color(1, 1, 0x1d / 31f);
                    FailIf(opaque != 6565 ||
                        logo.GetPixel(0, 0).A != 0 || subtitle.A != 1 ||
                        Math.Abs(subtitle.R - expected.R) > 1 / 255f ||
                        Math.Abs(subtitle.G - expected.G) > 1 / 255f ||
                        Math.Abs(subtitle.B - expected.B) > 1 / 255f,
                        "Game-select logos lost source title/subtitle pixels, palettes or transparent backdrops.");
                    // gfx_titlescreen_1: letter and TM shadows are shade $00,
                    // addressed through palette $04 (TM uses $06 in Seasons).
                    FailIf(logo.GetPixel(62, 70).A != 1 || logo.GetPixel(103, 68).A != 1,
                        "Ages/Seasons lettering and TM shadows must remain opaque over the panorama.");
                }
                // Inverted Seasons PNG shades address palette $00 in reverse;
                // paletteData4470 color $03 is RGB5 ($11,$1c,$00).
                byte[] backgroundPalette = OracleGraphicsData.ReadBytes(
                    SeasonsGameContent.PreviewRoot + "din_bg_palette.bin", 96);
                FailIf(backgroundPalette[0] != 0x11 || backgroundPalette[1] != 0x1c || backgroundPalette[2] != 0,
                    "The Din background palette lost its source PNG inversion.");
                root.Step(16, batched);
                FailIf(ages.AnimationUpdates != 16 || seasons.AnimationUpdates != 0,
                    "The inactive Seasons panorama advanced while Ages was selected.");
                root.Step(1, batched, "move_down");
                root.Step(80, batched);
                FailIf(ages.AnimationUpdates != 16 || seasons.AnimationUpdates != 81 || ages.IsSelected || !seasons.IsSelected ||
                    !((ShaderMaterial)ages.Material).GetShaderParameter("inactive").AsBool() ||
                    ((ShaderMaterial)seasons.Material).GetShaderParameter("inactive").AsBool(),
                    "Selecting Seasons must freeze and desaturate Ages, and resume only Seasons.");
                root.Step(4, batched);
                FailIf(!screen.IsActive || root._gameSelect != screen || root._frontendIntro is not null ||
                    root.GameplayPrepared || root._random.Calls != 0 || root.FileSlotExists(0, OracleGame.Ages),
                    "Previewing Seasons started a game, preparation, RNG or persistent file writes.");
                root.Step(1, batched, "move_up");
                FailIf(ages.AnimationUpdates != 17 || seasons.AnimationUpdates != 85,
                    "Switching back must resume the paused frame, rather than restart either preview.");
                root.Step(1, batched, "move_up", "move_down");
                FailIf(screen.SelectedGame != OracleGame.Ages, "Opposing navigation selected another game.");
                screen.RequestPointerSelection(OracleGame.Seasons, false);
                root.Step(1, batched);
                FailIf(!screen.IsActive || screen.SelectedGame != OracleGame.Seasons,
                    "Pointer selection must highlight Seasons without confirming it.");
                screen.RequestPointerSelection(OracleGame.Seasons, true);
                screen.RequestPointerSelection(OracleGame.Ages, false);
                root.Step(1, batched);
                FailIf(!screen.IsActive || screen.SelectedGame != OracleGame.Ages,
                    "A Seasons click followed by hovering Ages must not confirm Ages.");
                var agesOptions = ages.GetNode<TextureButton>("Options");
                var seasonsOptions = seasons.GetNode<TextureButton>("Options");
                var agesCursor = agesOptions.GetNode<TextureRect>("Acorn");
                var seasonsCursor = seasonsOptions.GetNode<TextureRect>("Acorn");
                agesOptions.EmitSignal(BaseButton.SignalName.Pressed);
                screen.RequestPointerSelection(OracleGame.Ages, false);
                root.Step(1, batched);
                FailIf(!screen.IsActive || screen.OptionsSelected || screen.Options.Visible,
                    "An Options click followed by hovering the same game's preview must not launch the game.");
                screen.RequestPointerSelection(OracleGame.Ages, true);
                screen.RequestOptionsSelection(OracleGame.Ages);
                root.Step(1, batched);
                FailIf(!screen.IsActive || !screen.OptionsSelected || screen.Options.Visible,
                    "A preview click followed by hovering Options must not confirm the Options menu.");
                root.Step(1, batched, "move_left");
                root.Step(1, batched, "move_right");
                FailIf(!screen.OptionsSelected || !agesCursor.Visible || seasonsCursor.Visible ||
                    agesCursor.Texture?.GetSize() != new Vector2(8, 16),
                    "Right must select Ages Options and show the native 8x16 acorn only there.");
                new ApplicationValidationFixture(root).Step(32, Vector2.Zero, batched: batched,
                    afterUpdate: () => FailIf(!agesCursor.Visible || seasonsCursor.Visible,
                        "The selected Options acorn must remain visible on every update without blinking."));
                root.Step(4, batched, "attack", "inventory");
                FailIf(!screen.IsActive || !screen.OptionsSelected || !screen.Options.Visible || root._gameSelect != screen,
                    "Confirming Options must open its save menu without launching Ages.");
                root.Step(1, batched, "item");
                FailIf(screen.Options.Visible || !screen.OptionsSelected,
                    "B must close the save menu and retain Options focus.");
                root.Step(1, batched, "move_down");
                FailIf(screen.SelectedGame != OracleGame.Seasons || !screen.OptionsSelected ||
                    agesCursor.Visible || !seasonsCursor.Visible,
                    "Vertical navigation must move the Options acorn to the other game's button.");
                root.Step(1, batched, "move_left");
                FailIf(screen.OptionsSelected || agesCursor.Visible || seasonsCursor.Visible,
                    "Left must return focus to the game selection and clear both Options cursors.");
                agesOptions.EmitSignal(BaseButton.SignalName.Pressed);
                root.Step(1, batched);
                FailIf(screen.SelectedGame != OracleGame.Ages || !screen.OptionsSelected || !screen.IsActive,
                    "Clicking Ages Options must select its button without passing through to launch.");
                root.Step(1, batched, "item", "attack");
                FailIf(screen.Options.Visible || !screen.OptionsSelected || !screen.IsActive,
                    "B must close the save menu without launching or moving the Options cursor.");
                root.Step(1, batched, "item");
                FailIf(screen.OptionsSelected || !screen.IsActive,
                    "B must leave Options without also consuming a simultaneous confirm as a launch.");
                seasonsOptions.EmitSignal(Control.SignalName.MouseEntered);
                root.Step(1, batched);
                FailIf(screen.SelectedGame != OracleGame.Seasons || !screen.OptionsSelected || !seasonsCursor.Visible,
                    "Hovering an Options button must select that game and its acorn.");
                seasons._GuiInput(new InputEventMouseMotion());
                root.Step(1, batched);
                FailIf(screen.OptionsSelected || seasonsCursor.Visible,
                    "Moving back onto the panorama must leave Options focus.");
                using (Image button = agesOptions.TextureNormal.GetImage())
                {
                    for (int y = 0; y < 3; y++)
                    for (int x = 0; x < 96; x++)
                        FailIf(button.GetPixel(x, y).A != 0, "Options button retained its black backdrop strip above the plank.");
                }
                // Use actual anchored controls at several window shapes, with no
                // gameplay/camera coordinates involved in either panel's bounds.
                foreach (var size in new[] { new Vector2(480, 270), new Vector2(640, 576), new Vector2(720, 1280) })
                {
                    screen.Size = size;
                    FailIf(ages.Position != Vector2.Zero || ages.Size != new Vector2(size.X, size.Y / 2) ||
                        seasons.Position != new Vector2(0, size.Y / 2) || seasons.Size != ages.Size,
                        "The two game previews must fill equal top/bottom halves at every aspect ratio.");
                    foreach (var preview in new[] { ages, seasons })
                    {
                        var title = preview.GetNode<TextureRect>("Title");
                        FailIf(title.Position != new Vector2(16, 16) ||
                            title.Size != new Vector2(144, 90),
                            "Native game-title graphics must stay in each panel's top-left corner without stretching or clipping.");
                        var options = preview.GetNode<TextureButton>("Options");
                        var cursor = options.GetNode<TextureRect>("Acorn");
                        FailIf(options.Position != preview.Size - new Vector2(112, 32) || options.Size != new Vector2(96, 16) ||
                            options.TextureNormal?.GetSize() != new Vector2(96, 16) || options.Disabled ||
                            cursor.Position != new Vector2(2, 0) || cursor.Size != new Vector2(8, 16) ||
                            options.MouseFilter != Control.MouseFilterEnum.Stop || !options.UseParentMaterial,
                            "Native Options planks must stay inside each panel's bottom-right corner, inherit its palette state and block launch clicks.");
                    }
                }
                ValidateGameSelectTransfers(root, screen, batched);
                screen.RequestPointerSelection(OracleGame.Ages, true);
                root.Step(4, batched);
                FailIf(screen.Visible || screen.IsActive || root._gameSelect is not null || root._frontendIntro is not null ||
                    !root.GetNode<BootLoadingScreen>("%BootLoading").Visible,
                    "Confirming Ages must transfer once to its existing resource-loading screen.");
                var deadline = System.Diagnostics.Stopwatch.StartNew();
                while (root._frontendIntro is null && deadline.Elapsed.TotalSeconds < 20)
                {
                    root.AdvanceHost(1.0 / 60);
                    System.Threading.Thread.Sleep(1);
                }
                FailIf(root._frontendIntro is null || root._frontendIntro.Stage != FrontendIntroStage.Boot ||
                    root._frontendIntro.FrameCounter != 0 || screen.Visible ||
                    GetWindow().ContentScaleSize != originalSize || GetWindow().ContentScaleAspect != originalAspect,
                    "Ages must reach its untouched original intro with the normal viewport and no leaked confirm edge.");
            }
            finally { root.Free(); }
            FailIf(GetWindow().ContentScaleSize != originalSize || GetWindow().ContentScaleAspect != originalAspect,
                "Leaving game select or boot loading failed to restore the native viewport.");
        }
        FailIf(!save.SequenceEqual(_saveData.Serialize()) || _random.Calls != rng,
            "Game selection changed the active gameplay save or RNG.");
        GD.Print("Validated split-screen selection, paused previews, grayscale, resizing, input batching and Ages boot handoff.");
    }

    private static void ValidateGameSelectTransfers(FrontendValidationRoot root, GameSelectScreen screen, bool batched)
    {
        Vector2I originalWindowSize = screen.GetWindow().Size;
        screen.GetWindow().Size = new Vector2I(640, 576);
        screen.Size = screen.GetViewportRect().Size;
        string directory = Path.Combine(Path.GetTempPath(), $"oracle-transfer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            foreach (OracleGame game in new[] { OracleGame.Ages, OracleGame.Seasons })
            {
                var existing = OracleSaveData.CreateStandardGame(game);
                existing.SetLinkName("OLD");
                root.SeedFileSlot(0, existing);
                root.SeedFileSlot(2, existing);
                var incoming = OracleSaveData.CreateStandardGame(game);
                incoming.SetLinkName("NEW");
                string source = Path.Combine(directory, "source.sav");
                File.WriteAllBytes(source, incoming.Serialize());
                screen.Preview(game).GetNode<TextureButton>("Options").EmitSignal(BaseButton.SignalName.Pressed);
                root.Step(1, batched);
                var options = screen.Options;
                var files = options.GetNode<FileDialog>("Files");
                files.UseNativeDialog = false;
                var replace = options.GetNode<Control>("Board/Replace");
                var notice = options.GetNode<Control>("Board/Notice");
                var import = options.GetNode<TextureButton>("Board/Import");
                var export = options.GetNode<TextureButton>("Board/Export");
                var board = options.GetNode<Control>("Board");
                var previous = options.GetNode<TextureButton>("Board/PreviousSlot");
                var next = options.GetNode<TextureButton>("Board/NextSlot");
                FailIf(options.Background.GetSize() != new Vector2(160, 144) || board.Size != new Vector2(160, 144) ||
                    import.Position != new Vector2(32, 72) || export.Position != new Vector2(32, 96) ||
                    options.HasNode("Center") || options.HasNode("Replace"),
                    "Save options must use native 160x144 graphics and controls instead of themed panels or confirmation windows.");
                var sounds = root._sound.AttachPlayRequestAudit();
                options.GetViewport().NotifyMouseEntered();
                foreach (var (button, selected) in new[] { (previous, 2), (next, 0), (next, 1), (previous, 0) })
                {
                    Vector2 position = button.GetGlobalTransformWithCanvas() * (button.Size / 2);
                    sounds.Clear();
                    options.GetViewport().PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);
                    options.GetViewport().PushInput(new InputEventMouseButton
                        { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = true }, true);
                    options.GetViewport().PushInput(new InputEventMouseButton
                        { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = false }, true);
                    root.Step(1, batched);
                    FailIf(options.SelectedSlot != selected || !sounds.Requests.SequenceEqual(new[] { 0x84 }) ||
                        !options.Visible || !screen.IsActive,
                        $"{game}, {button.Name}: selected {options.SelectedSlot}, expected {selected}; sounds [{string.Join(",", sounds.Requests)}]. Arrow clicks must wrap once and play $84.");
                }
                options.GetViewport().PushInput(new InputEventMouseMotion(), true);
                options.GetViewport().NotifyMouseExited();
                using (Image native = options.Background.GetImage())
                using (Texture2D template = new NativeMenuPresentation(OracleAssetPack.ForGame(game)).BuildBackground(false))
                using (Image original = template.GetImage())
                {
                    for (int row = 0; row < 4; row++)
                    for (int y = 3; y < 16; y++)
                    foreach (int x in new[] { 32, 33, 126, 127 })
                        FailIf(native.GetPixel(x, 48 + row * 24 + y) != original.GetPixel(x, 56 + y),
                            "Save-transfer planks must preserve the native CONTINUE panel end caps and palette.");
                }

                void DismissNotice()
                {
                    for (int page = 0; notice.Visible && page < 32; page++)
                    {
                        root.Step(1, batched, "attack");
                        root.Step(1, batched);
                    }
                    FailIf(notice.Visible, "Native save messages must be dismissible without launching a game.");
                }

                void Pick(TextureButton button, string path)
                {
                    DismissNotice();
                    button.EmitSignal(BaseButton.SignalName.Pressed);
                    FailIf(!files.Visible, "Save transfer must open a file picker.");
                    root.Step(4, batched, "move_down", "attack");
                    FailIf(!options.Visible || screen.SelectedGame != game || !screen.IsActive,
                        "The file picker must block game navigation and launch.");
                    int slot = options.SelectedSlot;
                    previous.EmitSignal(BaseButton.SignalName.Pressed);
                    next.EmitSignal(BaseButton.SignalName.Pressed);
                    FailIf(options.SelectedSlot != slot, "Slot arrows must not change the target while a file picker is open.");
                    files.Hide();
                    files.EmitSignal(FileDialog.SignalName.FileSelected, path);
                    root.Step(1, batched);
                }
                void Confirm()
                {
                    FailIf(!replace.Visible, "A valid import must ask before replacing a slot.");
                    replace.GetNode<TextureButton>("Import").EmitSignal(BaseButton.SignalName.Pressed);
                    root.Step(1, batched);
                }

                Pick(import, source);
                previous.EmitSignal(BaseButton.SignalName.Pressed);
                next.EmitSignal(BaseButton.SignalName.Pressed);
                FailIf(options.SelectedSlot != 0, "Slot arrows must not change the target during replacement confirmation.");
                FailIf(!root.FileSlotSnapshot(0, game).SequenceEqual(existing.Serialize()),
                    "Choosing an import file must not commit before confirmation.");
                root.Step(1, batched, "item");
                root.Step(1, batched);
                FailIf(!root.FileSlotSnapshot(0, game).SequenceEqual(existing.Serialize()),
                    "Canceling replacement changed the slot.");

                OracleGame other = game == OracleGame.Ages ? OracleGame.Seasons : OracleGame.Ages;
                byte[] corrupt = incoming.Serialize();
                corrupt[0x20] ^= 1;
                foreach (byte[] invalid in new[] { new byte[0x8000], corrupt,
                    OracleSaveData.CreateStandardGame(other).Serialize() })
                {
                    string rejected = Path.Combine(directory, "invalid.sav");
                    File.WriteAllBytes(rejected, invalid);
                    Pick(import, rejected);
                    FailIf(replace.Visible || !root.FileSlotSnapshot(0, game).SequenceEqual(existing.Serialize()),
                        "Wrong-size, corrupt and wrong-game imports must leave the slot untouched.");
                }
                Pick(import, Path.Combine(directory, "missing.sav"));
                FailIf(replace.Visible || string.IsNullOrEmpty(options.Status), "Unreadable imports must report failure.");
                Pick(import, source);
                root.NextSaveFailure = "Transfer validation write failure";
                Confirm();
                FailIf(options.Status != "Transfer validation write failure" ||
                    !root.FileSlotSnapshot(0, game).SequenceEqual(existing.Serialize()),
                    "Import write failures must remain visible and preserve the committed slot.");
                Pick(import, source);
                Confirm();
                FailIf(!root.FileSlotSnapshot(0, game).SequenceEqual(incoming.Serialize()) ||
                    !root.FileSlotSnapshot(2, game).SequenceEqual(existing.Serialize()) || root.FileSlotExists(1, game),
                    "Import must replace only the selected game's selected slot.");

                string destination = Path.Combine(directory, $"{game}.sav");
                File.WriteAllBytes(destination, new byte[] { 0 });
                Pick(export, destination);
                FailIf(!File.ReadAllBytes(destination).SequenceEqual(incoming.Serialize()) ||
                    File.Exists(destination + ".bak") || File.Exists(destination + ".tmp") ||
                    !File.ReadAllBytes(source).SequenceEqual(incoming.Serialize()),
                    "Export must replace one file with the committed slot, without changing the import source or leaving sidecars.");
                Pick(export, directory);
                FailIf(options.Status == "Save exported." || !options.Visible,
                    "An export I/O failure must keep the options menu open with an error.");

                DismissNotice();
                root.Step(1, batched, "move_up");
                root.Step(1, batched, "move_up");
                root.Step(1, batched, "move_right");
                FailIf(options.SelectedSlot != 1,
                    "Right must select the next save slot without switching games.");
                export.EmitSignal(BaseButton.SignalName.Pressed);
                FailIf(files.Visible || options.Status != "This slot has no readable save to export.",
                    "An empty slot must not export a new-game image.");
                Pick(import, destination);
                Confirm();
                FailIf(!root.FileSlotSnapshot(1, game).SequenceEqual(incoming.Serialize()),
                    "An exported save must import unchanged into another slot.");
                DismissNotice();
                import.EmitSignal(BaseButton.SignalName.Pressed);
                files.Hide();
                files.EmitSignal(FileDialog.SignalName.Canceled);
                root.Step(1, batched);
                root.Step(1, batched, "item", "attack");
                FailIf(options.Visible || !screen.IsActive || !screen.OptionsSelected || screen.SelectedGame != game,
                    "Canceling save transfer must return to the same Options button without launching.");
            }
        }
        finally
        {
            screen.GetWindow().Size = originalWindowSize;
            Directory.Delete(directory, recursive: true);
        }
    }

    private async Task CaptureGameSelectScreens()
    {
        string directory = OS.GetEnvironment("OOA_GAME_SELECT_SCREENSHOT_OUTPUT");
        if (string.IsNullOrEmpty(directory)) return;
        System.IO.Directory.CreateDirectory(directory);
        Vector2I originalWindowSize = GetWindow().Size;
        var layer = new CanvasLayer { Layer = 100 };
        AddChild(layer);
        var screen = ResourceLoader.Load<PackedScene>("res://scenes/launcher/game_select.tscn").Instantiate<GameSelectScreen>();
        layer.AddChild(screen);
        try
        {
            GetWindow().Size = new Vector2I(1280, 720);
            screen.Begin();
            screen.Options.Configure(OracleSaveStore.LoadSlot, OracleSaveStore.SaveSlot);
            foreach (var game in new[] { OracleGame.Ages, OracleGame.Seasons })
            {
                screen.RequestPointerSelection(game, false);
                for (int update = 0; update < 120; update++) screen.AdvanceFrame();
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using Image image = GetViewport().GetTexture().GetImage();
                image.SavePng(System.IO.Path.Combine(directory, $"game-select-{game.ToString().ToLowerInvariant()}.png"));
                screen.RequestOptionsSelection(game);
                screen.AdvanceFrame();
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using Image focused = GetViewport().GetTexture().GetImage();
                focused.SavePng(System.IO.Path.Combine(directory, $"game-select-{game.ToString().ToLowerInvariant()}-options.png"));
                screen.Options.Open(game);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using Image menu = GetViewport().GetTexture().GetImage();
                menu.SavePng(System.IO.Path.Combine(directory, $"game-select-{game.ToString().ToLowerInvariant()}-save-transfer.png"));
                string importPath = Path.Combine(Path.GetTempPath(), $"oracle-preview-{Guid.NewGuid():N}.sav");
                try
                {
                    var incoming = OracleSaveData.CreateStandardGame(game);
                    incoming.SetLinkName("LINK");
                    File.WriteAllBytes(importPath, incoming.Serialize());
                    var files = screen.Options.GetNode<FileDialog>("Files");
                    files.EmitSignal(FileDialog.SignalName.FileSelected, importPath);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using Image confirmation = GetViewport().GetTexture().GetImage();
                    confirmation.SavePng(Path.Combine(directory, $"game-select-{game.ToString().ToLowerInvariant()}-import-confirm.png"));
                    screen.Options.GetNode<TextureButton>("Board/Replace/Cancel").EmitSignal(BaseButton.SignalName.Pressed);
                    files.EmitSignal(FileDialog.SignalName.FileSelected, importPath + ".missing");
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using Image notice = GetViewport().GetTexture().GetImage();
                    notice.SavePng(Path.Combine(directory, $"game-select-{game.ToString().ToLowerInvariant()}-transfer-notice.png"));
                }
                finally { File.Delete(importPath); }
                screen.Options.Close();
            }
            GetWindow().Size = new Vector2I(640, 576);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using Image portrait = GetViewport().GetTexture().GetImage();
            portrait.SavePng(System.IO.Path.Combine(directory, "game-select-default.png"));
            foreach (Vector2I size in new[] { new Vector2I(640, 576), new Vector2I(960, 540), new Vector2I(720, 1280) })
            {
                GetWindow().Size = size;
                screen.Options.Open(OracleGame.Ages);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using Image menu = GetViewport().GetTexture().GetImage();
                menu.SavePng(Path.Combine(directory, $"game-select-save-transfer-{size.X}x{size.Y}.png"));
                screen.Options.Close();
            }
        }
        finally
        {
            layer.Free();
            GetWindow().Size = originalWindowSize;
        }
    }
}
