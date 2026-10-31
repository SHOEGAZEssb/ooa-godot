using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Oracles;

public sealed partial class RoomEntityManager
{
    private INativeSpecialObjectRoomEntity? _specialObjectOwner;

    internal bool SpecialObjectPresentationAvailable
    {
        get { ReconcileSpecialObjectOwner(); return _specialObjectOwner is null; }
    }

    internal bool SpecialObjectSlotAvailable => SpecialObjectPresentationAvailable &&
        !CompanionRuntimeState.AnyActive(_runtimeState);

    internal bool CanCaptureDebugState
    {
        get
        {
            try { ValidateDebugTransientInteractions(); _factory.ValidateDebugNativeInteractions(_runtimeState); ValidateDebugNativeItems(_runtimeState); }
            catch (NotSupportedException) { return false; }
            ReconcileSpecialObjectOwner();
            return !CompanionRuntimeState.AnyActive(_runtimeState) ||
                _specialObjectOwner is { SupportsDebugReconstruction: true } &&
                _specialObjectOwner is not IRoomInitializedCompanion { RoomInitialization.Pending: true } &&
                _factory.CanReconstructDebugSpecialObject(_runtimeState, _roomForActiveEntities.Group, _roomForActiveEntities.Id);
        }
    }

    private void ValidateDebugTransientInteractions()
    {
        // shortcut: reserved tile actor phases are not reconstructed; add their snapshot before permitting active captures.
        if (ReservedPushBlock is { Active: true } || ReservedKeyDoor is { Opening: true })
            throw new NotSupportedException("INTERAC $14/$1e: active reserved tile movement has no transient debug reconstruction.");
        // shortcut: $9f presentation/counters are not reconstructed; remove this guard with native-slot restore support.
        foreach(var actor in _activeEntities)
        {
            // shortcut: key-sprite counters are not reconstructed; admit captures when their native phase is captured.
            if(actor is DungeonKeyUseRoomEntity && actor is not IRoomEntityLifetime { Finished: true })
                throw new NotSupportedException("dungeonKeySprite.s: INTERAC $17 has no transient debug reconstruction.");
            if(actor is ExclamationMarkRoomEntity &&
                actor is not IRoomEntityLifetime { Finished: true })
                throw new NotSupportedException($"exclamationMark.s: INTERAC $9f slot${NativeInteractionSlot(actor):x2} has no transient debug reconstruction.");
        }
    }

    internal void ValidateDebugReconstruction(OracleRuntimeStateSnapshot snapshot, int group, int room)
    {
        if (snapshot.Game != _runtimeState.Game)
            throw new ArgumentException("Debug reconstruction requires the selected game's entity factory.", nameof(snapshot));
        var memory = new OracleRuntimeState(snapshot.Game);
        memory.RestoreState(snapshot);
        if (memory.ReadWramByte(0xd140) != 0 && memory.ReadWramByte(0xd141) == 0x14 ||
            memory.ReadWramByte(0xd040) != 0 && memory.ReadWramByte(0xd041) == 0x1e)
            throw new NotSupportedException("INTERAC $14/$1e: reserved tile movement has no transient debug reconstruction.");
        for(int page=2;page<16;page++)
            if(memory.ReadWramByte((0xd000+page*0x100)|0x40)!=0 && memory.ReadWramByte((0xd000+page*0x100)|0x41)==0x17)
                throw new NotSupportedException("dungeonKeySprite.s: INTERAC $17 has no transient debug reconstruction.");
        _factory.ValidateDebugNativeInteractions(memory);
        ValidateDebugNativeItems(memory);
        if (CompanionRuntimeState.AnyActive(memory) && !_factory.CanReconstructDebugSpecialObject(memory, group, room))
            throw new ArgumentException($"Debug state SPECIALOBJECT ${memory.ReadWramByte(0xd101):x2} state ${memory.ReadWramByte(0xd104):x2} cannot be reconstructed in room ${group:x2}:${room:x2} by the selected game's factory.", nameof(snapshot));
    }

