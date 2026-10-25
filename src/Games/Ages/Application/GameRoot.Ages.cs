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
                frameCounter: () => save.ReadWramByte(AgesWramAddress.wPlaytimeCounter));
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

    private void StartAgesFromGameSelect()
    {
        GameSelectScreen screen = _gameSelect!;
        screen.AgesSelected -= StartAgesFromGameSelect;
        screen.End();
        _gameSelect = null;
        _applicationInput.Clear();
        BeginBootLoading(GetNode<BootLoadingScreen>("%BootLoading"));
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
        OracleGraphicsCache.ApplicationExitedTree();
        _gameplayScriptResources.Clear();
    }

    private IEnumerable<bool> CreateControllersSteps()
    {
        var timePortals = new TimePortalDatabase();
        var enemies = EnemyDatabase.Shared;
        yield return false;
        _entities = new RoomEntityManager(
            _scene.WorldRoot, NpcDatabase.Shared, enemies,
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
        _entities.DeathRespawnPointRequested += _deathRespawnPoints.RecordCurrentPoint;
        _transitions = new RoomTransitionController(
            _rooms, WarpDatabase.Shared, _roomView, _scene.RoomLoadReveal,
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
        _entities.NativeMusicSelectionRequested += _sound.SetNativeMusicSelection;
        _entities.NativeSoundRequested += _sound.PlayNativeSound;
        _entities.NativeActiveMusicWritten += _sound.SetNativeActiveMusic;
        _entities.NativeActiveMusicSource = () => _sound.NativeActiveMusic;
        _entities.NativeChannelVolumeWritten += _sound.SetNativeChannelVolume;
        _entities.NativeSoundAllocationAliasWritten += _sound.WriteNativeAllocationAlias;
        _entities.NativeSoundAllocationAliasSource = _sound.ReadNativeAllocationAlias;
        _entities.RoomMusicRequested += _sound.PlayRoomMusic;
        _entities.RoomInitializing += InitializeRoomEntryEffects;
        _entities.RoomInitialized += ApplyRoomMusic;
        _entities.ScreenShakeChanged += offset => _roomCamera.Offset = offset;
        yield return false;
        _entities.EnemyDefeated += _inventory.RecordEnemyKill;
        _entities.RoomTileChanged += _roomView.QueueRedraw;
        _roomEvents = new RoomEventController(
            _rooms, _entities, _transitions, _dialogue, _player, _roomView,
            _transitions.WorldToGameplayScreen, () => (long)_animationTicks,
            _scene.InterfaceLayer, _warpFade, _hud, _inventory, _treasures,
            _sound, _roomCamera, deferPreparation: true);
        _roomEvents.SetFileActions(SaveActiveFile,()=>_nativeResetRequested=true);
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
        _entities.NonInteractionObjectsDisabledSource = () => _roomEvents.FreezesNonInteractionObjects ||
            _interactions?.FreezesNonInteractionObjects == true;
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
            new BreakableTileDatabase(), _saveData, _sound,
            () => (long)_animationTicks);
        _bracelet = new BraceletController(
            _scene.WorldRoot, _rooms, new BreakableTileDatabase(), _roomView,
            _entities, _combat, _saveData, _sound.PlaySound,
            () => (long)_animationTicks, _collision.HasFullWall);
        _entities.Bracelet = _bracelet;
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
        _roomEvents.Get<VasuShopEvent>().SetRingMenuCancellation(_ringMenu.CancelOwner);
        _secretEntry = new SecretEntryController(
            _scene.InterfaceLayer, _menuLifecycle, _saveData, _inventory, _sound.PlaySound);
        _roomEvents.SetSecretMenuOpener(_secretEntry.Open);
        _interactions.SetFaroreSecretMenuOpener(_secretEntry.OpenAnyShortSecret);
        _secretList = new SecretListController(_scene.SecretListScreen, _menuLifecycle, _saveData, _sound.PlaySound);
        _interactions.SetSecretListOpener(_secretList.Open);
        _interactions.BindFaroreTransitions(_transitions);
        _roomEvents.Get<VasuShopEvent>().SetRingSecretMenuOpener(_secretEntry.OpenRingSecret);
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
}
