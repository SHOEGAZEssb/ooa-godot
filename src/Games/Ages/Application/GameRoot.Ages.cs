using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// Ages frontend, arrival and controller bindings share the application owner;
// fixed updates, input, RNG and session state stay authoritative in GameRoot.
public partial class GameRoot
{
    private void StartSelectedFile(int slot, OracleSaveData save)
    {
        _activeSaveSlot = slot;
        // initializeGame owns the live file before either the pregame cutscene
        // or room construction. Resource preparation must not delay this.
        _saveData = save;
        _runtimeState = OracleRuntimeState.CreateInitialized(save.Game);
        _saveData.ResetHealthIfDepleted();
        _saveData.AdvancePlaytime();
        _mainMenuScreen?.QueueFree();
        _mainMenuScreen = null;
        _mainMenu = null;

        // bank0.s:mainThreadStart restarts the whole driver, including SFX
        // and queued requests, before the loaded game's initial music cue.
        _sound.RestartSound();

        if (!save.HasGlobalFlag(AgesGlobalFlag.PregameIntroDone))
        {
            _gameplaySceneResource.BeginPreload();
            if (_newGameIntroScreen is null)
            {
                _newGameIntroScreen = new NewGameIntroScreen
                {
                    AssetRoot = AgesGameContent.AssetRoot,
                    Name = "NewGameIntro",
                    ZIndex = ScreenDrawPriority.FrontendZIndex,
                    Visible = false
                };
                AddChild(_newGameIntroScreen);
            }
            _newGameIntroScreen.Visible = true;
            BeginPregame(_newGameIntroScreen,() => CompleteNewGameIntro(save));
            // Health restoration has already run; dormant scene construction
            // may now prepare the initialized live file without advancing it.
            if (save.ReadWramByte(AgesWramAddress.wLinkHealth) is > 0 and < 0x80)
                _gameplayPreparation = InitializeGameplaySteps(save,
                    initialRoomLoadKind: InitialRoomLoadKind.LinkSummonedCutscene,
                    prepare: true).GetEnumerator();
            return;
        }

        _loadingSavedGame = true;
        _gameplayPreparation = InitializeGameplaySteps(save,
            forceDeathRespawn: _persistSaveData && !_launchOptions.HasWorldOverride && !_launchOptions.Has("--skip-menu"),
            prepare: true).GetEnumerator();
    }