    internal void SetSpecialObjectBarrierCoordinate(bool horizontal, int coordinate)
    {
        ReconcileSpecialObjectOwner();
        if (_specialObjectOwner is not { } current)
            throw new NotSupportedException($"companionScript_restrict: native SPECIALOBJECT ${_runtimeState.ReadWramByte(0xd101):x2} has no bound presentation for its coordinate write.");
        if (current is ICompanionBarrierTarget animal)
            animal.SetBarrierCoordinate(horizontal, coordinate);
        else
        {
            Vector2 position = CompanionRuntimeState.Read(_runtimeState).Position;
            if (horizontal) position.X = coordinate + position.X - Mathf.Floor(position.X);
            else position.Y = coordinate + position.Y - Mathf.Floor(position.Y);
            current.NativeSpecialObjectPosition = position;
        }
        _runtimeState.SetWramByte(horizontal ? 0xd10d : 0xd10b, checked((byte)coordinate));
        _runtimeState.SetWramByte(0xd110, 0);
    }

    private static bool UsesSpecialObjectSlot(IRoomEntity entity) =>
        entity is INativeSpecialObjectRoomEntity { UsesSpecialObjectSlot: true };

    internal bool IsSpecialObjectOwner(INativeSpecialObjectRoomEntity entity)
    { ReconcileSpecialObjectOwner(); return ReferenceEquals(_specialObjectOwner, entity); }

    internal void RestoreDebugPlayerBindings(Player player)
    {
        ReconcileSpecialObjectOwner();
        _specialObjectOwner?.RestoreNativePresentation();
        if (_runtimeState.ReadField("wLinkObjectIndex") == 0xd1 &&
            _specialObjectOwner is IPlayerRideableRoomEntity { LinkRiding: true } &&
            _specialObjectOwner is IPlayerForcedMovement rider)
            rider.UpdatePlayerForcedMovement(player);
    }

    internal bool TryInteractNativeSpecialObject(NpcCharacter target, Player player)
    {
        ReconcileSpecialObjectOwner();
        return _specialObjectOwner is ScriptedCompanionRoomEntity body &&
            ReferenceEquals(body.Npc, target) && body.TryInteract(player);
    }

    internal IRoomEntity ReplaceSpecialObjectPresentation(INativeSpecialObjectRoomEntity previous, RoomEntitySpawn spawn)
    {
        ReconcileSpecialObjectOwner();
        bool outgoing = _outgoingEntities.Contains(previous);
        if (!ReferenceEquals(_specialObjectOwner, previous) || !_activeEntities.Contains(previous) && !outgoing)
            throw new InvalidOperationException("w1Companion $d100: only the bound body can transfer its presentation.");
        IRoomEntity next = _factory.Create(spawn, _roomForActiveEntities);
        if (next is not INativeSpecialObjectRoomEntity native ||
            !native.MatchesNativeSpecialObjectId(_runtimeState.ReadWramByte(0xd101)))
        {
            next.PresentationNode?.Free();
            throw new InvalidOperationException("w1Companion $d100: a presentation transfer must preserve native identity.");
        }
        // The source continues at the same physical slot, retaining all bytes.
        if (previous.PresentationNode is { } oldPresentation && next.PresentationNode is { } nextPresentation)
            nextPresentation.ZIndex = oldPresentation.ZIndex;
        Vector2 offset = previous.PresentationNode is TransitionOffsetNode2D oldDrawable
            ? oldDrawable.TransitionDrawOffset : Vector2.Zero;
        _specialObjectOwner = null;
        _activeEntities.Remove(previous);
        _outgoingEntities.Remove(previous);
        FreeEntity(previous);
        AddEntity(next);
        if (outgoing)
        {
            _activeEntities.Remove(next);
            _outgoingEntities.Add(next);
            next.SetTransitionDrawOffset(offset);
        }
        _specialObjectsUpdatedBeforePlayer.Add(next);
        return next;
    }

