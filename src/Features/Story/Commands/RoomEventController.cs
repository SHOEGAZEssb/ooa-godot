using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>
/// Selects and schedules room-entry events. Event-specific state and behavior
/// live in dedicated implementations; this class only coordinates their room
/// lifecycle, explicit update ownership, and externally visible restrictions.
/// </summary>
public sealed class RoomEventController
{
    private readonly RoomEventContext _context;
    private IRoomEvent[] _eventsByPriority = [];
    private NpcInteractionHandler[] _interactionHandlers = [];
    private Dictionary<Type, IRoomEvent> _eventsByType = new();
    private bool _preparationStarted;

    public RoomEventController(
        RoomSession rooms,
        RoomEntityManager entities,
        RoomTransitionController transitions,
        DialogueBox dialogue,
        Player player,
        RoomView roomView,
        Func<Vector2, Vector2> worldToScreen,
        Func<long> animationTick,
        CanvasLayer interfaceLayer,
        ColorRect fade,
        Hud hud,
        InventoryState inventory,
        TreasureDatabase treasures,
        OracleSoundEngine sound,
        Camera2D roomCamera,
        bool deferPreparation = false)
    {
        _context = new RoomEventContext(
            rooms,
            entities,
            transitions,
            dialogue,
            player,
            roomView,
            worldToScreen,
            animationTick,
            interfaceLayer,
            fade,
            hud,
            inventory,
            treasures,
            sound,
            roomCamera);
        if (!deferPreparation)
            foreach (bool _ in PrepareResources()) { }
    }

