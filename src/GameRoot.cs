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
    private LaunchOptions _launchOptions = null!;
    private RoomCollision _collision = null!;
    private PlayerWorld _playerWorld = null!;
    private GameSceneGraph _scene = null!;
    private TreasureDatabase _treasures = null!;
    private InventoryState _inventory = null!;
    private OracleSaveData _saveData = null!;
    private bool _persistSaveData;
    private int _activeSaveSlot;

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

        if (_launchOptions.ShowMainMenu)
        {
            _mainMenuScreen = new MainMenuScreen { Name = "MainMenu", ZIndex = 200 };
            AddChild(_mainMenuScreen);
            _mainMenu = new MainMenuController(_mainMenuScreen, StartSelectedFile);
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
        InitializeGameplay(save);
    }

    private void InitializeGameplay(OracleSaveData save)
    {
        _saveData = save;
        _saveData.Changed += PersistSaveData;
        _treasures = new TreasureDatabase();
        _inventory = new InventoryState(_treasures, _saveData);
        if (!_inventory.HasTreasure(TreasureDatabase.TreasureSword))
        {
            _inventory.GiveTreasure(_treasures.GetObject("TREASURE_OBJECT_SWORD_00"));
            _inventory.EquipA(InventoryState.ItemSword);
        }
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
        _scene = new GameSceneGraph(this);
        _hud.Initialize(_treasures, _inventory);
        _mapScreen.Initialize(_rooms);
        _inventoryScreen.Initialize(_treasures, _inventory);
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

        ScheduleRequestedValidation();
    }

    public override void _ExitTree()
    {
        PersistSaveData();
    }

    public override void _Process(double delta)
    {
        if (_mainMenu is not null)
        {
            _mainMenu.Update(delta);
            return;
        }
        if (_transitions is null)
            return;

        _debugFlagMenu.Update();
        if (_debugFlagMenu.IsActive)
            return;
        _inventoryMenu.Update();
        if (_inventoryMenu.IsActive)
            return;
        _mapMenu.Update(delta);
        if (_mapMenu.IsActive)
            return;
        _transitions.Update(delta);
        _entities.Update(delta, _player);
        _roomEvents.Update(delta);
        _interactions.Update(delta, _player);
        UpdateAnimatedTiles(delta);
        UpdateRoomDebugLabel();
        _debugWarps.Update();
    }

    private void CreateControllers()
    {
        _entities = new RoomEntityManager(this, new NpcDatabase(), new EnemyDatabase());
        _pushBlocks = new PushBlockController(
            _rooms, new PushableTileDatabase(), _roomView, () => (long)_animationTicks)
        {
            Name = "PushBlock"
        };
        AddChild(_pushBlocks);
        _collision = new RoomCollision(
            _rooms, _entities, _pushBlocks, point => _transitions.HasNeighborFor(point));
        _transitions = new RoomTransitionController(
            _rooms, new WarpDatabase(), _roomView, _player, _roomCamera,
            _warpFade, _hud, _dialogue, _entities, _collision.Collides);
        _entities.TimePortalEntered += portal =>
            _transitions.ApplyTimePortalWarp(_player, portal.Position);
        _interactions = new InteractionController(
            _rooms, _entities, new SignDatabase(), new ChestDatabase(), _treasures, _dialogue,
            this, _roomView, _transitions.WorldToScreen, () => (long)_animationTicks,
            _inventory);
        _roomEvents = new RoomEventController(
            _rooms, _entities, _transitions, _dialogue, _player, _roomView,
            _transitions.WorldToScreen, () => (long)_animationTicks);
        _bracelet = new BraceletController(
            _rooms, new BreakableTileDatabase(), _roomView, () => (long)_animationTicks);
        _terrain = new TerrainController(this, _rooms, _collision.Collides);
        _pushBlocks.EnteredHazard += (position, hazard) =>
        {
            if (hazard is OracleRoomData.HazardType.Water or OracleRoomData.HazardType.Lava)
                _terrain.SpawnDrowningSplash(position, hazard);
        };
        _combat = new CombatController(
            this, _rooms, _roomView, _entities, () => (long)_animationTicks);
        _playerWorld = new PlayerWorld(
            _transitions, _interactions, _collision, _pushBlocks, _terrain, _combat, _entities,
            _bracelet, _roomEvents,
            _inventory);
        _debugWarps = new DebugWarpController(
            _rooms, _player, LoadDebugRoom, FindSpawn, () => (long)_animationTicks,
            _interactions.ResetChestForTesting);
        _mapMenu = new MapMenuController(
            _mapScreen, _scene.MenuFade, _player, _roomDebug,
            () => !IsTransitioning && !DialogueOpen && !InventoryMenuOpen && !_roomEvents.Active,
            FastTravelFromMap);
        _inventoryMenu = new InventoryMenuController(
            _inventoryScreen, _player, _roomDebug,
            () => !IsTransitioning && !DialogueOpen && !MapMenuOpen && !_roomEvents.Active);
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

    private void PersistSaveData()
    {
        if (_persistSaveData && _saveData is not null)
            OracleSaveStore.SaveSlot(_activeSaveSlot, _saveData);
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
