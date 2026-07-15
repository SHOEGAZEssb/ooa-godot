using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

public partial class GameRoot : Node2D
{
    // Kept as aliases for the validation partial; production transition state
    // is owned by RoomTransitionController.
    private const float WarpFadeFrames = RoomTransitionController.WarpFadeFrames;
    private const float WarpLeaveFrames = RoomTransitionController.WarpLeaveFrames;
    private const float WarpEnterFrames = RoomTransitionController.WarpEnterFrames;

    private RoomSession _rooms = null!;
    private OracleSoundEngine _sound = null!;
    private RoomTransitionController _transitions = null!;
    private RoomEntityManager _entities = null!;
    private InteractionController _interactions = null!;
    private RoomEventController _roomEvents = null!;
    private PushBlockController _pushBlocks = null!;
    private TerrainController _terrain = null!;
    private CombatController _combat = null!;
    private BraceletController _bracelet = null!;
    private DebugWarpController _debugWarps = null!;
    private MapMenuController _mapMenu = null!;
    private InventoryMenuController _inventoryMenu = null!;
    private DebugFlagMenuController _debugFlagMenu = null!;
    private MainMenuController? _mainMenu;
    private MainMenuScreen? _mainMenuScreen;
    private NewGameIntroController? _newGameIntro;
    private NewGameIntroScreen? _newGameIntroScreen;
    private LaunchOptions _launchOptions = null!;
    private RoomCollision _collision = null!;
    private PlayerWorld _playerWorld = null!;
    private GameSceneGraph _scene = null!;
    private TreasureDatabase _treasures = null!;
    private InventoryState _inventory = null!;
    private OracleSaveData _saveData = null!;
    private DeathRespawnPointController _deathRespawnPoints = null!;
    private bool _persistSaveData;
    private int _activeSaveSlot;
    private int _saveWriteRequests;
    private double _newGameArrivalTicks;
    private int _newGameArrivalFadeFrames;
    private int _newGameArrivalFrames;
    private int _newGameArrivalPhase;
    private int _newGameArrivalLastFrame;
    private int _deferredIntroMusicGroup = -1;
    private int _deferredIntroMusicRoom = -1;

    private double _animationTicks;

    private RoomView _roomView => _scene.RoomView;
    private Player _player => _scene.Player;
    private Camera2D _roomCamera => _scene.RoomCamera;
    private Hud _hud => _scene.Hud;
    private Label _roomDebug => _scene.RoomDebug;
    private ColorRect _warpFade => _scene.WarpFade;
    private DialogueBox _dialogue => _scene.Dialogue;
    private MapScreen _mapScreen => _scene.MapScreen;
    private InventoryScreen _inventoryScreen => _scene.InventoryScreen;
    private SaveQuitScreen _saveQuitScreen => _scene.SaveQuitScreen;
    private DebugFlagScreen _debugFlagScreen => _scene.DebugFlagScreen;

    public bool IsTransitioning => _transitions?.IsTransitioning ?? false;
    public bool DialogueOpen => _interactions?.DialogueOpen ?? false;
    public bool MapMenuOpen => _mapMenu?.IsActive ?? false;
    public bool InventoryMenuOpen => _inventoryMenu?.IsActive ?? false;
    public bool DebugFlagMenuOpen => _debugFlagMenu?.IsActive ?? false;

    // Compatibility accessors used only by GameRoot.Validation.cs.
    private OracleWorldData _world => _rooms.World;
    private OracleRoomData _currentRoom
    {
        get => _rooms.CurrentRoom;
        set => _rooms.SetLoadedRoom(_rooms.ActiveGroup, value);
    }
    private int _activeGroup
    {
        get => _rooms.ActiveGroup;
        set => _rooms.SetActiveGroup(value);
    }
    private List<NpcCharacter> _npcNodes => _entities.Entities<NpcCharacter>();
    private bool _scrollTransitionActive => _transitions.ScrollActive;
    private Vector2I _scrollTransitionDirection => _transitions.ScrollDirection;
    private float _scrollTransitionDistance => _transitions.ScrollDistance;
    private int _scrollTransitionFrames => _transitions.ScrollFrames;

