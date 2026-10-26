using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Oracles;

public partial class GameRoot : Node2D
{
    private sealed record PreparedBootData(string[] Images, Action Install);
    private readonly GameplayScriptResources _gameplayScriptResources = new();
    private readonly ApplicationFixedUpdateScheduler _applicationUpdates = new();
    private readonly ApplicationInputBuffer _applicationInput = new();
    internal OracleGame _selectedGame = OracleGame.Ages;
    private string AssetRoot => OracleAssetPack.ForGame(_selectedGame);
    internal ApplicationFixedUpdateScheduler ApplicationUpdates => _applicationUpdates;
    internal ApplicationInputBuffer ApplicationInput => _applicationInput;
    private bool _debugFastForward;
    private BootLoadingScreen? _bootLoading;
    internal GameSelectScreen? _gameSelect;
    private readonly Queue<System.Func<IEnumerator<bool>?>> _bootTasks = new();
    private IEnumerator<bool>? _bootResourceSteps;
    private int _bootCompletedTasks;
    private int _bootTaskCount;
    private int _bootHostFrames;
    private bool _bootFinishing;
    private const double BootWorkBudgetMilliseconds = 3;
    private CancellationTokenSource? _bootCancellation;
    private Task<PreparedBootData>? _bootDataTask;
    internal OracleSoundEngine _sound = null!;
    internal MainMenuController? _mainMenu;
    internal MainMenuScreen? _mainMenuScreen;
    private LaunchOptions _launchOptions = null!;
    internal OracleRuntimeState _runtimeState = null!;
    internal OracleRandom _random = null!;
    private bool _persistSaveData;
    internal readonly PresentationSettings _presentationSettings = new();
    private int _activeSaveSlot;
    internal int _saveWriteRequests;

    public override void _EnterTree()
    {
        OracleGraphicsCache.ApplicationEnteredTree();
        var sound = GetNodeOrNull<OracleSoundEngine>("%SoundEngine") ?? GetNodeOrNull<OracleSoundEngine>("SoundEngine");
        sound?.SelectContent(new OracleSoundData(OracleAssetPack.ForGame(_selectedGame)));
    }

    public override void _Ready()
    {
        _launchOptions = new LaunchOptions();
        if (_launchOptions.Has("--validate") && GetType() == typeof(GameRoot))
        {
            GetTree().CallDeferred(
                SceneTree.MethodName.ChangeSceneToFile,
                "res://validation/validation.tscn");
            return;
        }
        _persistSaveData = !_launchOptions.Has("--validate");
        if (_persistSaveData)
            _presentationSettings.Load();
        _sound = GetNodeOrNull<OracleSoundEngine>("%SoundEngine") ??
            GetNodeOrNull<OracleSoundEngine>("SoundEngine") ??
            throw new InvalidOperationException(
                "The game scene is missing its required SoundEngine node.");
        _sound.ApplicationUpdateOwned = true;
        _random = new OracleRandom();

        if (_launchOptions.ShowMainMenu)
        {
            BeginGameSelect(GetNode<GameSelectScreen>("%GameSelect"));
            return;
        }

        StartAgesDirect();
    }

    public override void _Input(InputEvent @event)
    {
        if (_gameSelect is not null || _bootLoading is not null || _loadingSavedGame) return;
        if (@event is InputEventKey
            {
                PhysicalKeycode: Key.F5, Pressed: true, Echo: false,
                CtrlPressed: false, AltPressed: false, MetaPressed: false,
                ShiftPressed: false
            })
        {
            _debugFastForward = !_debugFastForward;
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_selectedGame == OracleGame.Ages) HandleAgesDebugInput(@event);
    }

    internal void BeginGameSelect(GameSelectScreen screen)
    {
        _gameSelect = screen;
        _applicationInput.Clear();
        screen.AgesSelected += StartAgesFromGameSelect;
        screen.SeasonsSelected += StartSeasonsFromGameSelect;
        screen.Begin();
    }


    internal void BeginBootLoading(BootLoadingScreen screen, OracleGame game)
    {
        if (_bootLoading is not null)
            throw new InvalidOperationException("Boot loading is already active.");
        _selectedGame = game;
        _bootLoading = screen;
        _bootHostFrames = 0;
        _bootCompletedTasks = 0;
        _bootFinishing = false;
        // Allocate the dormant hierarchy before the first animation frame.
        // Its resource preparation is staged below; it never enters the tree
        // or binds a save until selection. First-time script/node registration
        // must not interrupt the loading animation once it is visible.
        if (game == OracleGame.Ages)
            AllocateAgesBootHierarchy();
        else
            AllocateSeasonsBootHierarchy();
        screen.Begin(game);
        _bootCancellation = new CancellationTokenSource();
        CancellationToken cancellation = _bootCancellation.Token;
        _bootDataTask = Task.Run(() =>
        {
            string[] images = OracleAssetCache.Preload(OracleAssetPack.ForGame(game), cancellation);
            cancellation.ThrowIfCancellationRequested();
            if (game == OracleGame.Seasons)
            {
                return PrepareSeasonsBootData(images);
            }
            return PrepareAgesBootData(images);
        }, cancellation);
        if (game == OracleGame.Ages) _gameplaySceneResource.BeginPreload();
        // Only retained resources are warmed here. A selected file and its
        // gameplay owners are still created by their normal later handoff.
        _bootTasks.Enqueue(() => PrepareBootSources().GetEnumerator());
        _bootTasks.Enqueue(() => _gameplayScriptResources.Prepare().GetEnumerator());
        if (game == OracleGame.Seasons)
            _bootTasks.Enqueue(() => _preparedSeasonsOpening!.PrepareResources().GetEnumerator());
        else
        {
            QueueAgesBootPresentation();
        }
        _bootTasks.Enqueue(() =>
        {
            _mainMenuScreen = new MainMenuScreen
                { Name = "MainMenu", ZIndex = ScreenDrawPriority.FrontendZIndex, Visible = false, DeferPreparation = true,
                    AssetRoot = OracleAssetPack.ForGame(game) };
            AddChild(_mainMenuScreen);
            return _mainMenuScreen.PrepareResources().GetEnumerator();
        });
        if (game == OracleGame.Ages) QueueAgesNewGamePresentation();
        _bootTaskCount = _bootTasks.Count;
    }

