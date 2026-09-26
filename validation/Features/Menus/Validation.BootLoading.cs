using Godot;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBootLoading()
    {
        const string sourcePng = "res://assets/oracle/gfx/spr_nayru_1.png";
        FailIf(OracleAssetCache.ImageResourcePath(sourcePng + ".import") != sourcePng ||
            OracleAssetCache.ImageResourcePath(sourcePng + ".remap") != sourcePng ||
            OracleAssetCache.ImageResourcePath(sourcePng) != sourcePng,
            "Exported Android image sidecars must preload the same logical graphics resource.");
        var screen = ResourceLoader.Load<PackedScene>("res://scenes/boot_loading.tscn")
            .Instantiate<BootLoadingScreen>();
        AddChild(screen);
        byte[] save = _saveData.Serialize();
        int rng = _random.Calls;
        OracleRoomData room = _currentRoom;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot)
            .GetField("_applicationUpdates", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(this)!;
        long updates = scheduler.UpdateCount;
        Vector2I originalSize = GetWindow().ContentScaleSize;
        Window.ContentScaleAspectEnum originalAspect = GetWindow().ContentScaleAspect;
        BeginBootLoading(screen);
        FailIf(GetWindow().ContentScaleSize != new Vector2I(480, 270) ||
            GetWindow().ContentScaleAspect != Window.ContentScaleAspectEnum.Expand,
            "Boot panorama must use an aspect-preserving widescreen canvas.");
        FailIf(!screen.Visible || screen.Progress != 0 || _frontendIntro is not null,
            "Boot loading must precede the original frontend with no completed work.");
        base._Process(1.0 / 60.0);
        FailIf(screen.Progress != 0, "Boot loading did not allow its first presentation frame.");
        var nayru = screen.GetNode<NpcCharacter>("LoadingNayru");
        var leftNote = screen.GetNode<NpcCharacter>("LoadingNote0");
        var rightNote = screen.GetNode<NpcCharacter>("LoadingNote1");
        int singingFrame = nayru.CurrentAnimationFrame;
        Vector2 noteStart = leftNote.Position;
        screen.AdvancePresentation(0.5);
        FailIf(nayru.CurrentAnimationFrame == singingFrame || !leftNote.Visible || rightNote.Visible ||
            leftNote.Position.Y >= noteStart.Y || leftNote.Position.X >= noteStart.X,
            "Loading Nayru did not sing with a rising left music note.");
        screen.AdvancePresentation(0.3);
        FailIf(!rightNote.Visible || rightNote.Position.X <= nayru.Position.X ||
            rightNote.Position.Y >= nayru.Position.Y,
            "Loading Nayru did not alternate to a rising right music note.");
        float progress = 0;
        int stepsThisHost = 0;
        int mostStepsPerHost = 0;
        var tasks = (Queue<System.Func<IEnumerator<bool>?>>)typeof(GameRoot)
            .GetField("_bootTasks", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(this)!;
        var originalTasks = tasks.ToArray();
        tasks.Clear();
        foreach (var prepare in originalTasks)
            tasks.Enqueue(() => CountSteps(prepare()).GetEnumerator());
        IEnumerable<bool> CountSteps(IEnumerator<bool>? steps)
        {
            if (steps is null) yield break;
            using (steps)
                while (steps.MoveNext())
                {
                    stepsThisHost++;
                    yield return steps.Current;
                }
        }
        double longestHostMs = 0;
        var bootDeadline = System.Diagnostics.Stopwatch.StartNew();
        for (int host = 0; _frontendIntro is null && bootDeadline.Elapsed.TotalSeconds < 30; host++)
        {
            var hostTime = System.Diagnostics.Stopwatch.StartNew();
            stepsThisHost = 0;
            base._Process(1.0 / 60.0);
            mostStepsPerHost = System.Math.Max(mostStepsPerHost, stepsThisHost);
            longestHostMs = System.Math.Max(longestHostMs, hostTime.Elapsed.TotalMilliseconds);
            // Allow the I/O worker to progress while this synchronous headless
            // scenario pumps artificial host frames much faster than real time.
            System.Threading.Thread.Sleep(1);
            FailIf(screen.Progress < progress || !save.SequenceEqual(_saveData.Serialize()) ||
                _random.Calls != rng || _currentRoom != room || scheduler.UpdateCount != updates,
                "Boot resource loading regressed progress or advanced original game state.");
            progress = screen.Progress;
        }
        FailIf(_frontendIntro is null || screen.Visible || screen.Progress != 1 ||
            _frontendIntro.Stage != FrontendIntroStage.Boot || _frontendIntro.FrameCounter != 0,
            "Loading did not hand off to the untouched original Nintendo/Capcom sequence.");
        FailIf(mostStepsPerHost <= 1,
            "Boot loading imposed one rendered frame per resource instead of batching inexpensive work.");
        const BindingFlags fields = BindingFlags.NonPublic | BindingFlags.Instance;
        var preparedIntro = (NewGameIntroScreen)typeof(GameRoot)
            .GetField("_newGameIntroScreen", fields)!.GetValue(this)!;
        var preparedGameplay = (GameSceneGraph)typeof(GameRoot)
            .GetField("_preparedGameplayScene", fields)!.GetValue(this)!;
        FailIf(preparedGameplay is null || preparedGameplay.IsInsideTree(),
            "Startup gameplay presentation must remain detached until a file is selected.");
        FailIf(preparedIntro is null || preparedIntro.Visible || !preparedIntro.ResourcesPrepared ||
            preparedIntro.Dialogue.IsOpen,
            "Boot loading did not retain a fully prepared, dormant new-game intro.");
        screen.Free();
        FailIf(GetWindow().ContentScaleSize != originalSize || GetWindow().ContentScaleAspect != originalAspect,
            "Boot loading did not restore the original frontend viewport.");
        screen = ResourceLoader.Load<PackedScene>("res://scenes/boot_loading.tscn").Instantiate<BootLoadingScreen>();
        AddChild(screen);
        screen.Begin();
        screen.Free();
        FailIf(GetWindow().ContentScaleSize != originalSize || GetWindow().ContentScaleAspect != originalAspect,
            "Removing the loading screen did not restore the original viewport.");
        typeof(GameRoot).GetMethod("OpenFileSelectFromFrontend", fields)!.Invoke(this, null);
        var selectedSave = OracleSaveData.CreateStandardGame();
        byte[] selectedBytes = selectedSave.Serialize();
        typeof(GameRoot).GetMethod("StartSelectedFile", fields)!.Invoke(this, [0, selectedSave]);
        var activeIntro = (NewGameIntroController)typeof(GameRoot)
            .GetField("_newGameIntro", fields)!.GetValue(this)!;
        FailIf(!ReferenceEquals(preparedIntro, typeof(GameRoot)
                .GetField("_newGameIntroScreen", fields)!.GetValue(this)) ||
            !preparedIntro!.Visible || activeIntro.StageFrame != 0 ||
            activeIntro.TotalVoiceWaitFrames != 360 || !selectedBytes.SequenceEqual(selectedSave.Serialize()),
            "File selection rebuilt the intro, advanced its source timing, or changed the selected save.");
        string assetPath = "res://assets/oracle/groups/roomPacksPresent.bin";
        byte[] first = OracleAssetCache.ReadBytes(assetPath);
        byte[] second = OracleAssetCache.ReadBytes(assetPath);
        first[0] ^= 0xff;
        FailIf(ReferenceEquals(first, second) ||
            !second.SequenceEqual(Godot.FileAccess.GetFileAsBytes(assetPath)),
            "The retained asset cache exposed mutable shared room bytes.");
        GD.Print($"Boot preparation longest host step: {longestHostMs:F2} ms; elapsed {bootDeadline.Elapsed.TotalMilliseconds:F2} ms.");
        GD.Print("Validated boot loading progress, singing Nayru and notes, frozen original clocks/save/RNG and logo handoff.");
    }

    private async Task CaptureBootLoadingScreen()
    {
        string directory = OS.GetEnvironment("OOA_BOOT_SCREENSHOT_OUTPUT");
        if (string.IsNullOrEmpty(directory)) return;
        System.IO.Directory.CreateDirectory(directory);
        Vector2I originalWindowSize = GetWindow().Size;
        GetWindow().Size = new Vector2I(1280, 720);
        var layer = new CanvasLayer { Layer = 100 };
        AddChild(layer);
        var screen = ResourceLoader.Load<PackedScene>("res://scenes/boot_loading.tscn")
            .Instantiate<BootLoadingScreen>();
        layer.AddChild(screen);
        screen.Begin();
        screen.SetProgress(0.57f);
        screen.AdvancePresentation(0.75);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using (Image image = GetViewport().GetTexture().GetImage())
            image.SavePng(System.IO.Path.Combine(directory, "boot-loading.png"));
        screen.AdvancePresentation(0.125);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using (Image image = GetViewport().GetTexture().GetImage())
            image.SavePng(System.IO.Path.Combine(directory, "boot-loading-step.png"));
        layer.Free();
        GetWindow().Size = originalWindowSize;
    }
}
