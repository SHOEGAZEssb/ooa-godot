
namespace Oracles;

internal sealed class GameplayMenus : IDisposable
{
    private bool _disposed;
    private readonly GameplaySceneGraph _scene;
    private readonly OracleSaveData _save;
    private readonly RoomSession _rooms;
    private readonly StatusBarController _status;
    private readonly OracleSoundEngine _sound;
    private readonly Func<bool> _transitionActive;
    internal GameplayPauseController Pause { get; }
    internal OracleMenuLifecycle Lifecycle { get; }
    internal KidNameEntryController KidName { get; }
    internal DebugCollisionController Collision { get; } = new();
    internal MapMenuController Map { get; }
    internal InventoryMenuController Inventory { get; }
    internal DebugFlagMenuController Flags { get; }
    internal DebugObjectSpawnerController Spawner { get; }
    internal bool TransitionActive => !_disposed && _transitionActive();

    internal GameplayMenus(GameplaySceneGraph scene, RoomSession rooms, RoomEntityManager entities,
        InventoryState inventory, OracleRuntimeState memory, StatusBarController status,
        OracleSoundEngine sound, GameplayMenuBindings bindings, Func<bool> transitionActive)
    {
        if (scene.AssetRoot != rooms.AssetRoot || inventory.AssetRoot != rooms.AssetRoot ||
            !ReferenceEquals(memory,rooms.RuntimeState) || !ReferenceEquals(memory,inventory.RuntimeState))
            throw new ArgumentException("Gameplay menus require one scene, room, inventory and memory content pack.");
        _scene = scene; _rooms = rooms; _save = rooms.SaveData; _status = status; _sound = sound; _transitionActive = transitionActive;
        var treasures = TreasureDatabase.For(scene.AssetRoot);
        scene.MapScreen.Initialize(rooms, inventory, () => _save.ReadField("wPlaytimeCounter"));
        scene.InventoryScreen.Initialize(treasures, inventory,
            () => new(rooms.CurrentRoom.TilesetFlags, rooms.MinimapGroup,
                memory.ReadField("wLoadingRoomPack"), memory.ReadField("wRoomStateModifier")), scene.Hud);
        scene.DebugFlagScreen.Initialize(_save, new GlobalFlagDatabase(scene.AssetRoot), treasures, inventory);
        Pause = new(scene.Player, scene.RoomDebug);
        Lifecycle = new(scene.MenuFade, Pause, () =>
        {
            status.LoadCommonGraphics(rooms.CurrentRoom.TilesetFlags);
            scene.Player.ReassignNativeParentButtons();
        });
        KidName = new(scene.InterfaceLayer, Lifecycle, _save, memory, sound.PlaySound, sound.SetMusicVolume);
        rooms.RoomChanged += RoomChanged;
        Map = new(scene.MapScreen, scene.Dialogue, Lifecycle,
            () => bindings.CanOpen() && Lifecycle.IsIdle,
            () => _save.HasGlobalFlag(GlobalFlag.IntroDone), bindings.Travel, sound.PlaySound, sound.SetMusicVolume);
        Inventory = new(scene.InventoryScreen, scene.SaveQuitScreen, Lifecycle,
            () => _save.HasGlobalFlag(GlobalFlag.IntroDone),
            () => _save.HasGlobalFlag(GlobalFlag.IntroDone) && bindings.CanOpen() && Lifecycle.IsIdle,
            bindings.Save, bindings.Quit, sound.PlaySound, bindings.Restart,
            () => status.LoadCommonGraphics(rooms.CurrentRoom.TilesetFlags));
        Map.ConfigureSaveQuit(Inventory);
        Flags = new(scene.DebugFlagScreen, rooms, Pause,
            () => bindings.CanOpenDebug() && !Map.IsActive && !Inventory.IsActive, inventory, bindings.CompanionChanged);
        scene.DebugObjectSpawnerScreen.Initialize(scene.AssetRoot);
        Spawner = new(scene.DebugObjectSpawnerScreen,rooms,entities,scene.Player,Pause,
            () => bindings.CanOpenObjectSpawner() && !Map.IsActive && !Inventory.IsActive && !Flags.IsActive);
    }

    internal void BeginGameOver()
    {
        if (_disposed) return;
        _sound.RestartSound();
        _save.IncrementDeathCount();
        _sound.PlaySound(SoundId.MusGameOver);
        Inventory.BeginGameOver();
    }

    internal bool AdvanceNormal(double delta, bool updateInventory, bool updateMap, bool warningEligible)
    {
        if (_disposed) return false;
        bool kidNameOwnedFrame = KidName.IsActive;
        KidName.Update(delta);
        if (kidNameOwnedFrame || KidName.IsActive) return true;
        bool edge = Input.IsActionJustPressed("inventory") || Input.IsActionJustPressed("map");
        if (warningEligible && !Lifecycle.IsActive && (!edge || _save.HasGlobalFlag(GlobalFlag.IntroDone)))
            _status.UpdateLowHealthWarning();
        if (updateInventory) Inventory.Update(delta);
        if (!GodotObject.IsInstanceValid(_scene) || _scene.IsQueuedForDeletion()) return true;
        if (Inventory.IsActive)
        {
            if (_scene.InventoryScreen.Visible)
            {
                _status.Update(delta);
                _scene.InventoryScreen.QueueRedraw();
            }
            return true;
        }
        bool mapOwnedUpdate = Map.IsActive, transitioning = TransitionActive;
        if (updateMap) Map.Update(delta);
        if (mapOwnedUpdate && !transitioning && TransitionActive) return true;
        return Map.IsActive;
    }

    internal bool HandleInput(InputEvent input)
    {
        if (_disposed) return false;
        if (!GodotObject.IsInstanceValid(_scene) || !_scene.Visible) return false;
        if (input is not InputEventKey { PhysicalKeycode: Key.F6, Pressed: true, Echo: false,
            CtrlPressed: false, AltPressed: false, MetaPressed: false, ShiftPressed: false }) return false;
        if (!_scene.Player.IsDying)
        {
            _scene.Player.RefillHealth();
            _status.SynchronizeHealth();
        }
        return true;
    }

    private void RoomChanged(int group, OracleRoomData room) => KidName.Cancel();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _rooms.RoomChanged -= RoomChanged;
        KidName.Cancel();
        Spawner.Dispose();
        Flags.Dispose();
        Lifecycle.Dispose();
        Pause.Dispose();
    }
}
