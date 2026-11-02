using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Oracles;

public partial class GameRoot : IGameplayObjectPhases, IGameplayForegroundPhases
{
    private readonly AgesGameplaySceneResource _gameplaySceneResource = new();
    private IEnumerator<bool>? _gameplayPreparation;
    private bool _loadingSavedGame;
    internal bool GameplayPrepared { get; private set; }
    private GameplaySceneGraph? _preparedGameplayScene;
    private AgesWorldData? _preparedWorld;
    private AgesRoomSessionResources? _preparedRoomResources;
    private RoomEntryEffectDatabase? _roomEntryEffects;

    internal RoomSession _rooms = null!;
    internal AgesRoomTransitionController _transitions = null!;
    internal RoomEntityManager _entities = null!;
    internal InteractionController _interactions = null!;
    private AgesNpcInteractionScripts _npcInteractionScripts = null!;
    internal RoomEventController _roomEvents = null!;
    internal PushBlockController _pushBlocks = null!;
    internal DungeonKeyDoorController _keyDoors = null!;
    internal OverworldKeyholeController _keyholes = null!;
    internal TerrainController _terrain = null!;
    internal CombatController _combat = null!;
    internal BombController _bomb => _playerWorld.Bombs;
    internal BraceletController _bracelet => _playerWorld.Bracelet;
    internal ShovelController _shovel => _playerWorld.Shovel;
    internal SeedSatchelController _seedSatchel => _playerWorld.Satchel;
    internal SeedShooterController _seedShooter = null!;
    internal HarpController _harp = null!;
    internal RingMenuController _ringMenu = null!;
    internal SecretEntryController _secretEntry = null!;
    internal SecretListController _secretList = null!;
    internal FrontendIntroController? _frontendIntro;
    internal FrontendIntroScreen? _frontendIntroScreen;
    internal RoomCollision _collision = null!;
    internal AgesPlayerWorld _playerWorld = null!;
    internal TreasureDatabase _treasures = null!;
    internal InventoryState _inventory = null!;
    internal OracleSaveData _saveData = null!;
    internal DeathRespawnPointController _deathRespawnPoints = null!;
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

    internal double _animationTicks;

    internal RoomView _roomView => _scene.RoomView;
    internal Player _player => _scene.Player;
    internal Camera2D _roomCamera => _scene.RoomCamera;
    internal Hud _hud => _scene.Hud;
    internal EndingScreen _endingScreen => _scene.InterfaceLayer.GetNode<EndingScreen>("EndingScreen");
    internal ColorRect _warpFade => _scene.WarpFade;
    internal DialogueBox _dialogue => _scene.Dialogue;
    internal MapScreen _mapScreen => _scene.MapScreen;
    internal InventoryScreen _inventoryScreen => _scene.InventoryScreen;
    internal RingMenuScreen _ringMenuScreen => _scene.RingMenuScreen;
    internal SaveQuitScreen _saveQuitScreen => _scene.SaveQuitScreen;
    internal DebugFlagScreen _debugFlagScreen => _scene.DebugFlagScreen;

    public bool RingMenuOpen => _ringMenu?.IsActive ?? false;

    private IEnumerable<bool> PrepareGameplayPresentation()
    {
        _preparedWorld = new AgesWorldData();
        yield return false;
        foreach (bool step in _preparedGameplayScene!.PreparePresentation())
            yield return step;
        _preparedGameplayScene.Player.PrepareTransformationPresentation();
        yield return false;
    }

