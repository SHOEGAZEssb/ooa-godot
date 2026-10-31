using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

internal partial class SeasonsOpeningSession : GameplaySceneGraph, IGameplayObjectPhases, IGameplayForegroundPhases
{
    internal SeasonsOpeningData Data { get; private set; } = null!;
    internal Player Link => Player;
    internal OracleSoundEngine Sound { get; private set; } = null!;
    internal OracleSaveData Save { get; private set; } = null!;
    internal OracleRuntimeState Memory { get; private set; } = null!;
    internal OracleRandom Random { get; private set; } = null!;
    internal RoomSession Rooms { get; private set; } = null!;
    internal DeathRespawnPointController Checkpoints { get; private set; } = null!;
    internal OracleRoomData Room => Rooms.CurrentRoom;
    internal RoomCollision Collision { get; private set; } = null!;
    internal CombatController Combat { get; private set; } = null!;
    internal int FrameCounter => Memory.FrameCounter;
    internal int RoomId => Room.Id;
    internal byte DisabledObjects
    {
        get => Memory.ReadField("wDisabledObjects");
        set => Memory.SetField("wDisabledObjects", value);
    }
    internal int Signal(int index) => Memory.ReadWramByte(0xcfd0 + index);
    internal void Signal(int index, int value) => Memory.SetWramByte(0xcfd0 + index, unchecked((byte)value));
    internal bool Fading => _fadeRemaining != 0 || Transitions.PaletteFadeActive;
    internal SeasonsRoomTransitionController Transitions { get; private set; } = null!;
    internal bool Scrolling => Transitions.ScrollActive || Transitions.RoomPackTransitionActive;
    internal bool Complete => Phase == SeasonsOpeningPhase.Complete;
    internal SeasonsOpeningPhase Phase { get; private set; }
    internal RoomEntityManager Entities { get; private set; } = null!;
    internal IEnumerable<SeasonsOpeningActor> Actors => Entities is null ? [] :
        Entities.EntityAdapters<SeasonsOpeningActor>().Where(actor => !actor.Finished);
    private Node2D _actorLayer => WorldRoot;
    private ColorRect _overlay = null!;
    private readonly CanvasItemMaterial _whiteFade = new() { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
    private readonly CanvasItemMaterial _blackFade = new() { BlendMode = CanvasItemMaterial.BlendModeEnum.Sub };
    private InventoryState _inventory = null!;
    internal InventoryState Inventory => _inventory;
    internal StatusBarController StatusBar => Engine!.StatusBar;
    internal GameplaySession? Engine { get; set; }
    internal GameplayMenus? Menus => Engine?.Menus;
    internal InteractionController Interactions => Engine!.Interactions;
    internal bool MenuOpeningAvailable => Transitions.GameplayMenusAvailable && _fadeRemaining == 0 && !Dialogue.IsOpen &&
        !Link.GaleActive && !Link.IsDying && !Interactions.DialogueOpen && Memory.ReadField("wLinkPlayingInstrument") == 0 &&
        !_nativeLink.Active && !Entities.PlayerMenusDisabled && !Link.ElectricShockActive;
    internal bool DebugOpeningAvailable => !Transitions.IsTransitioning && _fadeRemaining == 0 && !Dialogue.IsOpen &&
        !Link.GaleActive && !Link.IsDying && !_nativeLink.Active && !_screen.Visible;
    // shortcut: Opening actor state is not serialized; add an event snapshot before permitting earlier captures.
    internal bool CanCaptureDebugState => Phase == SeasonsOpeningPhase.Complete &&
        Save.HasGlobalFlag(GlobalFlag.IntroDone) && MenuOpeningAvailable && DebugOpeningAvailable &&
        Menus is { Pause.IsLeased: false };
    internal double AnimationTicks => _roomTicks;
    private NewGameIntroScreen? _pregameScreen;
    private SeasonsOpeningLink _nativeLink = null!;
    private SeasonsOpeningSequence _sequence = null!;
    private bool _foregroundScroll, _foregroundTransition, _arrivalPaletteAdvanced;
    private bool _foregroundCollisions;
    private bool NormalCollisionCaller => Phase is SeasonsOpeningPhase.Forest or SeasonsOpeningPhase.Troupe or
        SeasonsOpeningPhase.Aftermath or SeasonsOpeningPhase.Complete;
    private SeasonsOpeningScreen _screen = null!;
    private int _roomTicks, _arrivalFrames, _fadeRemaining, _fadeDuration;
    private bool _fadeOut;
    private Color _fadeColor = Colors.White;
    internal int LastText { get; private set; } = -1;
    internal bool ButtonConsumed { get; private set; }

    public override void _EnterTree()
    {
        base._EnterTree();
        GetNode<NewGameIntroScreen>("Interface/Pregame").DeferPreparation = true;
    }

    internal void Start(OracleSoundEngine sound, OracleRandom random, OracleSaveData save,
        GameplayMenuBindings menus, Func<bool> hudBottom, Action<NewGameIntroScreen,Action> beginPregame,
        DebugSavestateData? debugSavestate = null)
    {
        if (!ResourcesPrepared) foreach (bool _ in PrepareResources()) { }
        Sound = sound; Random = random; Memory = OracleRuntimeState.CreateInitialized(OracleGame.Seasons);
        Save = save;
        if (Save.Game != OracleGame.Seasons || Save.IsLinkedGame)
            throw new System.NotSupportedException("Seasons content requires a standard Seasons file; linked-game opening content is not bound.");
        if (debugSavestate is null) GameplayInitialization.RestoreCheckpoint(Save, Memory);
        else debugSavestate.RestoreRuntimeState(Memory);
        bool restored = debugSavestate is not null || GameplayInitialization.PregameComplete(Save);
        int startingRoom = debugSavestate?.Room ?? (restored ? Save.RespawnRoom : 0xa7);
        int startingGroup = debugSavestate?.Group ?? Save.RespawnGroup;
        if (!Data.World.HasRoom(startingGroup,startingRoom))
            throw new NotSupportedException($"bank1.initializeGame: Seasons checkpoint ${startingGroup:x2}:${startingRoom:x2} has no imported room content.");
        Rooms = new RoomSession(new SeasonsRoomContent(Data.World), startingGroup, startingRoom,
            () => _roomTicks, () => _roomTicks = 0, Save, Memory, countAsRoomEntry: false);
        _inventory = new(TreasureDatabase.For(SeasonsGameContent.AssetRoot), Save,
            () => Rooms.CurrentDungeonIndex, Memory);
        if (debugSavestate is not null)
        {
            _roomTicks = checked((int)debugSavestate.AnimationTicks);
            Room.UpdateAnimation(_roomTicks);
        }
        Entities = new RoomEntityManager(_actorLayer, SeasonsGameContent.AssetRoot,
            owner => new SeasonsRoomEntityFactory(this,owner), Random, Save, Memory, _inventory,
            TreasureDatabase.For(SeasonsGameContent.AssetRoot), ItemDropDatabase.For(SeasonsGameContent.AssetRoot), animationTick: () => _roomTicks);
        Engine = new GameplaySession(this,Rooms,Entities,_inventory,Sound,menus,this,this,hudBottom);
        Engine.StatusBar.InitializeNativeState();
        Checkpoints = new(Rooms, Link);
        Entities.DeathRespawnPointRequested += Checkpoints.RecordCurrentPoint;
        Entities.WorldToScreen = point => _screen.Visible ?
            point - new Vector2(_screen.CameraX, _screen.CameraY) : Transitions.WorldToGameplayScreen(point);
        var tiles=Engine.BindTileContacts(() => _roomTicks);
        Collision = new(Rooms,Entities,tiles.Blocks,HasNeighbor,normalizeCornerWalls:false);
        Combat = new(Rooms,RoomView,Entities,new BreakableTileDatabase(SeasonsGameContent.AssetRoot),Save,Sound,() => _roomTicks);
        Transitions = new(Rooms,Data.World,this,Entities,Checkpoints,Sound);
        Transitions.ScreenTransitionsDisabledSource = () => Phase is not (SeasonsOpeningPhase.Troupe or SeasonsOpeningPhase.Complete);
        Engine.BindTransitions(Transitions);
        Engine.BindInteractions(null, () => _roomTicks, () =>
            StatusBar.DisplayedRupees == Inventory.Rupees && StatusBar.DisplayedHealth == Inventory.HealthQuarters);
        Entities.PaletteFadeActiveSource = () => Fading;
        Entities.RoomInitialized += (group,room) =>
        {
            if (!Transitions.IsTransitioning || Transitions.SuppressesDestinationMusic) return;
            Sound.SelectRoomMusic(group,room.Id,Save); Sound.CheckPlayRoomMusic();
        };
        if (!restored) LoadRoom(0xa7, 0, spawn: false, preserveActors: true);
        else
        {
            RoomView.SetRoom(Room.Texture);
            if (startingGroup == 0)
            {
                Memory.SetField("wRoomPack", (byte)Data.World.RoomPack(startingRoom));
                Memory.SetField("wLoadingRoomPack", (byte)Data.World.RoomPack(startingRoom));
            }
            Memory.SetField("wScrollMode", 1);
            if (debugSavestate is null) Rooms.MarkCurrentRoomVisited();
        }
        Link.Initialize(new SeasonsOpeningWorld(this), _inventory,
            debugSavestate?.PlayerPosition ?? (restored ? new(Save.RespawnX, Save.RespawnY) : new(0x48, 0x38)), Random);
        Link.SetPhysicsProcess(true); Link.SetProcess(true);
        _nativeLink = new(this); _sequence = new(this, _screen);
        if (restored)
        {
            _pregameScreen!.QueueFree(); _pregameScreen = null;
            if (debugSavestate is null)
            {
                GameplayInitialization.RestorePlayer(Save, Link);
                Checkpoints.RecordRoomInitialization(Link.PrecisePosition, Save.RespawnFacing);
            }
            else debugSavestate.RestoreRoomParseState(Entities,Memory);
            Entities.LoadRoom(startingGroup, Room, debugSavestate is null
                ? EnemyPlacementContext.Warp(Room.GetPackedPosition(Link.PrecisePosition)) : EnemyPlacementContext.Unrestricted);
            if (debugSavestate is null) EraOrSeasonInfoDatabase.For(Rooms.AssetRoot).TryDisplay(Rooms,Entities);
            if (debugSavestate is not null)
            {
                debugSavestate.RestoreLiveState(Save,Memory,Random,Entities);
                Link.Face(debugSavestate.PlayerFacing);
            }
            Sound.SelectRoomMusic(startingGroup, startingRoom, Save); Sound.CheckPlayRoomMusic();
            Link.Show(); RoomView.Show();
            if(debugSavestate is null)Hud.ShowStatusBar();else Hud.RestoreStatusBarPresentation();
            Phase = SeasonsOpeningPhase.Complete;
            return;
        }
        if (GameplayInitialization.LinkSummoned(Save)) { BeginArrival(); return; }
        Link.Hide(); RoomView.Hide(); Hud.Hide();
        _pregameScreen!.Show();
        beginPregame(_pregameScreen,BeginArrival);
        Phase = SeasonsOpeningPhase.Pregame;
    }

    GameplayForegroundMode IGameplayForegroundPhases.BeginFrame(double delta)
    {
        ButtonConsumed = false;
        _foregroundScroll = Transitions.ScrollActive;
        _foregroundTransition = Transitions.IsTransitioning;
        _foregroundCollisions = NormalCollisionCaller;
        AdvanceFade();
        if (Phase == SeasonsOpeningPhase.Arrival) { AdvanceArrival(); return GameplayForegroundMode.Handled; }
        if (_foregroundScroll) return GameplayForegroundMode.ObjectsOnly;
        return GameplayForegroundMode.Normal;
    }

    bool IGameplayForegroundPhases.PrepareElectricShock(double delta) =>
        Phase is (SeasonsOpeningPhase.Forest or SeasonsOpeningPhase.Troupe or
            SeasonsOpeningPhase.Aftermath or SeasonsOpeningPhase.Complete) && Transitions.GameplayMenusAvailable && Menus?.Inventory.GameOver != true;

    GameplayMenuDispatch IGameplayForegroundPhases.PrepareMenus(double delta)
    {
        GameplayMenus menus = Menus ?? throw new InvalidOperationException("Seasons foreground requires its bound gameplay session.");
        bool normal = Phase is SeasonsOpeningPhase.Forest or SeasonsOpeningPhase.Troupe or
            SeasonsOpeningPhase.Aftermath or SeasonsOpeningPhase.Complete;
        bool menuDispatch = normal && Transitions.GameplayMenusAvailable && !Dialogue.IsOpen && !Link.IsDying &&
            Memory.ReadField("wLinkPlayingInstrument") == 0;
        return new(menuDispatch || menus.Inventory.IsActive, normal || menus.Map.IsActive,
            menuDispatch && MenuOpeningAvailable);
    }

    GameplayObjectUpdateMode IGameplayForegroundPhases.BeforeObjects(double delta)
    {
        if (_foregroundTransition) return GameplayObjectUpdateMode.Full;
        _sequence.AdvanceFrame();
        if (!_sequence.SkipObjects) return GameplayObjectUpdateMode.Full;
        UpdateRoomAnimation();
        return GameplayObjectUpdateMode.ForegroundOnly;
    }

    bool IGameplayForegroundPhases.StatusBarUpdatesAllowed => !_foregroundScroll && !_screen.Visible;
    // bank1.s:cutscene01 reaches collisions only after getNextActiveRoom returns NC.
    bool IGameplayForegroundPhases.ObjectCollisionsAllowed => _foregroundCollisions && NormalCollisionCaller &&
        !_foregroundScroll && !Transitions.IsTransitioning && !Transitions.RoomExitPending;
    void IGameplayForegroundPhases.AfterStatusBar(double delta)
    {
        if (_foregroundTransition) return;
        _sequence.AdvanceShake();
    }
    DialogueBox? IGameplayForegroundPhases.FrameDialogue => Dialogue;
    void IGameplayForegroundPhases.ApplyPresentation(bool hudBottom) => ApplyGameplayPresentation(hudBottom);

    private void UpdateRoomAnimation()
    {
        if (!_foregroundScroll && !_screen.Visible && Room.UpdateAnimation(++_roomTicks)) RoomView.SetRoom(Room.Texture);
    }

    bool IGameplayObjectPhases.PrepareObjectUpdate(double delta)
    {
        if (Transitions.AdvanceFadeOutWarpBeforeObjects(delta)) return false;
        _arrivalPaletteAdvanced = Transitions.AdvanceStandingArrivalPaletteBeforeObjects(delta);
        return true;
    }

    void IGameplayObjectPhases.UpdateLink()
    {
        Transitions.BeginLinkUpdate();
        if (Transitions.ScrollActive) return;
        if (_nativeLink.Active) _nativeLink.AdvanceFrame();
        else Link.AdvanceApplicationUpdate(advanceElectricShock: Phase is not (
            SeasonsOpeningPhase.Forest or SeasonsOpeningPhase.Troupe or
            SeasonsOpeningPhase.Aftermath or SeasonsOpeningPhase.Complete));
    }

    bool IGameplayObjectPhases.PrepareEntityUpdate(double delta)
    {
        if (!_arrivalPaletteAdvanced) Transitions.UpdateWarpAndEffects(delta);
        return true;
    }
    bool IGameplayObjectPhases.UpdateRoomInteractions(double delta, bool resuming)
    {
        Interactions.Update(delta, Link);
        return true;
    }
    void IGameplayObjectPhases.BeforeObjectTail(double delta)
    {
        if (!_foregroundTransition && !Transitions.IsTransitioning) CheckRoomExit();
        if (_foregroundScroll) Transitions.UpdateScroll(delta);
    }
    void IGameplayObjectPhases.AfterObjectTail(double delta)
    {
        if (!_foregroundTransition) UpdateRoomAnimation();
    }
    bool IGameplayForegroundPhases.CutsceneRequestsAllowed => _foregroundCollisions && NormalCollisionCaller &&
        !_foregroundScroll && !_foregroundTransition && !Transitions.IsTransitioning;
    void IGameplayForegroundPhases.AfterCutsceneRequests(double delta)
    {
        if (!_foregroundScroll && !_foregroundTransition && !Transitions.IsTransitioning)
        {
            Transitions.CheckRequestedTileWarp(Link);
            if (!Transitions.IsTransitioning) Transitions.CommitRoomExit(Link);
        }
    }

    private void BeginArrival()
    {
        _pregameScreen!.QueueFree(); _pregameScreen = null;
        Phase = SeasonsOpeningPhase.Arrival;
        OrRoomFlag(OracleSaveData.RoomFlagVisited);
        RoomView.Show(); Hud.HideStatusBar();
        DisabledObjects = 0x81; _arrivalFrames = 0;
        _overlay.Color = Colors.White;
        _nativeLink.BeginFall();
        Link.Hide(); RoomView.SetHorizontalWave(0xff, 0); Sound.PlaySound(SoundId.SndWarpStart);
    }

    private void AdvanceArrival()
    {
        _arrivalFrames++;
        if (_arrivalFrames <= NewGameIntroController.ArrivalFadeWaitFrames)
        {
            _overlay.Color = new(1, 1, 1, 1 - Math.Min(32, (_arrivalFrames + 1) / 2) / 32f);
            RoomView.SetHorizontalWave(0xff, _arrivalFrames);
            return;
        }
        int wave = _arrivalFrames - NewGameIntroController.ArrivalFadeWaitFrames;
        RoomView.SetHorizontalWave(Math.Max(0, 255 - wave * 2), FrameCounter);
        if (wave >= 64) { Link.Show(); _nativeLink.AdvanceFrame(); }
        if (wave <= 128) return;
        RoomView.ClearHorizontalWave(); Hud.ShowStatusBar();
        DisabledObjects = 0;
        Save.SetGlobalFlag(0x21);
        Phase = SeasonsOpeningPhase.Forest; Entities.LoadRoom(0, Room);
    }

    internal void LoadRoom(int room, int season, bool spawn = true, bool preserveActors = false, int group = 0)
    {
        _roomTicks = 0;
        Memory.SetField("wRoomStateModifier", checked((byte)season));
        if (group == 0)
        {
            Memory.SetField("wRoomPack", (byte)Data.World.RoomPack(room));
            Memory.SetField("wLoadingRoomPack", (byte)Data.World.RoomPack(room));
        }
        if (preserveActors) Rooms.LoadCutsceneBackground(group, room);
        else Rooms.Load(group, room);
        RoomView.SetRoom(Room.Texture);
        Memory.SetField("wScrollMode", 1);
        if (preserveActors) Entities.RebindCutsceneBackground(Room);
        else if (spawn) Entities.LoadRoom(group, Room);
        else { Entities.Clear(); Entities.RebindCutsceneBackground(Room); }
    }

    internal SeasonsOpeningActor? Spawn(int id, int subid, Vector2 position, int variable = 0)
    {
        return Entities.TryAllocateInteraction(() => new SeasonsOpeningActor(this, id, subid, position, variable));
    }
    internal void Delete(SeasonsOpeningActor actor)
    {
        Entities.RetireEntity(actor);
    }
    internal void StrikeLightning(int bits, Vector2 position)
    {
        if (!Entities.PartSlotAvailable) return;
        var node = Data.CreateActor(0x27, 0, position);
        var effect = new LightningEffect(node, () => Random.Next().Value, Sound.PlaySound,
            frames => _sequence.Shake(frames), debris => Entities.TryCreateInteraction(new KillEnemyPuffSpawn(debris)),
            SeasonsGameContent.AssetRoot, () => Signal(2, Signal(2) | bits));
        Entities.AddEntity(new LightningRoomEntity(node, effect));
    }
    internal void ClearActors() => Entities.ClearDynamicInteractions();
    internal SeasonsOpeningActor? Din => Actors.FirstOrDefault(actor => actor is { Id: 0x4e, Subid: 6 });
    internal void StartNativeLink(bool dance) => _nativeLink.Begin(dance);
    internal void ReleaseLink() => _nativeLink.End();
    internal void SetPhase(SeasonsOpeningPhase phase) => Phase = phase;
    internal void StartCapture() => _sequence.StartDance();
    internal void FinishForest()
    {
        LoadRoom(0x97, 0); Link.WarpTo(new(0x48, 0x48)); Link.Face(Vector2I.Up);
        _nativeLink.End(); DisabledObjects = 0; Phase = SeasonsOpeningPhase.Troupe; Fade(false);
    }
    internal void BeginAftermath()
    {
        _screen.Hide(); Transitions.SetCutsceneCameraOrigin(Vector2.Zero); Link.Show();
        LoadRoom(0xb6, 0); Link.WarpTo(new(0x58, 0x48)); Link.Face(Vector2I.Up);
        Signal(0, 0); _nativeLink.Begin(false); Hud.Show(); RoomView.Show();
        Phase = SeasonsOpeningPhase.Aftermath; Fade(false, black: true);
    }
    internal void FinishOpening() { Phase = SeasonsOpeningPhase.Complete; }
    internal void DebugTravel(int group, int room)
    {
        if (!Rooms.HasRoom(group, room))
            throw new NotSupportedException($"objects/seasons/mainData.s: debug destination ${group:x2}:${room:x2} is not imported.");
        LoadRoom(room, Memory.ReadField("wRoomStateModifier") & 3,group:group);
        for (int y = 8; y < Room.Height; y += 16)
        for (int x = 8; x < Room.Width; x += 16)
            if (!Collision.Collides(new(x, y)))
            {
                Link.WarpTo(new(x, y)); Link.Face(Vector2I.Down); Transitions.ResetCamera(); return;
            }
        throw new NotSupportedException($"objects/seasons/mainData.s: debug room ${group:x2}:${room:x2} has no walkable Link position.");
    }
    internal bool TryTalk()
    {
        if (Interactions.DialogueOpen || ButtonConsumed) return false;
        ButtonConsumed = Interactions.TryInteract(Link);
        return ButtonConsumed;
    }
    internal void ShowText(int id, int? position = null)
    {
        LastText = id;
        Dialogue.ShowMessage(Data.Text(id), _screen.Visible ? DialogueScreenContext.FullScreen(Link.Position.Y) :
            DialogueScreenContext.Gameplay(Link.Position.Y), position, 0, id);
    }
    internal bool RoomFlag(int flag) => (Save.GetRoomFlags(0, RoomId) & flag) != 0;
    internal void OrRoomFlag(int flag) => Save.SetRoomFlag(0, RoomId, (byte)flag);
    internal void Fade(bool outward, bool fast = false, bool black = false)
    {
        _fadeOut = outward; _fadeRemaining = _fadeDuration = fast ? 11 : 32;
        _overlay.Material = black ? _blackFade : _whiteFade;
        _fadeColor = Colors.White;
        _overlay.Color = new(_fadeColor, outward ? 0 : 1);
    }
    private void AdvanceFade()
    {
        if (_fadeRemaining == 0) return;
        _fadeRemaining--;
        float amount = (_fadeDuration - _fadeRemaining) * (_fadeDuration == 11 ? 3 : 1) / 31f;
        _overlay.Color = new(_fadeColor, Math.Clamp(_fadeOut ? amount : 1 - amount, 0, 1));
    }
    internal void SetFullScreen(bool enabled)
    {
        RoomView.Visible = !enabled; Hud.Visible = !enabled;
        _actorLayer.Visible = !enabled || !_screen.Dance;
        Transitions.SetCutsceneCameraOrigin(enabled ? new(_screen.CameraX, _screen.CameraY) : Vector2.Zero);
        Link.Visible = !enabled;
    }
    internal void UpdateNativeCamera(int x, int y)
    {
        // _getObjectPositionOnScreen adds $10 before the OAM compositor's hardware Y bias.
        if (_screen.Visible) Transitions.SetCutsceneCameraOrigin(new(x, y));
    }
    internal SeasonsOpeningScreen NativeScreen => _screen;
    internal void ApplyGameplayPresentation(bool bottom)
    {
        int top = ApplyHudPlacement(bottom, _screen.Visible);
        Transitions.NativeRoomUsesFullScreen = _screen.Visible;
        Transitions.CutsceneCameraFrozen = _screen.Visible;
        Transitions.SetGameplayScreenTop(top);
        if (_screen.Visible) Transitions.SetCutsceneCameraOrigin(new(_screen.CameraX,_screen.CameraY));
        else Transitions.UpdateCamera();
    }
    internal void SetRoomShake(Vector2 offset) => RoomView.SetNativeScrollOffset(offset);
    internal void SetFlash(bool white) => _overlay.Color = new(1, 1, 1, white ? 1 : 0);
    internal void SetCrystalFlash(bool white) => _actorLayer.Visible = !white;
    private bool HasNeighbor(Vector2 sample) => Phase is SeasonsOpeningPhase.Troupe or SeasonsOpeningPhase.Complete &&
        Transitions.HasNeighborFor(sample);
    internal void CheckRoomExit()
    {
        if (Phase is SeasonsOpeningPhase.Troupe or SeasonsOpeningPhase.Complete)
            Transitions.SampleRoomExit(Link);
    }


    internal void ReleaseRuntime() { Engine?.Dispose(); Entities?.Dispose(); }
    public override void _ExitTree() => ReleaseRuntime();
}

internal enum SeasonsOpeningPhase { Pregame, Arrival, Forest, Troupe, Dance, Capture, Imprisonment, TempleSinking, Aftermath, Complete }