    public override void _Ready()
    {
        _launchOptions = new LaunchOptions();
        _persistSaveData = !_launchOptions.Has("--validate");
        _sound = new OracleSoundEngine { Name = "SoundEngine" };
        AddChild(_sound);

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
        _treasures = new TreasureDatabase();
        _inventory = new InventoryState(_treasures, _saveData);
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
        _rooms.RoomChanged += ApplyRoomMusic;
        _scene = new GameSceneGraph(this);
        _dialogue.MessageSpeed = _saveData.TextSpeed;
        _hud.Initialize(_treasures, _inventory);
        _mapScreen.Initialize(_rooms, _inventory);
        _inventoryScreen.Initialize(_treasures, _inventory,
            () => _rooms.ActiveGroup is 1 or 3);
        _debugFlagScreen.Initialize(_saveData, new GlobalFlagDatabase());
        CreateControllers();

        _entities.LoadRoom(_rooms.ActiveGroup, _rooms.CurrentRoom);
        _roomView.SetRoom(_rooms.CurrentRoom.Texture);
        bool useSavedSpawn = !_launchOptions.HasWorldOverride && _persistSaveData;
        Vector2 spawn = useSavedSpawn
            ? new Vector2(_saveData.RespawnX, _saveData.RespawnY)
            : FindSpawn();
        _player.Initialize(_playerWorld, _inventory, spawn);
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

        ScheduleRequestedValidation();
    }

    public override void _ExitTree()
    {
        // The original engine does not write SRAM merely because play stops.
        // Unsaved changes remain only in the live WRAM-style save image.
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
        _deathRespawnPoints.Update();
        _entities.Update(delta, _player);
        _roomEvents.Update(delta);
        _interactions.Update(delta, _player);
        UpdateAnimatedTiles(delta);
        UpdateRoomDebugLabel();
        _debugWarps.Update();
    }