    internal IEnumerable<bool> PrepareResources()
    {
        if (_preparationStarted)
            throw new InvalidOperationException("Room event resources have already begun preparation.");
        _preparationStarted = true;
        var tokayInteractions = new TokayInteractionDatabase();
        var oceanInteractions = new OceanInteractionDatabase();
        yield return false;
        var impa = new ImpaIntroEvent(_context);
        yield return false;
        var remoteMakuThirdEssence = new RemoteMakuThirdEssenceEvent(_context);
        var remoteMakuSeventhEssence = new RemoteMakuSeventhEssenceEvent(_context);
        yield return false;
        // Entry precedence is explicit. Construction and typed lookup share this
        // one registration; A-button routing below has its own source order.
        Func<IRoomEvent>[] factories =
        [
            () => new HarpOfAgesEvent(_context),
            () => new DungeonEssenceEvent(_context),
            () => new WallRetractionEvent(_context),
            () => new PalaceBootEvent(_context),
            () => new PalacePassageEvent(_context),
            () => new PalacePossessionEvent(_context,()=>Get<NayruRescueWarpEvent>().Begin()),
            () => new NayruRescueWarpEvent(_context),
            () => new BlackTowerEscapeAttemptEvent(_context,()=>Get<PalacePossessionEvent>().ReleaseForNativeContinuation()),
            () => new DefeatedMoblinEvent(_context),
            () => new RemoteMakuFirstEssenceEvent(_context),
            () => new RemoteMakuSecondEssenceEvent(_context),
            () => new RemoteMakuFourthEssenceEvent(_context),
            () => new RemoteMakuFifthEssenceEvent(_context),
            () => new RemoteMakuSixthEssenceEvent(_context),
            () => new RemoteMakuEighthEssenceEvent(_context),
            () => remoteMakuSeventhEssence,
            () => new PostD7Event(_context, remoteMakuSeventhEssence),
            () => new RemoteMakuHarpEvent(_context),
            () => new RemoteMakuWingDungeonEvent(_context),
            () => new PostD3RemoteMakuEvent(_context, remoteMakuThirdEssence),
            () => remoteMakuThirdEssence,
            () => new CompanionForestEvent(_context),
            () => new FairiesWoodsEvent(_context),
            () => new WingDungeonCollapseEvent(
                _context, () => Get<RemoteMakuWingDungeonEvent>().StartWarning()),
            () => new NayruIntroEvent(_context, impa),
            () => new GraveyardGateEvent(_context),
            () => new CrownDungeonEntranceEvent(_context),
            () => new MermaidsCaveEntranceEvent(_context),
            () => new LibraryKeyholeEvent(_context),
            () => new RickyGlovesEvent(_context),
            () => new TingleEvent(_context),
            () => new CarpenterEvent(_context),
            () => new SymmetryEvent(_context),
            () => new PlenEvent(_context),
            () => new PatchEvent(_context),
            () => new BombUpgradeFairyEvent(_context),
            () => new MooshRescueEvent(_context),
            () => new MakuSproutRescueEvent(_context),
            () => new DekuForestSoldierEvent(_context),
            () => new DekuForestPalaceEvent(_context),
            () => new BusinessScrubEvent(_context),
            () => new LynnaShopEvent(_context),
            () => new SyrupShopEvent(_context),
            () => new VasuShopEvent(_context),
            () => new ShootingGalleryEvent(_context),
            () => new ComedianEvent(_context),
            () => new MaskSalesmanEvent(_context),
            () => new OldZoraEvent(_context),
            () => new LibraryEvent(_context),
            () => new KingZoraEvent(_context, oceanInteractions),
            () => new PirateCaptainEvent(_context),
            () => new OceanZoraEvent(_context, oceanInteractions),
            () => new SeaFairyEvent(_context),
            () => new CleanSeasEvent(_context),
            () => new JabuExteriorEvent(_context),
            () => new MamamuEvent(_context),
            () => new GoronCaveEvent(_context),
            () => new DumbbellManEvent(_context),
            () => new TokkeyEvent(_context),
            () => new ChevalEvent(_context),
            () => new RalphAfterChevalEvent(_context),
            () => new RalphBeforeCurrentsEvent(_context),
            () => new RalphAfterRaftonEvent(_context),
            () => new RaftwreckEvent(_context),
            () => new TokayTheftEvent(_context),
            () => new TokayCookEvent(_context, tokayInteractions),
            () => new RosaShovelEvent(_context, tokayInteractions),
            () => new TokayTradingEvent(_context, tokayInteractions, new TokayShopDatabase()),
            () => new WildTokayGameEvent(_context, tokayInteractions, new WildTokayGameDatabase()),
            () => new RaftonEvent(_context),
            () => new DepressedBoyEvent(_context),
            () => new ToiletHandEvent(_context),
            () => new PoeEvent(_context),
            () => new MakuTreeSavedEvent(_context),
            () => new MakuTreeAdviceEvent(_context),
            () => new MakuSeedEvent(_context),
            () => new MakuTreeDisappearanceEvent(_context),
            () => new RalphPortalEvent(_context),
            () => new PreBlackTowerEvent(_context),
            () => new ZeldaRescueEvent(_context),
            () => new BlackTowerDoorwayEvent(_context),
            () => new BlackTowerSeedEvent(_context),
            () => new BlackTowerConfrontationEvent(_context),
            () => new BlackTowerPossessionEvent(_context),
            () => new BlackTowerEndingEvent(_context,()=>Get<PalacePossessionEvent>().ReleaseForNativeContinuation(restoreColor:false),()=>Get<PostD7Event>().Flash(2)),
            () => new BlackTowerEntranceEvent(_context),
            () => new EnterPastEvent(_context),
            () => new GraveyardGhostKidsEvent(_context),
            () => impa,
        ];
        var events = new List<IRoomEvent>();
        foreach (var factory in factories)
        {
            events.Add(factory());
            yield return false;
        }
        _eventsByPriority = events.ToArray();
        _eventsByType = _eventsByPriority.ToDictionary(roomEvent => roomEvent.GetType());
        _context.NativeDialogueScreen = () =>
            (UpdateOwner as IRoomEventDialogueContext)?.DialogueScreen;
        static NpcInteractionHandler Npc(
            string source, params Func<NpcCharacter, bool>[] handlers) =>
            NpcInteractionHandler.ForNpc(source, (target, _) =>
                Array.Exists(handlers, handler => handler(target.Npc)));
        _interactionHandlers =
        [
            Npc("impaInCutscene.s:$31:$07;bird.s:$4c:$04", Get<ZeldaRescueEvent>().TryInteractNpc),
            Npc("goron.s:scriptTable;goronElder.s;shootingGallery.s", Get<GoronCaveEvent>().TryInteractNpc),
            Npc("plen.s:plenSubid0Script", Get<PlenEvent>().TryInteractNpc),
            Npc("symmetryNpc.s:scriptTable",
                Get<SymmetryEvent>().TryInteractNpc),
            Npc("patch.s:interactionCode94", Get<PatchEvent>().TryInteractNpc),
            Npc("carpenter.s:room025Scripts",
                Get<CarpenterEvent>().TryInteractNpc),
            Npc("forestFairy.s:forestFairy_discovered",
                Get<FairiesWoodsEvent>().TryInteractNpc),
            Npc("shopkeeper.s:lynnaShop:npc",
                Get<LynnaShopEvent>().TryInteractNpc),
            Npc("syrup.s:interactionCode5f", Get<SyrupShopEvent>().TryInteractNpc),
            Npc("businessScrub.s:interactionCodece",
                Get<BusinessScrubEvent>().TryInteractNpc),
            Npc("vasu.s+ringHelpBook.s:room2eeActors",
                Get<VasuShopEvent>().TryInteractNpc),
            Npc("shootingGallery.s:shootingGalleryScript",
                Get<ShootingGalleryEvent>().TryInteractNpc),
            Npc("miscCutscenes.s:CUTSCENE_NAYRU_SINGING",
                Get<NayruIntroEvent>().TryInteractNpc),
            Npc("hardhatWorker.s:blackTowerEntrance",
                Get<BlackTowerEntranceEvent>().TryInteractNpc),
            Npc("ralph.s:ralphSubid0c", Get<BlackTowerConfrontationEvent>().TryInteract),
            Npc("makuSprout.s:interactionCode88",
                Get<MakuSproutRescueEvent>().TryInteractNpc),
            Npc("companionScripts.s:companionScript_subid00Script",
                Get<MooshRescueEvent>().TryInteractNpc),
            Npc("companionScripts.s:companionScript_subid03Script",
                Get<RickyGlovesEvent>().TryInteractNpc),
            Npc("tingle.s:interactionCodec8; scripts.s:tingleScript",
                Get<TingleEvent>().TryInteractNpc),
            Npc("tokay.s:tokayScriptTable; rosa.s:interactionCode68",
                Get<TokayCookEvent>().TryInteractNpc,
                Get<WildTokayGameEvent>().TryInteractNpc,
                Get<RosaShovelEvent>().TryInteractNpc),
            Npc("makuTree.s:interactionCode87Subid02",
                Get<MakuTreeSavedEvent>().TryInteractNpc,
                Get<MakuTreeAdviceEvent>().TryInteractNpc),
            Npc("maskSalesman.s:maskSalesmanScript",
                Get<MaskSalesmanEvent>().TryInteractNpc),
            Npc("oldZora.s:oldZoraScript", Get<OldZoraEvent>().TryInteractNpc),
            Npc("oldMan.s/bookOfSealsPodium.s", Get<LibraryEvent>().TryInteractNpc),
            Npc("kingZora.s:interactionCode9c", Get<KingZoraEvent>().TryInteractNpc),
            Npc("pirateCaptain.s/pirate.s:$c3/$c4", Get<PirateCaptainEvent>().TryInteractPirate),
            Npc("fakeOctorok.s:greatFairyOctorokCode $32:$02", Get<SeaFairyEvent>().TryInteractNpc),
            Npc("zora.s:$10-$12", Get<OceanZoraEvent>().TryInteractNpc),
            Npc("mamamuYan.s:mamamuYanScript", Get<MamamuEvent>().TryInteractNpc),
            Npc("dumbellMan.s:dumbbellManScript",
                Get<DumbbellManEvent>().TryInteractNpc),
            Npc("tokkey.s:interactionCode9d",
                Get<TokkeyEvent>().TryInteractNpc),
            Npc("cheval.s:interactionCode6a",
                Get<ChevalEvent>().TryInteractNpc),
            Npc("rafton.s:interactionCode69",
                Get<RaftonEvent>().TryInteractNpc),
            Npc("boy.s:boySubid07Script",
                Get<DepressedBoyEvent>().TryInteractNpc),
            Npc("toiletHand.s:toiletHandScript",
                Get<ToiletHandEvent>().TryInteractNpc),
            Npc("poe.s:poeScript",
                Get<PoeEvent>().TryInteractNpc),
            Npc("comedian.s:comedianScript",
                Get<ComedianEvent>().TryInteractNpc),
            Npc("soldier.s:soldierSubid02/07/09",
                Get<DekuForestPalaceEvent>().TryInteractNpc),
            NpcInteractionHandler.ForPlayer(
                "shopkeeper.s:lynnaShop:player",
                Get<LynnaShopEvent>().TryInteractPlayer),
            NpcInteractionHandler.ForPlayer(
                "syrup.s:shopItem", Get<SyrupShopEvent>().TryInteractPlayer),
            NpcInteractionHandler.ForPlayer(
                "tokayShopItem.s:interactionCode81",
                Get<TokayTradingEvent>().TryInteractPlayer)
        ];
        _context.Entities.RoomEntitiesLoaded += OnRoomEntitiesLoaded;
        _context.Entities.PostD7AmbiFactory = Get<PostD7Event>().CreatePlacedAmbi;
        _context.Entities.BlackTowerRalphFactory = Get<BlackTowerConfrontationEvent>().CreateRalph;
        _context.Entities.BlackTowerAmbiFactory = Get<BlackTowerPossessionEvent>().CreateAmbi;
        _context.Entities.ZeldaRescueFactory = Get<ZeldaRescueEvent>().CreatePlaced;
        VeranFaceDatabase? veranFace=null;
        _context.Entities.VeranFaceFactory=record=>new VeranFaceRoomEntity(_context,veranFace??=new(),record);
        _context.Entities.VeranFinalDeathFade=parent=>
        {
            if(!_context.Entities.InteractionSlotAvailable)return false;
            _context.Entities.AddEntity(new VeranDeathFadeRoomEntity(_context,parent.Data,parent.Position,parent.ZFixed>>8,
                delay=>Get<PalacePossessionEvent>().Fade(true,delay)));
            return true;
        };
        _context.Entities.VeranFinalEscape=Get<BlackTowerEndingEvent>().Begin;
        _context.Entities.BlackTowerPossessionAftermath = Get<BlackTowerPossessionEvent>().Aftermath;
        Get<BlackTowerPossessionEvent>().BeginEscapeAttempt=Get<BlackTowerEscapeAttemptEvent>().Begin;
        Get<RemoteMakuFifthEssenceEvent>().SpawnTunnelGoron=Get<GoronCaveEvent>().SpawnTunnelGoron;
        _context.Entities.ObjectFellInHole += NotifyObjectFellInHole;
        _context.Entities.DungeonEssenceTriggered += Get<DungeonEssenceEvent>().Begin;
        _context.Entities.WallRetractionTriggered += Get<WallRetractionEvent>().Begin;
        Get<SeaFairyEvent>().BeginWhiteFade = Get<CleanSeasEvent>().BeginFade;
        Get<SeaFairyEvent>().BeginCleanSeas = Get<CleanSeasEvent>().Begin;
        var palaceBoot = Get<PalaceBootEvent>();
        _context.Entities.PalaceBootTriggered += palaceBoot.Begin;
        _context.Entities.PalacePassageTriggered += Get<PalacePassageEvent>().Begin;
        _context.Entities.BeginPalacePossessionFade = Get<PalacePossessionEvent>().Fade;
        _context.Entities.PalacePossessionPaletteActive = () => Get<PalacePossessionEvent>().PaletteActive;
        _context.Entities.PalaceRescueTriggered += Get<PalacePossessionEvent>().BeginRescue;
        _context.Entities.InitializePirateCaptain = Get<PirateCaptainEvent>().InitializeCaptain;
        _context.Entities.UpdatePirateCaptain = Get<PirateCaptainEvent>().AdvanceInteraction;
        _context.Entities.NayruRescueWarpTriggered += Get<PalacePossessionEvent>().RequestWarp;
        palaceBoot.PrepareEntrance = () => Get<DekuForestPalaceEvent>().Cancel();
        palaceBoot.AdvanceEntrance = () =>
        {
            if (palaceBoot.State == 2 && !Get<DekuForestPalaceEvent>().HasState)
                Get<DekuForestPalaceEvent>().Start(_context.Rooms.CurrentRoom);
            if (Get<DekuForestPalaceEvent>().HasState) Get<DekuForestPalaceEvent>().UpdateFrame();
        };
    }