    internal void ReinitializeCompanion(INativeSpecialObjectRoomEntity previous,RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)
    {
        var initializer=(ScriptedCompanionRoomEntity)ReplaceSpecialObjectPresentation(previous,
            new CompanionInitializationSpawn(previous.NativeSpecialObjectId,previous.NativeSpecialObjectPosition));
        initializer.RoomInitialization=CreateCompanionRoomInitialization();
        // dimitriStateC/mooshStateC jump to state0 inside this D1 visit; retain the caller's state.
        initializer.UpdateFrame(frame,spawns);
    }

    private void BindSpecialObject(INativeSpecialObjectRoomEntity entity)
    {
        ReconcileSpecialObjectOwner();
        if (!entity.UsesSpecialObjectSlot) return;
        if (_activeEntities.Contains(entity) || _outgoingEntities.Contains(entity))
            throw new InvalidOperationException($"w1Companion $d100: {entity.GetType().Name} is already bound in the entity graph.");
        if (_specialObjectOwner is not null && !ReferenceEquals(_specialObjectOwner, entity))
            throw new InvalidOperationException($"w1Companion $d100: {entity.GetType().Name} cannot allocate SPECIALOBJECT ${entity.NativeSpecialObjectId:x2}; {_specialObjectOwner.GetType().Name} still owns the physical slot.");
        if (CompanionRuntimeState.AnyActive(_runtimeState))
        {
            int id = CompanionRuntimeState.Read(_runtimeState).Id;
            if (!entity.MatchesNativeSpecialObjectId(id))
                throw new InvalidOperationException($"w1Companion $d100: cannot bind SPECIALOBJECT ${entity.NativeSpecialObjectId:x2} over native ${id:x2} without its source overwrite.");
        }
        else
        {
            if (entity is CompanionCreditsRoomEntity)
                throw new InvalidOperationException("INTERAC $a8 must publish the native companion credits ID before binding its presentation.");
            if (entity is IPlayerRideableRoomEntity { LinkRiding: true })
            {
                CompanionRuntimeState.Begin(_runtimeState, entity.NativeSpecialObjectId,
                    entity.NativeSpecialObjectPosition, entity.NativeSpecialObjectDirection);
                if (entity.NativeSpecialObjectId is >= 0x0b and <= 0x0d)
                    _runtimeState.SetWramByte(0xd104, 5);
            }
            else
                CompanionRuntimeState.BeginIdle(_runtimeState, entity.NativeSpecialObjectId, entity.NativeSpecialObjectPosition);
        }
        _specialObjectOwner = entity;
        entity.NativeSpecialObjectPosition = CompanionRuntimeState.Read(_runtimeState).Position;
        entity.OnNativeSpecialObjectBound();
        // D1 precedes every dynamic category; a later birth waits for its
        // next special-object visit, never the logical interaction fallback.
        if (_specialObjectPassVisited || _nativeEntityWalkActive)
            _specialObjectsUpdatedBeforePlayer.Add(entity);
    }

    internal void BindNativeSpecialObjectPresentation(int id, RoomEntitySpawn spawn)
    {
        if (!CompanionRuntimeState.IsActive(_runtimeState, id))
            throw new InvalidOperationException($"SPECIALOBJECT ${id:x2}: presentation binding requires its live native header.");
        ReconcileSpecialObjectOwner();
        if (_specialObjectOwner is not null) return;
        AddEntity(_factory.Create(spawn, _roomForActiveEntities));
    }

