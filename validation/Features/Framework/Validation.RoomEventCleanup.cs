using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private readonly HashSet<Type> _roomEventCleanupOwners = new();
    private readonly HashSet<Type> _roomEventActiveCleanupOwners = new();
    private int _roomEventCleanupBoundaries;

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_eventsByType")]
    private static extern ref Dictionary<Type, IRoomEvent> RegisteredRoomEvents(RoomEventController controller);

    private void ValidateRoomEventsBeforeReset()
    {
        if (_roomEvents is null || _entities is null || _entities.IsDisposed || !GodotObject.IsInstanceValid(_scene)) return;
        CancelAndCheckRoomEvents(RegisteredRoomEvents(_roomEvents).Values.ToArray(),
            $"{_executingValidationName ?? "scenario setup"}, before session replacement");
    }

    private void CancelAndCheckRoomEvents(IRoomEvent[] events, string boundary)
    {
        foreach (IRoomEvent owner in events)
        {
            _roomEventCleanupOwners.Add(owner.GetType());
            if (owner.HasState) _roomEventActiveCleanupOwners.Add(owner.GetType());
            try { owner.Cancel(); }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"{boundary}: {owner.GetType().Name}.Cancel failed.", exception);
            }
        }
        foreach (IRoomEvent owner in events)
            FailIf(HasRoomEventRestrictions(owner) || _player.IsCutsceneControlOwner(owner),
                $"{boundary}: {owner.GetType().Name} retained state, restrictions or owned player control after cancellation.");
        // Every owner must tolerate a second call, including after a different
        // owner has acquired control. Do not invoke unrelated item interruption
        // after those item nodes have already crossed their deletion boundary.
        object successor = new();
        _player.BeginCutsceneControl(interruptBracelet: false, owner: successor);
        try
        {
            foreach (IRoomEvent owner in events)
            {
                owner.Cancel();
                FailIf(HasRoomEventRestrictions(owner),
                    $"{boundary}: {owner.GetType().Name} retained restrictions after repeated cancellation.");
            }
            FailIf(!_player.IsCutsceneControlOwner(successor),
                $"{boundary}: repeated cancellation released a successor's player control.");
        }
        finally { _player.EndCutsceneControl(successor); }
        _roomEventCleanupBoundaries++;
    }

    // MenusDisabled can publish the shared WRAM byte (LibraryEvent). A
    // cancellation must not clear another writer's byte just to satisfy a
    // generic assertion; focused source fixtures verify raw mask ownership.
    private static bool HasRoomEventRestrictions(IRoomEvent owner) =>
        owner.HasState || owner.BlocksGameplay || owner.OwnsGameLogic || owner.SkipsObjectUpdate ||
        owner.DisablesLink || owner.ObjectUpdateSuspended || owner.FreezesNonInteractionObjects ||
        owner.SuppressesStatusBar || owner.SuppressesNormalMenuDispatch ||
        owner.ScreenTransitionsDisabled || owner.AllScreenTransitionsDisabled;

    private void ReportRoomEventCleanup() => GD.Print(
        $"VALIDATION_EVENT_CLEANUP boundaries={_roomEventCleanupBoundaries} owners={_roomEventCleanupOwners.Count} active_owners={_roomEventActiveCleanupOwners.Count}");

    // Every scenario tests the current registration, including future owners.
    // No second event inventory or catch-and-unlock recovery exists in runtime.
    private async Task ValidateScenarioRoomEventCleanup(string scenario)
    {
        if (_entities.IsDisposed || !GodotObject.IsInstanceValid(_scene)) return;
        // A few mask fixtures replace dispatch priority; typed registration
        // still retains every production owner and its original insertion order.
        IRoomEvent[] events = RegisteredRoomEvents(_roomEvents).Values.ToArray();
        FailIf(events.Length == 0 || events.Select(owner => owner.GetType()).Distinct().Count() != events.Length,
            $"{scenario}: room-event cleanup requires the complete unique runtime registration.");
        Node[] actors = _entities.EntityAdapters<IRoomEntity>().Select(entity => (Node)entity.Node).ToArray();
        var mode = _scene.ProcessMode;
        _scene.ProcessMode = ProcessModeEnum.Disabled;
        try
        {
            _entities.Clear();
            // ProcessFrame resumes before the deletion queue of that frame.
            // A second boundary observes the preceding frame's actual frees.
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            FailIf(actors.Any(GodotObject.IsInstanceValid),
                $"{scenario}: retired room entities survived Godot's deletion boundary.");
            CancelAndCheckRoomEvents(events, $"{scenario}, after native disposal");
        }
        finally
        {
            if (GodotObject.IsInstanceValid(_scene)) _scene.ProcessMode = mode;
        }
    }
}