    public bool Active => _eventsByPriority.Any(roomEvent => roomEvent.BlocksGameplay);
    internal bool OwnsGameLogic => _eventsByPriority.Any(roomEvent => roomEvent.OwnsGameLogic);
    internal bool StatusBarUpdatesDisabled=>_eventsByPriority.Any(roomEvent=>roomEvent.HasState&&roomEvent.SuppressesStatusBar);
    internal bool NormalMenuDispatchSuppressed=>_eventsByPriority.Any(roomEvent=>roomEvent.HasState&&roomEvent.SuppressesNormalMenuDispatch);
    internal bool CameraUpdatesDisabled => Get<WallRetractionEvent>().CameraUpdatesDisabled || Get<PostD7Event>().HasState;
    internal bool NativeCutsceneActive => Get<BlackTowerEndingEvent>().HasState || Get<BlackTowerEscapeAttemptEvent>().HasState || Get<MakuSeedEvent>().OwnsGameLogic || Get<WallRetractionEvent>().HasState || Get<PalaceBootEvent>().HasState || Get<PalacePassageEvent>().HasState || Get<NayruRescueWarpEvent>().HasState || Get<JabuExteriorEvent>().Opening || Get<CleanSeasEvent>().NativeActive || Get<PostD7Event>().NativeActive;
    internal void PublishNativeGameInput()=>Get<BlackTowerEndingEvent>().PublishGameKeys();
    internal bool AdvanceNativeCutscenesBeforeObjects()
    {
        var retraction=Get<WallRetractionEvent>();
        var boot=Get<PalaceBootEvent>();
        var passage=Get<PalacePassageEvent>();
        var rescueWarp=Get<NayruRescueWarpEvent>();
        var jabu=Get<JabuExteriorEvent>();
        var cleanSeas=Get<CleanSeasEvent>();
        var postD7 = Get<PostD7Event>();
        var makuSeed = Get<MakuSeedEvent>();
        bool owned=Get<BlackTowerEndingEvent>().HasState || Get<BlackTowerEscapeAttemptEvent>().HasState || makuSeed.OwnsGameLogic || retraction.HasState || boot.HasState || passage.HasState || rescueWarp.HasState || jabu.Opening || cleanSeas.NativeActive || postD7.NativeActive;
        retraction.AdvanceBeforeObjects();
        boot.AdvanceBeforeObjects();
        passage.AdvanceBeforeObjects();
        Get<PalacePossessionEvent>().AdvanceBeforeObjects();
        Get<BlackTowerEscapeAttemptEvent>().AdvanceBeforeObjects();
        Get<BlackTowerEndingEvent>().AdvanceBeforeObjects();
        Get<BlackTowerSeedEvent>().AdvanceBeforeObjects();
        Get<BlackTowerConfrontationEvent>().AdvanceBeforeObjects();
        rescueWarp.AdvanceBeforeObjects();
        jabu.AdvanceBeforeObjects();
        cleanSeas.AdvanceBeforeObjects();
        postD7.AdvanceBeforeObjects();
        makuSeed.AdvanceBeforeObjects();
        Get<WildTokayGameEvent>().AdvanceBeforeObjects();
        return owned || rescueWarp.HasState || makuSeed.OwnsGameLogic;
    }
    internal bool DisablesLink => _eventsByPriority.Any(roomEvent => roomEvent.DisablesLink);
    internal bool ObjectUpdateSuspended => _eventsByPriority.Any(roomEvent => roomEvent.ObjectUpdateSuspended);
    internal bool SkipsObjectUpdate => _eventsByPriority.Any(roomEvent => roomEvent.SkipsObjectUpdate);
    internal void ResumeObjectUpdate() =>
        _eventsByPriority.Single(roomEvent => roomEvent.ObjectUpdateSuspended).ResumeObjectUpdate();
    internal void UpdateSpecialObjects()
    {
        foreach (IRoomEvent roomEvent in _eventsByPriority)
            if (roomEvent.HasState) roomEvent.UpdateSpecialObjectFrame();
    }
    internal bool FreezesNonInteractionObjects =>
        _eventsByPriority.Any(roomEvent => roomEvent.FreezesNonInteractionObjects);
    private bool HasEventState => _eventsByPriority.Any(roomEvent => roomEvent.HasState);
    internal T Get<T>() where T : class, IRoomEvent => (T)_eventsByType[typeof(T)];

