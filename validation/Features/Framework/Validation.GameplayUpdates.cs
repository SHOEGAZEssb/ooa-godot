using System;
using System.Collections.Generic;
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

    private void ReinitializeGameplayForValidation() => Application.ResetGameplay();

    // Feed explicit host samples to the actual application scheduler and
    // complete gameplay loop. Godot's native just-pressed flags otherwise
    // persist across synchronous validation calls within one host frame.
    private void StepGameplayUpdates(int updates, Vector2 movement,
        string[]? held = null, string[]? pressed = null, bool batched = false,
        Action? afterUpdate = null) =>
        Application.Step(updates, movement, held, pressed, batched, afterUpdate);
}
