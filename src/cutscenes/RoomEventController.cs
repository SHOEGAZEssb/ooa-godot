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
    private readonly ImpaIntroEvent _impa;
    private readonly NayruIntroEvent _nayru;
    private double _frameAccumulator;

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
        OracleSoundEngine sound)
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
            sound);
        _makuTree = new MakuTreeDisappearanceEvent(_context);
        _ralph = new RalphPortalEvent(_context);
        _impa = new ImpaIntroEvent(_context);
        _nayru = new NayruIntroEvent(_context, _impa);
        entities.RoomEntitiesLoaded += OnRoomEntitiesLoaded;
    }

    public bool Active =>
        _nayru.BlocksGameplay || _makuTree.BlocksGameplay ||
        _ralph.BlocksGameplay || _impa.BlocksGameplay;

    private bool HasEventState =>
        _nayru.HasState || _makuTree.HasState || _ralph.HasState || _impa.HasState;

    internal MakuTreeDisappearanceEvent MakuTree => _makuTree;
    internal RalphPortalEvent Ralph => _ralph;
    internal ImpaIntroEvent Impa => _impa;
    internal NayruIntroEvent Nayru => _nayru;

    public void Update(double delta)
    {
        if (!HasEventState)
            return;

        _frameAccumulator += delta * 60.0;
        if (_context.Transitions.IsTransitioning)
        {
            // Following interactions keep updating during room scrolling while
            // ordinary room objects are frozen.
            while (_impa.UpdatesDuringTransition && _frameAccumulator >= 1.0)
            {
                _frameAccumulator -= 1.0;
                _impa.UpdateDuringTransition();
            }
            return;
        }

        while (HasEventState && _frameAccumulator >= 1.0)
        {
            _frameAccumulator -= 1.0;
            if (_nayru.HasState)
            {
                _nayru.UpdateFrame();
                if (_nayru.CrowdActive && _impa.Following)
                    _impa.UpdateFollower();
            }
            else if (_makuTree.HasState)
                _makuTree.UpdateFrame();
            else if (_ralph.HasState)
                _ralph.UpdateFrame();
            else
                _impa.UpdateFrame();
        }
    }

    private void OnRoomEntitiesLoaded(int group, OracleRoomData room)
    {
        _nayru.RestoreCompletedPortal(group, room);
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

        if (_makuTree.Matches(group, room))
        {
            _makuTree.Start(room);
            ResetClock();
            return;
        }
        if (_ralph.Matches(group, room))
        {
            _ralph.Start();
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
        _nayru.Cancel();
        _makuTree.Cancel();
        _ralph.Cancel();
        _impa.Cancel();
        _context.Player.EndCutsceneControl();
        ResetClock();
    }

    private void ResetClock() => _frameAccumulator = 0.0;
}
