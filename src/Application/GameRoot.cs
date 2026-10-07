using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace oracleofages;

public partial class GameRoot : Node2D
{
    private readonly ApplicationFixedUpdateScheduler _applicationUpdates = new();
    private readonly ApplicationInputBuffer _applicationInput = new();
    internal ApplicationFixedUpdateScheduler ApplicationUpdates => _applicationUpdates;
    internal ApplicationInputBuffer ApplicationInput => _applicationInput;
    private bool _debugFastForward;
    private readonly GameplaySceneResource _gameplaySceneResource = new();
    private IEnumerator<bool>? _gameplayPreparation;
    internal bool GameplayPrepared { get; private set; }
    private BootLoadingScreen? _bootLoading;
    private readonly Queue<System.Func<IEnumerator<bool>?>> _bootTasks = new();
    private IEnumerator<bool>? _bootResourceSteps;
    private int _bootCompletedTasks;
    private int _bootTaskCount;
    private int _bootHostFrames;
    private bool _bootFinishing;
    private const double BootWorkBudgetMilliseconds = 3;
    private CancellationTokenSource? _bootCancellation;
    private Task<PreparedBootData>? _bootDataTask;
    private sealed record PreparedBootData(string[] Images, RoomSessionResources Rooms);
    private GameSceneGraph? _preparedGameplayScene;
    private OracleWorldData? _preparedWorld;
    private RoomSessionResources? _preparedRoomResources;

    internal RoomSession _rooms = null!;
    internal OracleSoundEngine _sound = null!;
    internal RoomTransitionController _transitions = null!;
    internal RoomEntityManager _entities = null!;
    internal InteractionController _interactions = null!;
    internal RoomEventController _roomEvents = null!;
    internal PushBlockController _pushBlocks = null!;
    internal DungeonKeyDoorController _keyDoors = null!;
    internal OverworldKeyholeController _keyholes = null!;
    internal TerrainController _terrain = null!;
    internal CombatController _combat = null!;
    internal BombController _bomb = null!;
    internal BraceletController _bracelet = null!;
    internal ShovelController _shovel = null!;
    internal SeedSatchelController _seedSatchel = null!;
    internal HarpController _harp = null!;
    internal DebugWarpController _debugWarps = null!;
    internal DebugCollisionController _debugCollision = null!;
    internal DebugMapleController _debugMaple = null!;
    internal MapMenuController _mapMenu = null!;
    internal InventoryMenuController _inventoryMenu = null!;
    internal RingMenuController _ringMenu = null!;
    internal SecretEntryController _secretEntry = null!;
    internal DebugFlagMenuController _debugFlagMenu = null!;
    internal DebugObjectSpawnerController _debugObjectSpawner = null!;
    internal GameplayPauseController _gameplayPause = null!;
    internal OracleMenuLifecycle _menuLifecycle = null!;
    internal FrontendIntroController? _frontendIntro;
    internal FrontendIntroScreen? _frontendIntroScreen;
    internal MainMenuController? _mainMenu;
    internal MainMenuScreen? _mainMenuScreen;
    private NewGameIntroController? _newGameIntro;
    private NewGameIntroScreen? _newGameIntroScreen;
    private LaunchOptions _launchOptions = null!;
    internal RoomCollision _collision = null!;
    internal PlayerWorld _playerWorld = null!;
    internal GameSceneGraph _scene = null!;
    internal TreasureDatabase _treasures = null!;
    internal InventoryState _inventory = null!;
    internal StatusBarController _statusBar = null!;
    internal OracleSaveData _saveData = null!;
    internal OracleRuntimeState _runtimeState = null!;
    internal OracleRandom _random = null!;
    internal DeathRespawnPointController _deathRespawnPoints = null!;
    private bool _persistSaveData;
    internal readonly PresentationSettings _presentationSettings = new();
    private int _activeSaveSlot;
    internal int _saveWriteRequests;
    internal double _newGameArrivalTicks;
    internal int _newGameArrivalFadeFrames;
    internal int _newGameArrivalFrames;
    internal int _newGameArrivalPhase;
    internal int _newGameArrivalLastFrame;
    private bool _newGameArrivalRoomPending;
    private bool _newGameArrivalRoomVisitPending;
    private PirateShipCourse? _pirateShipCourse;
    private int _deferredIntroMusicGroup = -1;
    private int _deferredIntroMusicRoom = -1;
    private string _debugSavestateStatus = string.Empty;
    private double _debugSavestateStatusFrames;

    internal double _animationTicks;

    internal RoomView _roomView => _scene.RoomView;
    internal Player _player => _scene.Player;
    internal Camera2D _roomCamera => _scene.RoomCamera;
    internal Hud _hud => _scene.Hud;
    internal Label _roomDebug => _scene.RoomDebug;
    internal ColorRect _warpFade => _scene.WarpFade;
    internal DialogueBox _dialogue => _scene.Dialogue;
    internal MapScreen _mapScreen => _scene.MapScreen;
    internal InventoryScreen _inventoryScreen => _scene.InventoryScreen;
    internal RingMenuScreen _ringMenuScreen => _scene.RingMenuScreen;
    internal SaveQuitScreen _saveQuitScreen => _scene.SaveQuitScreen;
    internal DebugFlagScreen _debugFlagScreen => _scene.DebugFlagScreen;