    private void PrepareSpecialObjectSpawn(RoomEntitySpawn spawn)
    {
        (int Id, Vector2 Position, int Direction, bool Riding, bool Force, bool Flute)? birth = spawn switch
        {
            RickyCompanionSpawn r => (SpecialObjectId.Ricky, r.Position, r.Direction, r.Riding, r.ForceMount, r.FluteDestination is not null),
            DimitriCompanionSpawn d => (SpecialObjectId.Dimitri, d.Position, d.Direction, d.Riding, false, d.FluteDestination is not null),
            MooshCompanionSpawn m => (SpecialObjectId.Moosh, m.Position, m.Direction, m.Riding, m.ForceMount, m.FluteDestination is not null),
            _ => null
        };
        if (birth is not { } animal) return;
        bool presentationFree = SpecialObjectPresentationAvailable;
        bool active = CompanionRuntimeState.AnyActive(_runtimeState);
        bool reconstruct = presentationFree && active && (animal.Riding || animal.Force) &&
            CompanionRuntimeState.IsActive(_runtimeState, animal.Id);
        if (!presentationFree || active && !reconstruct)
            throw new InvalidOperationException($"companionSpawner: SPECIALOBJECT ${animal.Id:x2} attempted checked creation while w1Companion $d100 is occupied.");
        if (!active)
        {
            if (animal.Riding)
            {
                CompanionRuntimeState.Begin(_runtimeState, animal.Id, animal.Position, animal.Direction);
                _runtimeState.SetWramByte(0xd104, 5);
                _runtimeState.SetWramByte(0xd109, 0xff);
                _runtimeState.SetWramByte(0xd11a, 0x81);
            }
            else CompanionRuntimeState.BeginIdle(_runtimeState, animal.Id, animal.Position);
        }
        if (animal.Flute || animal.Force)
        {
            _runtimeState.SetWramByte(0xd104, animal.Flute ? (byte)0x0c : (byte)3);
            _runtimeState.SetWramByte(0xd108, (byte)animal.Direction);
            _runtimeState.SetWramByte(0xd109, animal.Flute ? (byte)(animal.Direction * 8) : (byte)0xff);
        }
    }

    private void ReconcileSpecialObjectOwner()
    {
        if (_specialObjectOwner is { } current)
        {
            bool finished = current is IRoomEntityLifetime { Finished: true };
            bool active = CompanionRuntimeState.AnyActive(_runtimeState);
            bool matches = active && current.MatchesNativeSpecialObjectId(CompanionRuntimeState.Read(_runtimeState).Id);
            if (finished || !current.UsesSpecialObjectSlot || !matches)
            {
                _specialObjectOwner = null;
                if (finished && matches)
                    CompanionRuntimeState.Clear(_runtimeState, CompanionRuntimeState.Read(_runtimeState).Id);
                // An explicit native overwrite/disable replaces the dispatch at
                // this address; retiring its old body must not clear the successor.
                if (!finished && current.UsesSpecialObjectSlot)
                {
                    _activeEntities.Remove(current);
                    _outgoingEntities.Remove(current);
                    FreeEntity(current);
                }
            }
        }
        if (!CompanionRuntimeState.AnyActive(_runtimeState)) return;
        int id = CompanionRuntimeState.Read(_runtimeState).Id;
        foreach (var candidate in _activeEntities.Concat(_outgoingEntities).OfType<INativeSpecialObjectRoomEntity>())
        {
            if (!candidate.UsesSpecialObjectSlot || candidate is IRoomEntityLifetime { Finished: true } ||
                !candidate.MatchesNativeSpecialObjectId(id)) continue;
            if (_specialObjectOwner is not null && !ReferenceEquals(_specialObjectOwner, candidate))
                throw new InvalidOperationException($"w1Companion $d100/${id:x2}: two presentations claim the same native slot.");
            _specialObjectOwner = candidate;
        }
    }

    private void ReleaseSpecialObject(IRoomEntity entity, bool preserveRetained)
    {
        if (!ReferenceEquals(_specialObjectOwner, entity)) return;
        var native = _specialObjectOwner!;
        _specialObjectOwner = null;
        if (CompanionRuntimeState.AnyActive(_runtimeState) &&
            native.MatchesNativeSpecialObjectId(CompanionRuntimeState.Read(_runtimeState).Id) &&
            (!preserveRetained || (_runtimeState.ReadWramByte(0xd100) & 3) != 3))
            CompanionRuntimeState.Clear(_runtimeState, CompanionRuntimeState.Read(_runtimeState).Id);
    }
}