    internal void ApplyHudPlacement(bool bottom) => _transitions.SetGameplayScreenTop(
        _scene.ApplyHudPlacement(bottom, _transitions.NativeRoomUsesFullScreen));

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
        _runtimeState = OracleRuntimeState.CreateInitialized(OracleGame.Ages);
        _treasures = TreasureDatabase.For(AgesGameContent.AssetRoot);
        yield return false;
        bool useDebugSavestate = debugSavestate is not null;
        bool useSavedSpawn = !useDebugSavestate && (forceDeathRespawn ||
            (!_launchOptions.HasWorldOverride && !_launchOptions.Has("--skip-menu") && (_persistSaveData ||
                initialRoomLoadKind == InitialRoomLoadKind.LinkSummonedCutscene)));
        if (useSavedSpawn)
        {
            GameplayInitialization.RestoreCheckpoint(_saveData, _runtimeState);
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
        _rooms = AgesRoomContent.CreateSession(
            startingGroup, startingRoom,
            () => (long)_animationTicks,
            () => _animationTicks = 0.0,
            _saveData,
            countAsRoomEntry: !useDebugSavestate && !prepare && initialRoomLoadKind != InitialRoomLoadKind.LinkSummonedCutscene,
            toggleState: () => _runtimeState.ReadWramByte(AgesGameContent.ToggleBlocksStateAddress),
            resources: _preparedRoomResources,
            world: _preparedWorld,
            runtimeState: _runtimeState);
        _preparedRoomResources = null;
        _preparedWorld = null;
        _inventory = new InventoryState(
            _treasures, _saveData, () => _rooms.CurrentDungeonIndex, _runtimeState);
        yield return false;
        PackedScene gameplayScene = _gameplaySceneResource.Load();
        _scene = _preparedGameplayScene ?? gameplayScene.Instantiate<GameplaySceneGraph>();
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
        foreach (bool step in CreateControllersSteps())
            yield return step;
        if (prepare)
        {
            foreach (bool step in _entities.PrepareResources())
                yield return step;
            // Stop here until CUTSCENE_PREGAME_INTRO completes. No room
            // objects exist and no original game update has run in this graph.
            yield return true;
        }

        _statusBar.InitializeNativeState();
        if (prepare && initialRoomLoadKind != InitialRoomLoadKind.LinkSummonedCutscene)
            _rooms.EnterPreparedRoom();

        Vector2 spawn = useDebugSavestate
            ? debugSavestate!.PlayerPosition
            : new Vector2(_saveData.RespawnX, _saveData.RespawnY);
        debugSavestate?.RestoreRoomParseState(_entities, _runtimeState);
        EnemyPlacementContext placementContext = useSavedSpawn
            ? EnemyPlacementContext.Warp(_rooms.CurrentRoom.GetPackedPosition(spawn))
            : EnemyPlacementContext.Unrestricted;
        if (useSavedSpawn && initialRoomLoadKind == InitialRoomLoadKind.Ordinary)
            _deathRespawnPoints.RecordRoomInitialization(spawn, _saveData.RespawnFacing);
        if (initialRoomLoadKind != InitialRoomLoadKind.LinkSummonedCutscene)
            _entities.LoadRoom(_rooms.ActiveGroup, _rooms.CurrentRoom, placementContext);
        TryDisplayEraInfoAfterInitialRoomLoad(initialRoomLoadKind);
        _roomView.SetRoom(_rooms.CurrentRoom.Texture);
        _roomView.UpdateUnderwaterWave(_rooms.CurrentRoom, _entities.FrameCounter);
        if (!useSavedSpawn && !useDebugSavestate)
            spawn = FindSpawn();
        _player.Initialize(_playerWorld, _inventory, spawn, _random);
        // Restore live bytes after Link initialization has cleared its transient parents.
        debugSavestate?.RestoreLiveState(_saveData, _runtimeState, _random, _entities);
        if (useSavedSpawn)
        {
            // loadingRoom/func_5c18 call setEnteredWarpPosition before
            // initializeRoom so a saved death checkpoint on a doorway or
            // stair cannot immediately activate that warp again.
            _transitions.DeactivateWarpAtPlayerPosition(_player);
        }
        if (useDebugSavestate)
        {
            _player.Face(debugSavestate!.PlayerFacing);
        }
        else if (useSavedSpawn)
        {
            GameplayInitialization.RestorePlayer(_saveData, _player,
                protect: initialRoomLoadKind == InitialRoomLoadKind.Ordinary);
        }
        if (!useSavedSpawn && !useDebugSavestate && initialRoomLoadKind == InitialRoomLoadKind.Ordinary)
            _deathRespawnPoints.RecordRoomInitialization();
        _transitions.ResetCamera();
        ApplyHudPlacement(_presentationSettings.HudBottom);
        if (prepare)
        {
            _scene.ProcessMode = ProcessModeEnum.Inherit;
            _scene.Visible = true;
            _scene.InterfaceLayer.Visible = true;
            _roomCamera.Enabled = true;
        }
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
        if (_loadingSavedGame && GameplayPrepared)
        {
            // Resource preparation has no original updates. Commit entry and
            // placement once, then let the next application update run play.
            while (_gameplayPreparation.MoveNext()) { }
            _gameplayPreparation.Dispose();
            _gameplayPreparation = null;
            GameplayPrepared = false;
            _loadingSavedGame = false;
        }
    }

