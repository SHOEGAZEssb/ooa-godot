using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

internal sealed class GameplaySession : IDisposable
{
    private readonly GameplaySceneGraph _scene;
    private readonly OracleSaveData _save;
    private readonly OracleRuntimeState _memory;
    private readonly StatusBarController _status;
    private readonly RoomEntityManager _entities;
    private readonly RoomSession _rooms;
    private readonly InventoryState _inventory;
    private readonly IGameplayForegroundPhases _phases;
    private readonly Func<bool> _hudBottom;
    private readonly GameplayObjectDispatcher _objects;
    private readonly Action _releaseEntitySound;
    private RoomTransitionController? _transitions;
    internal RoomTransitionController Transitions => _transitions ??
        throw new InvalidOperationException("Gameplay requires its bound shared transition owner.");
    internal GameplayMenus Menus { get; }
    internal GroundTreasureController GroundTreasures { get; }
    private InteractionController? _interactions;
    internal InteractionController Interactions => _interactions ??
        throw new InvalidOperationException("Gameplay requires its bound common interaction service.");

    internal InteractionController BindInteractions(INpcInteractionScripts? scripts, Func<long> animationTick,
        Func<bool>? displayedCountersCaughtUp = null, IReadOnlyList<NpcInteractionHandler>? handlers = null)
    {
        if (!Active || _interactions is not null)
            throw new InvalidOperationException("Gameplay binds one interaction service for its active session.");
        return _interactions = new(_rooms,_entities,new SignDatabase(_rooms.AssetRoot),new ChestDatabase(_rooms.AssetRoot),
            _entities.Treasures,_scene.Dialogue,_scene.WorldRoot,_scene.RoomView,
            point => _entities.WorldToScreen(point),animationTick,_inventory,scripts,GroundTreasures,
            _soundPlayer,displayedCountersCaughtUp,handlers);
    }
    private readonly Action<int> _soundPlayer;
    private readonly OracleSoundEngine _sound;
    private OverworldKeyholeController? _keyholes;
    internal OverworldKeyholeController Keyholes => _keyholes ??
        throw new InvalidOperationException("Gameplay requires its bound overworld-keyhole owner.");
    private (PushBlockController Blocks,DungeonKeyDoorController Keys)? _tileOwners;
    internal (PushBlockController Blocks,DungeonKeyDoorController Keys) BindTileContacts(Func<long> animationTick,
        Func<byte,bool>? blockPermitted = null,Action<int>? pushSomaria = null)
    {
        if (!Active || _tileOwners is not null)
            throw new InvalidOperationException("Gameplay binds one native tile-contact owner set.");
        var blocks = new PushBlockController(_rooms,new PushableTileDatabase(_rooms.AssetRoot),_scene.RoomView,
            animationTick,_soundPlayer,blockPermitted,pushSomaria,
            braceletLevelSource:() => _inventory.BraceletLevel,movementMemory:_memory)
        {
            Name="PushBlock",BraceletObtainedSource=() => _inventory.HasTreasure(TreasureId.Bracelet)
        };
        _scene.WorldRoot.AddChild(blocks);blocks.SetPhysicsProcess(false);
        _entities.ReservedPushBlock=blocks;
        var keys = new DungeonKeyDoorController(_rooms,_inventory,_entities,_entities.Treasures,animationTick,_soundPlayer)
            { Name="DungeonKeyDoors" };
        _scene.WorldRoot.AddChild(keys);keys.SetPhysicsProcess(false);
        blocks.MessageRequested += TileInfoRequested;
        keys.MessageRequested += TileInfoRequested;
        _keyholes = new(_rooms,_inventory,_entities,new OverworldKeyholeDatabase(_rooms.AssetRoot),_sound)
            { Name="OverworldKeyholes" };
        _scene.WorldRoot.AddChild(_keyholes);_keyholes.SetPhysicsProcess(false);
        _keyholes.MessageRequested += TileInfoRequested;
        blocks.EnteredHazard += PushBlockEnteredHazard;
        _tileOwners=(blocks,keys);
        return (blocks,keys);
    }
    private void TileInfoRequested(string message) => Interactions.ShowRoomInteractionMessage(message,_scene.Player);
    private void PushBlockEnteredHazard(Vector2 position,HazardType hazard)
    {
        if (hazard is HazardType.Water or HazardType.Lava) _entities.TryCreateSplash(position,hazard);
        else if (hazard == HazardType.Hole && _entities.InteractionSlotAvailable)
        {
            _entities.Spawn<FallingDownHoleEffect>(new FallingDownHoleSpawn(position));
            _entities.OnObjectFellInHole(ObjectFellInHoleKind.PushBlock);
        }
    }
    internal RoomSession Rooms => _rooms;
    internal StatusBarController StatusBar => _status;
    internal bool Active { get; private set; } = true;
    internal bool CanCaptureDebugState => Active && _entities.CanCaptureDebugState;