    internal IReadOnlyList<NpcInteractionHandler> InteractionHandlers =>
        _interactionHandlers;
    internal void SetBraceletActions(
        Action<bool> interrupter,
        Action advance) =>
        _context.SetBraceletActions(interrupter, advance);
    internal void SetFileActions(Func<SaveResult> save,Action reset)
    {_context.SaveFile=save;_context.ResetGame=reset;}
    internal void NotifyBraceletTileLifted(BraceletTileLifted lifted) =>
        Get<WingDungeonCollapseEvent>().OnTileLifted(lifted);
    internal void NotifyBraceletTileLiftCompleted(BraceletTileLifted lifted) =>
        Get<WingDungeonCollapseEvent>().OnTileLiftCompleted(lifted);
    internal void NotifyObjectFellInHole(ObjectFellInHoleKind kind) =>
        Get<ToiletHandEvent>().OnObjectFellInHole(kind);
    internal void SetRingMenuOpener(Func<RingMenuMode, Action, bool> opener) =>
        Get<VasuShopEvent>().SetRingMenuOpener(opener);
    internal void SetSecretMenuOpener(Func<int, Action<bool>, bool> opener)
    {
        Get<WildTokayGameEvent>().SetSecretMenuOpener(opener);
        Get<SymmetryEvent>().OpenSecretMenu = opener;
        Get<PlenEvent>().OpenSecretMenu = opener;
        Get<MamamuEvent>().OpenSecretMenu = opener;
        Get<GoronCaveEvent>().OpenSecretMenu = opener;
        Get<KingZoraEvent>().OpenSecretMenu = opener;
        Get<LibraryEvent>().OpenSecretMenu = opener;
    }
    internal bool SupportsOverworldKeyhole(int group, int room) =>
        Get<GraveyardGateEvent>().CanTrigger(group, room) ||
        Get<CrownDungeonEntranceEvent>().CanTrigger(group, room) ||
        Get<MermaidsCaveEntranceEvent>().CanTrigger(group, room) ||
        Get<LibraryKeyholeEvent>().CanTrigger(group, room);
    internal void TriggerOverworldKeyhole(int group, int room)
    {
        if (Get<CrownDungeonEntranceEvent>().CanTrigger(group, room))
            Get<CrownDungeonEntranceEvent>().Trigger(group, room);
        else if (Get<MermaidsCaveEntranceEvent>().CanTrigger(group, room))
            Get<MermaidsCaveEntranceEvent>().Trigger(group, room);
        else if (Get<LibraryKeyholeEvent>().CanTrigger(group, room))
            Get<LibraryKeyholeEvent>().Trigger(group, room);
        else
            Get<GraveyardGateEvent>().Trigger(group, room);
    }
    internal bool ScreenTransitionsDisabled =>
        _eventsByPriority.Any(roomEvent => roomEvent.ScreenTransitionsDisabled);
    internal bool AllScreenTransitionsDisabled =>
        _eventsByPriority.Any(roomEvent => roomEvent.AllScreenTransitionsDisabled);
    internal bool MenusDisabled =>
        _eventsByPriority.Any(roomEvent => roomEvent.MenusDisabled);
    internal ICutsceneCommandTraceSink? CommandTraceSink
    {
        set => _context.CommandTraceSink = value;
    }

