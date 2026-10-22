using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private ApplicationValidationFixture? _applicationFixture;
    private ApplicationValidationFixture Application => _applicationFixture ??= new(this);

    // Test convenience aliases belong here, rather than on the production host.
    private OracleWorldData _world => _rooms.World;
    private OracleRoomData _currentRoom
    {
        get => _rooms.CurrentRoom;
        set => _rooms.SetLoadedRoom(_rooms.ActiveGroup, value);
    }
    private int _activeGroup
    {
        get => _rooms.ActiveGroup;
        set => _rooms.SetActiveGroup(value);
    }
    private List<NpcCharacter> _npcNodes => _entities.Entities<NpcCharacter>();
    private bool _scrollTransitionActive => _transitions.ScrollActive;
    private Vector2I _scrollTransitionDirection => _transitions.ScrollDirection;
    private float _scrollTransitionDistance => _transitions.ScrollDistance;
    private int _scrollTransitionFrames => _transitions.ScrollFrames;

    private void ReinitializeGameplayForValidation()
    {
        ValidateRoomEventsBeforeReset();
        if (_profileValidationMemory && (_validationMemoryResetCount++ & 15) == 0)
            TraceValidationMemory();
        Application.ResetGameplay();
        DrainRetiredValidationNodes();
    }

    private void InitializeCompletedIntroRoom(int group, int room, bool clearEntities = true)
    {
        var save = OracleSaveData.CreateStandardGame();
        save.SetLinkName("Link");
        save.SetGlobalFlag(GlobalFlag.PregameIntroDone);
        save.SetGlobalFlag(GlobalFlag.IntroDone);
        InitializeTransientSession(save);
        LoadValidationRoom(group, room);
        if (clearEntities) _entities.Clear();
    }

    // Observation only: CaptureState also clones the 256-byte placement buffer.
    // These comparisons never inspect or restore that buffer. Keep reads tied
    // to the authoritative RNG fields without a reflection lookup per update.
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_rng1")]
    private static extern ref byte ValidationRng1(OracleRandom owner);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_rng2")]
    private static extern ref byte ValidationRng2(OracleRandom owner);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool RandomDiffers(in OracleRandomState initial, byte expectedRng1, byte expectedRng2, int expectedCalls) =>
        ValidationRng1(_random) != expectedRng1 || ValidationRng2(_random) != expectedRng2 ||
        _random.Calls - initial.Calls != expectedCalls;

    private void DrainRetiredValidationNodes()
    {
        // RoomEntityManager removes retired actors from their parent before
        // QueueFree. A synchronous matrix can replace hundreds of sessions
        // without returning to the engine's deletion queue; those orphaned
        // script instances then root the discarded worlds/controllers.
        // At this reset boundary their updates and the old scene are finished.
        // Free only parentless nodes ALREADY queued for deletion. Never touch
        // live orphan fixtures or change retirement during a gameplay update.
        ulong[] orphans = ValidationNodeLifetime.OrphanIds();
        int drained = 0;
        foreach (ulong id in orphans)
            if (GodotObject.InstanceFromId(id) is Node node &&
                node.GetParent() is null && node.IsQueuedForDeletion())
            {
                node.Free();
                drained++;
            }
        if (_profileValidationMemory && (_validationMemoryResetCount & 15) == 1)
            GD.Print($"VALIDATION_DRAIN reset={_validationMemoryResetCount} orphans={orphans.Length} freed={drained}");
    }

    // Feed explicit host samples to the actual application scheduler and
    // complete gameplay loop. Godot's native just-pressed flags otherwise
    // persist across synchronous validation calls within one host frame.
    private void StepGameplayUpdates(int updates, Vector2 movement,
        string[]? held = null, string[]? pressed = null, bool batched = false,
        Action? afterUpdate = null) =>
        Application.Step(updates, movement, held, pressed, batched, afterUpdate);

    private static void AssertMovementPublication(OracleRuntimeState memory, int y, int x, string source)
    {
        for (int i = 0; i < 4; i++)
        {
            byte expected = unchecked((byte)((i < 2 ? y : x) >> ((i & 1) * 8)));
            FailIf(memory.ReadWramByte(0xcec0 + i) != expected,
                $"{source}: expected movement byte ${expected:x2} at ${0xcec0 + i:x4}.");
        }
    }
}