    internal void ValidateDebugState(DebugSavestateData state) => state.ValidateReconstruction(_entities);
    internal void RestoreDebugPlayerBindings()
    {
        _entities.RestoreDebugPlayerBindings(_scene.Player);
        _status.RestoreDisplay();
    }

    internal DebugSavestateData CaptureDebugState(double animationTicks)
    {
        if (!Active) throw new InvalidOperationException("Cannot capture a retired gameplay session.");
        return DebugSavestateData.Capture(_rooms,_scene.Player,_save,_memory,_entities.Random,_entities,animationTicks);
    }

    internal GameplaySession(GameplaySceneGraph scene, RoomSession rooms, RoomEntityManager entities,
        InventoryState inventory, OracleSoundEngine sound, GameplayMenuBindings bindings,
        IGameplayForegroundPhases phases, IGameplayObjectPhases objects, Func<bool> hudBottom)
    {
        OracleRuntimeState memory = rooms.RuntimeState;
        if (scene.AssetRoot != rooms.AssetRoot || rooms.AssetRoot != entities.AssetRoot ||
            !ReferenceEquals(entities.RuntimeState,memory) ||
            !ReferenceEquals(inventory.RuntimeState,memory) ||
            !ReferenceEquals(entities.SaveData,rooms.SaveData) || !ReferenceEquals(inventory.SaveData,rooms.SaveData) ||
            !ReferenceEquals(entities.Inventory,inventory))
            throw new ArgumentException("Gameplay composition requires one selected scene, room, inventory, entity, memory and menu owner.");
        _scene = scene; _save = rooms.SaveData; _memory = memory;
        _sound = sound; _soundPlayer = sound.PlaySound;
        _phases = phases; _hudBottom = hudBottom; _entities = entities; _rooms = rooms; _inventory = inventory;
        scene.Player.ApplicationUpdateOwned = true;
        scene.Dialogue.ApplicationUpdateOwned = true;
        scene.Dialogue.SetSoundPlayer(sound.PlaySound);
        scene.Dialogue.SetLinkNameProvider(() => _save.LinkName);
        scene.Dialogue.SetBackgroundPaletteState(rooms.BackgroundPalettes);
        scene.Dialogue.SetAlternatePalettePriorityHandler(scene.Player.SetAlternateTextboxPalettePriority);
        scene.Dialogue.MessageSpeed = _save.TextSpeed;
        scene.Hud.DungeonIndex = rooms.CurrentDungeonIndex; scene.Hud.TilesetFlags = rooms.CurrentRoom.TilesetFlags;
        scene.Hud.Initialize(TreasureDatabase.For(scene.AssetRoot),inventory);
        _status = new(inventory,scene.Hud,sound.PlaySound,() => memory.FrameCounter);
        scene.RingMenuScreen.Initialize(inventory);
        _releaseEntitySound = GameplayEntitySound.Bind(entities,sound);
        Menus = new(scene,rooms,entities,inventory,memory,_status,sound,bindings,() => Transitions.IsTransitioning);
        GroundTreasures = new(rooms,entities,inventory,scene.Dialogue,
            position => Transitions.WorldToGameplayScreen(position),sound.PlaySound);
        scene.Player.GameOverRequested += Menus.BeginGameOver;
        _objects = new(scene.Player,entities,rooms,objects,CompleteObjects);
        inventory.Changed += SynchronizeHud;
        entities.EnemyDefeated += inventory.RecordEnemyKill;
        rooms.RoomChanged += RoomChanged;
        entities.RoomInitialized += RoomGraphicsLoaded;
        entities.ScreenWaveRequested += ApplyScreenWave;
        SynchronizeHud();
        scene.TreeExiting += Dispose;
    }