    // GameRoot's ApplicationFixedUpdateScheduler owns elapsed time.
    public void UpdateFrame() => UpdateFrame(inObjectPass: false);

    internal void UpdateInteractionPhase() => UpdateFrame(inObjectPass: true);

    internal void UpdatePartPhase()
    {
        if (!_context.Transitions.IsTransitioning &&
            UpdateOwner is { UpdatesInObjectPhases: true } owner)
            owner.UpdatePartFrame();
    }

    private void UpdateFrame(bool inObjectPass)
    {
        if (_context.Transitions.IsTransitioning)
        {
            if (inObjectPass) return;
            Get<ImpaIntroEvent>().InitializeEncounterInteraction();
            Get<GoronCaveEvent>().InitializeDuringTransition();
            Get<FairiesWoodsEvent>().InitializeDuringTransition();
            Get<CompanionForestEvent>().InitializeDuringTransition();
            Get<TokayTheftEvent>().InitializeDuringTransition();
            Get<DefeatedMoblinEvent>().UpdateDuringWarpArrival();
            if (Get<ImpaIntroEvent>().UpdatesDuringTransition)
                Get<ImpaIntroEvent>().UpdateDuringTransition();
            return;
        }

        IRoomEvent? owner = UpdateOwner;
        if (owner is null || owner.UpdatesInObjectPhases != inObjectPass) return;
        if (_context.DialogueOpen)
            (owner as IUpdatesDuringDialogueRoomEvent)?.UpdateDuringDialogueFrame();
        else
            owner?.UpdateFrame();
    }