    private IEnumerable<bool> PrepareBootSources()
    {
        Task<PreparedBootData> task = _bootDataTask!;
        while (!task.IsCompleted) yield return false;
        PreparedBootData prepared = task.GetAwaiter().GetResult();
        prepared.Install();
        string[] images = prepared.Images;
        for (int index = 0; index < images.Length; index++)
        {
            _ = OracleGraphicsCache.LoadImage(images[index]);
            _bootLoading!.SetProgress((_bootCompletedTasks + (index + 1.0f) / images.Length) / _bootTaskCount);
            yield return false;
        }
    }

    private void AdvanceBootLoading(double delta)
    {
        BootLoadingScreen screen = _bootLoading!;
        screen.AdvancePresentation(delta);
        // Give the vignette a rendered frame before executing resource work.
        if (++_bootHostFrames < 2) return;
        // A pending data worker cannot make progress by polling it repeatedly
        // within one host frame. Leave the render thread available immediately.
        if (_bootDataTask is { IsCompleted: false }) return;
        // Yield on elapsed work, not on asset count: one tiny image per frame
        // imposed seconds of idle waiting at 60 Hz. Individual Godot operations
        // remain atomic, so this is a soft budget checked between operations.
        long started = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(started).TotalMilliseconds < BootWorkBudgetMilliseconds)
        {
            if (_bootResourceSteps is not null)
            {
                if (_bootResourceSteps.MoveNext()) continue;
                _bootResourceSteps.Dispose();
                _bootResourceSteps = null;
                screen.SetProgress((float)++_bootCompletedTasks / _bootTaskCount);
                continue;
            }
            if (!_bootTasks.TryDequeue(out var prepare)) break;
            _bootResourceSteps = prepare();
            if (_bootResourceSteps is null)
                screen.SetProgress((float)++_bootCompletedTasks / _bootTaskCount);
        }
        if (_bootResourceSteps is not null || _bootTasks.Count != 0) return;
        if (!_bootFinishing)
        {
            _bootFinishing = true;
            screen.Finish();
        }
        if (!screen.Finished) return;
        screen.End();
        _bootLoading = null;
        _applicationInput.Clear();
        if (_selectedGame == OracleGame.Seasons) OpenSeasonsMainMenu();
        else StartFrontendController(startAtTitle: false);
    }


    public override void _Process(double delta)
    {
        if (_bootLoading is not null)
        {
            AdvanceBootLoading(delta);
            return;
        }
        _applicationInput.CaptureHostFrame();
        // Debug speed changes the number of complete original updates, never
        // their 1/60 delta, input-edge consumption, or subsystem order.
        double elapsed = delta * (_debugFastForward ? 4 : 1);
        _applicationUpdates.Advance(elapsed, AdvanceApplicationUpdate);
        AdvanceGameplayPreparation();
    }

    internal void AdvanceApplicationUpdate()
    {
        Input.BeginOriginalUpdate(_applicationInput.ConsumeOriginalUpdate());
        try
        {
            AdvanceApplicationState();
            _sound.AdvanceApplicationUpdate();
        }
        finally
        {
            Input.EndOriginalUpdate();
        }
    }

    private void AdvanceApplicationState()
    {
        const double delta = ApplicationFixedUpdateScheduler.UpdateDelta;
        if (_gameSelect is not null)
        {
            _gameSelect.AdvanceFrame();
            return;
        }
        if (_seasonsOpening is not null) { _seasonsOpening.AdvanceFrame(); return; }
        if (_bootLoading is not null) return;
        if (_loadingSavedGame) return;
        if (_selectedGame == OracleGame.Ages) AdvanceAgesApplicationState();
        else _mainMenu?.Update(delta);
    }

    // File-menu and gameplay commits share the same persistence boundary.
    // Alternative hosts can provide an isolated store without changing save
    // decisions, restart locations, or the lifetime of the live save image.
    protected virtual OracleSaveData? LoadFileSlot(int slot) => OracleSaveStore.LoadSlot(slot, _selectedGame);
    protected virtual SaveResult StoreFileSlot(int slot, OracleSaveData save) => OracleSaveStore.SaveSlot(slot, save);
    protected virtual void EraseFileSlot(int slot) => OracleSaveStore.EraseSlot(slot, _selectedGame);
}

internal enum InitialRoomLoadKind
{
    Ordinary,
    LinkSummonedCutscene
}