    private GameplayObjectPass _objectPass;
    private bool _objectArrivalPaletteAdvanced;
    private bool _nativeResetRequested;
    private readonly record struct GameplayObjectPass(
        bool ArrivalOwnsUpdate, bool FinishingArrival, bool ToggleOwnedUpdate,
        bool RoomTransitionOwnedUpdate, bool ScrollOwnedUpdate, bool NativeCutsceneOwnedUpdate = false,bool StatusBarUpdatesDisabled = false, bool InitializingArrival = false, bool ToggleOwnsPostObjects = false);

    GameplayForegroundMode IGameplayForegroundPhases.BeginFrame(double delta)
    {
        bool finishingArrival = _newGameArrivalRoomVisitPending;
        bool initializingArrival = _newGameArrivalRoomPending &&
            _newGameArrivalFadeFrames == 0 && _newGameArrivalFrames == 0;
        if (UpdateNewGameArrival(delta))
            return GameplayForegroundMode.Handled;
        bool arrivalOwnsUpdate = initializingArrival || finishingArrival;
        _objectPass = new(arrivalOwnsUpdate, finishingArrival, false,
            false, false, InitializingArrival: initializingArrival);
        return GameplayForegroundMode.Normal;
    }


    bool IGameplayForegroundPhases.AdvanceAdditionalMenus(double delta)
    {
        bool secretOwnedFrame = _secretEntry.IsActive;
        _secretEntry.Update(delta);
        if (secretOwnedFrame || _secretEntry.IsActive) return true;
        bool secretListOwnedFrame = _secretList.IsActive;
        _secretList.Update(delta);
        if (secretListOwnedFrame || _secretList.IsActive) return true;
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
            return true;
        }
        return false;
    }

    bool IGameplayForegroundPhases.PrepareElectricShock(double delta)
    {
        bool arrivalOwnsUpdate = _objectPass.ArrivalOwnsUpdate;
        // cutscene02 advances before updateAllObjects. Its pending trigger
        // already blocks opening a menu between selection and state0.
        bool toggleOwnedUpdate = _entities.FloorToggle?.Active == true;
        _entities.FloorToggle?.AdvanceBeforeObjects();
        // bank1.s cutscene01 advances shock before updateMenus, including
        // its terminal update. Loading/warp and toggle cutscenes omit it.
        bool shock = !arrivalOwnsUpdate && _transitions.GameplayMenusAvailable && !toggleOwnedUpdate && !_roomEvents.NativeCutsceneActive;
        _objectPass = _objectPass with { ToggleOwnedUpdate = toggleOwnedUpdate };
        return shock;
    }

    GameplayMenuDispatch IGameplayForegroundPhases.PrepareMenus(double delta)
    {
        bool arrivalOwnsUpdate = _objectPass.ArrivalOwnsUpdate;
        bool toggleOwnedUpdate = _objectPass.ToggleOwnedUpdate;
        bool normalDispatch = !arrivalOwnsUpdate && !toggleOwnedUpdate;
        bool inventoryDispatch = normalDispatch && (_inventoryMenu.IsActive ||
            !_roomEvents.NormalMenuDispatchSuppressed && _transitions.GameplayMenusAvailable &&
            !DialogueOpen && !_player.IsDying && _harp.PlayingInstrument == 0);
        bool mapDispatch = normalDispatch && (_mapMenu.IsActive || !_roomEvents.NormalMenuDispatchSuppressed);
        bool warningEligible = normalDispatch && !_roomEvents.NormalMenuDispatchSuppressed && _transitions.GameplayMenusAvailable && !DialogueOpen &&
            !_player.IsDying && _harp.PlayingInstrument == 0 && !_gameplayMenus!.KidName.IsActive &&
            !_roomEvents.MenusDisabled && !_entities.PlayerMenusDisabled &&
            !_player.ElectricShockActive && !_player.GaleActive;
        return new(inventoryDispatch,mapDispatch,warningEligible);
    }

    GameplayObjectUpdateMode IGameplayForegroundPhases.BeforeObjects(double delta)
    {
        // Retain the caller's eligibility even when its handler selects a new
        // native stage or an interaction suspends this object pass.
        bool statusBarUpdatesDisabled=_roomEvents.StatusBarUpdatesDisabled;
        // The shared arrival palette runs before whichever main-thread
        // handler is selected, including a newly requested native cutscene.
        _objectArrivalPaletteAdvanced = !_roomEvents.DisablesLink && !_player.NativeCutsceneControlled &&
            !_transitions.NativePaletteFadeActive && _transitions.AdvanceStandingArrivalPaletteBeforeObjects(delta);
        _npcInteractionScripts.AdvanceFarorePaletteBeforeObjects();
        bool nativeCutsceneOwnedUpdate = _roomEvents.AdvanceNativeCutscenesBeforeObjects();
        if(_nativeResetRequested)return GameplayObjectUpdateMode.Handled;
        if (_roomEvents.SkipsObjectUpdate)
        {
            return GameplayObjectUpdateMode.Handled;
        }
        _objectPass = _objectPass with { NativeCutsceneOwnedUpdate = nativeCutsceneOwnedUpdate,
            StatusBarUpdatesDisabled = statusBarUpdatesDisabled };
        return GameplayObjectUpdateMode.Full;
    }

    bool IGameplayForegroundPhases.MainThreadRunning => !_roomEvents.ObjectUpdateSuspended;
    bool IGameplayForegroundPhases.ObjectUpdateSuspended => _roomEvents.ObjectUpdateSuspended;
    bool IGameplayForegroundPhases.StatusBarUpdatesAllowed => !_objectPass.StatusBarUpdatesDisabled;
    DialogueBox? IGameplayForegroundPhases.FrameDialogue => _dialogue;
    bool IGameplayForegroundPhases.PresentationAllowed => !_nativeResetRequested;
    void IGameplayForegroundPhases.ApplyPresentation(bool hudBottom) => ApplyHudPlacement(hudBottom);

    void IGameplayForegroundPhases.CompleteFrame()
    {
        if (!_nativeResetRequested) return;
        _nativeResetRequested = false;
        _roomEvents.Get<BlackTowerEndingEvent>().Cancel();
        ReleaseGameplayScene(); StartFrontend(startAtTitle:false);
    }

    bool IGameplayObjectPhases.PrepareObjectUpdate(double delta)
    {
        // A cutscene selected before this pass starts its palette next update.
        if (!_objectPass.NativeCutsceneOwnedUpdate && _transitions.AdvanceFadeOutWarpBeforeObjects(delta))
            return false;
        if (!_objectPass.ArrivalOwnsUpdate && !IsTransitioning && !_objectPass.ToggleOwnedUpdate && !_roomEvents.OwnsGameLogic)
        {
            _entities.CheckLoadPirateShip();
            (_pirateShipCourse ??= new PirateShipCourse()).Update(
                _saveData, _runtimeState, DialogueOpen, _harp.PlayingInstrument != 0);
        }
        return true;
    }

    int? IGameplayObjectPhases.SampleSimulatedInput() => _roomEvents.SampleSimulatedInput();


    void IGameplayObjectPhases.UpdateLink()
    {
        _objectPass = _objectPass with
        {
            ScrollOwnedUpdate = _transitions.ScrollActive,
            RoomTransitionOwnedUpdate = IsTransitioning
        };
        _transitions.BeginLinkUpdate();
        // warpTransitionB's final initialization returns before Link movement.
        if (!_objectPass.InitializingArrival)
            _player.AdvanceApplicationUpdate(advanceElectricShock: false);
    }

    void IGameplayObjectPhases.AfterLinkUpdate() => _roomEvents.UpdateSpecialObjects();

    bool IGameplayObjectPhases.PrepareEntityUpdate(double delta)
    {
        // A different main-thread cutscene does not replace a live Link's
        // state-$0a arrival. updateAllObjects still dispatches that object.
        bool linkArrival = _transitions.LinkArrivalActive && !_roomEvents.DisablesLink && !_player.NativeCutsceneControlled;
        if ((!_objectPass.NativeCutsceneOwnedUpdate || linkArrival) && !_objectArrivalPaletteAdvanced)
            _transitions.UpdateWarpAndEffects(delta);
        return !_transitions.TimeWarpActive;
    }

    bool IGameplayObjectPhases.UpdateRoomInteractions(double delta, bool resuming)
    {
        if (resuming) _roomEvents.ResumeObjectUpdate();
        else if (_transitions.TimeWarpActive)
        {
            _roomEvents.UpdateDuringTimeWarpFrame();
            return true;
        }
        else if (_entities.FloorToggle?.Frozen == true) return true;
        else _roomEvents.UpdateFrame();
        if (_roomEvents.ObjectUpdateSuspended) return false;
        _interactions.Update(delta, _player);
        return true;
    }

    void IGameplayObjectPhases.BeforeObjectTail(double delta)
    {
        GameplayObjectPass pass = _objectPass;
        _entities.SwitchHook?.UpdatePost(_player);
        _entities.Somaria?.UpdatePost(_player);
        _entities.Biggoron?.UpdatePost(_player);
        if (!pass.ArrivalOwnsUpdate && !pass.ToggleOwnedUpdate && !pass.RoomTransitionOwnedUpdate && !pass.NativeCutsceneOwnedUpdate)
            _roomView.UpdateUnderwaterWave(_rooms.CurrentRoom, _entities.FrameCounter);
        if (!pass.ArrivalOwnsUpdate && !IsTransitioning && !pass.NativeCutsceneOwnedUpdate && !_roomEvents.NativeCutsceneActive)
            _transitions.SampleRoomExit(_player);
        // bank0.updateAllObjects calls bank1.func_4000 after interactions,
        // before rider copies and updateItemsPost. Destination AI already froze.
        if (pass.ScrollOwnedUpdate) _transitions.UpdateScroll(delta);
    }

    bool IGameplayObjectPhases.TileGraphicsUpdateAllowed => !_transitions.TimeWarpActive;

    void IGameplayObjectPhases.AfterObjectTail(double delta)
    {
        _roomEvents.AdvanceNativeCutscenesAfterObjects();
    }

    void IGameplayForegroundPhases.AfterStatusBar(double delta)
    {
        UpdateAnimatedTiles(delta);
        GameplayObjectPass pass = _objectPass;
        if (!pass.ArrivalOwnsUpdate && !pass.ToggleOwnedUpdate && !pass.RoomTransitionOwnedUpdate &&
            !IsTransitioning && !_roomEvents.Active)
            _entities.FloorToggle?.CheckAfterObjects();
        _objectPass = pass with { ToggleOwnsPostObjects = pass.ToggleOwnedUpdate || _entities.FloorToggle?.Active == true };
    }

    bool IGameplayForegroundPhases.CutsceneRequestsAllowed =>
        !_objectPass.ArrivalOwnsUpdate && !_objectPass.RoomTransitionOwnedUpdate &&
        !_objectPass.ToggleOwnsPostObjects && !_objectPass.NativeCutsceneOwnedUpdate &&
        !IsTransitioning && !_roomEvents.NativeCutsceneActive;

    void IGameplayForegroundPhases.SelectRequestedCutscene(int index)
    {
        if (index != 0x13)
            throw new NotSupportedException($"bank1.setCutsceneIndexIfCutsceneTriggerSet: unbound Ages cutscene ${index:x2}.");
    }

    void IGameplayForegroundPhases.AfterCutsceneRequests(double delta)
    {
        if (!_objectPass.ScrollOwnedUpdate && !_objectPass.ArrivalOwnsUpdate &&
            !_objectPass.ToggleOwnsPostObjects && !_objectPass.NativeCutsceneOwnedUpdate && !_roomEvents.NativeCutsceneActive)
            UpdatePostObjectPlayerState(roomExitSampled: true);
    }

    bool IGameplayForegroundPhases.ObjectCollisionsAllowed =>
        !_objectPass.ArrivalOwnsUpdate && !IsTransitioning && !_objectPass.ToggleOwnsPostObjects &&
        !_objectPass.NativeCutsceneOwnedUpdate && !_roomEvents.NativeCutsceneActive;

    bool IGameplayForegroundPhases.RoomInitializationMayComplete =>
        !IsTransitioning && !_objectPass.NativeCutsceneOwnedUpdate && !_roomEvents.NativeCutsceneActive &&
        !_objectPass.ToggleOwnsPostObjects;

    void IGameplayForegroundPhases.AfterObjectCollisions(double delta)
    {
        GameplayObjectPass pass = _objectPass;
        bool finishingArrival = pass.FinishingArrival;
        bool roomTransitionOwnedUpdate = pass.RoomTransitionOwnedUpdate;
        if (roomTransitionOwnedUpdate && !IsTransitioning)
        {
            _entities.FloorToggle?.CompleteRoomInitialization();
            _roomView.UpdateUnderwaterWave(_rooms.CurrentRoom, _entities.FrameCounter);
        }
        if (finishingArrival)
        {
            // cutscene00 records the visit after updateAllObjects. The
            // arrival path never calls cutscene01's addToGashaMaturity.
            _newGameArrivalRoomVisitPending = false;
            _rooms.MarkCurrentRoomVisited();
        }
    }

    internal void UpdatePostObjectPlayerState(bool roomExitSampled = false)
    {
        // screenTransitionState2 runs after updateAllObjects in the original.
        // Interactions and moving platforms can move Link after his own state
        // handler, so check the final object-authored position as well. This is
        // required for side-view edge warps reached on a moving platform.
        if (!IsTransitioning)
        {
            bool nativeWarpStarted = _transitions.BeginRequestedNativeWarp(_player);
            bool tileWarpStarted = !nativeWarpStarted && _transitions.CheckRequestedTileWarp(_player);
            bool deepWaterStarted = !nativeWarpStarted && _transitions.BeginRequestedDeepWaterDive(_player);
            if (!nativeWarpStarted && !tileWarpStarted && !deepWaterStarted)
            {
                if (roomExitSampled) _transitions.CommitRoomExit(_player);
                else _transitions.CheckRoomExit(_player);
            }
        }

        // The camera likewise observes the final post-object Link position.
        // AgesRoomTransitionController.Update still advances active warps and
        // scrolls at its normal point; this inactive-only sample prevents a
        // platform displacement from appearing one application update late.
        if (!IsTransitioning)
            _transitions.UpdateCamera();
    }



    internal void UpdateAnimatedTiles(double delta)
    {
        // updateAnimations returns while wScrollMode bit 0 is clear. Both
        // scrolling and warp transitions keep that bit clear, so the original
        // animation counters and queued VRAM state remain completely frozen.
        if (IsTransitioning || (_runtimeState.ReadWramByte(AgesWramAddress.wScrollMode) & 1) == 0)
            return;

        _animationTicks += delta * 60.0;
        if (_rooms.CurrentRoom.UpdateAnimation((long)_animationTicks))
            _roomView.QueueRedraw();
    }

    private bool CanCaptureAgesDebugSavestate() =>
        _newGameArrivalFadeFrames <= 0 &&
        _newGameArrivalFrames <= 0 &&
        !IsTransitioning &&
        !DialogueOpen &&
        !MapMenuOpen &&
        !InventoryMenuOpen &&
        !RingMenuOpen &&
        !DebugFlagMenuOpen &&
        !_gameplayMenus!.KidName.IsActive &&
        !_gameplayPause.IsLeased &&
        !_player.GaleActive && !_player.IsDying &&
        !_player.IsUsingHarp &&
        !_roomEvents.Active &&
        !_roomEvents.MenusDisabled &&
        !_entities.PlayerMenusDisabled && !_player.ElectricShockActive;

    private void RestoreAgesDebugSavestate(DebugSavestateData debugSavestate)
    {
        ArgumentNullException.ThrowIfNull(debugSavestate);
        ReleaseGameplayScene();
        _sound.RestartSound();
        _random = new OracleRandom();
        InitializeGameplay(
            debugSavestate.CreateSaveData(),
            debugSavestate: debugSavestate);
    }

    private void ReturnToTitle()
    {
        ReleaseGameplayScene();
        StartFrontend(startAtTitle: true);
    }

    private void ReleaseGameplayScene(bool immediate = false)
    {
        _gameplaySession?.Dispose();
        _gameplaySession = null;
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
        _nativeResetRequested=false;
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
        _loadingSavedGame = false;
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

    private void InitializeRoomEntryEffects(int group, OracleRoomData room)
    {
        // loadScreenMusic precedes initializeRoom; roomSpecificCode runs
        // before object parsing. Background-only loads bypass this hook.
        _sound.SelectRoomMusic(group, room.Id, _saveData);
        (_roomEntryEffects ??= new()).Apply(group, room.Id, _saveData, _sound, _transitions.ScreenEntryDirection);
    }

    private void ApplyRoomMusic(int group, OracleRoomData room)
    {
        _deferredIntroMusicGroup = -1;
        _deferredIntroMusicRoom = -1;
        if (_transitions?.SuppressesDestinationMusic == true)
            return;

        bool playableIntro =
            _saveData.HasGlobalFlag(AgesGlobalFlag.PregameIntroDone) &&
            !_saveData.HasGlobalFlag(GlobalFlag.IntroDone);
        if (!playableIntro)
        {
            _sound.CheckPlayRoomMusic();
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


    internal bool Collides(Vector2 playerPosition) => _collision.Collides(playerPosition);
    internal bool TryInteract(Player player) => _interactions.TryInteract(player);
    internal TerrainInfo GetTerrainInfo(Vector2 position) => _terrain.GetTerrainInfo(position);
    internal ActiveTerrainInfo GetActiveTerrain(Vector2 position) => _terrain.GetActiveTerrain(position);
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
            _rooms.HasRoom(group + 2, room))
        {
            loaded = _rooms.Load(group + 2, room);
        }
        _roomView.SetRoom(loaded.Texture);
        _entities.LoadRoom(_rooms.ActiveGroup, loaded);
        _roomView.UpdateUnderwaterWave(loaded, _entities.FrameCounter);
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
    private void StartAgesDirect()
    {
        _selectedGame = OracleGame.Ages;
        _activeSaveSlot = 0;
        OracleSaveData save = _persistSaveData
            ? OracleSaveStore.LoadOrCreate(AgesGameContent.SavePath, OracleGame.Ages)
            : OracleSaveData.CreateStandardGame(OracleGame.Ages);
        InitializeGameplay(save);
    }

    private void AllocateAgesBootHierarchy() =>
        _preparedGameplayScene = _gameplaySceneResource.Load().Instantiate<GameplaySceneGraph>();

    private PreparedBootData PrepareAgesBootData(string[] images)
    {
        // These constructors parse immutable managed records only. Scene
        // nodes, images, texture uploads and gameplay stay on the main thread.
        _ = LinkItemDatabase.For(AgesGameContent.AssetRoot);
        _ = SideScrollPlayerDatabase.For(AgesGameContent.AssetRoot);
        _ = TopDownSwimmingDatabase.For(AgesGameContent.AssetRoot);
        _ = EnemyBehaviorTables.For(AgesGameContent.AssetRoot);
        _ = FrontendIntroDatabase.Shared;
        _ = NpcDatabase.For(AgesGameContent.AssetRoot);
        _ = EnemyDatabase.For(AgesGameContent.AssetRoot);
        TreasureDatabase.For(AgesGameContent.AssetRoot).PrepareSources();
        _ = WarpDatabase.For(AgesGameContent.AssetRoot);
        _ = RoomEventSourceData.Shared;
        AgesRoomEntityResources.PrepareSources();
        var resources = new AgesRoomSessionResources();
        return new PreparedBootData(images, () => _preparedRoomResources = resources);
    }

    private void QueueAgesBootPresentation()
    {
        _bootTasks.Enqueue(() => _gameplaySceneResource.Prepare().GetEnumerator());
        _bootTasks.Enqueue(() => PrepareGameplayPresentation().GetEnumerator());
        _bootTasks.Enqueue(() =>
        {
            _frontendIntroScreen = new FrontendIntroScreen
                { Name = "FrontendIntro", ZIndex = ScreenDrawPriority.FrontendZIndex, Visible = false, DeferPreparation = true };
            AddChild(_frontendIntroScreen);
            return _frontendIntroScreen.PrepareResources().GetEnumerator();
        });
    }

    private void QueueAgesNewGamePresentation()
    {
        _bootTasks.Enqueue(() =>
        {
            _newGameIntroScreen = new NewGameIntroScreen
                {
                    AssetRoot = AgesGameContent.AssetRoot, Name = "NewGameIntro", ZIndex = ScreenDrawPriority.FrontendZIndex, Visible = false, DeferPreparation = true };
            AddChild(_newGameIntroScreen);
            return _newGameIntroScreen.PrepareResources().GetEnumerator();
        });
    }
}