    internal void StartFrontend(bool startAtTitle)
    {
        _selectedGame = OracleGame.Ages;
        _mainMenu = null;
        _frontendIntroScreen = new FrontendIntroScreen
        {
            Name = "FrontendIntro",
            ZIndex = ScreenDrawPriority.FrontendZIndex
        };
        _mainMenuScreen = new MainMenuScreen
        {
            AssetRoot = AgesGameContent.AssetRoot,
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

    private void StartAgesFromGameSelect()
    {
        GameSelectScreen screen = _gameSelect!;
        screen.AgesSelected -= StartAgesFromGameSelect;
        screen.End();
        _gameSelect = null;
        _applicationInput.Clear();
        BeginBootLoading(GetNode<BootLoadingScreen>("%BootLoading"), OracleGame.Ages);
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

        // bank1.linkSummonedCutscene state 0 enables the boundary pass when
        // loading arrival graphics, before the fade and deferred initializeRoom.
        _runtimeState.SetWramByte(AgesWramAddress.wScrollMode, 1);
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
                _saveData.SetGlobalFlag(AgesGlobalFlag.PregameIntroDone);
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

    private void PlayNayruApproachMusic()
    {
        _sound.PlayMusicIfChanged(SoundId.MusNayru);
        _sound.SetMusicVolume(2);
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

    private IEnumerable<bool> CreateControllersSteps()
    {
        var timePortals = new TimePortalDatabase();
        var enemies = EnemyDatabase.For(AgesGameContent.AssetRoot);
        yield return false;
        _entities = AgesRoomEntityFactory.CreateManager(
            _scene.WorldRoot, NpcDatabase.For(AgesGameContent.AssetRoot), enemies,
            new ItemDropDatabase(AgesGameContent.AssetRoot), timePortals, _random, _saveData,
            runtimeState: _runtimeState,
            inventory: _inventory,
            animationTick: () => (long)_animationTicks,
            treasures: _treasures,
            rooms: _rooms);
        BindGameplaySession(new GameplaySession(_scene,_rooms,_entities,_inventory,_sound,
            new GameplayMenuBindings(
                () => _transitions.GameplayMenusAvailable && !DialogueOpen &&
                    !_player.GaleActive && !_player.IsDying && _harp.PlayingInstrument == 0 && !_roomEvents.Active &&
                    !_roomEvents.MenusDisabled && !_entities.PlayerMenusDisabled && !_player.ElectricShockActive,
                () => !IsTransitioning && !DialogueOpen && !_player.GaleActive && !_player.IsDying &&
                    !_roomEvents.Active && !_roomEvents.MenusDisabled,
                FastTravelFromMap, SaveActiveFile, ReturnToTitle,
                RestartGameplayAfterDeath, RefreshDebugCompanionLayout,
                () => !IsTransitioning && !DialogueOpen && !MapMenuOpen &&
                    !InventoryMenuOpen && !_player.GaleActive && !_player.IsDying &&
                    !_player.IsUsingHarp && !_player.ElectricShockActive &&
                    !_roomEvents.Active && !_roomEvents.MenusDisabled &&
                    !_entities.PlayerMenusDisabled && !_gameplayMenus!.KidName.IsActive),
            this,this,() => _presentationSettings.HudBottom),
            () => ReleaseGameplayScene(), save => InitializeGameplay(save, forceDeathRespawn:true),
            CanCaptureAgesDebugSavestate,() => _animationTicks,RestoreAgesDebugSavestate);
        yield return false;
        (_pushBlocks,_keyDoors) = _gameplaySession!.BindTileContacts(() => (long)_animationTicks,
            _entities.PushBlockPermittedByColoredCube,_entities.RequestSomariaPush);
        _keyholes = _gameplaySession.Keyholes;
        _collision = new RoomCollision(
            _rooms, _entities, _pushBlocks, point => _transitions.HasNeighborFor(point));
        _deathRespawnPoints = new DeathRespawnPointController(
            _rooms, _player);
        _entities.DeathRespawnPointRequested += _deathRespawnPoints.RecordCurrentPoint;
        _transitions = new AgesRoomTransitionController(
            _rooms, WarpDatabase.For(AgesGameContent.AssetRoot), _roomView, _scene.RoomLoadReveal,
            _player, _roomCamera,
            _warpFade, _hud, _dialogue, _entities,
            _deathRespawnPoints, _sound, timePortals);
        _gameplaySession!.BindTransitions(_transitions);
        _entities.WorldToScreen = _transitions.WorldToGameplayScreen;
        _dialogue.SetGameplayCameraYProvider(
            () => -_transitions.WorldToGameplayScreen(Vector2.Zero).Y);
        _transitions.ScrollingTransitionFinished += _ => ApplyDeferredIntroMusic();
        _entities.TimePortalEntered += portal =>
            _transitions.ApplyTimePortalWarp(_player, portal);
        _entities.RoomWarpRequested += warp =>
            _transitions.ApplyWarp(_player, warp);
        _entities.RoomInitializing += InitializeRoomEntryEffects;
        _entities.RoomInitialized += ApplyRoomMusic;
        _entities.ScreenShakeChanged += offset => _roomCamera.Offset = offset;
        yield return false;
        _entities.RoomTileChanged += _roomView.QueueRedraw;
        _roomEvents = new RoomEventController(
            _rooms, _entities, _transitions, _dialogue, _player, _roomView,
            _transitions.WorldToGameplayScreen, () => (long)_animationTicks,
            _scene.InterfaceLayer, _warpFade, _hud, _inventory, _treasures,
            _sound, _roomCamera, deferPreparation: true);
        _roomEvents.SetFileActions(SaveActiveFile,()=>_nativeResetRequested=true);
        foreach (bool step in _roomEvents.PrepareResources())
            yield return step;
        _npcInteractionScripts = new AgesNpcInteractionScripts(
            _rooms, _entities, _dialogue, _treasures, _entities.FamilyStateResolver,
            _inventory, () => _statusBar.DisplayedRupees == _inventory.Rupees,
            () => _roomView.QueueRedraw());
        _interactions = _gameplaySession!.BindInteractions(_npcInteractionScripts, () => (long)_animationTicks,
            () => _statusBar.DisplayedRupees == _inventory.Rupees &&
                _statusBar.DisplayedHealth == _inventory.HealthQuarters,
            _roomEvents.InteractionHandlers);
        yield return false;
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
        _entities.CameraUpdatesDisabledSource = () => _roomEvents.CameraUpdatesDisabled;
        _entities.PartPhaseUpdate = _roomEvents.UpdatePartPhase;
        _entities.InteractionPhaseUpdate = _roomEvents.UpdateInteractionPhase;
        _entities.InitializedObjectsDisabledSource = () => _transitions.AwaitingLinkWarpState;
        _entities.TextAllowsScriptSource = () => _dialogue.AllowsNativeScriptCommands ||
            (_runtimeState.ReadWramByte(AgesWramAddress.wTextIsActive) & 0x80) != 0;
        _entities.FloorToggle = new DungeonToggleController(_rooms, _runtimeState, _entities,
            _sound.PlaySound, () => (long)_animationTicks);
        _combat = new CombatController(
            _rooms, _roomView, _entities,
            new BreakableTileDatabase(AgesGameContent.AssetRoot), _saveData, _sound,
            () => (long)_animationTicks);
        var seeds = new SeedSatchelDatabase(_rooms.AssetRoot);
        _seedShooter = new SeedShooterController(_inventory, _entities, seeds, _rooms, _sound.PlaySound);
        _entities.SwitchHook = new SwitchHookController(_scene.WorldRoot, _rooms, _entities, _sound.PlaySound,
            () => (long)_animationTicks, _combat.SpawnBreakEffect, () => _pushBlocks.Active);
        _entities.Somaria = new SomariaController(_scene.WorldRoot,_rooms,_entities,_sound.PlaySound);
        _entities.Biggoron = new BiggoronSwordController(_scene.WorldRoot,_entities,_combat,_sound.PlaySound);
        _harp = new HarpController(
            _rooms, _entities, _transitions, _interactions, _sound);
        yield return false;
        _playerWorld = new AgesPlayerWorld(
            _transitions, _interactions, _collision, _keyholes,
            _combat, _entities,
            _seedShooter, _harp, _roomEvents,
            _inventory, _sound, () => _debugCollision.CollisionsDisabled, _rooms, _scene);
        _terrain = _playerWorld.Terrain;
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
        _ringMenu = new RingMenuController(
            _ringMenuScreen, _dialogue, _menuLifecycle, _inventory, _saveData,
            _treasures, _roomEvents.Get<VasuShopEvent>().Database, _sound.PlaySound,
            () => _statusBar.LoadCommonGraphics(_rooms.CurrentRoom.TilesetFlags));
        _roomEvents.SetRingMenuOpener(_ringMenu.Open);
        _roomEvents.Get<VasuShopEvent>().SetRingMenuCancellation(_ringMenu.CancelOwner);
        _secretEntry = new SecretEntryController(
            _scene.InterfaceLayer, _menuLifecycle, _saveData, _inventory, _sound.PlaySound);
        _roomEvents.SetSecretMenuOpener(_secretEntry.Open);
        _npcInteractionScripts.OpenFaroreSecretMenu = _secretEntry.OpenAnyShortSecret;
        _secretList = new SecretListController(_scene.SecretListScreen, _menuLifecycle, _saveData, _sound.PlaySound);
        _npcInteractionScripts.OpenSecretList = _secretList.Open;
        _npcInteractionScripts.OpenKidName = _gameplayMenus!.KidName.Open;
        _npcInteractionScripts.FaroreTransitions = _transitions;
        _roomEvents.Get<VasuShopEvent>().SetRingSecretMenuOpener(_secretEntry.OpenRingSecret);
    }
}