    /// <summary>
    /// Destination interactions continue during TRANSITION_DEST_TIMEWARP.
    /// Only the room $1:$39 entry event currently needs that overlap.
    /// </summary>
    public void UpdateDuringTimeWarpFrame()
    {
        if (Get<EnterPastEvent>().HasState)
            Get<EnterPastEvent>().UpdateFrame();
    }

    private IRoomEvent? UpdateOwner => SelectUpdateOwner(
        _eventsByPriority, _context.Rooms.ActiveGroup, _context.Rooms.CurrentRoom.Id);

    internal static IRoomEvent? SelectUpdateOwner(
        IReadOnlyList<IRoomEvent> events, int group, int room)
    {
        IRoomEvent? owner = null;
        foreach (IRoomEvent candidate in events)
        {
            if (!candidate.HasState)
                continue;
            if (owner is null || candidate.OwnsUpdatesOf(owner))
                owner = candidate;
            else if (!owner.OwnsUpdatesOf(candidate))
                throw new InvalidOperationException(
                    $"Room {group:x}:{room:x2} has competing event update owners " +
                    $"{owner.GetType().Name} and {candidate.GetType().Name}; " +
                    "coordinate their source update order explicitly.");
        }
        return owner;
    }

    private void OnRoomEntitiesLoaded(int group, OracleRoomData room)
    {
        _context.Transitions.CancelRequestedNativeWarp();
        ImpaIntroEvent impa = Get<ImpaIntroEvent>();
        NayruIntroEvent nayru = Get<NayruIntroEvent>();
        Get<TingleEvent>().OnRoomLoaded(group, room);
        Get<WingDungeonCollapseEvent>().RestoreCollapsedEntrance(group, room);
        Get<CrownDungeonEntranceEvent>().RestoreEntrance(group, room);
        Get<FairiesWoodsEvent>().OnRoomLoaded(group, room);
        Get<CompanionForestEvent>().OnRoomLoaded(group, room);
        Get<GraveyardGateEvent>().RetireCompletedControllerOnRoomLoad();
        nayru.RestoreCompletedPortal(group, room);
        // The placed $31:$00 Impa object shares room $0:$6a with Ricky's
        // later $71:$03 controller. Apply Impa's completed-room suppression
        // before a higher-priority room event can claim the entry update.
        impa.SuppressPlacedActorIfCompleted(group, room);
        foreach (IRoomEvent roomEvent in _eventsByPriority)
            roomEvent.ReleaseOutgoingActors(group, room);
        if (nayru.Matches(group, room) && !nayru.IntroCompleted)
        {
            impa.TryTransferToRoom(group, room);
            nayru.Start(room);
            return;
        }
        if (impa.TryTransferToRoom(group, room))
            return;
        if (HasEventState)
            CancelAll();

        Get<WildTokayGameEvent>().OnRoomLoaded(group, room);

        foreach (IRoomEvent roomEvent in _eventsByPriority)
        {
            if (roomEvent is not IRoomEntryEvent entryEvent ||
                !entryEvent.Matches(group, room))
            {
                continue;
            }

            entryEvent.Start(room);
            return;
        }
    }

    private void CancelAll()
    {
        foreach (IRoomEvent roomEvent in _eventsByPriority)
            roomEvent.Cancel();
    }
}
