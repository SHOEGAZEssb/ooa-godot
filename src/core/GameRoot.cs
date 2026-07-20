using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public partial class GameRoot : Node2D
{
    // Internal aliases and state form the narrow host surface used by the
    // friend validation assembly. Production transition state remains owned
    // by RoomTransitionController.
    internal const float WarpFadeFrames = RoomTransitionController.WarpFadeFrames;
    internal const float WarpLeaveFrames = RoomTransitionController.WarpLeaveFrames;
    internal const float WarpEnterFrames = RoomTransitionController.WarpEnterFrames;

    internal RoomSession _rooms = null!;
    internal OracleSoundEngine _sound = null!;
    internal RoomTransitionController _transitions = null!;
    internal RoomEntityManager _entities = null!;
    internal InteractionController _interactions = null!;
    internal RoomEventController _roomEvents = null!;
    internal PushBlockController _pushBlocks = null!;
    internal DungeonKeyDoorController _keyDoors = null!;
    internal TerrainController _terrain = null!;
    internal CombatController _combat = null!;
    private BraceletController _bracelet = null!;
    internal ShovelController _shovel = null!;
    internal SeedSatchelController _seedSatchel = null!;
    private DebugWarpController _debugWarps = null!;
    internal DebugCollisionController _debugCollision = null!;
    internal MapMenuController _mapMenu = null!;
    internal InventoryMenuController _inventoryMenu = null!;
    internal DebugFlagMenuController _debugFlagMenu = null!;
    internal GameplayPauseController _gameplayPause = null!;
    internal OracleMenuLifecycle _menuLifecycle = null!;
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
    internal OracleRandom _random = null!;
    internal DeathRespawnPointController _deathRespawnPoints = null!;
    private bool _persistSaveData;
    private int _activeSaveSlot;
    internal int _saveWriteRequests;
    internal double _newGameArrivalTicks;
    internal int _newGameArrivalFadeFrames;
    internal int _newGameArrivalFrames;
    internal int _newGameArrivalPhase;
    internal int _newGameArrivalLastFrame;
    private int _deferredIntroMusicGroup = -1;
    private int _deferredIntroMusicRoom = -1;

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
    internal SaveQuitScreen _saveQuitScreen => _scene.SaveQuitScreen;
    internal DebugFlagScreen _debugFlagScreen => _scene.DebugFlagScreen;

    public bool IsTransitioning => _transitions?.IsTransitioning ?? false;
    public bool DialogueOpen => _interactions?.DialogueOpen ?? false;
    public bool MapMenuOpen => _mapMenu?.IsActive ?? false;
    public bool InventoryMenuOpen => _inventoryMenu?.IsActive ?? false;
    public bool DebugFlagMenuOpen => _debugFlagMenu?.IsActive ?? false;

    // Compatibility accessors used only by the friend validation assembly.
    internal OracleWorldData _world => _rooms.World;
    internal OracleRoomData _currentRoom
    {
        get => _rooms.CurrentRoom;
        set => _rooms.SetLoadedRoom(_rooms.ActiveGroup, value);
    }
    internal int _activeGroup
    {
        get => _rooms.ActiveGroup;
        set => _rooms.SetActiveGroup(value);
    }
    internal List<NpcCharacter> _npcNodes => _entities.Entities<NpcCharacter>();
    internal bool _scrollTransitionActive => _transitions.ScrollActive;
    internal Vector2I _scrollTransitionDirection => _transitions.ScrollDirection;
    internal float _scrollTransitionDistance => _transitions.ScrollDistance;
    internal int _scrollTransitionFrames => _transitions.ScrollFrames;

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
        _sound = GetNodeOrNull<OracleSoundEngine>("%SoundEngine") ??
            GetNodeOrNull<OracleSoundEngine>("SoundEngine") ??
            throw new InvalidOperationException(
                "The game scene is missing its required SoundEngine node.");

        if (_launchOptions.ShowMainMenu)
        {
            _mainMenuScreen = new MainMenuScreen { Name = "MainMenu", ZIndex = 200 };
            AddChild(_mainMenuScreen);
            _mainMenu = new MainMenuController(
                _mainMenuScreen, StartSelectedFile, playSound: _sound.PlaySound);
            return;
        }

        _activeSaveSlot = 0;
        OracleSaveData save = _persistSaveData
            ? OracleSaveStore.LoadOrCreate()
            : OracleSaveData.CreateStandardGame();
        InitializeGameplay(save);
    }

    private void StartSelectedFile(int slot, OracleSaveData save)
    {
        _activeSaveSlot = slot;
        _mainMenuScreen?.QueueFree();
        _mainMenuScreen = null;
        _mainMenu = null;

        // The playable intro begins with no active room music. This also
        // prevents MUS_FILE_SELECT from leaking into an interrupted pre-intro
        // file, even though the original does not expose saving in this span.
        if (!save.HasGlobalFlag(OracleSaveData.GlobalFlagIntroDone))
            _sound.PlaySound(OracleSoundEngine.SndCtrlStopMusic);

        if (!save.HasGlobalFlag(OracleSaveData.GlobalFlagPregameIntroDone))
        {
            _newGameIntroScreen = new NewGameIntroScreen
            {
                Name = "NewGameIntro",
                ZIndex = 200
            };
            AddChild(_newGameIntroScreen);
            _newGameIntroScreen.Dialogue.MessageSpeed = save.TextSpeed;
            _newGameIntro = new NewGameIntroController(
                _newGameIntroScreen,
                () => CompleteNewGameIntro(save),
                _sound);
            return;
        }

        InitializeGameplay(save);
    }

    private void CompleteNewGameIntro(OracleSaveData save)
    {
        NewGameIntroDatabase.NewGameIntroRecord record =
            _newGameIntro!.Record;
        save.SetGlobalFlag(record.LinkSummonedFlag);
        save.SetGlobalFlag(record.PregameIntroDoneFlag);
        _newGameIntroScreen?.QueueFree();
        _newGameIntroScreen = null;
        _newGameIntro = null;
        InitializeGameplay(save);

        // linkSummonedCutscene state 0 starts SND_WARP_START when it loads the
        // arrival room and initializes the divisor-2 white fade/wave.
        _sound.PlaySound(OracleSoundEngine.SndWarpStart);
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

    private void InitializeGameplay(OracleSaveData save)
    {
        _saveData = save;
        _random = new OracleRandom();
        _treasures = new TreasureDatabase();
        int startingGroup = _launchOptions.HasWorldOverride || !_persistSaveData
            ? _launchOptions.StartingGroup
            : _saveData.RespawnGroup;
        int startingRoom = _launchOptions.HasWorldOverride || !_persistSaveData
            ? _launchOptions.StartingRoom
            : _saveData.RespawnRoom;
        _rooms = new RoomSession(
            startingGroup, startingRoom,
            () => (long)_animationTicks,
            () => _animationTicks = 0.0,
            _saveData);
        _inventory = new InventoryState(
            _treasures, _saveData, () => _rooms.CurrentDungeonIndex);
        _rooms.RoomChanged += ApplyRoomMusic;
        PackedScene gameplayScene = ResourceLoader.Load<PackedScene>(
            GameSceneGraph.ScenePath, string.Empty, ResourceLoader.CacheMode.Reuse) ??
            throw new InvalidOperationException(
                $"Could not load gameplay scene {GameSceneGraph.ScenePath}.");
        _scene = gameplayScene.Instantiate<GameSceneGraph>();
        AddChild(_scene);
        _dialogue.SetSoundPlayer(_sound.PlaySound);
        _dialogue.MessageSpeed = _saveData.TextSpeed;
        _hud.Initialize(_treasures, _inventory);
        _rooms.RoomChanged += SyncHudToRoom;
        _statusBar = new StatusBarController(_inventory, _hud, _sound.PlaySound);
        _mapScreen.Initialize(_rooms, _inventory);
        _inventoryScreen.Initialize(_treasures, _inventory,
            () => _rooms.ActiveGroup is 1 or 3);
        _debugFlagScreen.Initialize(
            _saveData, new GlobalFlagDatabase(), _treasures, _inventory);
        CreateControllers();

        bool useSavedSpawn = !_launchOptions.HasWorldOverride && _persistSaveData;
        Vector2 spawn = new(_saveData.RespawnX, _saveData.RespawnY);
        EnemyPlacementContext placementContext = useSavedSpawn
            ? EnemyPlacementContext.Warp(_rooms.CurrentRoom.GetPackedPosition(spawn))
            : EnemyPlacementContext.Unrestricted;
        _entities.LoadRoom(_rooms.ActiveGroup, _rooms.CurrentRoom, placementContext);
        _roomView.SetRoom(_rooms.CurrentRoom.Texture);
        if (!useSavedSpawn)
            spawn = FindSpawn();
        _player.Initialize(_playerWorld, _inventory, spawn, _random);
        if (useSavedSpawn)
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
        _transitions.UpdateCamera();
        ApplyRoomMusic(_rooms.ActiveGroup, _rooms.CurrentRoom);
    }

    public override void _ExitTree()
    {
        // The original engine does not write SRAM merely because play stops.
        // Unsaved changes remain only in the live WRAM-style save image.
        OracleGraphicsCache.Shutdown();
    }

    public override void _Process(double delta)
    {
        if (_mainMenu is not null)
        {
            _mainMenu.Update(delta);
            return;
        }
        if (_newGameIntro is not null)
        {
            _newGameIntro.Update(delta);
            return;
        }
        if (_transitions is null)
            return;
        if (UpdateNewGameArrival(delta))
            return;

        _debugCollision.Update();
        _debugFlagMenu.Update();
        if (_debugFlagMenu.IsActive)
            return;
        _inventoryMenu.Update(delta);
        if (_mainMenu is not null)
            return;
        if (_inventoryMenu.IsActive)
            return;
        _mapMenu.Update(delta);
        if (_mapMenu.IsActive)
            return;
        // MENU_KIDNAME is a gameplay-owned file-menu screen in the original.
        // Keep servicing its controller while freezing the room beneath it.
        if (_interactions.GameplayMenuActive)
        {
            _interactions.Update(delta, _player);
            return;
        }
        _transitions.Update(delta);
        if (!_transitions.TimeWarpActive)
        {
            _deathRespawnPoints.Update();
            _entities.Update(delta, _player);
        }
        // A portal can begin the time warp from the contact pass above. The
        // original DISABLE_ALL_BUT_INTERACTIONS|DISABLE_LINK state freezes
        // ordinary room scripts from the following handler onward; the
        // transition controller advances only its imported warp interactions.
        if (_transitions.TimeWarpActive)
        {
            _roomEvents.UpdateDuringTimeWarp(delta);
        }
        else
        {
            _roomEvents.Update(delta);
            _interactions.Update(delta, _player);
        }
        _statusBar.Update(delta);
        UpdateAnimatedTiles(delta);
        UpdateRoomDebugLabel();
        _debugWarps.Update();
    }

    internal bool UpdateNewGameArrival(double delta)
    {
        if (_newGameArrivalFadeFrames <= 0 && _newGameArrivalFrames <= 0)
            return false;

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
                    _transitions.WorldToScreen(_player.Position).Y);
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
        return false;
    }

    private void CreateControllers()
    {
        _entities = new RoomEntityManager(
            _scene.WorldRoot, new NpcDatabase(), new EnemyDatabase(),
            new ItemDropDatabase(), new TimePortalDatabase(), _random, _saveData,
            animationTick: () => (long)_animationTicks);
        _pushBlocks = new PushBlockController(
            _rooms, new PushableTileDatabase(), _roomView,
            () => (long)_animationTicks, _sound.PlaySound)
        {
            Name = "PushBlock"
        };
        _scene.WorldRoot.AddChild(_pushBlocks);
        _keyDoors = new DungeonKeyDoorController(
            _rooms, _inventory, _entities, _treasures,
            () => (long)_animationTicks, _sound.PlaySound)
        {
            Name = "DungeonKeyDoors"
        };
        _scene.WorldRoot.AddChild(_keyDoors);
        _collision = new RoomCollision(
            _rooms, _entities, _pushBlocks, point => _transitions.HasNeighborFor(point));
        _deathRespawnPoints = new DeathRespawnPointController(_rooms, _player);
        _transitions = new RoomTransitionController(
            _rooms, new WarpDatabase(), _roomView, _player, _roomCamera,
            _warpFade, _hud, _dialogue, _entities, _collision.Collides,
            _deathRespawnPoints, _sound);
        _entities.WorldToScreen = _transitions.WorldToScreen;
        _transitions.ScrollingTransitionFinished += _ => ApplyDeferredIntroMusic();
        _entities.TimePortalEntered += portal =>
            _transitions.ApplyTimePortalWarp(_player, portal.Position);
        _entities.SoundRequested += _sound.PlaySound;
        _entities.RoomTileChanged += _roomView.QueueRedraw;
        _interactions = new InteractionController(
            _rooms, _entities, new SignDatabase(), new ChestDatabase(), _treasures, _dialogue,
            _scene.WorldRoot, _roomView, _transitions.WorldToScreen, () => (long)_animationTicks,
            _inventory, _scene.InterfaceLayer, _sound.PlaySound);
        _keyDoors.MessageRequested += message =>
            _interactions.ShowRoomInteractionMessage(message, _player);
        _entities.DungeonEntranceTriggered += (_, message) =>
        {
            _interactions.ShowRoomInteractionMessage(message, _player);
            _deathRespawnPoints.RecordCurrentPoint();
        };
        _roomEvents = new RoomEventController(
            _rooms, _entities, _transitions, _dialogue, _player, _roomView,
            _transitions.WorldToScreen, () => (long)_animationTicks,
            _scene.InterfaceLayer, _warpFade, _hud, _inventory, _treasures,
            _sound, _roomCamera);
        _interactions.NpcInteractionOverride = _roomEvents.TryInteractNpc;
        _bracelet = new BraceletController(
            _rooms, new BreakableTileDatabase(), _roomView, () => (long)_animationTicks);
        _shovel = new ShovelController(
            _rooms, new BreakableTileDatabase(), _roomView, _entities, _saveData,
            _sound.PlaySound, () => (long)_animationTicks);
        _seedSatchel = new SeedSatchelController(
            _inventory, _entities, new SeedSatchelDatabase(), _rooms);
        _terrain = new TerrainController(
            _scene.WorldRoot, _rooms, _collision.Collides, _sound.PlaySound);
        _entities.ItemDropEnteredHazard += _terrain.SpawnSplash;
        _pushBlocks.EnteredHazard += (position, hazard) =>
        {
            if (hazard is OracleRoomData.HazardType.Water or OracleRoomData.HazardType.Lava)
                _terrain.SpawnSplash(position, hazard);
            else if (hazard == OracleRoomData.HazardType.Hole)
                _entities.Spawn<FallingDownHoleEffect>(
                    new FallingDownHoleSpawn(position));
        };
        _combat = new CombatController(
            _scene.WorldRoot, _rooms, _roomView, _entities, new BreakableTileDatabase(), _sound,
            () => (long)_animationTicks);
        _debugCollision = new DebugCollisionController();
        _playerWorld = new PlayerWorld(
            _transitions, _interactions, _collision, _pushBlocks, _keyDoors,
            _terrain, _combat, _entities,
            _bracelet, _shovel, _seedSatchel, _roomEvents,
            _inventory, _sound, () => _debugCollision.CollisionsDisabled);
        _debugWarps = new DebugWarpController(
            _rooms, _player, LoadDebugRoom, FindSpawn, () => (long)_animationTicks,
            _interactions.ResetChestForTesting);
        _gameplayPause = new GameplayPauseController(_player, _roomDebug);
        _menuLifecycle = new OracleMenuLifecycle(_scene.MenuFade, _gameplayPause);
        _mapMenu = new MapMenuController(
            _mapScreen, _dialogue, _menuLifecycle,
            () => !IsTransitioning && !DialogueOpen && !InventoryMenuOpen && !_roomEvents.Active,
            () => _saveData.HasGlobalFlag(OracleSaveData.GlobalFlagIntroDone),
            FastTravelFromMap, _sound.PlaySound);
        _inventoryMenu = new InventoryMenuController(
            _inventoryScreen, _saveQuitScreen, _menuLifecycle,
            () => _saveData.HasGlobalFlag(OracleSaveData.GlobalFlagIntroDone),
            () => _saveData.HasGlobalFlag(OracleSaveData.GlobalFlagIntroDone) &&
                !IsTransitioning && !DialogueOpen && !MapMenuOpen && !_roomEvents.Active,
            SaveActiveFile, ReturnToTitle, _sound.PlaySound);
        _debugFlagMenu = new DebugFlagMenuController(
            _debugFlagScreen, _rooms, _gameplayPause,
            () => !IsTransitioning && !DialogueOpen && !MapMenuOpen &&
                !InventoryMenuOpen && !_roomEvents.Active);
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
        if (_roomDebug.Text != roomText)
            _roomDebug.Text = roomText;
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

    private OracleSaveStore.SaveResult SaveActiveFile()
    {
        _saveWriteRequests++;
        if (_persistSaveData && _saveData is not null)
            return OracleSaveStore.SaveSlot(_activeSaveSlot, _saveData);
        return OracleSaveStore.SaveResult.Succeeded;
    }

    private void ReturnToTitle()
    {
        if (_inventory is not null)
            _inventory.Changed -= SyncHudToInventory;
        _statusBar?.Dispose();
        if (_rooms is not null)
        {
            _rooms.RoomChanged -= ApplyRoomMusic;
            _rooms.RoomChanged -= SyncHudToRoom;
        }

        // gameplay.tscn owns every persistent and transient gameplay node.
        // Freeing this one root leaves the application-owned sound engine in
        // place for the title screen and the next selected file.
        _scene.QueueFree();

        _mainMenuScreen = new MainMenuScreen { Name = "MainMenu", ZIndex = 200 };
        AddChild(_mainMenuScreen);
        _mainMenu = new MainMenuController(
            _mainMenuScreen, StartSelectedFile, playSound: _sound.PlaySound);
    }

    private void ApplyRoomMusic(int group, OracleRoomData room)
    {
        _deferredIntroMusicGroup = -1;
        _deferredIntroMusicRoom = -1;

        bool playableIntro =
            _saveData.HasGlobalFlag(OracleSaveData.GlobalFlagPregameIntroDone) &&
            !_saveData.HasGlobalFlag(OracleSaveData.GlobalFlagIntroDone);
        if (!playableIntro)
        {
            _sound.PlayRoomMusic(group, room.Id);
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
        _sound.PlayMusicIfChanged(OracleSoundEngine.MusNayru);
        _sound.SetMusicVolume(2);
    }

    internal bool Collides(Vector2 playerPosition) => _collision.Collides(playerPosition);
    internal bool TryInteract(Player player) => _interactions.TryInteract(player);
    internal OracleRoomData.TerrainInfo GetTerrainInfo(Vector2 position) => _terrain.GetTerrainInfo(position);
    internal ActiveTerrainInfo GetActiveTerrain(Vector2 position) => _terrain.GetActiveTerrain(position);
    internal bool TryStartLedgeHop(Player player, Vector2 from, Vector2 movement) =>
        _terrain.TryStartLedgeHop(player, from, movement);
    internal bool CheckTileWarp(Player player) => _transitions.CheckTileWarp(player);
    internal void CheckRoomExit(Player player)
    {
        if (!_roomEvents.ScreenTransitionsDisabled)
            _transitions.CheckRoomExit(player);
    }

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

    internal void LoadDebugRoom(int group, int room)
    {
        _dialogue.Close();
        _transitions.ClearDeactivatedWarp();
        _entities.ClearRecentEnemyDefeats();
        OracleRoomData loaded = _rooms.Load(group, room);
        _roomView.SetRoom(loaded.Texture);
        _entities.LoadRoom(group, loaded);
        _hud.Refresh();
        _transitions.UpdateCamera();
    }

    private void FastTravelFromMap(int group, int room)
    {
        LoadDebugRoom(group, room);
        _player.WarpTo(FindSpawn());
        _player.Face(Vector2I.Down);
    }

    // Validation compatibility wrappers.
    internal void WarpToSignTest() => _debugWarps.WarpToSign();
    private void WarpToAnimationTest() => _debugWarps.WarpToAnimation();
    internal void WarpToBushTest() => _debugWarps.WarpToBush();
    internal void WarpToHouseTest() => _debugWarps.WarpToHouse();
    internal void WarpToNpcTest() => _debugWarps.WarpToNpc();
    internal void WarpToChestTest() => _debugWarps.WarpToChest();
    internal void ClearDeactivatedWarp() => _transitions.ClearDeactivatedWarp();
    internal void RefreshRoomObjects() => _entities.LoadRoom(_rooms.ActiveGroup, _rooms.CurrentRoom);
    private void ClearRoomObjects() => _entities.Clear();
    private bool TryGetNeighborId(Vector2I direction, out int id) => _rooms.TryGetNeighbor(direction, out id);
    internal void UpdateRoomCamera() => _transitions.UpdateCamera();
    internal Vector2 WorldToScreen(Vector2 position) => _transitions.WorldToScreen(position);
    internal void UpdateRoomWarpTransition(double delta)
    {
        // Validation often advances several nominal frames at once. The live
        // time-warp controller deliberately processes at most one vblank step
        // per rendered call, so preserve that call boundary in bulk checks.
        const double frame = 1.0 / 60.0;
        while (_transitions.TimeWarpActive && delta > frame + 0.000001)
        {
            _transitions.UpdateWarp(frame);
            if (_transitions.TimeWarpActive)
                _roomEvents.UpdateDuringTimeWarp(frame);
            else
                _roomEvents.Update(frame);
            delta -= frame;
        }
        if (delta > 0.000001)
        {
            _transitions.UpdateWarp(delta);
            if (_transitions.TimeWarpActive)
                _roomEvents.UpdateDuringTimeWarp(delta);
            else
                _roomEvents.Update(delta);
        }
    }
    internal void UpdateScrollingTransition(double delta) => _transitions.UpdateScroll(delta);
}
