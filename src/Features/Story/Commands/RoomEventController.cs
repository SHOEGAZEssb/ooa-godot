using Godot;
using System;

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
    private readonly PoeEvent _poe;
    private readonly RalphPortalEvent _ralph;
    private readonly PreBlackTowerEvent _preBlackTower;
    private readonly BlackTowerDoorwayEvent _blackTowerDoorway;
    private readonly BlackTowerEntranceEvent _blackTowerEntrance;
    private readonly EnterPastEvent _enterPast;
    private readonly GraveyardGateEvent _graveyardGate;
    private readonly GraveyardGhostKidsEvent _graveyardGhostKids;
    private readonly ImpaIntroEvent _impa;
    private readonly NayruIntroEvent _nayru;
    private readonly MakuSproutRescueEvent _makuSproutRescue;
    private readonly LynnaShopEvent _lynnaShop;
    private readonly VasuShopEvent _vasuShop;
    private readonly HarpOfAgesEvent _harpOfAges;
    private readonly SpiritsGraveEssenceEvent _spiritsGraveEssence;
    private readonly RemoteMakuFirstEssenceEvent _remoteMakuFirstEssence;
    private readonly RemoteMakuHarpEvent _remoteMakuHarp;
    private readonly RemoteMakuWingDungeonEvent _remoteMakuWingDungeon;
    private readonly FairiesWoodsEvent _fairiesWoods;
    private readonly WingDungeonCollapseEvent _wingDungeonCollapse;
    private readonly IRoomEvent[] _eventsByPriority;
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
        _poe = new PoeEvent(_context);
        _ralph = new RalphPortalEvent(_context);
        _preBlackTower = new PreBlackTowerEvent(_context);
        _blackTowerDoorway = new BlackTowerDoorwayEvent(_context);
        _blackTowerEntrance = new BlackTowerEntranceEvent(_context);
        _enterPast = new EnterPastEvent(_context);
        _graveyardGate = new GraveyardGateEvent(_context);
        _graveyardGhostKids = new GraveyardGhostKidsEvent(_context);
        _impa = new ImpaIntroEvent(_context);
        _nayru = new NayruIntroEvent(_context, _impa);
        _makuSproutRescue = new MakuSproutRescueEvent(_context);
        _lynnaShop = new LynnaShopEvent(_context);
        _vasuShop = new VasuShopEvent(_context);
        _harpOfAges = new HarpOfAgesEvent(_context);
        _spiritsGraveEssence = new SpiritsGraveEssenceEvent(_context);
        _remoteMakuFirstEssence = new RemoteMakuFirstEssenceEvent(_context);
        _remoteMakuHarp = new RemoteMakuHarpEvent(_context);
        _remoteMakuWingDungeon = new RemoteMakuWingDungeonEvent(_context);
        _fairiesWoods = new FairiesWoodsEvent(_context);
        _wingDungeonCollapse = new WingDungeonCollapseEvent(
            _context,
            () => _remoteMakuWingDungeon.StartWarning());
        _eventsByPriority =
        [
            _harpOfAges,
            _spiritsGraveEssence,
            _remoteMakuFirstEssence,
            _remoteMakuHarp,
            _remoteMakuWingDungeon,
            _fairiesWoods,
            _wingDungeonCollapse,
            _nayru,
            _graveyardGate,
            _makuSproutRescue,
            _lynnaShop,
            _vasuShop,
            _shootingGallery,
            _comedian,
            _maskSalesman,
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
        entities.RoomEntitiesLoaded += OnRoomEntitiesLoaded;
        entities.SpiritsGraveEssenceTriggered += _spiritsGraveEssence.Begin;
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
    internal PoeEvent Poe => _poe;
    internal RalphPortalEvent Ralph => _ralph;
    internal PreBlackTowerEvent PreBlackTower => _preBlackTower;
    internal BlackTowerDoorwayEvent BlackTowerDoorway => _blackTowerDoorway;
    internal BlackTowerEntranceEvent BlackTowerEntrance => _blackTowerEntrance;
    internal EnterPastEvent EnterPast => _enterPast;
    internal GraveyardGateEvent GraveyardGate => _graveyardGate;
    internal GraveyardGhostKidsEvent GraveyardGhostKids => _graveyardGhostKids;
    internal ImpaIntroEvent Impa => _impa;
    internal NayruIntroEvent Nayru => _nayru;
    internal MakuSproutRescueEvent MakuSproutRescue => _makuSproutRescue;
    internal LynnaShopEvent LynnaShop => _lynnaShop;
    internal VasuShopEvent VasuShop => _vasuShop;
    internal HarpOfAgesEvent HarpOfAges => _harpOfAges;
    internal SpiritsGraveEssenceEvent SpiritsGraveEssence => _spiritsGraveEssence;
    internal RemoteMakuFirstEssenceEvent RemoteMakuFirstEssence =>
        _remoteMakuFirstEssence;
    internal RemoteMakuHarpEvent RemoteMakuHarp => _remoteMakuHarp;
    internal RemoteMakuWingDungeonEvent RemoteMakuWingDungeon =>
        _remoteMakuWingDungeon;
    internal FairiesWoodsEvent FairiesWoods => _fairiesWoods;
    internal WingDungeonCollapseEvent WingDungeonCollapse =>
        _wingDungeonCollapse;
    internal void SetBraceletActions(
        Action<bool> interrupter,
        Action advance) =>
        _context.SetBraceletActions(interrupter, advance);
    internal void NotifyBraceletTileLifted(BraceletTileLifted lifted) =>
        _wingDungeonCollapse.OnTileLifted(lifted);
    internal void NotifyBraceletTileLiftCompleted(BraceletTileLifted lifted) =>
        _wingDungeonCollapse.OnTileLiftCompleted(lifted);
    internal void SetRingMenuOpener(Func<RingMenuMode, Action, bool> opener) =>
        _vasuShop.SetRingMenuOpener(opener);
    internal bool SupportsOverworldKeyhole(int group, int room) =>
        _graveyardGate.CanTrigger(group, room);
    internal void TriggerOverworldKeyhole(int group, int room) =>
        _graveyardGate.Trigger(group, room);
    internal bool ScreenTransitionsDisabled =>
        _makuSproutRescue.ScreenTransitionsDisabled ||
        _fairiesWoods.ScreenTransitionsDisabled;
    internal bool MenusDisabled => _shootingGallery.MenusDisabled;
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

    public bool TryInteractNpc(NpcCharacter npc) =>
        _fairiesWoods.TryInteractNpc(npc) ||
        _lynnaShop.TryInteractNpc(npc) ||
        _vasuShop.TryInteractNpc(npc) ||
        _shootingGallery.TryInteractNpc(npc) ||
        _nayru.TryInteractNpc(npc) ||
        _blackTowerEntrance.TryInteractNpc(npc) ||
        _makuSproutRescue.TryInteractNpc(npc) ||
        _makuTreeSaved.TryInteractNpc(npc) ||
        _maskSalesman.TryInteractNpc(npc) ||
        _poe.TryInteractNpc(npc) ||
        _comedian.TryInteractNpc(npc);

    public bool TryInteractPlayer(Player player) =>
        _lynnaShop.TryInteractPlayer(player);

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
        _wingDungeonCollapse.RestoreCollapsedEntrance(group, room);
        _fairiesWoods.OnRoomLoaded(group, room);
        _graveyardGate.RetireCompletedControllerOnRoomLoad();
        _nayru.RestoreCompletedPortal(group, room);
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
