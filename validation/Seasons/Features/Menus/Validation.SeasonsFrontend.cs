using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeasonsMenuAssets()
    {
        const string root = SeasonsGameContent.AssetRoot;
        // Independent fingerprints of the pinned Seasons disassembly's source files.
        foreach (var asset in new[] {
            ("gfx_titlescreen_1.png","FD5F1EFF377C88B24A4935FC4530EB6FBA709A6DE5D2232059B5FB444214538B"),
            ("map_titlescreen.bin","5DEB3D1029EDFAFBE4281E23EA539EB6395630A0FE4727FBBB4D8D9942411B67"),
            ("flags_titlescreen.bin","F87319A645E3CD49A5365BDD94518515D9FA68B61FA44F59734B35DC51F0B78D") })
            FailIf(Convert.ToHexString(SHA256.HashData(OracleAssetCache.ReadBytes(root+"menu/"+asset.Item1)))!=asset.Item2,
                $"Seasons title import differs from its source {asset.Item1}.");
        var bg = OracleGraphicsData.LoadPalette(root+"menu/palette_file_bg.bin");
        var sprites = OracleGraphicsData.LoadPalette(root+"menu/palette_file_sprites.bin");
        FailIf(bg[2,0]!=new Color(0x1b/31f,0x12/31f,0x0a/31f) || sprites[4,1]!=new Color(1,0x1d/31f,0x0a/31f),
            "PALH_05 must select Seasons paletteData5db8/5d98, not Ages' file palettes.");
        FailIf(OracleSaveStore.PathForSlot(0,OracleGame.Seasons)!="user://oracle_of_seasons.sav" ||
            OracleSaveStore.PathForSlot(2,OracleGame.Seasons)!="user://oracle_of_seasons_3.sav" ||
            OracleSaveStore.PathForSlot(0, OracleGame.Ages)!="user://oracle_of_ages.sav", "File slot filenames must retain separate game boundaries.");
    }
    private void StartSeasonsFileForValidation(FrontendValidationRoot root, bool batched)
    {
        root.Step(1, batched);
        root.Step(1, batched, "inventory"); root.Step(32, batched);
        CreateSeasonsFileForValidation(root, batched);
        root.Step(1, batched, "attack"); root.Step(1, batched);
        root.Step(1, batched, "attack"); root.Step(32, batched);
    }
    private void CreateSeasonsFileForValidation(FrontendValidationRoot root, bool batched)
    {
        root.Step(1, batched, "attack"); root.Step(1, batched);
        root.Step(1, batched, "attack"); root.Step(1, batched);
        FailIf(root._mainMenu!.CurrentPage != Page.NameEntry, "Seasons new-file selection did not reach native name entry.");
        root.Step(1, batched, "attack");
        root.Step(1, batched, "inventory"); root.Step(1, batched);
        root.Step(1, batched, "inventory"); root.Step(2, batched);
        FailIf(!root.FileSlotExists(0, OracleGame.Seasons) || root._mainMenu.CurrentPage != Page.FileSelect,
            "Seasons name confirmation did not commit its new file before returning to file select.");
    }

    private async Task ValidateSeasonsFrontend()
    {
        byte[]? baseline = null;
        int baselineRng = 0;
        foreach (bool batched in new[] { false,true })
        {
            var root = new FrontendValidationRoot();
            CanvasLayer? layer = null;
            if (!string.IsNullOrEmpty(OS.GetEnvironment("OOA_SEASONS_SCREENSHOT_OUTPUT")))
            { layer = new() { Layer = 100 }; AddChild(layer); layer.AddChild(root); }
            else AddChild(root);
            try
            {
                var selector = root.InitializeGameSelect();
                var ages = OracleSaveData.CreateStandardGame(OracleGame.Ages); ages.SetLinkName("AGES"); root.SeedFileSlot(0,ages);
                byte[] agesBytes = root.FileSlotSnapshot(0, OracleGame.Ages);
                selector.RequestPointerSelection(OracleGame.Seasons,true); root.Step(1,batched);
                var loading = root.GetNode<BootLoadingScreen>("BootLoading");
                var din = loading.GetNode<NpcCharacter>("LoadingDin");
                FailIf(!loading.Visible || loading.Progress != 0 || selector.IsActive || root._seasonsOpening is not null ||
                    din.CurrentAnimationOpaquePixels == 0 || loading.GetNodeOrNull("LoadingNayru") is not null,
                    "Seasons must enter its Din boot presentation before creating a live game.");
                int frame = din.CurrentAnimationFrame; loading.AdvancePresentation(0.5);
                FailIf(din.CurrentAnimationFrame == frame || loading.GetNodeOrNull("LoadingNote0") is not null,
                    "Boot Din must dance using her own animation without Nayru's note emitters.");
                root.AdvanceHost(1.0/60.0);
                FailIf(loading.Progress != 0, "Seasons boot must present a frame before starting resource work.");
                if (!batched) await CaptureSeasonsOpeningScreen("seasons-loading",root.GetViewport());
                root.CompleteBootLoading();
                const BindingFlags fields = BindingFlags.NonPublic | BindingFlags.Instance;
                var prepared = (SeasonsOpeningSession)typeof(GameRoot).GetField("_preparedSeasonsOpening",fields)!.GetValue(root)!;
                FailIf(loading.Visible || loading.Progress != 1 || root._mainMenu is null || !root._mainMenuScreen!.ResourcesPrepared ||
                    prepared is null || prepared.IsInsideTree() || !prepared.ResourcesPrepared || prepared.Save is not null ||
                    prepared.Memory is not null || prepared.Actors.Any(actor => actor is not null) || root._random.Calls != 0 ||
                    !agesBytes.SequenceEqual(root.FileSlotSnapshot(0, OracleGame.Ages)),
                    "Seasons boot must retain prepared presentation without binding a save, entering a room or advancing RNG.");
                root.Step(1,batched);
                FailIf(root._mainMenu.CurrentPage != Page.Title || root._mainMenuScreen.TitleBlinkVisible || root._random.Calls != 1,
                    "Seasons title initialization must consume one shared RNG sample and retain counter $0960's hidden Start prompt.");
                root.Step(32,batched);
                FailIf(!root._mainMenuScreen.TitleBlinkVisible || root._random.Calls != 33,
                    "Seasons title blinking must follow bit 5 of the descending native idle counter.");
                if (!batched) await CaptureSeasonsOpeningScreen("seasons-title",root.GetViewport());
                root.Step(1,batched,"inventory"); root.Step(31,batched);
                FailIf(root._mainMenu.CurrentPage != Page.Title || root._seasonsOpening is not null,
                    "Start must retain the title during its first 31 fade updates.");
                root.Step(1,batched);
                FailIf(root._mainMenu.CurrentPage != Page.FileSelect || root._mainMenuScreen.WhiteFadeOffset != 0 || root._random.Calls != 66,
                    "The 32nd title fade update must publish Seasons file select with its palettes restored.");
                if (!batched) await CaptureSeasonsOpeningScreen("seasons-files",root.GetViewport());
                CreateSeasonsFileForValidation(root,batched);
                FailIf(!OracleSaveData.TryDeserialize(root.FileSlotSnapshot(0,OracleGame.Seasons), OracleGame.Seasons,out var fresh) ||
                    fresh!.LinkName != "A" || fresh.Game != OracleGame.Seasons,
                    "Name entry must create a Seasons save, using the entered name.");
                void Press(string action) { root.Step(1,batched,action); root.Step(1,batched); }
                void Cursor(int target)
                {
                    for (int update=0;root._mainMenu!.Cursor!=target && update<4;update++) Press("move_down");
                    FailIf(root._mainMenu!.Cursor!=target,"Seasons file cursor did not reach its native row.");
                }
                Cursor(3); Press("attack");
                Cursor(0); Press("attack");
                Cursor(1); Press("attack"); Press("move_right"); Press("attack");
                FailIf(!root.FileSlotExists(1,OracleGame.Seasons) ||
                    !root.FileSlotSnapshot(0,OracleGame.Seasons).SequenceEqual(root.FileSlotSnapshot(1,OracleGame.Seasons)),
                    "Seasons Copy must retain the game signature and exact saved image.");
                Cursor(3); Press("move_right"); Press("attack");
                Cursor(1); Press("attack"); Press("move_right"); Press("attack");
                for (int update=0;root._mainMenu!.CurrentPage!=Page.FileSelect && update<1024;update++) root.Step(1,batched);
                FailIf(root.FileSlotExists(1,OracleGame.Seasons) || !root.FileSlotExists(0,OracleGame.Seasons),
                    "Seasons Erase must affect only its selected file.");
                Cursor(2); Press("attack"); Press("move_down"); Press("attack");
                FailIf(root._mainMenu!.CurrentPage!=Page.Notice || root.FileSlotExists(2,OracleGame.Seasons),
                    "Unimplemented linked Seasons games must stay explicitly unavailable.");
                Press("item"); Press("item"); Cursor(0); Press("attack");
                for (int speed=0;speed<4;speed++) Press("move_right");
                if (!batched) await CaptureSeasonsOpeningScreen("seasons-speed",root.GetViewport());
                Press("attack"); root.Step(31,batched);
                var session = root._seasonsOpening;
                FailIf(!ReferenceEquals(prepared,session) || session!.Phase!=SeasonsOpeningPhase.Pregame || session.Save.LinkName!="A" ||
                    session.Save.TextSpeed!=4 || !agesBytes.SequenceEqual(root.FileSlotSnapshot(0, OracleGame.Ages)),
                    "File start must reuse the prepared Seasons scene and selected save, retaining message speed and Ages files.");
                if (baseline is null) { baseline=session.Save.Serialize(); baselineRng=root._random.Calls; }
                else FailIf(!baseline.SequenceEqual(session.Save.Serialize()) || baselineRng!=root._random.Calls,
                    "Seasons frontend state or RNG changed when original updates were batched.");
            }
            finally { root.Free(); layer?.Free(); ResetValidationInput(); }
        }
        Vector2I size = GetWindow().ContentScaleSize;
        var aspect = GetWindow().ContentScaleAspect;
        var cancelled = new FrontendValidationRoot(); AddChild(cancelled);
        try
        {
            var selector = cancelled.InitializeGameSelect();
            selector.RequestPointerSelection(OracleGame.Seasons,true); cancelled.Step(1,false);
            cancelled.AdvanceHost(1.0/60.0); cancelled.AdvanceHost(1.0/60.0);
            FailIf(cancelled._random.Calls!=0 || cancelled.FileSlotExists(0,OracleGame.Seasons),
                "Cancelling Seasons preparation must not create a game or consume original RNG.");
        }
        finally { cancelled.Free(); ResetValidationInput(); }
        FailIf(GetWindow().ContentScaleSize!=size || GetWindow().ContentScaleAspect!=aspect,
            "Cancelling Seasons boot must restore the original viewport.");
    }
}