    internal void BindTransitions(RoomTransitionController transitions)
    {
        if (!Active || _transitions is not null || !transitions.UsesOwners(_rooms,_entities))
            throw new ArgumentException("Gameplay requires one transition owner bound to its authoritative room and entities.");
        _transitions = transitions;
        _entities.RoomWarpRequested += ApplyRequestedWarp;
        transitions.CommonGraphicsReloadRequested += ReloadCommonGraphics;
        Menus.Map.ConfigureGale(_rooms,target => transitions.ApplyWarp(_scene.Player,new Warp(
            _rooms.ActiveGroup,_rooms.CurrentRoom.Id,0,0,0,_rooms.ActiveGroup,target.Room,target.Position,0,WarpDestinationTransition.Fall)),
            () => _scene.Player.ReturnFromGale((int)transitions.WorldToGameplayScreen(_scene.Player.Position).Y));
        _entities.GaleMenuRequested += Menus.Map.OpenGale;
    }
    private void ApplyRequestedWarp(Warp warp)=>Transitions.ApplyWarp(_scene.Player,warp);

    internal void Advance()
    {
        if (!Active) return;
        _ = Transitions;
        if (_phases.MainThreadRunning && !Menus.Inventory.GameOver)
            MainThreadClock.Advance(_save,_memory);
        try { AdvanceForeground(); }
        finally
        {
            if (Active && _phases.PresentationAllowed && GodotObject.IsInstanceValid(_scene) && !_scene.IsQueuedForDeletion())
            {
                DialogueBox? dialogue = _phases.FrameDialogue;
                if (GodotObject.IsInstanceValid(dialogue)) dialogue!.AdvanceApplicationUpdate();
                _entities.ProjectNativeNpcPositions();
                // Preloaded scenes can retain enabled cameras; foreground session ownership selects the active one.
                if (_scene.RoomCamera.Enabled && !_scene.RoomCamera.IsCurrent()) _scene.RoomCamera.MakeCurrent();
                _phases.ApplyPresentation(_hudBottom());
            }
        }
        if (Active) _phases.CompleteFrame();
    }

    private void AdvanceForeground()
    {
        const double delta = ApplicationFixedUpdateScheduler.UpdateDelta;
        if (_phases.ObjectUpdateSuspended)
        {
            _objects.Resume(delta);
            return;
        }
        _objects.Cancel();
        GameplayForegroundMode mode = _phases.BeginFrame(delta);
        if (mode == GameplayForegroundMode.Handled) return;
        if (mode == GameplayForegroundMode.Normal)
        {
            Menus.Collision.Update();
            if (Menus.Spawner.Update()) return;
            Menus.Flags.Update();
            if (Menus.Flags.IsActive || _phases.AdvanceAdditionalMenus(delta)) return;
            if (_phases.PrepareElectricShock(delta)) _scene.Player.AdvanceElectricShock();
            if (Menus.Lifecycle.IsActive) Transitions.HandOffArrivalPaletteToMenu();
            _phases.BeforeMenuUpdate();
            GameplayMenuDispatch dispatch = _phases.PrepareMenus(delta);
            if (Menus.AdvanceNormal(delta,dispatch.Inventory,dispatch.Map,dispatch.LowHealthWarning) || !Active) return;
            GameplayObjectUpdateMode objects = _phases.BeforeObjects(delta);
            if (objects == GameplayObjectUpdateMode.Handled) return;
            if (objects == GameplayObjectUpdateMode.ForegroundOnly) { CompleteObjects(delta); return; }
        }
        _objects.Advance(delta);
    }

