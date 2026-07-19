using Godot;

namespace oracleofages;

public sealed class DebugCollisionController
{
    private const string InputAction = "debug_collision";

    public bool CollisionsDisabled { get; private set; }

    public DebugCollisionController()
    {
        EnsureInputAction();
    }

    public void Update()
    {
        if (Input.IsActionJustPressed(InputAction))
            Toggle();
    }

    internal void ToggleForValidation() => Toggle();

    private void Toggle() => CollisionsDisabled = !CollisionsDisabled;

    private static void EnsureInputAction()
    {
        if (InputMap.HasAction(InputAction))
            return;
        InputMap.AddAction(InputAction);
        InputMap.ActionAddEvent(
            InputAction,
            new InputEventKey { PhysicalKeycode = Key.F2 });
    }
}
