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
    private readonly IRoomEvent[] _eventsByPriority;
    private readonly NpcInteractionHandler[] _interactionHandlers;
    private readonly Dictionary<Type, IRoomEvent> _eventsByType;

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
        Camera2D roomCamera)
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
        var tokayInteractions = new TokayInteractionDatabase();
        var impa = new ImpaIntroEvent(_context);
        var remoteMakuThirdEssence = new RemoteMakuThirdEssenceEvent(_context);
        // Entry precedence is explicit. Construction and typed lookup share this
        // one registration; A-button routing below has its own source order.
        _eventsByPriority =
        [
            new HarpOfAgesEvent(_context),
            new DungeonEssenceEvent(_context),
            new RemoteMakuFirstEssenceEvent(_context),
            new RemoteMakuSecondEssenceEvent(_context),
            new RemoteMakuHarpEvent(_context),
            new RemoteMakuWingDungeonEvent(_context),
            new PostD3RemoteMakuEvent(_context, remoteMakuThirdEssence),
            remoteMakuThirdEssence,
            new CompanionForestEvent(_context),
            new FairiesWoodsEvent(_context),
            new WingDungeonCollapseEvent(
                _context, () => Get<RemoteMakuWingDungeonEvent>().StartWarning()),
            new NayruIntroEvent(_context, impa),
            new GraveyardGateEvent(_context),
            new RickyGlovesEvent(_context),
            new TingleEvent(_context),
            new CarpenterEvent(_context),
            new SymmetryEvent(_context),
            new PatchEvent(_context),
            new MooshRescueEvent(_context),
            new MakuSproutRescueEvent(_context),
            new DekuForestSoldierEvent(_context),
            new DekuForestPalaceEvent(_context),
            new BusinessScrubEvent(_context),
            new LynnaShopEvent(_context),
            new VasuShopEvent(_context),
            new ShootingGalleryEvent(_context),
            new ComedianEvent(_context),
            new MaskSalesmanEvent(_context),
            new DumbbellManEvent(_context),
            new TokkeyEvent(_context),
            new ChevalEvent(_context),
            new RalphAfterChevalEvent(_context),
            new RalphAfterRaftonEvent(_context),
            new RaftwreckEvent(_context),
            new TokayTheftEvent(_context),
            new TokayCookEvent(_context, tokayInteractions),
            new TokayHoldingItemEvent(_context, tokayInteractions),
            new TokayRunningFromRosaEvent(_context, tokayInteractions),
            new TokayDimitriEvent(_context, tokayInteractions),
            new TokaySeedlingPlotEvent(
                _context, tokayInteractions, new TokaySeedlingPlotDatabase()),
            new TokayShieldUpgradeEvent(_context, tokayInteractions),
            new TokayVineExplanationEvent(_context, tokayInteractions),
            new RosaShovelEvent(_context, tokayInteractions),
            new TokayTradingEvent(_context, tokayInteractions, new TokayShopDatabase()),
            new WildTokayGameEvent(_context, tokayInteractions, new WildTokayGameDatabase()),
            new RaftonEvent(_context),
            new DepressedBoyEvent(_context),
            new ToiletHandEvent(_context),
            new PoeEvent(_context),
            new MakuTreeSavedEvent(_context),
            new MakuTreeDisappearanceEvent(_context),
            new RalphPortalEvent(_context),
            new PreBlackTowerEvent(_context),
            new BlackTowerDoorwayEvent(_context),
            new BlackTowerEntranceEvent(_context),
            new EnterPastEvent(_context),
            new GraveyardGhostKidsEvent(_context),
            impa,
        ];
        _eventsByType = _eventsByPriority.ToDictionary(roomEvent => roomEvent.GetType());
        _context.NativeDialogueScreen = () =>
            (UpdateOwner as IRoomEventDialogueContext)?.DialogueScreen;
        static NpcInteractionHandler Npc(
            string source, params Func<NpcCharacter, bool>[] handlers) =>
            NpcInteractionHandler.ForNpc(source, (target, _) =>
                Array.Exists(handlers, handler => handler(target.Npc)));
        _interactionHandlers =
        [
            Npc("symmetryNpc.s:scriptTable",
                Get<SymmetryEvent>().TryInteractNpc),
            Npc("patch.s:interactionCode94", Get<PatchEvent>().TryInteractNpc),
            Npc("carpenter.s:room025Scripts",
                Get<CarpenterEvent>().TryInteractNpc),
            Npc("forestFairy.s:forestFairy_discovered",
                Get<FairiesWoodsEvent>().TryInteractNpc),
            Npc("shopkeeper.s:lynnaShop:npc",
                Get<LynnaShopEvent>().TryInteractNpc),
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
                Get<TokayHoldingItemEvent>().TryInteractNpc,
                Get<WildTokayGameEvent>().TryInteractNpc,
                Get<TokayTradingEvent>().TryInteractNpc,
                Get<TokayDimitriEvent>().TryInteractNpc,
                Get<TokaySeedlingPlotEvent>().TryInteractNpc,
                Get<TokayShieldUpgradeEvent>().TryInteractNpc,
                Get<TokayVineExplanationEvent>().TryInteractNpc,
                Get<RosaShovelEvent>().TryInteractNpc),
            Npc("makuTree.s:interactionCode87Subid02",
                Get<MakuTreeSavedEvent>().TryInteractNpc),
            Npc("maskSalesman.s:maskSalesmanScript",
                Get<MaskSalesmanEvent>().TryInteractNpc),
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
                "tokayShopItem.s:interactionCode81",
                Get<TokayTradingEvent>().TryInteractPlayer)
        ];
        entities.RoomEntitiesLoaded += OnRoomEntitiesLoaded;
        entities.ObjectFellInHole += NotifyObjectFellInHole;
        entities.DungeonEssenceTriggered += Get<DungeonEssenceEvent>().Begin;
    }

    public bool Active => _eventsByPriority.Any(roomEvent => roomEvent.BlocksGameplay);
    private bool HasEventState => _eventsByPriority.Any(roomEvent => roomEvent.HasState);
    internal T Get<T>() where T : class, IRoomEvent => (T)_eventsByType[typeof(T)];

    internal IReadOnlyList<NpcInteractionHandler> InteractionHandlers =>
        _interactionHandlers;
    internal void SetBraceletActions(
        Action<bool> interrupter,
        Action advance) =>
        _context.SetBraceletActions(interrupter, advance);
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
    }
    internal bool SupportsOverworldKeyhole(int group, int room) =>
        Get<GraveyardGateEvent>().CanTrigger(group, room);
    internal void TriggerOverworldKeyhole(int group, int room) =>
        Get<GraveyardGateEvent>().Trigger(group, room);
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
    public void UpdateFrame()
    {
        if (_context.Transitions.IsTransitioning)
        {
            if (Get<ImpaIntroEvent>().UpdatesDuringTransition)
                Get<ImpaIntroEvent>().UpdateDuringTransition();
            return;
        }

        IRoomEvent? owner = UpdateOwner;
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
        ImpaIntroEvent impa = Get<ImpaIntroEvent>();
        NayruIntroEvent nayru = Get<NayruIntroEvent>();
        Get<TingleEvent>().OnRoomLoaded(group, room);
        Get<WingDungeonCollapseEvent>().RestoreCollapsedEntrance(group, room);
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
