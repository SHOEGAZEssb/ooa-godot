using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private bool _profileValidationMemory;
    private int _validationMemoryResetCount;

    private void TraceValidationMemory()
    {
        using Process process = Process.GetCurrentProcess();
        GCMemoryInfo gc = GC.GetGCMemoryInfo();
        GD.Print(FormattableString.Invariant(
            $"VALIDATION_MEMORY reset={_validationMemoryResetCount} private_bytes={process.PrivateMemorySize64} managed_bytes={GC.GetTotalMemory(false)} gc_committed_bytes={gc.TotalCommittedBytes} gen0={GC.CollectionCount(0)} gen1={GC.CollectionCount(1)} gen2={GC.CollectionCount(2)}"));
    }

    private async Task ValidateApplicationSessionLifetime()
    {
        var liveOrphan = new Node();
        ulong liveOrphanId = liveOrphan.GetInstanceId();
        try
        {
            FailIf(!ValidationNodeLifetime.OrphanIds().Contains(liveOrphanId),
                "Orphan snapshots must preserve the full 64-bit native instance ID.");
            var released = new WeakReference<RoomEventController>[64];
            var npcRecords = NpcDatabase.Shared.AllRecords.ToArray();
            var enemyRecords = EnemyDatabase.Shared.GetRoomObjects(4, 0x13).ToArray();
            var treasureRecords = TreasureDatabase.Shared.Objects.ToArray();
            for (int index = 0; index < released.Length; index++)
                released[index] = ReplaceSessionForLifetimeValidation();
            FailIf(!npcRecords.SequenceEqual(NpcDatabase.Shared.AllRecords) ||
                !enemyRecords.SequenceEqual(EnemyDatabase.Shared.GetRoomObjects(4, 0x13)) ||
                !treasureRecords.SequenceEqual(TreasureDatabase.Shared.Objects),
                "Session replacement mutated shared NPC, enemy or treasure source records.");
            CollectReleasedSessions();
            int beforeFrames = released.Count(reference => reference.TryGetTarget(out _));
            GD.Print($"VALIDATION_SESSION_LIFETIME phase=after-gc retained={beforeFrames}");
            TraceValidationMemory();

            ProcessModeEnum mode = _scene.ProcessMode;
            _scene.ProcessMode = ProcessModeEnum.Disabled;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            CollectReleasedSessions();
            int afterFrames = released.Count(reference => reference.TryGetTarget(out _));
            GD.Print($"VALIDATION_SESSION_LIFETIME phase=after-engine-frames retained={afterFrames}");
            TraceValidationMemory();
            _scene.ProcessMode = mode;
            FailIf(!GodotObject.IsInstanceIdValid(liveOrphanId),
                "Session cleanup destroyed an orphan fixture that was not queued for deletion.");
            // One active native invocation can retain its session until the
            // next engine frame. Discarded sessions must not accumulate.
            FailIf(beforeFrames > 1 || afterFrames != 0,
                "Synchronous validation resets accumulated discarded application sessions.");
        }
        finally
        {
            if (GodotObject.IsInstanceValid(liveOrphan)) liveOrphan.Free();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private WeakReference<RoomEventController> ReplaceSessionForLifetimeValidation()
    {
        // Exercise real retirement: removed room actors are unparented and
        // queued, so freeing the scene root alone cannot destroy them.
        LoadValidationRoom(4, 0x13);
        _entities.Clear();
        var released = new WeakReference<RoomEventController>(_roomEvents);
        var previousRooms = _rooms;
        var previousRuntime = _runtimeState;
        var previousInventory = _inventory;
        var previousSave = _saveData;
        var previousRandom = _random;
        var previousEntities = _entities;
        ReinitializeGameplayForValidation();
        FailIf(ReferenceEquals(previousRooms, _rooms) ||
            ReferenceEquals(previousRooms.World, _rooms.World) ||
            ReferenceEquals(previousRuntime, _runtimeState) ||
            ReferenceEquals(previousInventory, _inventory) ||
            ReferenceEquals(previousSave, _saveData) ||
            ReferenceEquals(previousRandom, _random) ||
            ReferenceEquals(previousEntities, _entities),
            "Session replacement reused live rooms, world, runtime, inventory, save, RNG or entities.");
        var factory = SomariaPrivate<RoomEntityFactory>(_entities, "_factory");
        FailIf(!ReferenceEquals(_treasures, TreasureDatabase.Shared) ||
            !ReferenceEquals(SomariaPrivate<EnemyDatabase>(factory, "enemies"), EnemyDatabase.Shared) ||
            !ReferenceEquals(SomariaPrivate<NpcDatabase>(_entities.FamilyStateResolver, "_npcs"), NpcDatabase.Shared),
            "Session replacement rebuilt read-only NPC, enemy or treasure source tables.");
        return released;
    }

    private static void CollectReleasedSessions()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