    private bool UpdateNewGameArrival(double delta)
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
            this, new NpcDatabase(), new EnemyDatabase(), _saveData);
        _pushBlocks = new PushBlockController(
            _rooms, new PushableTileDatabase(), _roomView, () => (long)_animationTicks)
        {
            Name = "PushBlock"
        };
        AddChild(_pushBlocks);
        _collision = new RoomCollision(
            _rooms, _entities, _pushBlocks, point => _transitions.HasNeighborFor(point));
        _deathRespawnPoints = new DeathRespawnPointController(_rooms, _player);
        _transitions = new RoomTransitionController(
            _rooms, new WarpDatabase(), _roomView, _player, _roomCamera,
            _warpFade, _hud, _dialogue, _entities, _collision.Collides, _deathRespawnPoints);
        _transitions.ScrollingTransitionFinished += _ => ApplyDeferredIntroMusic();
        _entities.TimePortalEntered += portal =>
            _transitions.ApplyTimePortalWarp(_player, portal.Position);
        _interactions = new InteractionController(
            _rooms, _entities, new SignDatabase(), new ChestDatabase(), _treasures, _dialogue,
            this, _roomView, _transitions.WorldToScreen, () => (long)_animationTicks,
            _inventory, _scene.InterfaceLayer, _sound.PlaySound);
        _roomEvents = new RoomEventController(
            _rooms, _entities, _transitions, _dialogue, _player, _roomView,
            _transitions.WorldToScreen, () => (long)_animationTicks,
            _scene.InterfaceLayer, _warpFade, _hud, _inventory, _treasures,
            _sound);
        _interactions.NpcInteractionOverride = _roomEvents.Nayru.TryInteractNpc;
        _bracelet = new BraceletController(
            _rooms, new BreakableTileDatabase(), _roomView, () => (long)_animationTicks);
        _terrain = new TerrainController(this, _rooms, _collision.Collides);
        _pushBlocks.EnteredHazard += (position, hazard) =>
        {
            if (hazard is OracleRoomData.HazardType.Water or OracleRoomData.HazardType.Lava)
                _terrain.SpawnDrowningSplash(position, hazard);
        };
        _combat = new CombatController(
            this, _rooms, _roomView, _entities, new BreakableTileDatabase(), _sound,
            () => (long)_animationTicks);
        _playerWorld = new PlayerWorld(
            _transitions, _interactions, _collision, _pushBlocks, _terrain, _combat, _entities,
            _bracelet, _roomEvents,
            _inventory, _sound);
        _debugWarps = new DebugWarpController(
            _rooms, _player, LoadDebugRoom, FindSpawn, () => (long)_animationTicks,
            _interactions.ResetChestForTesting);
        _mapMenu = new MapMenuController(
            _mapScreen, _scene.MenuFade, _dialogue, _player, _roomDebug,
            () => !IsTransitioning && !DialogueOpen && !InventoryMenuOpen && !_roomEvents.Active,
            () => _saveData.HasGlobalFlag(OracleSaveData.GlobalFlagIntroDone),
            FastTravelFromMap);
        _inventoryMenu = new InventoryMenuController(
            _inventoryScreen, _saveQuitScreen, _scene.MenuFade, _player, _roomDebug,
            () => _saveData.HasGlobalFlag(OracleSaveData.GlobalFlagIntroDone) &&
                !IsTransitioning && !DialogueOpen && !MapMenuOpen && !_roomEvents.Active,
            SaveActiveFile, ReturnToTitle);
        _debugFlagMenu = new DebugFlagMenuController(
            _debugFlagScreen, _rooms, _player, _roomDebug,
            () => !IsTransitioning && !DialogueOpen && !MapMenuOpen &&
                !InventoryMenuOpen && !_roomEvents.Active);
    }

    private void ScheduleRequestedValidation()
    {
        if (_launchOptions.Has("--validate"))
            CallDeferred(MethodName.ValidateAll);
    }

    private void UpdateAnimatedTiles(double delta)
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

    private void UpdateRoomDebugLabel()
    {
        string roomText = $"{_rooms.ActiveGroup:x1}:{_rooms.CurrentRoom.Id:x2}";
        if (_roomDebug.Text != roomText)
            _roomDebug.Text = roomText;
    }

    private void SyncHudToInventory()
    {
        if (_hud == null || _inventory == null)
            return;
        if (_hud.HealthQuarters == _inventory.HealthQuarters &&
            _hud.MaxHealthQuarters == _inventory.MaxHealthQuarters &&
            _hud.Rupees == _inventory.Rupees &&
            _hud.EquippedA == _inventory.EquippedA &&
            _hud.EquippedB == _inventory.EquippedB)
            return;
        _hud.HealthQuarters = _inventory.HealthQuarters;
        _hud.MaxHealthQuarters = _inventory.MaxHealthQuarters;
        _hud.Rupees = _inventory.Rupees;
        _hud.EquippedA = _inventory.EquippedA;
        _hud.EquippedB = _inventory.EquippedB;
        _hud.Refresh();
    }

    private void SaveActiveFile()
    {
        _saveWriteRequests++;
        if (_persistSaveData && _saveData is not null)
            OracleSaveStore.SaveSlot(_activeSaveSlot, _saveData);
    }

    private void ReturnToTitle()
    {
        if (_inventory is not null)
            _inventory.Changed -= SyncHudToInventory;

        foreach (Node child in GetChildren())
        {
            if (child != _sound)
                child.QueueFree();
        }

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

    private bool Collides(Vector2 playerPosition) => _collision.Collides(playerPosition);
    private bool TryInteract(Player player) => _interactions.TryInteract(player);
    private OracleRoomData.TerrainInfo GetTerrainInfo(Vector2 position) => _terrain.GetTerrainInfo(position);
    private ActiveTerrainInfo GetActiveTerrain(Vector2 position) => _terrain.GetActiveTerrain(position);
    private bool TryStartLedgeHop(Player player, Vector2 from, Vector2 movement) =>
        _terrain.TryStartLedgeHop(player, from, movement);
    private bool CheckTileWarp(Player player) => _transitions.CheckTileWarp(player);
    private void CheckRoomExit(Player player) => _transitions.CheckRoomExit(player);

    private Vector2 FindSpawn()
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

    private void LoadDebugRoom(int group, int room)
    {
        _dialogue.Close();
        _transitions.ClearDeactivatedWarp();
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
    private void WarpToSignTest() => _debugWarps.WarpToSign();
    private void WarpToAnimationTest() => _debugWarps.WarpToAnimation();
    private void WarpToBushTest() => _debugWarps.WarpToBush();
    private void WarpToHouseTest() => _debugWarps.WarpToHouse();
    private void WarpToNpcTest() => _debugWarps.WarpToNpc();
    private void WarpToChestTest() => _debugWarps.WarpToChest();
    private void ClearDeactivatedWarp() => _transitions.ClearDeactivatedWarp();
    private void RefreshRoomObjects() => _entities.LoadRoom(_rooms.ActiveGroup, _rooms.CurrentRoom);
    private void ClearRoomObjects() => _entities.Clear();
    private bool TryGetNeighborId(Vector2I direction, out int id) => _rooms.TryGetNeighbor(direction, out id);
    private void UpdateRoomCamera() => _transitions.UpdateCamera();
    private Vector2 WorldToScreen(Vector2 position) => _transitions.WorldToScreen(position);
    private void UpdateRoomWarpTransition(double delta) => _transitions.UpdateWarp(delta);
    private void UpdateScrollingTransition(double delta) => _transitions.UpdateScroll(delta);
}
