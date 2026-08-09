using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Selects and schedules room-entry events. Event-specific state and behavior
/// live in dedicated implementations; this class only coordinates their room
/// lifecycle, update priority, and externally visible gameplay blocking.
/// </summary>
public sealed class RoomEventController
{
    private readonly RoomEventContext _context;
    private readonly MakuTreeDisappearanceEvent _makuTree;
    private readonly MakuTreeSavedEvent _makuTreeSaved;
    private readonly ShootingGalleryEvent _shootingGallery;
    private readonly ComedianEvent _comedian;
    private readonly MaskSalesmanEvent _maskSalesman;
    private readonly ChevalEvent _cheval;
    private readonly RalphAfterChevalEvent _ralphAfterCheval;
    private readonly RalphAfterRaftonEvent _ralphAfterRafton;
    private readonly RaftonEvent _rafton;
    private readonly RaftwreckEvent _raftwreck;
    private readonly TokayTheftEvent _tokayTheft;
    private readonly TokayCookEvent _tokayCook;
    private readonly TokayHoldingItemEvent _tokayHoldingItem;
    private readonly TokayRunningFromRosaEvent _tokayRunningFromRosa;
    private readonly TokayDimitriEvent _tokayDimitri;
    private readonly TokaySeedlingPlotEvent _tokaySeedlingPlot;
    private readonly TokayShieldUpgradeEvent _tokayShieldUpgrade;
    private readonly TokayVineExplanationEvent _tokayVineExplanation;
    private readonly RosaShovelEvent _rosaShovel;
    private readonly TokayTradingEvent _tokayTrading;
    private readonly WildTokayGameEvent _wildTokayGame;
    private readonly DepressedBoyEvent _depressedBoy;
    private readonly ToiletHandEvent _toiletHand;
    private readonly PoeEvent _poe;
    private readonly RalphPortalEvent _ralph;
    private readonly PreBlackTowerEvent _preBlackTower;
    private readonly BlackTowerDoorwayEvent _blackTowerDoorway;
    private readonly BlackTowerEntranceEvent _blackTowerEntrance;
    private readonly EnterPastEvent _enterPast;
    private readonly GraveyardGateEvent _graveyardGate;
    private readonly GraveyardGhostKidsEvent _graveyardGhostKids;
    private readonly RickyGlovesEvent _rickyGloves;
    private readonly TingleEvent _tingle;
    private readonly MooshRescueEvent _mooshRescue;
    private readonly ImpaIntroEvent _impa;
    private readonly NayruIntroEvent _nayru;
    private readonly MakuSproutRescueEvent _makuSproutRescue;
    private readonly DekuForestSoldierEvent _dekuForestSoldier;
    private readonly DekuForestPalaceEvent _dekuForestPalace;
    private readonly BusinessScrubEvent _businessScrub;
    private readonly LynnaShopEvent _lynnaShop;
    private readonly VasuShopEvent _vasuShop;
    private readonly HarpOfAgesEvent _harpOfAges;
    private readonly DungeonEssenceEvent _dungeonEssence;
    private readonly RemoteMakuFirstEssenceEvent _remoteMakuFirstEssence;
    private readonly RemoteMakuSecondEssenceEvent _remoteMakuSecondEssence;
    private readonly RemoteMakuHarpEvent _remoteMakuHarp;
    private readonly RemoteMakuWingDungeonEvent _remoteMakuWingDungeon;
    private readonly FairiesWoodsEvent _fairiesWoods;
    private readonly WingDungeonCollapseEvent _wingDungeonCollapse;
    private readonly IRoomEvent[] _eventsByPriority;
    private readonly NpcInteractionHandler[] _interactionHandlers;
    private double _frameAccumulator;
    private double _transitionFrameAccumulator;

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
        _makuTree = new MakuTreeDisappearanceEvent(_context);
        _makuTreeSaved = new MakuTreeSavedEvent(_context);
        _shootingGallery = new ShootingGalleryEvent(_context);
        _comedian = new ComedianEvent(_context);
        _maskSalesman = new MaskSalesmanEvent(_context);
        _cheval = new ChevalEvent(_context);
        _ralphAfterCheval = new RalphAfterChevalEvent(_context);
        _ralphAfterRafton = new RalphAfterRaftonEvent(_context);
        _rafton = new RaftonEvent(_context);
        _raftwreck = new RaftwreckEvent(_context);
        _tokayTheft = new TokayTheftEvent(_context);
        var tokayIslandDatabase = new TokayIslandDatabase();
        _tokayCook = new TokayCookEvent(_context, tokayIslandDatabase);
        _tokayHoldingItem = new TokayHoldingItemEvent(_context, tokayIslandDatabase);
        _tokayRunningFromRosa = new TokayRunningFromRosaEvent(
            _context, tokayIslandDatabase);
        _tokayDimitri = new TokayDimitriEvent(_context, tokayIslandDatabase);
        _tokaySeedlingPlot = new TokaySeedlingPlotEvent(
            _context, tokayIslandDatabase);
        _tokayShieldUpgrade = new TokayShieldUpgradeEvent(
            _context, tokayIslandDatabase);
        _tokayVineExplanation = new TokayVineExplanationEvent(
            _context, tokayIslandDatabase);
        _rosaShovel = new RosaShovelEvent(_context, tokayIslandDatabase);
        _tokayTrading = new TokayTradingEvent(_context, tokayIslandDatabase);
        _wildTokayGame = new WildTokayGameEvent(_context, tokayIslandDatabase);
        _depressedBoy = new DepressedBoyEvent(_context);
        _toiletHand = new ToiletHandEvent(_context);
        _poe = new PoeEvent(_context);
        _ralph = new RalphPortalEvent(_context);
        _preBlackTower = new PreBlackTowerEvent(_context);
        _blackTowerDoorway = new BlackTowerDoorwayEvent(_context);
        _blackTowerEntrance = new BlackTowerEntranceEvent(_context);
        _enterPast = new EnterPastEvent(_context);
        _graveyardGate = new GraveyardGateEvent(_context);
        _graveyardGhostKids = new GraveyardGhostKidsEvent(_context);
        _rickyGloves = new RickyGlovesEvent(_context);
        _tingle = new TingleEvent(_context);
        _mooshRescue = new MooshRescueEvent(_context);
        _impa = new ImpaIntroEvent(_context);
        _nayru = new NayruIntroEvent(_context, _impa);
        _makuSproutRescue = new MakuSproutRescueEvent(_context);
        _dekuForestSoldier = new DekuForestSoldierEvent(_context);
        _dekuForestPalace = new DekuForestPalaceEvent(_context);
        _businessScrub = new BusinessScrubEvent(_context);
        _lynnaShop = new LynnaShopEvent(_context);
        _vasuShop = new VasuShopEvent(_context);
        _harpOfAges = new HarpOfAgesEvent(_context);
        _dungeonEssence = new DungeonEssenceEvent(_context);
        _remoteMakuFirstEssence = new RemoteMakuFirstEssenceEvent(_context);
        _remoteMakuSecondEssence = new RemoteMakuSecondEssenceEvent(_context);
        _remoteMakuHarp = new RemoteMakuHarpEvent(_context);
        _remoteMakuWingDungeon = new RemoteMakuWingDungeonEvent(_context);
        _fairiesWoods = new FairiesWoodsEvent(_context);
        _wingDungeonCollapse = new WingDungeonCollapseEvent(
            _context,
            () => _remoteMakuWingDungeon.StartWarning());
        _eventsByPriority =
        [
            _harpOfAges,
            _dungeonEssence,
            _remoteMakuFirstEssence,
            _remoteMakuSecondEssence,
            _remoteMakuHarp,
            _remoteMakuWingDungeon,
            _fairiesWoods,
            _wingDungeonCollapse,
            _nayru,
            _graveyardGate,
            _rickyGloves,
            _tingle,
            _mooshRescue,
            _makuSproutRescue,
            _dekuForestSoldier,
            _dekuForestPalace,
            _businessScrub,
            _lynnaShop,
            _vasuShop,
            _shootingGallery,
            _comedian,
            _maskSalesman,
            _cheval,
            _ralphAfterCheval,
            _ralphAfterRafton,
            _raftwreck,
            _tokayTheft,
            _tokayCook,
            _tokayHoldingItem,
            _tokayRunningFromRosa,
            _tokayDimitri,
            _tokaySeedlingPlot,
            _tokayShieldUpgrade,
            _tokayVineExplanation,
            _rosaShovel,
            _tokayTrading,
            _wildTokayGame,
            _rafton,
            _depressedBoy,
            _toiletHand,
            _poe,
            _makuTreeSaved,
            _makuTree,
            _ralph,
            _preBlackTower,
            _blackTowerDoorway,
            _blackTowerEntrance,
            _enterPast,
            _graveyardGhostKids,
            _impa,
        ];
        _interactionHandlers =
        [
            NpcInteractionHandler.ForNpc(
                "forestFairy.s:forestFairy_discovered",
                (target, _) => _fairiesWoods.TryInteractNpc(target.Npc)),
            NpcInteractionHandler.ForNpc(
                "shopkeeper.s:lynnaShop:npc",
                (target, _) => _lynnaShop.TryInteractNpc(target.Npc)),
            NpcInteractionHandler.ForNpc(
                "businessScrub.s:interactionCodece",
                (target, _) => _businessScrub.TryInteractNpc(target.Npc)),
            NpcInteractionHandler.ForNpc(
                "vasu.s+ringHelpBook.s:room2eeActors",
                (target, _) => _vasuShop.TryInteractNpc(target.Npc)),
            NpcInteractionHandler.ForNpc(
                "shootingGallery.s:shootingGalleryScript",
                (target, _) => _shootingGallery.TryInteractNpc(target.Npc)),
            NpcInteractionHandler.ForNpc(
                "miscCutscenes.s:CUTSCENE_NAYRU_SINGING",
                (target, _) => _nayru.TryInteractNpc(target.Npc)),
            NpcInteractionHandler.ForNpc(
                "hardhatWorker.s:blackTowerEntrance",
                (target, _) => _blackTowerEntrance.TryInteractNpc(target.Npc)),
            NpcInteractionHandler.ForNpc(
                "makuSprout.s:interactionCode88",
                (target, _) => _makuSproutRescue.TryInteractNpc(target.Npc)),
            NpcInteractionHandler.ForNpc(
                "companionScripts.s:companionScript_subid00Script",
                (target, _) => _mooshRescue.TryInteractNpc(target.Npc)),
            NpcInteractionHandler.ForNpc(
                "companionScripts.s:companionScript_subid03Script",
                (target, _) => _rickyGloves.TryInteractNpc(target.Npc)),
            NpcInteractionHandler.ForNpc(
                "tingle.s:interactionCodec8; scripts.s:tingleScript",
                (target, _) => _tingle.TryInteractNpc(target.Npc)),
            NpcInteractionHandler.ForNpc(
                "tokay.s:tokayScriptTable; rosa.s:interactionCode68",
                (target, _) => _tokayCook.TryInteractNpc(target.Npc) ||
                    _tokayHoldingItem.TryInteractNpc(target.Npc) ||
                    _wildTokayGame.TryInteractNpc(target.Npc) ||
                    _tokayTrading.TryInteractNpc(target.Npc) ||
                    _tokayDimitri.TryInteractNpc(target.Npc) ||
                    _tokaySeedlingPlot.TryInteractNpc(target.Npc) ||
                    _tokayShieldUpgrade.TryInteractNpc(target.Npc) ||
                    _tokayVineExplanation.TryInteractNpc(target.Npc) ||
                    _rosaShovel.TryInteractNpc(target.Npc)),
            NpcInteractionHandler.ForNpc(
                "makuTree.s:interactionCode87Subid02",
                (target, _) => _makuTreeSaved.TryInteractNpc(target.Npc)),
            NpcInteractionHandler.ForNpc(
                "maskSalesman.s:maskSalesmanScript",
                (target, _) => _maskSalesman.TryInteractNpc(target.Npc)),
            NpcInteractionHandler.ForNpc(
                "cheval.s:interactionCode6a",
                (target, _) => _cheval.TryInteractNpc(target.Npc)),
            NpcInteractionHandler.ForNpc(
                "rafton.s:interactionCode69",
                (target, _) => _rafton.TryInteractNpc(target.Npc)),
            NpcInteractionHandler.ForNpc(
                "boy.s:boySubid07Script",
                (target, _) => _depressedBoy.TryInteractNpc(target.Npc)),
            NpcInteractionHandler.ForNpc(
                "toiletHand.s:toiletHandScript",
                (target, _) => _toiletHand.TryInteractNpc(target.Npc)),
            NpcInteractionHandler.ForNpc(
                "poe.s:poeScript",
                (target, _) => _poe.TryInteractNpc(target.Npc)),
            NpcInteractionHandler.ForNpc(
                "comedian.s:comedianScript",
                (target, _) => _comedian.TryInteractNpc(target.Npc)),
            NpcInteractionHandler.ForNpc(
                "soldier.s:soldierSubid02/07/09",
                (target, _) => _dekuForestPalace.TryInteractNpc(target.Npc)),
            NpcInteractionHandler.ForPlayer(
                "shopkeeper.s:lynnaShop:player",
                _lynnaShop.TryInteractPlayer),
            NpcInteractionHandler.ForPlayer(
                "tokayShopItem.s:interactionCode81",
                _tokayTrading.TryInteractPlayer)
        ];
        entities.RoomEntitiesLoaded += OnRoomEntitiesLoaded;
        entities.ObjectFellInHole += NotifyObjectFellInHole;
        entities.DungeonEssenceTriggered += _dungeonEssence.Begin;
    }

    public bool Active
    {
        get
        {
            foreach (IRoomEvent roomEvent in _eventsByPriority)
            {
                if (roomEvent.BlocksGameplay)
                    return true;
            }
            return false;
        }
    }

    private bool HasEventState
    {
        get
        {
            foreach (IRoomEvent roomEvent in _eventsByPriority)
            {
                if (roomEvent.HasState)
                    return true;
            }
            return false;
        }
    }

    internal MakuTreeDisappearanceEvent MakuTree => _makuTree;
    internal MakuTreeSavedEvent MakuTreeSaved => _makuTreeSaved;
    internal ShootingGalleryEvent ShootingGallery => _shootingGallery;
    internal ComedianEvent Comedian => _comedian;
    internal MaskSalesmanEvent MaskSalesman => _maskSalesman;
    internal ChevalEvent Cheval => _cheval;
    internal RalphAfterChevalEvent RalphAfterCheval => _ralphAfterCheval;
    internal RalphAfterRaftonEvent RalphAfterRafton => _ralphAfterRafton;
    internal RaftonEvent Rafton => _rafton;
    internal RaftwreckEvent Raftwreck => _raftwreck;
    internal TokayTheftEvent TokayTheft => _tokayTheft;
    internal TokayCookEvent TokayCook => _tokayCook;
    internal TokayHoldingItemEvent TokayHoldingItem => _tokayHoldingItem;
    internal TokayRunningFromRosaEvent TokayRunningFromRosa =>
        _tokayRunningFromRosa;
    internal TokayDimitriEvent TokayDimitri => _tokayDimitri;
    internal TokaySeedlingPlotEvent TokaySeedlingPlot => _tokaySeedlingPlot;
    internal TokayShieldUpgradeEvent TokayShieldUpgrade => _tokayShieldUpgrade;
    internal TokayVineExplanationEvent TokayVineExplanation =>
        _tokayVineExplanation;
    internal RosaShovelEvent RosaShovel => _rosaShovel;
    internal TokayTradingEvent TokayTrading => _tokayTrading;
    internal WildTokayGameEvent WildTokayGame => _wildTokayGame;
    internal DepressedBoyEvent DepressedBoy => _depressedBoy;
    internal ToiletHandEvent ToiletHand => _toiletHand;
    internal PoeEvent Poe => _poe;
    internal RalphPortalEvent Ralph => _ralph;
    internal PreBlackTowerEvent PreBlackTower => _preBlackTower;
    internal BlackTowerDoorwayEvent BlackTowerDoorway => _blackTowerDoorway;
    internal BlackTowerEntranceEvent BlackTowerEntrance => _blackTowerEntrance;
    internal EnterPastEvent EnterPast => _enterPast;
    internal GraveyardGateEvent GraveyardGate => _graveyardGate;
    internal GraveyardGhostKidsEvent GraveyardGhostKids => _graveyardGhostKids;
    internal RickyGlovesEvent RickyGloves => _rickyGloves;
    internal TingleEvent Tingle => _tingle;
    internal MooshRescueEvent MooshRescue => _mooshRescue;
    internal ImpaIntroEvent Impa => _impa;
    internal NayruIntroEvent Nayru => _nayru;
    internal MakuSproutRescueEvent MakuSproutRescue => _makuSproutRescue;
    internal DekuForestSoldierEvent DekuForestSoldier =>
        _dekuForestSoldier;
    internal DekuForestPalaceEvent DekuForestPalace =>
        _dekuForestPalace;
    internal BusinessScrubEvent BusinessScrub => _businessScrub;
    internal LynnaShopEvent LynnaShop => _lynnaShop;
    internal VasuShopEvent VasuShop => _vasuShop;
    internal HarpOfAgesEvent HarpOfAges => _harpOfAges;
    internal DungeonEssenceEvent DungeonEssence => _dungeonEssence;
    internal RemoteMakuFirstEssenceEvent RemoteMakuFirstEssence =>
        _remoteMakuFirstEssence;
    internal RemoteMakuSecondEssenceEvent RemoteMakuSecondEssence =>
        _remoteMakuSecondEssence;
    internal RemoteMakuHarpEvent RemoteMakuHarp => _remoteMakuHarp;
    internal RemoteMakuWingDungeonEvent RemoteMakuWingDungeon =>
        _remoteMakuWingDungeon;
    internal FairiesWoodsEvent FairiesWoods => _fairiesWoods;
    internal WingDungeonCollapseEvent WingDungeonCollapse =>
        _wingDungeonCollapse;
    internal IReadOnlyList<NpcInteractionHandler> InteractionHandlers =>
        _interactionHandlers;
    internal void SetBraceletActions(
        Action<bool> interrupter,
        Action advance) =>
        _context.SetBraceletActions(interrupter, advance);
    internal void NotifyBraceletTileLifted(BraceletTileLifted lifted) =>
        _wingDungeonCollapse.OnTileLifted(lifted);
    internal void NotifyBraceletTileLiftCompleted(BraceletTileLifted lifted) =>
        _wingDungeonCollapse.OnTileLiftCompleted(lifted);
    internal void NotifyObjectFellInHole(ObjectFellInHoleKind kind) =>
        _toiletHand.OnObjectFellInHole(kind);
    internal void SetRingMenuOpener(Func<RingMenuMode, Action, bool> opener) =>
        _vasuShop.SetRingMenuOpener(opener);
    internal bool SupportsOverworldKeyhole(int group, int room) =>
        _graveyardGate.CanTrigger(group, room);
    internal void TriggerOverworldKeyhole(int group, int room) =>
        _graveyardGate.Trigger(group, room);
    internal bool ScreenTransitionsDisabled =>
        _makuSproutRescue.ScreenTransitionsDisabled ||
        _fairiesWoods.ScreenTransitionsDisabled ||
        _mooshRescue.ScreenTransitionsDisabled ||
        _wildTokayGame.ScreenTransitionsDisabled;
    internal bool MenusDisabled =>
        _shootingGallery.MenusDisabled ||
        _ralphAfterCheval.MenusDisabled ||
        _ralphAfterRafton.MenusDisabled ||
        _raftwreck.MenusDisabled ||
        _tokayTheft.MenusDisabled ||
        _tokayCook.HasState ||
        _tokayHoldingItem.HasState ||
        _tokayRunningFromRosa.HasState ||
        _tokayDimitri.HasState ||
        _tokaySeedlingPlot.HasState ||
        _tokayShieldUpgrade.HasState ||
        _tokayVineExplanation.HasState ||
        _rosaShovel.HasState ||
        _tokayTrading.HasState ||
        _wildTokayGame.HasState ||
        _dekuForestSoldier.MenusDisabled ||
        _dekuForestPalace.MenusDisabled ||
        _rickyGloves.MenusDisabled;
    internal ICutsceneCommandTraceSink? CommandTraceSink
    {
        set => _context.CommandTraceSink = value;
    }

    public void Update(double delta)
    {
        if (!HasEventState)
            return;

        if (_context.Transitions.IsTransitioning)
        {
            // Following interactions keep updating during room scrolling while
            // ordinary room objects are frozen.
            if (!_impa.UpdatesDuringTransition)
            {
                _transitionFrameAccumulator = 0.0;
                return;
            }

            _transitionFrameAccumulator += delta * 60.0;
            while (_impa.UpdatesDuringTransition && _transitionFrameAccumulator >= 1.0)
            {
                _transitionFrameAccumulator -= 1.0;
                _impa.UpdateDuringTransition();
            }
            return;
        }

        _transitionFrameAccumulator = 0.0;
        _frameAccumulator += delta * 60.0;
        while (HasEventState && _frameAccumulator >= 1.0)
        {
            _frameAccumulator -= 1.0;
            UpdatePrimaryEventFrame();
        }
    }

    /// <summary>
    /// Destination interactions continue updating during TRANSITION_DEST_TIMEWARP.
    /// Only the room $1:$39 entry event currently needs that overlap.
    /// </summary>
    public void UpdateDuringTimeWarp(double delta)
    {
        if (!_enterPast.HasState)
            return;

        _frameAccumulator += delta * 60.0;
        while (_enterPast.HasState && _frameAccumulator >= 1.0)
        {
            _frameAccumulator -= 1.0;
            _enterPast.UpdateFrame();
        }
    }

    private void OnRoomEntitiesLoaded(int group, OracleRoomData room)
    {
        _tingle.OnRoomLoaded(group, room);
        _wingDungeonCollapse.RestoreCollapsedEntrance(group, room);
        _fairiesWoods.OnRoomLoaded(group, room);
        _graveyardGate.RetireCompletedControllerOnRoomLoad();
        _nayru.RestoreCompletedPortal(group, room);
        // The placed $31:$00 Impa object shares room $0:$6a with Ricky's
        // later $71:$03 controller. Apply Impa's completed-room suppression
        // before a higher-priority room event can claim the entry update.
        _impa.SuppressPlacedActorIfCompleted(group, room);
        if (_fairiesWoods.HasState &&
            _context.Entities.ScreenTransitionActive)
        {
            // Dynamic $49:$01 fairies in room $0:$82 have already moved into
            // the outgoing entity set. Release event ownership without hiding
            // them; RoomEntityManager retires them after the scroll finishes.
            _fairiesWoods.Cancel(deactivateDiscoveredActors: false);
        }
        if (_nayru.HasState && !_nayru.Matches(group, room))
        {
            // $6b:$01 recreates its dynamic object list on every pre-intro
            // room entry. Retire the outgoing list while its nodes are still
            // valid, before following Impa's transfer takes the early return.
            _nayru.Cancel(deactivateActors: false);
        }
        if (_dekuForestPalace.HasState &&
            _context.Entities.ScreenTransitionActive)
        {
            // The palace controller restarts for each northward room, but the
            // outgoing interactions remain enabled-$02 objects frozen for the
            // complete scroll. Release command ownership without hiding them;
            // RoomEntityManager retires the outgoing set at the camera handoff.
            _dekuForestPalace.Cancel(deactivateActors: false);
        }
        if (_nayru.Matches(group, room) && !_nayru.IntroCompleted)
        {
            TransferFollowingImpaIfNeeded(group, room);
            _nayru.Start(room);
            ResetClock();
            return;
        }
        if (_impa.CanTransferFollowing)
        {
            TransferFollowingImpaIfNeeded(group, room);
            if (_impa.MatchesStone(group, room))
                _impa.StartStoneRoom();
            return;
        }
        if (HasEventState)
            CancelAll();

        _wildTokayGame.OnRoomLoaded(group, room);

        foreach (IRoomEvent roomEvent in _eventsByPriority)
        {
            if (roomEvent is not IRoomEntryEvent entryEvent ||
                !entryEvent.Matches(group, room))
            {
                continue;
            }

            entryEvent.Start(room);
            ResetClock();
            return;
        }
        if (_impa.MatchesEncounter(group, room))
        {
            _impa.StartEncounter(room);
            ResetClock();
            return;
        }
        if (_impa.MatchesHelp(group, room))
        {
            _impa.StartHelp();
            ResetClock();
            return;
        }
        if (_impa.MatchesStone(group, room))
        {
            _impa.StartStoneRoom();
            ResetClock();
        }
    }

    private void TransferFollowingImpaIfNeeded(int group, OracleRoomData room)
    {
        if (!_impa.CanTransferFollowing)
            return;
        _impa.SuppressPlacedActorIfCompleted(group, room);
        _impa.TransferFollowingActor(group, room);
        if (!_impa.MatchesStone(group, room))
            _impa.LeaveStoneRoom();
    }

    private void CancelAll()
    {
        foreach (IRoomEvent roomEvent in _eventsByPriority)
            roomEvent.Cancel();
        _context.Player.EndCutsceneControl();
        ResetClock();
    }

    private void UpdatePrimaryEventFrame()
    {
        foreach (IRoomEvent roomEvent in _eventsByPriority)
        {
            if (!roomEvent.HasState)
                continue;

            // Event-owned interactions are outside RoomEntityManager but use
            // the same wTextIsActive reduced pass. The callback contains only
            // source-enabled objects or non-object cutscene-handler work.
            if (_context.DialogueOpen)
            {
                if (roomEvent is IUpdatesDuringDialogueRoomEvent alwaysUpdate)
                    alwaysUpdate.UpdateDuringDialogueFrame();
                if (ReferenceEquals(roomEvent, _nayru) &&
                    _nayru.CrowdActive && _impa.Following)
                {
                    // Nayru owns this composite room event, but following
                    // Impa is still a distinct bit-7 interaction.
                    _impa.UpdateFollower();
                }
                return;
            }

            roomEvent.UpdateFrame();
            if (ReferenceEquals(roomEvent, _nayru) &&
                _nayru.CrowdActive && _impa.Following)
            {
                _impa.UpdateFollower();
            }
            return;
        }
    }

    private void ResetClock()
    {
        _frameAccumulator = 0.0;
        _transitionFrameAccumulator = 0.0;
    }
}
