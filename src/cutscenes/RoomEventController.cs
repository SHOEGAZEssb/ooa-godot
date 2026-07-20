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
    private readonly RalphPortalEvent _ralph;
    private readonly PreBlackTowerEvent _preBlackTower;
    private readonly BlackTowerDoorwayEvent _blackTowerDoorway;
    private readonly BlackTowerEntranceEvent _blackTowerEntrance;
    private readonly EnterPastEvent _enterPast;
    private readonly ImpaIntroEvent _impa;
    private readonly NayruIntroEvent _nayru;
    private readonly MakuSproutRescueEvent _makuSproutRescue;
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
        _ralph = new RalphPortalEvent(_context);
        _preBlackTower = new PreBlackTowerEvent(_context);
        _blackTowerDoorway = new BlackTowerDoorwayEvent(_context);
        _blackTowerEntrance = new BlackTowerEntranceEvent(_context);
        _enterPast = new EnterPastEvent(_context);
        _impa = new ImpaIntroEvent(_context);
        _nayru = new NayruIntroEvent(_context, _impa);
        _makuSproutRescue = new MakuSproutRescueEvent(_context);
        _eventsByPriority =
        [
            _nayru,
            _makuSproutRescue,
            _makuTree,
            _ralph,
            _preBlackTower,
            _blackTowerDoorway,
            _blackTowerEntrance,
            _enterPast,
            _impa,
        ];
        entities.RoomEntitiesLoaded += OnRoomEntitiesLoaded;
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
    internal RalphPortalEvent Ralph => _ralph;
    internal PreBlackTowerEvent PreBlackTower => _preBlackTower;
    internal BlackTowerDoorwayEvent BlackTowerDoorway => _blackTowerDoorway;
    internal BlackTowerEntranceEvent BlackTowerEntrance => _blackTowerEntrance;
    internal EnterPastEvent EnterPast => _enterPast;
    internal ImpaIntroEvent Impa => _impa;
    internal NayruIntroEvent Nayru => _nayru;
    internal MakuSproutRescueEvent MakuSproutRescue => _makuSproutRescue;
    internal bool ScreenTransitionsDisabled =>
        _makuSproutRescue.ScreenTransitionsDisabled;
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
        _nayru.TryInteractNpc(npc) ||
        _blackTowerEntrance.TryInteractNpc(npc) ||
        _makuSproutRescue.TryInteractNpc(npc);

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
        _nayru.RestoreCompletedPortal(group, room);
        _makuSproutRescue.RestoreCompletedSprout(group, room);
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