    private void CompleteObjects(double delta)
    {
        if (!Active) return;
        if (_phases.StatusBarUpdatesAllowed) _status.Update(delta);
        _phases.AfterStatusBar(delta);
        if (_phases.CutsceneRequestsAllowed && NativeCutsceneRequest.TrySelect(_memory, _phases.SelectRequestedCutscene)) return;
        _phases.AfterCutsceneRequests(delta);
        _transitions?.CompleteHoleWarpUpdate();
        if (_phases.ObjectCollisionsAllowed) _entities.ResolvePostObjectCollisions(_scene.Player);
        _phases.AfterObjectCollisions(delta);
        if (_phases.RoomInitializationMayComplete)
            NativeObjectStreamDatabase.For(_rooms.AssetRoot).CompleteRoomInitialization(_memory);
    }

    private void RoomChanged(int group, OracleRoomData room) => SynchronizeHud();

    private void RoomGraphicsLoaded(int group, OracleRoomData room)
    {
        // Warps reload after cutscene03's status-gate write; scrolling retains VRAM.
        if (!_entities.ScreenTransitionActive && _transitions?.IsTransitioning != true) ReloadCommonGraphics();
    }

    private void ReloadCommonGraphics() => _status.LoadCommonGraphics(_rooms.CurrentRoom.TilesetFlags);
    private void ApplyScreenWave(int amplitude, int phase) => _scene.RoomView.SetHorizontalWave(amplitude, phase);

    private void SynchronizeHud()
    {
        Hud hud = _scene.Hud;
        hud.MaxHealthQuarters = _inventory.MaxHealthQuarters;
        hud.EquippedA = _inventory.EquippedA; hud.EquippedB = _inventory.EquippedB;
        hud.DungeonIndex = _rooms.CurrentDungeonIndex; hud.TilesetFlags = _rooms.CurrentRoom.TilesetFlags;
        hud.Refresh();
    }

    public void Dispose()
    {
        if (!Active) return;
        Active = false;
        _entities.RoomInitialized -= RoomGraphicsLoaded;
        _entities.ScreenWaveRequested -= ApplyScreenWave;
        try
        {
            _scene.TreeExiting -= Dispose;
            if (_tileOwners is { } tiles)
            {
                tiles.Blocks.MessageRequested -= TileInfoRequested;
                tiles.Keys.MessageRequested -= TileInfoRequested;
                tiles.Blocks.EnteredHazard -= PushBlockEnteredHazard;
                tiles.Blocks.ReleaseRuntime();tiles.Keys.ReleaseRuntime();
            }
            _keyholes?.ReleaseRuntime();
            _interactions?.Dispose();
            GroundTreasures.Dispose();
            Menus.Dispose();
            _objects.Cancel();
            _scene.Player.GameOverRequested -= Menus.BeginGameOver;
            if (_transitions is not null) _entities.GaleMenuRequested -= Menus.Map.OpenGale;
            if (_transitions is not null) _entities.RoomWarpRequested -= ApplyRequestedWarp;
            if (_transitions is not null) _transitions.CommonGraphicsReloadRequested -= ReloadCommonGraphics;
            _transitions?.Dispose();
            _inventory.Changed -= SynchronizeHud;
            _entities.EnemyDefeated -= _inventory.RecordEnemyKill;
            _rooms.RoomChanged -= RoomChanged;
            _status.Dispose();
            _entities.Dispose();
        }
        finally { _releaseEntitySound(); }
    }
}
