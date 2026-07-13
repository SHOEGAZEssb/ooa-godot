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
    private PushBlockController _pushBlocks = null!;
    private TerrainController _terrain = null!;
    private CombatController _combat = null!;
    private DebugWarpController _debugWarps = null!;
    private LaunchOptions _launchOptions = null!;
    private RoomCollision _collision = null!;
    private PlayerWorld _playerWorld = null!;
    private GameSceneGraph _scene = null!;

    private double _animationTicks;

    private RoomView _roomView => _scene.RoomView;
    private Player _player => _scene.Player;
    private Camera2D _roomCamera => _scene.RoomCamera;
    private Hud _hud => _scene.Hud;
    private Label _roomDebug => _scene.RoomDebug;
    private ColorRect _warpFade => _scene.WarpFade;
    private DialogueBox _dialogue => _scene.Dialogue;

    public bool IsTransitioning => _transitions?.IsTransitioning ?? false;
    public bool DialogueOpen => _interactions?.DialogueOpen ?? false;

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
    private List<NpcCharacter> _npcNodes => _entities.Npcs;
    private bool _scrollTransitionActive => _transitions.ScrollActive;
    private Vector2I _scrollTransitionDirection => _transitions.ScrollDirection;
    private float _scrollTransitionDistance => _transitions.ScrollDistance;
    private int _scrollTransitionFrames => _transitions.ScrollFrames;

    public override void _Ready()
    {
        _launchOptions = new LaunchOptions();
        _rooms = new RoomSession(
            _launchOptions.StartingGroup, _launchOptions.StartingRoom,
            () => (long)_animationTicks,
            () => _animationTicks = 0.0);
        if (_launchOptions.Has("--validate-world"))
            _rooms.World.ValidateRepresentativeRooms();

        _scene = new GameSceneGraph(this);
        CreateControllers();

        _entities.LoadRoom(_rooms.ActiveGroup, _rooms.CurrentRoom);
        _roomView.SetRoom(_rooms.CurrentRoom.Texture);
        _player.Initialize(_playerWorld, FindSpawn());
        _player.HealthChanged += SyncHudToPlayer;
        _player.RupeesChanged += SyncHudToPlayer;
        SyncHudToPlayer();
        _transitions.UpdateCamera();

        ScheduleRequestedValidation();
    }

    public override void _Process(double delta)
    {
        _transitions.Update(delta);
        _entities.Update(delta, _player.Position);
        _interactions.Update(delta, _player);
        UpdateAnimatedTiles(delta);
        UpdateRoomDebugLabel();
        _debugWarps.Update();
    }

    private void CreateControllers()
    {
        _entities = new RoomEntityManager(this, new NpcDatabase());
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
        _interactions = new InteractionController(
            _rooms, _entities, new SignDatabase(), new ChestDatabase(), _dialogue,
            this, _roomView, _transitions.WorldToScreen, () => (long)_animationTicks,
            amount => _player.AddRupees(amount));
        _terrain = new TerrainController(this, _rooms, _collision.Collides);
        _pushBlocks.EnteredHazard += (position, hazard) =>
        {
            if (hazard is OracleRoomData.HazardType.Water or OracleRoomData.HazardType.Lava)
                _terrain.SpawnDrowningSplash(position, hazard);
        };
        _combat = new CombatController(this, _rooms, _roomView, () => (long)_animationTicks);
        _playerWorld = new PlayerWorld(
            _transitions, _interactions, _collision, _pushBlocks, _terrain, _combat);
        _debugWarps = new DebugWarpController(
            _rooms, _player, LoadDebugRoom, FindSpawn, () => (long)_animationTicks,
            _interactions.ResetChestForTesting);
    }

    private void ScheduleRequestedValidation()
    {
        if (_launchOptions.Has("--validate-transition")) CallDeferred(MethodName.ValidateStartupTransition);
        if (_launchOptions.Has("--validate-symmetry-transition")) CallDeferred(MethodName.ValidateSymmetryTransition);
        if (_launchOptions.Has("--validate-signs")) CallDeferred(MethodName.ValidateSigns);
        if (_launchOptions.Has("--validate-npcs")) CallDeferred(MethodName.ValidateNpcs);
        if (_launchOptions.Has("--validate-animations")) CallDeferred(MethodName.ValidateAnimations);
        if (_launchOptions.Has("--validate-sword-bush")) CallDeferred(MethodName.ValidateSwordBush);
        if (_launchOptions.Has("--validate-house-warp")) CallDeferred(MethodName.ValidateHouseWarp);
        if (_launchOptions.Has("--validate-cave-warps")) CallDeferred(MethodName.ValidateCaveWarps);
        if (_launchOptions.Has("--validate-terrain")) CallDeferred(MethodName.ValidateTerrain);
        if (_launchOptions.Has("--validate-health")) CallDeferred(MethodName.ValidateHealth);
        if (_launchOptions.Has("--validate-chests")) CallDeferred(MethodName.ValidateChests);
        if (_launchOptions.Has("--validate-push-blocks")) CallDeferred(MethodName.ValidatePushBlocks);
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

    private void SyncHudToPlayer()
    {
        if (_hud == null || _player == null)
            return;
        if (_hud.HealthQuarters == _player.HealthQuarters &&
            _hud.MaxHealthQuarters == _player.MaxHealthQuarters &&
            _hud.Rupees == _player.Rupees)
            return;
        _hud.HealthQuarters = _player.HealthQuarters;
        _hud.MaxHealthQuarters = _player.MaxHealthQuarters;
        _hud.Rupees = _player.Rupees;
        _hud.Refresh();
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