    public bool IsTransitioning => _transitions?.IsTransitioning ?? false;
    public bool DialogueOpen => _interactions?.DialogueOpen ?? false;
    public bool MapMenuOpen => _mapMenu?.IsActive ?? false;
    public bool InventoryMenuOpen => _inventoryMenu?.IsActive ?? false;
    public bool RingMenuOpen => _ringMenu?.IsActive ?? false;
    public bool DebugFlagMenuOpen => _debugFlagMenu?.IsActive ?? false;

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
            BeginBootLoading(GetNode<BootLoadingScreen>("%BootLoading"));
            return;
        }

        _activeSaveSlot = 0;
        OracleSaveData save = _persistSaveData
            ? OracleSaveStore.LoadOrCreate()
            : OracleSaveData.CreateStandardGame();
        InitializeGameplay(save);
    }

    public override void _Input(InputEvent @event)
    {
        if (_bootLoading is not null) return;
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

        if (_transitions is null ||
            _frontendIntro is not null ||
            _mainMenu is not null ||
            _newGameIntro is not null ||
            !DebugSavestateController.TryDecodeInput(@event, out var command))
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        if (command.Kind == DebugSavestateCommandKind.Save)
        {
            if (!CanCaptureDebugSavestate())
            {
                SetDebugSavestateStatus($"S{command.Slot} BUSY");
                return;
            }

            SaveResult result = DebugSavestateStore.SaveSlot(
                command.Slot,
                CaptureDebugSavestate());
            if (result.Success)
            {
                GD.Print($"Saved debug state slot {command.Slot}.");
                SetDebugSavestateStatus($"S{command.Slot} SAVED");
            }
            else
            {
                GD.PushWarning(
                    $"Could not save debug state slot {command.Slot}: " +
                    result.ErrorMessage);
                SetDebugSavestateStatus($"S{command.Slot} SAVE ERR");
            }
            return;
        }

        DebugSavestateLoadResult loaded =
            DebugSavestateStore.LoadSlot(command.Slot);
        if (loaded.Success)
        {
            RestoreDebugSavestate(loaded.State!);
            GD.Print($"Loaded debug state slot {command.Slot}.");
            SetDebugSavestateStatus($"S{command.Slot} LOADED");
        }
        else if (!loaded.Found)
        {
            SetDebugSavestateStatus($"S{command.Slot} EMPTY");
        }
        else
        {
            GD.PushWarning(
                $"Could not load debug state slot {command.Slot}: " +
                loaded.ErrorMessage);
            SetDebugSavestateStatus($"S{command.Slot} LOAD ERR");
        }
    }

    private void StartSelectedFile(int slot, OracleSaveData save)
    {
        _activeSaveSlot = slot;
        // initializeGame owns the live file before either the pregame cutscene
        // or room construction. Resource preparation must not delay this.
        _saveData = save;
        _saveData.ResetHealthIfDepleted();
        _saveData.AdvancePlaytime();
        _mainMenuScreen?.QueueFree();
        _mainMenuScreen = null;
        _mainMenu = null;

        // bank0.s:mainThreadStart restarts the whole driver, including SFX
        // and queued requests, before the loaded game's initial music cue.
        _sound.RestartSound();

        if (!save.HasGlobalFlag(GlobalFlag.PregameIntroDone))
        {
            _gameplaySceneResource.BeginPreload();
            if (_newGameIntroScreen is null)
            {
                _newGameIntroScreen = new NewGameIntroScreen
                {
                    Name = "NewGameIntro",
                    ZIndex = ScreenDrawPriority.FrontendZIndex,
                    Visible = false
                };
                AddChild(_newGameIntroScreen);
            }
            _newGameIntroScreen.Visible = true;
            _newGameIntroScreen.Dialogue.ApplicationUpdateOwned = true;
            _newGameIntroScreen.Dialogue.MessageSpeed = save.TextSpeed;
            _newGameIntroScreen.Dialogue.SetLinkNameProvider(
                () => save.LinkName);
            _newGameIntro = new NewGameIntroController(
                _newGameIntroScreen,
                () => CompleteNewGameIntro(save),
                _sound,
                initializing: true,
                frameCounter: () => save.ReadWramByte(WramAddress.wPlaytimeCounter));
            // Health restoration has already run; dormant scene construction
            // may now prepare the initialized live file without advancing it.
            if (save.ReadWramByte(WramAddress.wLinkHealth) is > 0 and < 0x80)
                _gameplayPreparation = InitializeGameplaySteps(save,
                    initialRoomLoadKind: InitialRoomLoadKind.LinkSummonedCutscene,
                    prepare: true).GetEnumerator();
            return;
        }

        InitializeGameplay(save);
    }

    internal void StartFrontend(bool startAtTitle)
    {
        _mainMenu = null;
        _frontendIntroScreen = new FrontendIntroScreen
        {
            Name = "FrontendIntro",
            ZIndex = ScreenDrawPriority.FrontendZIndex
        };
        _mainMenuScreen = new MainMenuScreen
        {
            Name = "MainMenu",
            ZIndex = ScreenDrawPriority.FrontendZIndex,
            Visible = false
        };
        AddChild(_frontendIntroScreen);
        AddChild(_mainMenuScreen);
        StartFrontendController(startAtTitle);
    }

    private void StartFrontendController(bool startAtTitle)
    {
        _frontendIntro = new FrontendIntroController(
            _frontendIntroScreen!,
            _mainMenuScreen!,
            _random,
            _sound.RestartSound,
            _sound.PlaySound,
            OpenFileSelectFromFrontend,
            startAtTitle);
    }

    internal void BeginBootLoading(BootLoadingScreen screen)
    {
        if (_bootLoading is not null)
            throw new InvalidOperationException("Boot loading is already active.");
        _bootLoading = screen;
        _bootHostFrames = 0;
        _bootCompletedTasks = 0;
        _bootFinishing = false;
        // Allocate the dormant hierarchy before the first animation frame.
        // Its resource preparation is staged below; it never enters the tree
        // or binds a save until selection. First-time script/node registration
        // must not interrupt Nayru once the loading presentation is visible.
        _preparedGameplayScene = _gameplaySceneResource.Load().Instantiate<GameSceneGraph>();
        screen.Begin();
        _bootCancellation = new CancellationTokenSource();
        CancellationToken cancellation = _bootCancellation.Token;
        _bootDataTask = Task.Run(() =>
        {
            string[] images = OracleAssetCache.Preload(cancellation);
            cancellation.ThrowIfCancellationRequested();
            // These constructors parse immutable managed records only. Scene
            // nodes, images, texture uploads and gameplay stay on the main thread.
            _ = LinkItemDatabase.Shared;
            _ = SideScrollPlayerDatabase.Shared;
            _ = TopDownSwimmingDatabase.Shared;
            _ = EnemyBehaviorTables.Shared;
            _ = FrontendIntroDatabase.Shared;
            _ = new NpcDatabase();
            _ = new EnemyDatabase();
            _ = new TreasureDatabase();
            _ = new WarpDatabase();
            return new PreparedBootData(images, new RoomSessionResources());
        }, cancellation);
        _gameplaySceneResource.BeginPreload();
        // Only retained resources are warmed here. A selected file and its
        // gameplay owners are still created by their normal later handoff.
        _bootTasks.Enqueue(() => PrepareBootSources().GetEnumerator());
        _bootTasks.Enqueue(() => _gameplaySceneResource.Prepare().GetEnumerator());
        _bootTasks.Enqueue(() => PrepareGameplayPresentation().GetEnumerator());
        _bootTasks.Enqueue(() =>
        {
            _frontendIntroScreen = new FrontendIntroScreen
                { Name = "FrontendIntro", ZIndex = ScreenDrawPriority.FrontendZIndex, Visible = false, DeferPreparation = true };
            AddChild(_frontendIntroScreen);
            return _frontendIntroScreen.PrepareResources().GetEnumerator();
        });
        _bootTasks.Enqueue(() =>
        {
            _mainMenuScreen = new MainMenuScreen
                { Name = "MainMenu", ZIndex = ScreenDrawPriority.FrontendZIndex, Visible = false, DeferPreparation = true };
            AddChild(_mainMenuScreen);
            return _mainMenuScreen.PrepareResources().GetEnumerator();
        });
        _bootTasks.Enqueue(() =>
        {
            _newGameIntroScreen = new NewGameIntroScreen
                { Name = "NewGameIntro", ZIndex = ScreenDrawPriority.FrontendZIndex, Visible = false, DeferPreparation = true };
            AddChild(_newGameIntroScreen);
            return _newGameIntroScreen.PrepareResources().GetEnumerator();
        });
        _bootTaskCount = _bootTasks.Count;
    }

    private IEnumerable<bool> PrepareBootSources()
    {
        Task<PreparedBootData> task = _bootDataTask!;
        while (!task.IsCompleted) yield return false;
        PreparedBootData prepared = task.GetAwaiter().GetResult();
        _preparedRoomResources = prepared.Rooms;
        string[] images = prepared.Images;
        for (int index = 0; index < images.Length; index++)
        {
            _ = OracleGraphicsCache.LoadImage(images[index]);
            _bootLoading!.SetProgress((_bootCompletedTasks + (index + 1.0f) / images.Length) / _bootTaskCount);
            yield return false;
        }
    }

    private IEnumerable<bool> PrepareGameplayPresentation()
    {
        _preparedWorld = new OracleWorldData();
        yield return false;
        foreach (bool step in _preparedGameplayScene!.PreparePresentation())
            yield return step;
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
        StartFrontendController(startAtTitle: false);
    }

    private void OpenFileSelectFromFrontend()
    {
        _frontendIntroScreen?.QueueFree();
        _frontendIntroScreen = null;
        _frontendIntro = null;
        _mainMenu = new MainMenuController(
            _mainMenuScreen ?? throw new InvalidOperationException(
                "The frontend lost its shared main-menu screen."),
            StartSelectedFile,
            load: LoadFileSlot,
            save: StoreFileSlot,
            erase: EraseFileSlot,
            playSound: _sound.PlaySound,
            startAtFileSelect: true);
    }

    private void CompleteNewGameIntro(OracleSaveData save)
    {
        NewGameIntroRecord record =
            _newGameIntro!.Record;
        _newGameIntroScreen?.QueueFree();
        _newGameIntroScreen = null;
        _newGameIntro = null;
        if (_gameplayPreparation is not null)
        {
            while (_gameplayPreparation.MoveNext()) { }
            _gameplayPreparation.Dispose();
            _gameplayPreparation = null;
            GameplayPrepared = false;
        }
        else
            InitializeGameplay(save,
                initialRoomLoadKind: InitialRoomLoadKind.LinkSummonedCutscene);

        // linkSummonedCutscene state 0 starts SND_WARP_START when it loads the
        // arrival room and initializes the divisor-2 white fade/wave.
        _sound.PlaySound(SoundId.SndWarpStart);
        _newGameArrivalRoomPending = true;
        _newGameArrivalTicks = 0.0;
        _newGameArrivalFadeFrames = NewGameIntroController.ArrivalFadeWaitFrames;
        _newGameArrivalFrames = record.SummonFrames;
        _newGameArrivalPhase = 0;
        _newGameArrivalLastFrame = 0;
        _warpFade.Color = Colors.White;
        _roomView.SetHorizontalWave(0xff, 0);
        _player.Visible = false;
        _player.SetPhysicsProcess(false);
        _player.SetProcess(false);
    }

    private void InitializeGameplay(
        OracleSaveData save,
        bool forceDeathRespawn = false,
        DebugSavestateData? debugSavestate = null,
        InitialRoomLoadKind initialRoomLoadKind = InitialRoomLoadKind.Ordinary)
    {
        foreach (bool _ in InitializeGameplaySteps(save, forceDeathRespawn,
            debugSavestate, initialRoomLoadKind)) { }
    }

    private IEnumerable<bool> InitializeGameplaySteps(
        OracleSaveData save,
        bool forceDeathRespawn = false,
        DebugSavestateData? debugSavestate = null,
        InitialRoomLoadKind initialRoomLoadKind = InitialRoomLoadKind.Ordinary,
        bool prepare = false)
    {
        // initializeGame restores depleted saved/live health before Link is
        // constructed. Save and Continue deliberately leaves the disk image
        // at zero health until the next explicit save.
        _saveData = debugSavestate?.CreateSaveData() ?? save;
        if (debugSavestate is null)
            _saveData.ResetHealthIfDepleted();
        else
            _animationTicks = debugSavestate.AnimationTicks;
        _random ??= new OracleRandom();
        _runtimeState = new OracleRuntimeState();
        _treasures = new TreasureDatabase();
        yield return false;
        bool useDebugSavestate = debugSavestate is not null;
        bool useSavedSpawn = !useDebugSavestate && (forceDeathRespawn ||
            (!_launchOptions.HasWorldOverride && (_persistSaveData ||
                initialRoomLoadKind == InitialRoomLoadKind.LinkSummonedCutscene)));
        if (useSavedSpawn)
        {
            DeathRespawnInitialization.Apply(_saveData);
            CompanionRuntimeState.RestoreRememberedFromDeathRespawn(
                _runtimeState, _saveData);
        }
        int startingGroup = useDebugSavestate
            ? debugSavestate!.Group
            : useSavedSpawn
            ? _saveData.RespawnGroup
            : _launchOptions.StartingGroup;
        int startingRoom = useDebugSavestate
            ? debugSavestate!.Room
            : useSavedSpawn
            ? _saveData.RespawnRoom
            : _launchOptions.StartingRoom;
        _rooms = new RoomSession(
            startingGroup, startingRoom,
            () => (long)_animationTicks,
            () => _animationTicks = 0.0,
            _saveData,
            countAsRoomEntry: !useDebugSavestate && !prepare && initialRoomLoadKind != InitialRoomLoadKind.LinkSummonedCutscene,
            toggleState: () => _runtimeState.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress),
            resources: _preparedRoomResources,
            world: _preparedWorld,
            runtimeState: _runtimeState);
        _preparedRoomResources = null;
        _preparedWorld = null;
        _inventory = new InventoryState(
            _treasures, _saveData, () => _rooms.CurrentDungeonIndex, _runtimeState);
        _rooms.RoomChanged += ApplyRoomMusic;
        yield return false;
        PackedScene gameplayScene = _gameplaySceneResource.Load();
        _scene = _preparedGameplayScene ?? gameplayScene.Instantiate<GameSceneGraph>();
        _preparedGameplayScene = null;
        if (prepare)
        {
            _scene.ProcessMode = ProcessModeEnum.Disabled;
            _scene.Visible = false;
            _scene.GetNode<CanvasLayer>("Interface").Visible = false;
            _scene.GetNode<Camera2D>("World/RoomCamera").Enabled = false;
        }
        AddChild(_scene);
        yield return false;
        _dialogue.ApplicationUpdateOwned = true;
        _player.ApplicationUpdateOwned = true;
        _dialogue.SetSoundPlayer(_sound.PlaySound);
        _dialogue.SetLinkNameProvider(() => _saveData.LinkName);
        _dialogue.SetBackgroundPaletteState(_rooms.World.BackgroundPalettes);
        _dialogue.SetAlternatePalettePriorityHandler(
            _player.SetAlternateTextboxPalettePriority);
        _dialogue.MessageSpeed = _saveData.TextSpeed;
        _hud.Initialize(_treasures, _inventory);
        _rooms.RoomChanged += SyncHudToRoom;
        _statusBar = new StatusBarController(_inventory, _hud, _sound.PlaySound,
            () => _saveData.ReadWramByte(WramAddress.wPlaytimeCounter));
        _mapScreen.Initialize(_rooms, _inventory,
            () => _saveData.ReadWramByte(WramAddress.wPlaytimeCounter));
        _inventoryScreen.Initialize(_treasures, _inventory,
            () => (_rooms.CurrentRoom.TilesetFlags & (int)TilesetFlags.Past) != 0, _hud);
        _ringMenuScreen.Initialize(_inventory);
        _debugFlagScreen.Initialize(
            _saveData, new GlobalFlagDatabase(), _treasures, _inventory);
        foreach (bool step in CreateControllersSteps())
            yield return step;
        if (prepare)
        {
            foreach (bool step in _entities.PrepareResources())
                yield return step;
            // Stop here until CUTSCENE_PREGAME_INTRO completes. No room
            // objects exist and no original game update has run in this graph.
            yield return true;
            if (initialRoomLoadKind != InitialRoomLoadKind.LinkSummonedCutscene)
                _rooms.EnterPreparedRoom();
        }

        Vector2 spawn = useDebugSavestate
            ? debugSavestate!.PlayerPosition
            : new Vector2(_saveData.RespawnX, _saveData.RespawnY);
        debugSavestate?.RestoreRoomParseState(_entities, _runtimeState);
        EnemyPlacementContext placementContext = useSavedSpawn
            ? EnemyPlacementContext.Warp(_rooms.CurrentRoom.GetPackedPosition(spawn))
            : EnemyPlacementContext.Unrestricted;
        if (initialRoomLoadKind != InitialRoomLoadKind.LinkSummonedCutscene)
            _entities.LoadRoom(_rooms.ActiveGroup, _rooms.CurrentRoom, placementContext);
        TryDisplayEraInfoAfterInitialRoomLoad(initialRoomLoadKind);
        debugSavestate?.RestoreLiveState(
            _saveData,
            _runtimeState,
            _random,
            _entities);
        _roomView.SetRoom(_rooms.CurrentRoom.Texture);
        if (!useSavedSpawn && !useDebugSavestate)
            spawn = FindSpawn();
        _player.Initialize(_playerWorld, _inventory, spawn, _random);
        if (useSavedSpawn && initialRoomLoadKind == InitialRoomLoadKind.Ordinary)
        {
            // bank1.initializeGame sets signed invincibility $88: 120
            // non-flashing object updates. Pregame cutscenes have already
            // consumed that interval before their separate arrival handoff.
            _player.ApplyInteractionInvincibility(0x78);
        }
        if (useSavedSpawn)
        {
            // loadingRoom/func_5c18 call setEnteredWarpPosition before
            // initializeRoom so a saved death checkpoint on a doorway or
            // stair cannot immediately activate that warp again.
            _transitions.DeactivateWarpAtPlayerPosition(_player);
        }
        _player.GameOverRequested += BeginGameOver;
        if (useDebugSavestate)
        {
            _player.Face(debugSavestate!.PlayerFacing);
        }
        else if (useSavedSpawn)
        {
            _player.Face(_saveData.RespawnFacing switch
            {
                0 => Vector2I.Up,
                1 => Vector2I.Right,
                2 => Vector2I.Down,
                _ => Vector2I.Left
            });
        }
        _inventory.Changed += SyncHudToInventory;
        SyncHudToInventory();
        _transitions.ResetCamera();
        if (initialRoomLoadKind != InitialRoomLoadKind.LinkSummonedCutscene)
            ApplyRoomMusic(_rooms.ActiveGroup, _rooms.CurrentRoom);
        _scene.ApplyHudPlacement(_presentationSettings.HudBottom, _transitions);
        if (prepare)
        {
            _scene.ProcessMode = ProcessModeEnum.Inherit;
            _scene.Visible = true;
            _scene.InterfaceLayer.Visible = true;
            _roomCamera.Enabled = true;
        }
    }

    internal bool TryDisplayEraInfoAfterInitialRoomLoad(
        InitialRoomLoadKind initialRoomLoadKind) =>
        initialRoomLoadKind switch
        {
            // The original linkSummonedCutscene state 0 loads the arrival room
            // through its own path and never calls checkDisplayEraOrSeasonInfo.
            InitialRoomLoadKind.LinkSummonedCutscene => false,
            InitialRoomLoadKind.Ordinary =>
                _transitions.CheckDisplayEraInfoAfterFullRoomLoad(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(initialRoomLoadKind),
                initialRoomLoadKind,
                "Unknown initial room-load kind.")
        };

    public override void _ExitTree()
    {
        _preparedGameplayScene?.Free();
        _preparedGameplayScene = null;
        _bootCancellation?.Cancel();
        _bootCancellation?.Dispose();
        _bootResourceSteps?.Dispose();
        _gameplayPreparation?.Dispose();
        // The original engine does not write SRAM merely because play stops.
        // Unsaved changes remain only in the live WRAM-style save image.
        OracleGraphicsCache.Shutdown();
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

    internal void AdvanceGameplayPreparation()
    {
        if (_gameplayPreparation is null || GameplayPrepared) return;
        // Godot images and nodes stay on the main thread. Spread construction
        // over host frames, independently of original-update batching.
        var budget = System.Diagnostics.Stopwatch.StartNew();
        do
        {
            if (!_gameplayPreparation.MoveNext())
                throw new InvalidOperationException("Gameplay preparation missed its handoff boundary.");
            GameplayPrepared = _gameplayPreparation.Current;
        } while (!GameplayPrepared && budget.Elapsed.TotalMilliseconds < 2);
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
        if (_frontendIntro is not null)
        {
            _frontendIntro.Update(delta);
            return;
        }
        if (_mainMenu is not null)
        {
            _mainMenu.Update(delta);
            return;
        }
        if (_newGameIntro is { GraphicsLoadPending: true })
        {
            _newGameIntro.CompleteGraphicsLoad();
            return;
        }
        // saveQuitMenu_state0 stops THREAD_1 during Game Over; ordinary
        // gameplay menus leave the main thread running.
        if (_newGameIntro is not null || (_transitions is not null &&
            !_inventoryMenu.GameOver && !_roomEvents.ObjectUpdateSuspended))
            _saveData.AdvancePlaytime();
        if (_newGameIntro is not null)
        {
            Stage previous = _newGameIntro.CurrentStage;
            _newGameIntro.Update(delta);
            if (previous == Stage.PostVanish && _newGameIntro?.CurrentStage == Stage.RestartGame)
                _saveData.SetGlobalFlag(_newGameIntro.Record.LinkSummonedFlag);
            _newGameIntroScreen?.Dialogue.AdvanceApplicationUpdate();
            return;
        }
        if (_transitions is null)
            return;

        DialogueBox dialogue = _dialogue;
        try
        {
            if (_roomEvents.ObjectUpdateSuspended)
            {
                GameplayObjectPass pass = _suspendedObjectPass ?? throw new InvalidOperationException(
                    "A room interaction yielded outside the gameplay object pass.");
                _roomEvents.ResumeObjectUpdate();
                if (!_roomEvents.ObjectUpdateSuspended)
                {
                    _suspendedObjectPass = null;
                    _interactions.Update(delta, _player);
                    FinishGameplayObjectPass(delta, pass);
                }
            }
            else
            {
                _suspendedObjectPass = null;
                AdvanceGameplayState(delta);
            }
        }
        finally
        {
            if (GodotObject.IsInstanceValid(dialogue))
            {
                dialogue.AdvanceApplicationUpdate();
                _scene.ApplyHudPlacement(_presentationSettings.HudBottom, _transitions);
            }
        }
    }

    private GameplayObjectPass? _suspendedObjectPass;
    private readonly record struct GameplayObjectPass(
        bool ArrivalOwnsUpdate, bool FinishingArrival, bool ToggleOwnedUpdate,
        bool RoomTransitionOwnedUpdate, bool ScrollOwnedUpdate);

    private void AdvanceGameplayState(double delta)
    {
        AdvanceDebugSavestateStatus(delta);
        bool finishingArrival = _newGameArrivalRoomVisitPending;
        bool initializingArrival = _newGameArrivalRoomPending &&
            _newGameArrivalFadeFrames == 0 && _newGameArrivalFrames == 0;
        if (UpdateNewGameArrival(delta))
            return;
        bool arrivalOwnsUpdate = initializingArrival || finishingArrival;

        _debugCollision.Update();
        if (_debugObjectSpawner.Update())
            return;
        _debugFlagMenu.Update();
        if (_debugFlagMenu.IsActive)
            return;
        _debugMaple.Update();
        bool secretOwnedFrame = _secretEntry.IsActive;
        _secretEntry.Update(delta);
        if (secretOwnedFrame || _secretEntry.IsActive) return;
        bool ringMenuOwnedFrame = _ringMenu.IsActive;
        _ringMenu.Update(delta);
        // A close callback resumes Vasu's native script. The original menu
        // consumes that update, so do not also decrement his post-menu wait
        // on the frame where ownership is released.
        if (ringMenuOwnedFrame || _ringMenu.IsActive)
        {
            // runRingMenu updates the status bar after its state dispatch
            // only for appraisal, including the visible opening fade-in.
            if (_ringMenuScreen.Visible && _ringMenuScreen.Mode == RingMenuMode.Appraisal)
                _statusBar.Update(delta);
            return;
        }
        // cutscene02 advances before updateAllObjects. Its pending trigger
        // already blocks opening a menu between selection and state0.
        bool toggleOwnedUpdate = _entities.FloorToggle?.Active == true;
        _entities.FloorToggle?.AdvanceBeforeObjects();
        // bank1.s cutscene01 advances shock before updateMenus, including
        // its terminal update. Loading/warp and toggle cutscenes omit it.
        if (!arrivalOwnsUpdate && _transitions.GameplayMenusAvailable && !toggleOwnedUpdate)
            _player.AdvanceElectricShock();
        // b2_updateMenus warns only on its idle, eligible dispatch. IntroDone
        // rejects a fresh Start/Select edge before this cue; active menus and
        // their final closing update bypass it entirely.
        bool menuEdge = Input.IsActionJustPressed("inventory") || Input.IsActionJustPressed("map");
        if (_menuLifecycle.IsActive) _transitions.HandOffArrivalPaletteToMenu();
        if (!arrivalOwnsUpdate && !toggleOwnedUpdate && !_menuLifecycle.IsActive &&
            _transitions.GameplayMenusAvailable && !DialogueOpen && !_player.IsDying && _harp.PlayingInstrument == 0 &&
            !_interactions.GameplayMenuActive && !_roomEvents.MenusDisabled &&
            !_entities.PlayerMenusDisabled && !_player.ElectricShockActive && !_player.GaleActive &&
            (!menuEdge || _saveData.HasGlobalFlag(GlobalFlag.IntroDone)))
            _statusBar.UpdateLowHealthWarning();
        // b2_updateMenus checks scrolling, active text, death and instrument
        // before the IntroDone error cue. Active menus retain their dispatch.
        InventoryMenuController inventoryOwner = _inventoryMenu;
        if (!arrivalOwnsUpdate && !toggleOwnedUpdate && (_inventoryMenu.IsActive ||
            _transitions.GameplayMenusAvailable && !DialogueOpen && !_player.IsDying && _harp.PlayingInstrument == 0))
            _inventoryMenu.Update(delta);
        // Game Over restarts THREAD_1 into initializeGame. Its room's first
        // object update belongs to the following dispatch, not this old pass.
        if (_mainMenu is not null || !ReferenceEquals(inventoryOwner, _inventoryMenu))
            return;
        if (_inventoryMenu.IsActive)
        {
            if (_inventoryScreen.Visible)
            {
                _statusBar.Update(delta);
                _inventoryScreen.QueueRedraw();
            }
            return;
        }
        bool mapOwnedUpdate = _mapMenu.IsActive;
        bool transitioningBeforeMap = IsTransitioning;
        if (!arrivalOwnsUpdate && !toggleOwnedUpdate) _mapMenu.Update(delta);
        // Accepted Gale travel loads the destination at white. cutscene03
        // consumes that update; its new Link object starts on the next pass.
        if (mapOwnedUpdate && !transitioningBeforeMap && _transitions.IsTransitioning) return;
        // updateMenus returns the post-update wOpenedMenuType. Completing
        // menuStateFadeIntoGame resumes cutscene01 on this same update.
        if (_mapMenu.IsActive)
            return;
        // MENU_KIDNAME is a gameplay-owned file-menu screen in the original.
        // Keep servicing its controller while freezing the room beneath it.
        if (_interactions.GameplayMenuActive)
        {
            _interactions.Update(delta, _player);
            return;
        }

        // updateSpecialObjects runs w1Companion before w1Link. A waiting raft
        // remains in the later interaction pass until it allocates that slot.
        _player.AdvanceDeathPrelude();
        if (_transitions.AdvanceFadeOutWarpBeforeObjects(delta))
        {
            // mainThread advances wFrameCounter even when CUTSCENE_03
            // omits updateAllObjects, including its terminal room load.
            _entities.AdvanceFrozenRoomFrame();
            return;
        }
        if (!arrivalOwnsUpdate && !IsTransitioning && !toggleOwnedUpdate && !_roomEvents.OwnsGameLogic)
            (_pirateShipCourse ??= new PirateShipCourse()).Update(
                _saveData, _runtimeState, DialogueOpen, _harp.PlayingInstrument != 0);
        bool arrivalPaletteAdvanced = _transitions.AdvanceBasicArrivalPaletteBeforeObjects(delta);
        if (!IsTransitioning && !_harp.IsPlaying)
            _entities.UpdateSpecialObjectsBeforePlayer(_player);
        // updateAllObjects begins with updateSpecialObjects (companion, then
        // Link), followed by item parents. Link's physics/process split is
        // replayed here before enemies, parts, and interactions.
        bool scrollOwnedUpdate = _transitions.ScrollActive;
        bool roomTransitionOwnedUpdate = IsTransitioning;
        // linkState01 returns for active text before clearing the shared
        // instrument byte. A completed Flute/Harp signal survives that text.
        if (!DialogueOpen) _harp.BeginObjectUpdate();
        _transitions.BeginObjectUpdate();
        // warpTransitionB's final state initializes normal Link and returns;
        // movement/input starts at the following cutscene00 object update.
        if (!initializingArrival) _player.AdvanceApplicationUpdate(advanceElectricShock: false);
        _roomEvents.UpdateSpecialObjects();
        _entities.ClearSignalsAfterPlayer();
        if (!arrivalPaletteAdvanced) _transitions.UpdateWarpAndEffects(delta);
        if (!_transitions.TimeWarpActive)
        {
            _deathRespawnPoints.Update();
            if (_harp.IsPlaying)
                _entities.UpdateDuringHarp(delta, _player);
            else
                _entities.Update(delta, _player);
        }
        // A portal can begin the time warp from the contact pass above. The
        // original DISABLE_ALL_BUT_INTERACTIONS|DISABLE_LINK state freezes
        // ordinary room scripts from the following handler onward; the
        // transition controller advances only its imported warp interactions.
        if (_transitions.TimeWarpActive)
        {
            _roomEvents.UpdateDuringTimeWarpFrame();
        }
        else if (_entities.FloorToggle?.Frozen != true)
        {
            _roomEvents.UpdateFrame();
            if (_roomEvents.ObjectUpdateSuspended)
            {
                _suspendedObjectPass = new(arrivalOwnsUpdate, finishingArrival, toggleOwnedUpdate,
                    roomTransitionOwnedUpdate, scrollOwnedUpdate);
                return;
            }
            _interactions.Update(delta, _player);
        }
        FinishGameplayObjectPass(delta, new(arrivalOwnsUpdate, finishingArrival, toggleOwnedUpdate,
            roomTransitionOwnedUpdate, scrollOwnedUpdate));
    }

    private void FinishGameplayObjectPass(double delta, GameplayObjectPass pass)
    {
        bool arrivalOwnsUpdate = pass.ArrivalOwnsUpdate;
        bool finishingArrival = pass.FinishingArrival;
        bool toggleOwnedUpdate = pass.ToggleOwnedUpdate;
        bool roomTransitionOwnedUpdate = pass.RoomTransitionOwnedUpdate;
        bool scrollOwnedUpdate = pass.ScrollOwnedUpdate;
        _entities.SwitchHook?.UpdatePost(_player);
        _entities.Somaria?.UpdatePost(_player);
        _entities.Biggoron?.UpdatePost(_player);
        if (!arrivalOwnsUpdate && !IsTransitioning)
            _transitions.SampleRoomExit(_player);
        _entities.SynchronizeCompanionRiderAfterObjects(_player);
        _entities.UpdateHeldObjectPosition(_player);
        _player.PublishTilePushingDirection();
        // updateAllObjects drains up to four queued tile graphics after the
        // object passes, before screen-transition handling and animation.
        // Preserve the current scroll gate on its final frozen update.
        if (!_transitions.TimeWarpActive)
            _rooms.UpdateChangedTileGraphics(_transitions.ScrollActive ? (byte)8 : (byte)1);
        // cutscene01 selects a changed orb bit before getNextActiveRoom and
        // the enemy/part collision pass. cutscene02 only runs its handler
        // and updateAllObjects, including the update that releases its freeze.
        if (!arrivalOwnsUpdate && !toggleOwnedUpdate && !roomTransitionOwnedUpdate &&
            !IsTransitioning && !_roomEvents.Active)
            _entities.FloorToggle?.CheckAfterObjects();
        bool toggleOwnsPostObjects = toggleOwnedUpdate || _entities.FloorToggle?.Active == true;
        // The source screen-transition handler follows updateAllObjects.
        // In particular, the final scroll update still freezes destination
        // entities and room events; ordinary updates resume next tick.
        if (scrollOwnedUpdate)
            _transitions.UpdateScroll(delta);
        else if (!arrivalOwnsUpdate && !toggleOwnsPostObjects)
            UpdatePostObjectPlayerState(roomExitSampled: true);
        _harp.Update(delta);
        _statusBar.Update(delta);
        UpdateAnimatedTiles(delta);
        if (!arrivalOwnsUpdate && !IsTransitioning && !toggleOwnsPostObjects)
            _entities.ResolvePostObjectCollisions(_player);
        if (roomTransitionOwnedUpdate && !IsTransitioning)
            _entities.FloorToggle?.CompleteRoomInitialization();
        if (finishingArrival)
        {
            // cutscene00 records the visit after updateAllObjects. The
            // arrival path never calls cutscene01's addToGashaMaturity.
            _newGameArrivalRoomVisitPending = false;
            _rooms.MarkCurrentRoomVisited();
        }
        UpdateRoomDebugLabel();
        _debugWarps.Update();
    }

    internal void UpdatePostObjectPlayerState(bool roomExitSampled = false)
    {
        // screenTransitionState2 runs after updateAllObjects in the original.
        // Interactions and moving platforms can move Link after his own state
        // handler, so check the final object-authored position as well. This is
        // required for side-view edge warps reached on a moving platform.
        if (!IsTransitioning)
        {
            bool tileWarpStarted = _transitions.CheckRequestedTileWarp(_player);
            bool deepWaterStarted = _transitions.BeginRequestedDeepWaterDive(_player);
            if (!tileWarpStarted && !deepWaterStarted)
            {
                if (roomExitSampled) _transitions.CommitRoomExit(_player);
                else _transitions.CheckRoomExit(_player);
            }
        }

        // The camera likewise observes the final post-object Link position.
        // RoomTransitionController.Update still advances active warps and
        // scrolls at its normal point; this inactive-only sample prevents a
        // platform displacement from appearing one application update late.
        if (!IsTransitioning)
            _transitions.UpdateCamera();
    }

    internal bool UpdateNewGameArrival(double delta)
    {
        if (_newGameArrivalFadeFrames <= 0 && _newGameArrivalFrames <= 0)
        {
            if (_newGameArrivalRoomPending)
            {
                // linkSummonedCutscene state 2, substate 2 runs on the update
                // after the final wave step. Only now does initializeRoom
                // create the native room objects and consume placement RNG.
                _newGameArrivalRoomPending = false;
                _newGameArrivalRoomVisitPending = true;
                _saveData.SetGlobalFlag(GlobalFlag.PregameIntroDone);
                _entities.LoadRoom(_rooms.ActiveGroup, _rooms.CurrentRoom,
                    EnemyPlacementContext.Warp(_rooms.CurrentRoom.GetPackedPosition(_player.Position)));
            }
            return false;
        }

        if (_newGameArrivalFadeFrames > 0)
        {
            _newGameArrivalTicks = Math.Min(
                _newGameArrivalFadeFrames,
                _newGameArrivalTicks + delta * 60.0);
            int fadeFrame = Mathf.FloorToInt(_newGameArrivalTicks);
            _newGameArrivalPhase = fadeFrame;
            _roomView.SetHorizontalWave(0xff, _newGameArrivalPhase);
            // The 32 palette offsets occur on updates 1,3,...63; updates 64-65
            // retain the completed palette while the thread reports completion.
            int paletteStep = Math.Min(32, (fadeFrame + 1) / 2);
            _warpFade.Color = new Color(1, 1, 1, 1.0f - paletteStep / 32.0f);
            if (_newGameArrivalTicks < _newGameArrivalFadeFrames)
                return true;

            _newGameArrivalFadeFrames = 0;
            _newGameArrivalTicks = 0.0;
            _warpFade.Color = new Color(1, 1, 1, 0);
            return true;
        }

        _newGameArrivalTicks = Math.Min(
            _newGameArrivalFrames,
            _newGameArrivalTicks + delta * 60.0);
        int frame = Mathf.FloorToInt(_newGameArrivalTicks);
        int amplitude = Math.Max(0, 0xff - frame * 2);
        _roomView.SetHorizontalWave(amplitude, _newGameArrivalPhase + frame);
        int slowFallStartFrame = _newGameArrivalFrames / 2;
        for (int update = _newGameArrivalLastFrame + 1; update <= frame; update++)
        {
            if (update == slowFallStartFrame)
            {
                int screenY = Mathf.FloorToInt(
                    _transitions.WorldToGameplayScreen(_player.Position).Y);
                _player.BeginNewGameSlowFall(
                    Player.NewGameSlowFallInitialZ(screenY));
            }
            else if (update > slowFallStartFrame && _player.IsNewGameSlowFalling)
            {
                _player.AdvanceNewGameSlowFall();
            }
        }
        _newGameArrivalLastFrame = frame;
        if (_newGameArrivalTicks < _newGameArrivalFrames)
            return true;

        _newGameArrivalFrames = 0;
        _player.EndNewGameSlowFall();
        _roomView.ClearHorizontalWave();
        _warpFade.Color = new Color(1, 1, 1, 0);
        _player.Visible = true;
        _player.SetPhysicsProcess(true);
        _player.SetProcess(true);
        return true;
    }

    private IEnumerable<bool> CreateControllersSteps()
    {
        var timePortals = new TimePortalDatabase();
        var enemies = new EnemyDatabase();
        yield return false;
        _entities = new RoomEntityManager(
            _scene.WorldRoot, new NpcDatabase(), enemies,
            new ItemDropDatabase(), timePortals, _random, _saveData,
            runtimeState: _runtimeState,
            inventory: _inventory,
            animationTick: () => (long)_animationTicks,
            treasures: _treasures,
            rooms: _rooms);
        _entities.DisplayedHealthSource = () => _statusBar.DisplayedHealth;
        yield return false;
        _pushBlocks = new PushBlockController(
            _rooms, new PushableTileDatabase(), _roomView,
            () => (long)_animationTicks, _sound.PlaySound,
            _entities.PushBlockPermittedByColoredCube,
            _entities.RequestSomariaPush,
            braceletLevelSource: () => _inventory.BraceletLevel,
            movementMemory: _runtimeState)
        {
            Name = "PushBlock",
            BraceletObtainedSource = () => _inventory.HasTreasure(TreasureId.Bracelet)
        };
        _scene.WorldRoot.AddChild(_pushBlocks);
        _pushBlocks.SetPhysicsProcess(false);
        _entities.ReservedPushBlock = _pushBlocks;
        _keyDoors = new DungeonKeyDoorController(
            _rooms, _inventory, _entities, _treasures,
            () => (long)_animationTicks, _sound.PlaySound)
        {
            Name = "DungeonKeyDoors"
        };
        _scene.WorldRoot.AddChild(_keyDoors);
        _keyDoors.SetPhysicsProcess(false);
        _keyholes = new OverworldKeyholeController(
            _rooms, _inventory, _entities, new OverworldKeyholeDatabase(),
            _sound)
        {
            Name = "OverworldKeyholes"
        };
        _scene.WorldRoot.AddChild(_keyholes);
        _collision = new RoomCollision(
            _rooms, _entities, _pushBlocks, point => _transitions.HasNeighborFor(point));
        _deathRespawnPoints = new DeathRespawnPointController(
            _rooms, _player, _runtimeState);
        _transitions = new RoomTransitionController(
            _rooms, new WarpDatabase(), _roomView, _scene.RoomLoadReveal,
            _player, _roomCamera,
            _warpFade, _hud, _dialogue, _entities,
            _deathRespawnPoints, _sound, timePortals);
        _entities.WorldToScreen = _transitions.WorldToGameplayScreen;
        _dialogue.SetGameplayCameraYProvider(
            () => -_transitions.WorldToGameplayScreen(Vector2.Zero).Y);
        _transitions.ScrollingTransitionFinished += _ => ApplyDeferredIntroMusic();
        _entities.TimePortalEntered += portal =>
            _transitions.ApplyTimePortalWarp(_player, portal);
        _entities.RoomWarpRequested += warp =>
            _transitions.ApplyWarp(_player, warp);
        _entities.SoundRequested += _sound.PlaySound;
        _entities.NativeChannelVolumeWritten += _sound.SetNativeChannelVolume;
        _entities.RoomMusicRequested += _sound.PlayRoomMusic;
        _entities.ScreenShakeChanged += offset => _roomCamera.Offset = offset;
        yield return false;
        _entities.EnemyDefeated += _inventory.RecordEnemyKill;
        _entities.RoomTileChanged += _roomView.QueueRedraw;
        _roomEvents = new RoomEventController(
            _rooms, _entities, _transitions, _dialogue, _player, _roomView,
            _transitions.WorldToGameplayScreen, () => (long)_animationTicks,
            _scene.InterfaceLayer, _warpFade, _hud, _inventory, _treasures,
            _sound, _roomCamera, deferPreparation: true);
        foreach (bool step in _roomEvents.PrepareResources())
            yield return step;
        _interactions = new InteractionController(
            _rooms, _entities, new SignDatabase(), new ChestDatabase(), _treasures, _dialogue,
            _scene.WorldRoot, _roomView, _transitions.WorldToGameplayScreen,
            () => (long)_animationTicks,
            _inventory, _scene.InterfaceLayer, _sound.PlaySound,
            () => _statusBar.DisplayedRupees == _inventory.Rupees &&
                _statusBar.DisplayedHealth == _inventory.HealthQuarters,
            _roomEvents.InteractionHandlers,
            () => _statusBar.DisplayedRupees == _inventory.Rupees);
        yield return false;
        _pushBlocks.MessageRequested += message =>
            _interactions.ShowRoomInteractionMessage(message, _player);
        _keyDoors.MessageRequested += message =>
            _interactions.ShowRoomInteractionMessage(message, _player);
        _keyholes.MessageRequested += message =>
            _interactions.ShowRoomInteractionMessage(message, _player);
        _entities.DungeonEntranceTriggered += (_, message) =>
        {
            _interactions.ShowRoomInteractionMessage(message, _player);
            _deathRespawnPoints.RecordCurrentPoint();
        };
        _transitions.ScreenTransitionsDisabledSource = () =>
            _roomEvents.ScreenTransitionsDisabled ||
            _entities.ScreenTransitionsDisabled;
        _transitions.AllScreenTransitionsDisabledSource = () =>
            _roomEvents.AllScreenTransitionsDisabled;
        _keyholes.SetEventHandler(
            _roomEvents.SupportsOverworldKeyhole,
            _roomEvents.TriggerOverworldKeyhole);
        _entities.NonInteractionObjectsDisabledSource = () => _roomEvents.FreezesNonInteractionObjects;
        _entities.InitializedObjectsDisabledSource = () => _transitions.AwaitingLinkWarpState;
        _entities.TextAllowsScriptSource = () => _dialogue.AllowsNativeScriptCommands;
        _entities.FloorToggle = new DungeonToggleController(_rooms, _runtimeState, _entities,
            _sound.PlaySound, () => (long)_animationTicks);
        _combat = new CombatController(
            _rooms, _roomView, _entities,
            new BreakableTileDatabase(), _saveData, _sound,
            () => (long)_animationTicks);
        _bracelet = new BraceletController(
            _scene.WorldRoot, _rooms, new BreakableTileDatabase(), _roomView,
            _entities, _combat, _saveData, _sound.PlaySound,
            () => (long)_animationTicks, _collision.HasFullWall);
        _bomb = new BombController(
            _inventory,
            _entities,
            _rooms,
            _sound.PlaySound,
            () => (_rooms.CurrentRoom.TilesetFlags & (int)TilesetFlags.Underwater) != 0);
        _roomEvents.SetBraceletActions(
            discard => _bracelet.Interrupt(_player, discard),
            () => _bracelet.Update(
                _player,
                Vector2.Zero,
                primaryHeld: false,
                secondaryHeld: false,
                itemButtonJustPressed: false));
        _bracelet.TileLifted += _roomEvents.NotifyBraceletTileLifted;
        _bracelet.TileLiftCompleted +=
            _roomEvents.NotifyBraceletTileLiftCompleted;
        _shovel = new ShovelController(
            _rooms, new BreakableTileDatabase(), _roomView, _entities, _saveData,
            _sound.PlaySound, () => (long)_animationTicks);
        _seedSatchel = new SeedSatchelController(
            _inventory, _entities, new SeedSatchelDatabase(), _rooms,
            _sound.PlaySound);
        _entities.SwitchHook = new SwitchHookController(_scene.WorldRoot, _rooms, _entities, _sound.PlaySound,
            () => (long)_animationTicks, _combat.SpawnBreakEffect, () => _pushBlocks.Active);
        _entities.Somaria = new SomariaController(_scene.WorldRoot,_rooms,_entities,_sound.PlaySound);
        _entities.Biggoron = new BiggoronSwordController(_scene.WorldRoot,_entities,_combat,_sound.PlaySound);
        _harp = new HarpController(
            _rooms, _entities, _transitions, _interactions, _sound);
        yield return false;
        _entities.PlayingInstrumentSource = () => _harp.PlayingInstrument;
        _terrain = new TerrainController(
            _rooms, new BreakableTileDatabase(),
            _collision.AdjacentWallsBitset, (position, hazard) => _entities.TryCreateSplash(position, hazard));
        _entities.ItemDropEnteredHazard += _terrain.SpawnSplash;
        _pushBlocks.EnteredHazard += (position, hazard) =>
        {
            if (hazard is HazardType.Water or HazardType.Lava)
            {
                // objectReplaceWithSplash retires $14 even when its checked
                // INTERAC $03/$04 allocation fails.
                _entities.TryCreateSplash(position, hazard);
            }
            else if (hazard == HazardType.Hole)
            {
                // objectCreateFallingDownHoleInteraction is checked. A full
                // pool still retires $14, without a hole actor/cue/event.
                if (!_entities.InteractionSlotAvailable) return;
                _entities.Spawn<FallingDownHoleEffect>(
                    new FallingDownHoleSpawn(position));
                _roomEvents.NotifyObjectFellInHole(
                    ObjectFellInHoleKind.PushBlock);
            }
        };
        _debugCollision = new DebugCollisionController();
        _playerWorld = new PlayerWorld(
            _transitions, _interactions, _collision, _pushBlocks, _keyDoors, _keyholes,
            _terrain, _combat, _entities,
            _bomb, _bracelet, _shovel, _seedSatchel, _harp, _roomEvents,
            _inventory, _sound, () => _debugCollision.CollisionsDisabled, _rooms);
        _debugWarps = new DebugWarpController(
            _player, LoadDebugRoom, FindSpawn,
            _launchOptions.DebugWarpGroup, _launchOptions.DebugWarpRoom);
        _debugMaple = new DebugMapleController(
            _entities, _rooms, _saveData, _inventory, _player,
            LoadDebugRoom, FindSpawn,
            () => !IsTransitioning && !DialogueOpen && !MapMenuOpen &&
                !InventoryMenuOpen && !RingMenuOpen &&
                !_player.GaleActive && !_player.IsDying && !_player.IsUsingHarp && !_roomEvents.Active &&
                !_roomEvents.MenusDisabled &&
                !_interactions.GameplayMenuActive &&
                !_entities.PlayerMenusDisabled && !_player.ElectricShockActive);
        _gameplayPause = new GameplayPauseController(_player, _roomDebug);
        _gameplayPause.SetRoomOverlayEnabled(_presentationSettings.RoomOverlayEnabled);
        _menuLifecycle = new OracleMenuLifecycle(_scene.MenuFade, _gameplayPause);
        _mapMenu = new MapMenuController(
            _mapScreen, _dialogue, _menuLifecycle,
            () => _transitions.GameplayMenusAvailable && !DialogueOpen && !InventoryMenuOpen &&
                !_player.GaleActive && !_player.IsDying && _harp.PlayingInstrument == 0 && !_roomEvents.Active &&
                !_roomEvents.MenusDisabled &&
                !_entities.PlayerMenusDisabled && !_player.ElectricShockActive,
            () => _saveData.HasGlobalFlag(GlobalFlag.IntroDone),
            FastTravelFromMap, _sound.PlaySound, _sound.SetMusicVolume);
        _mapMenu.ConfigureGale(_rooms,
            target => _transitions.ApplyWarp(_player, new Warp(
                _rooms.ActiveGroup, _rooms.CurrentRoom.Id, 0, 0, 0,
                _rooms.ActiveGroup, target.Room, target.Position, 0, WarpDestinationTransition.Fall)),
            () => _player.ReturnFromGale((int)_transitions.WorldToGameplayScreen(_player.Position).Y));
        _entities.GaleMenuRequested += _mapMenu.OpenGale;
        _inventoryMenu = new InventoryMenuController(
            _inventoryScreen, _saveQuitScreen, _menuLifecycle,
            () => _saveData.HasGlobalFlag(GlobalFlag.IntroDone),
            () => _saveData.HasGlobalFlag(GlobalFlag.IntroDone) &&
                _transitions.GameplayMenusAvailable && !DialogueOpen && !MapMenuOpen &&
                !_player.GaleActive && !_player.IsDying && _harp.PlayingInstrument == 0 && !_roomEvents.Active &&
                !_roomEvents.MenusDisabled &&
                !_entities.PlayerMenusDisabled && !_player.ElectricShockActive,
            SaveActiveFile, ReturnToTitle, _sound.PlaySound,
            RestartGameplayAfterDeath);
        _inventoryMenu.ConfigureOptions(
            option => option switch
            {
                0 => _debugCollision.CollisionsDisabled,
                1 => _gameplayPause.RoomOverlayEnabled,
                2 => _presentationSettings.HudBottom,
                _ => throw new ArgumentOutOfRangeException(nameof(option))
            },
            (option, enabled) =>
            {
                if (option == 0)
                    _debugCollision.SetEnabled(enabled);
                else if (option == 1)
                {
                    _presentationSettings.RoomOverlayEnabled = enabled;
                    _gameplayPause.SetRoomOverlayEnabled(enabled);
                }
                else if (option == 2)
                {
                    _presentationSettings.HudBottom = enabled;
                }
                else
                    throw new ArgumentOutOfRangeException(nameof(option));
                if (option is 1 or 2 && _persistSaveData &&
                    _presentationSettings.Save() is var error && error != Error.Ok)
                    GD.PushWarning($"Could not save presentation preference: {error}.");
            });
        _mapMenu.ConfigureSaveQuit(_inventoryMenu);
        _ringMenu = new RingMenuController(
            _ringMenuScreen, _dialogue, _menuLifecycle, _inventory, _saveData,
            _treasures, _roomEvents.Get<VasuShopEvent>().Database, _sound.PlaySound);
        _roomEvents.SetRingMenuOpener(_ringMenu.Open);
        _secretEntry = new SecretEntryController(
            _scene.InterfaceLayer, _menuLifecycle, _saveData, _sound.PlaySound);
        _roomEvents.SetSecretMenuOpener(_secretEntry.Open);
        _debugFlagMenu = new DebugFlagMenuController(
            _debugFlagScreen, _rooms, _gameplayPause,
            () => !IsTransitioning && !DialogueOpen && !MapMenuOpen &&
                !InventoryMenuOpen && !_player.GaleActive && !_player.IsDying &&
                !_roomEvents.Active && !_roomEvents.MenusDisabled,
            _inventory, RefreshDebugCompanionLayout);
        _scene.DebugObjectSpawnerScreen.Initialize(enemies);
        _debugObjectSpawner = new DebugObjectSpawnerController(
            _scene.DebugObjectSpawnerScreen, _rooms, _entities, _player, _gameplayPause,
            () => !IsTransitioning && !DialogueOpen && !MapMenuOpen &&
                !InventoryMenuOpen && !_player.GaleActive && !_player.IsDying &&
                !_player.IsUsingHarp && !_player.ElectricShockActive &&
                !_roomEvents.Active && !_roomEvents.MenusDisabled &&
                !_entities.PlayerMenusDisabled && !_interactions.GameplayMenuActive);
    }

    internal void UpdateAnimatedTiles(double delta)
    {
        // updateAnimations returns while wScrollMode bit 0 is clear. Both
        // scrolling and warp transitions keep that bit clear, so the original
        // animation counters and queued VRAM state remain completely frozen.
        if (IsTransitioning)
            return;

        _animationTicks += delta * 60.0;
        if (_rooms.CurrentRoom.UpdateAnimation((long)_animationTicks))
            _roomView.QueueRedraw();
    }

    internal void UpdateRoomDebugLabel()
    {
        string roomText = $"{_rooms.ActiveGroup:x1}:{_rooms.CurrentRoom.Id:x2}";
        if (_debugCollision.CollisionsDisabled)
            roomText += " NOCLIP";
        if (_debugFastForward)
            roomText += " FF x4";
        if (_debugSavestateStatusFrames > 0.0 &&
            !string.IsNullOrEmpty(_debugSavestateStatus))
        {
            roomText += $"  {_debugSavestateStatus}";
        }
        if (_roomDebug.Text != roomText)
            _roomDebug.Text = roomText;
    }

    private void AdvanceDebugSavestateStatus(double delta)
    {
        if (_debugSavestateStatusFrames <= 0.0)
            return;

        _debugSavestateStatusFrames = Math.Max(
            0.0,
            _debugSavestateStatusFrames + delta * -60.0);
        if (_debugSavestateStatusFrames == 0.0)
            _debugSavestateStatus = string.Empty;
    }

    private void SetDebugSavestateStatus(string status)
    {
        _debugSavestateStatus = status;
        _debugSavestateStatusFrames = 120.0;
        UpdateRoomDebugLabel();
    }

    internal void ClearDebugSavestateStatus()
    {
        _debugSavestateStatus = string.Empty;
        _debugSavestateStatusFrames = 0.0;
        UpdateRoomDebugLabel();
    }

    private bool CanCaptureDebugSavestate() =>
        _newGameArrivalFadeFrames <= 0 &&
        _newGameArrivalFrames <= 0 &&
        !IsTransitioning &&
        !DialogueOpen &&
        !MapMenuOpen &&
        !InventoryMenuOpen &&
        !RingMenuOpen &&
        !DebugFlagMenuOpen &&
        !_interactions.GameplayMenuActive &&
        !_gameplayPause.IsLeased &&
        !_player.GaleActive && !_player.IsDying &&
        !_player.IsUsingHarp &&
        !_roomEvents.Active &&
        !_roomEvents.MenusDisabled &&
        !_entities.PlayerMenusDisabled && !_player.ElectricShockActive;

    internal DebugSavestateData CaptureDebugSavestate() =>
        DebugSavestateData.Capture(
            _rooms,
            _player,
            _saveData,
            _runtimeState,
            _random,
            _entities,
            _animationTicks);

    internal void RestoreDebugSavestate(DebugSavestateData debugSavestate)
    {
        ArgumentNullException.ThrowIfNull(debugSavestate);
        ReleaseGameplayScene();
        _sound.RestartSound();
        _random = new OracleRandom();
        InitializeGameplay(
            debugSavestate.CreateSaveData(),
            debugSavestate: debugSavestate);
    }

    internal void SyncHudToInventory()
    {
        if (_hud == null || _inventory == null)
            return;
        _hud.MaxHealthQuarters = _inventory.MaxHealthQuarters;
        _hud.EquippedA = _inventory.EquippedA;
        _hud.EquippedB = _inventory.EquippedB;
        _hud.DungeonIndex = _rooms.CurrentDungeonIndex;
        _hud.TilesetFlags = _rooms.CurrentRoom.TilesetFlags;
        _hud.Refresh();
    }

    private void SyncHudToRoom(int group, OracleRoomData room) =>
        SyncHudToInventory();

    // File-menu and gameplay commits share the same persistence boundary.
    // Alternative hosts can provide an isolated store without changing save
    // decisions, restart locations, or the lifetime of the live save image.
    protected virtual OracleSaveData? LoadFileSlot(int slot) => OracleSaveStore.LoadSlot(slot);
    protected virtual SaveResult StoreFileSlot(int slot, OracleSaveData save) => OracleSaveStore.SaveSlot(slot, save);
    protected virtual void EraseFileSlot(int slot) => OracleSaveStore.EraseSlot(slot);

    private SaveResult SaveActiveFile()
    {
        _saveWriteRequests++;
        if (_persistSaveData && _saveData is not null)
            return StoreFileSlot(_activeSaveSlot, _saveData);
        return SaveResult.Succeeded;
    }

    internal void BeginGameOver()
    {
        _sound.RestartSound();
        _saveData.IncrementDeathCount();
        _sound.PlaySound(SoundId.MusGameOver);
        _inventoryMenu.BeginGameOver();
    }

    private void RestartGameplayAfterDeath()
    {
        OracleSaveData liveSave = _saveData;
        ReleaseGameplayScene();
        _sound.RestartSound();
        liveSave.AdvancePlaytime();
        InitializeGameplay(liveSave, forceDeathRespawn: true);
    }

    private void ReturnToTitle()
    {
        ReleaseGameplayScene();
        StartFrontend(startAtTitle: true);
    }

    private void ReleaseGameplayScene(bool immediate = false)
    {
        if (_player is not null)
            _player.GameOverRequested -= BeginGameOver;
        if (_inventory is not null)
            _inventory.Changed -= SyncHudToInventory;
        _statusBar?.Dispose();
        if (_rooms is not null)
        {
            _rooms.RoomChanged -= ApplyRoomMusic;
            _rooms.RoomChanged -= SyncHudToRoom;
        }
        _entities?.Dispose();

        // gameplay.tscn owns every persistent and transient gameplay node.
        // Freeing this one root leaves the application-owned sound engine in
        // place for the title screen and the next selected file.
        if (immediate)
            _scene.Free();
        else
            _scene.QueueFree();
    }

    /// <summary>
    /// Releases frontend and gameplay resources and starts an in-memory
    /// session from the supplied save with fresh runtime state and RNG.
    /// </summary>
    internal void InitializeTransientSession(OracleSaveData save)
    {
        _suspendedObjectPass = null;
        _bootLoading?.End();
        _bootLoading = null;
        _bootTasks.Clear();
        _bootResourceSteps?.Dispose();
        _bootResourceSteps = null;
        _bootCancellation?.Cancel();
        _bootCancellation?.Dispose();
        _bootCancellation = null;
        _bootDataTask = null;
        _preparedGameplayScene?.Free();
        _preparedGameplayScene = null;
        _preparedWorld = null;
        _preparedRoomResources = null;
        _gameplayPreparation?.Dispose();
        _gameplayPreparation = null;
        GameplayPrepared = false;
        if (_persistSaveData)
        {
            throw new InvalidOperationException(
                "Transient-session initialization requires disabled save persistence.");
        }

        _mainMenu = null;
        _frontendIntro = null;
        _newGameIntro = null;
        if (_frontendIntroScreen is not null &&
            GodotObject.IsInstanceValid(_frontendIntroScreen))
        {
            _frontendIntroScreen.Free();
        }
        if (_mainMenuScreen is not null &&
            GodotObject.IsInstanceValid(_mainMenuScreen))
        {
            _mainMenuScreen.Free();
        }
        if (_newGameIntroScreen is not null &&
            GodotObject.IsInstanceValid(_newGameIntroScreen))
        {
            _newGameIntroScreen.Free();
        }
        _mainMenuScreen = null;
        _frontendIntroScreen = null;
        _newGameIntroScreen = null;

        if (_scene is not null && GodotObject.IsInstanceValid(_scene))
        {
            if (_scene.IsQueuedForDeletion())
                _scene.Free();
            else
                ReleaseGameplayScene(immediate: true);
        }

        // A previous session may have queued roots for the end of the host
        // frame. Remove them before constructing the new ownership graph.
        foreach (Node child in GetChildren())
        {
            if (child != _sound && GodotObject.IsInstanceValid(child))
                child.Free();
        }

        _applicationUpdates.Reset();
        _applicationInput.Clear();
        _saveWriteRequests = 0;
        _newGameArrivalTicks = 0.0;
        _newGameArrivalFadeFrames = 0;
        _newGameArrivalFrames = 0;
        _newGameArrivalPhase = 0;
        _newGameArrivalLastFrame = 0;
        _newGameArrivalRoomPending = false;
        _newGameArrivalRoomVisitPending = false;
        _deferredIntroMusicGroup = -1;
        _deferredIntroMusicRoom = -1;
        _debugSavestateStatus = string.Empty;
        _debugSavestateStatusFrames = 0.0;
        _animationTicks = 0.0;

        _sound.ApplicationUpdateOwned = true;
        _sound.RestartSound();
        if (_sound.Disabled)
            _sound.PlaySound(SoundId.SndCtrlEnable);
        _sound.SetMusicVolume(3);

        _random = new OracleRandom();
        InitializeGameplay(save);
    }

    private void ApplyRoomMusic(int group, OracleRoomData room)
    {
        _deferredIntroMusicGroup = -1;
        _deferredIntroMusicRoom = -1;
        if (_transitions?.SuppressesDestinationMusic == true)
            return;

        bool playableIntro =
            _saveData.HasGlobalFlag(GlobalFlag.PregameIntroDone) &&
            !_saveData.HasGlobalFlag(GlobalFlag.IntroDone);
        if (!playableIntro)
        {
            _sound.PlayRoomMusic(group, room.Id, _saveData);
            return;
        }

        // INTERAC_PLAY_NAYRU_MUSIC $2f exists in 0:49, not on Nayru's
        // gathering screen. Destination interactions remain frozen during a
        // scroll, so its volume-2 override starts only when that scroll ends.
        if (group == 0 && room.Id == 0x49)
        {
            if (_transitions is not null && _transitions.ScrollActive)
            {
                _deferredIntroMusicGroup = group;
                _deferredIntroMusicRoom = room.Id;
                return;
            }
            PlayNayruApproachMusic();
        }

        // All other room assignments are suppressed until an intro
        // interaction explicitly changes the active track.
    }

    private void ApplyDeferredIntroMusic()
    {
        if (_deferredIntroMusicGroup != _rooms.ActiveGroup ||
            _deferredIntroMusicRoom != _rooms.CurrentRoom.Id)
            return;

        _deferredIntroMusicGroup = -1;
        _deferredIntroMusicRoom = -1;
        PlayNayruApproachMusic();
    }

    private void PlayNayruApproachMusic()
    {
        _sound.PlayMusicIfChanged(SoundId.MusNayru);
        _sound.SetMusicVolume(2);
    }

    internal bool Collides(Vector2 playerPosition) => _collision.Collides(playerPosition);
    internal bool TryInteract(Player player) => _interactions.TryInteract(player);
    internal TerrainInfo GetTerrainInfo(Vector2 position) => _terrain.GetTerrainInfo(position);
    internal ActiveTerrainInfo GetActiveTerrain(Vector2 position) => _terrain.GetActiveTerrain(position);
    internal bool TryStartLedgeHop(Player player, Vector2 from, Vector2 movement) =>
        _terrain.TryStartLedgeHop(player, from, movement);
    internal bool CheckTileWarp(Player player) => _transitions.CheckTileWarp(player);
    internal void CheckRoomExit(Player player)
        => _transitions.CheckRoomExit(player);

    internal Vector2 FindSpawn()
    {
        Vector2 center = new(80, 64);
        Vector2 best = center;
        float bestDistance = float.MaxValue;
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 10; x++)
        {
            Vector2 candidate = new(x * 16 + 8, y * 16 + 8);
            if (Collides(candidate))
                continue;
            float distance = candidate.DistanceSquaredTo(center);
            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }
        return best;
    }

    private void RefreshDebugCompanionLayout()
    {
        if (!_rooms.CurrentRoom.IsCompanionRegion)
            return;
        // Debug selection may replace the terrain underneath a mounted animal.
        // Drop the old live slot before rebuilding entities and finding safe ground.
        foreach (int id in new[] { CompanionRuntimeState.RickyId,
                     CompanionRuntimeState.DimitriId, CompanionRuntimeState.MooshId })
            CompanionRuntimeState.Clear(_runtimeState, id);
        CompanionRuntimeState.ForgetRemembered(_runtimeState);
        LoadDebugRoom(_rooms.ActiveGroup, _rooms.CurrentRoom.Id);
        _player.WarpTo(FindSpawn());
    }

    internal void LoadDebugRoom(int group, int room)
    {
        _dialogue.Close();
        _transitions.ClearDeactivatedWarp();
        _entities.ClearRecentEnemyDefeats();
        OracleRoomData loaded = _rooms.Load(group, room);
        // Dungeon side-view layouts live in object/tileset groups $04/$05,
        // but the retail room loader switches the active group to $06/$07 so
        // checkWarpsSidescrolling can resolve their edge-warp tables. Direct
        // room launches bypass that retail warp, so reproduce the group switch
        // here when the requested dungeon room is side-scrolling.
        if (group is 4 or 5 &&
            (loaded.TilesetFlags & (int)TilesetFlags.Sidescroll) != 0 &&
            _rooms.World.HasRoom(group + 2, room))
        {
            loaded = _rooms.Load(group + 2, room);
        }
        _roomView.SetRoom(loaded.Texture);
        _entities.LoadRoom(_rooms.ActiveGroup, loaded);
        _hud.Refresh();
        _transitions.ResetCamera();
    }

    private void FastTravelFromMap(int group, int room)
    {
        LoadDebugRoom(group, room);
        _player.WarpTo(FindSpawn());
        _player.Face(Vector2I.Down);
        _transitions.ResetCamera();
    }

    internal void ClearDeactivatedWarp() => _transitions.ClearDeactivatedWarp();
    internal void RefreshRoomObjects() => _entities.LoadRoom(_rooms.ActiveGroup, _rooms.CurrentRoom);
    internal void UpdateRoomCamera() => _transitions.ResetCamera();
    internal Vector2 WorldToScreen(Vector2 position) => _transitions.WorldToScreen(position);
    internal void UpdateScrollingTransition(double delta) => _transitions.UpdateScroll(delta);
}

internal enum InitialRoomLoadKind
{
    Ordinary,
    LinkSummonedCutscene
}
