using Godot;

namespace oracleofages;

internal sealed class DebugSavestateController
{
    private DebugSavestateController()
    {
    }

    internal static bool TryDecodeInput(
        InputEvent @event,
        out DebugSavestateCommand command)
    {
        command = default;
        if (@event is not InputEventKey key ||
            !key.Pressed ||
            key.Echo ||
            key.CtrlPressed ||
            key.AltPressed ||
            key.MetaPressed)
        {
            return false;
        }

        int slot = key.PhysicalKeycode switch
        {
            Key.Key0 => 0,
            Key.Key1 => 1,
            Key.Key2 => 2,
            Key.Key3 => 3,
            Key.Key4 => 4,
            Key.Key5 => 5,
            Key.Key6 => 6,
            Key.Key7 => 7,
            Key.Key8 => 8,
            Key.Key9 => 9,
            _ => -1
        };
        if (slot < 0)
            return false;

        command = new DebugSavestateCommand(
            slot,
            key.ShiftPressed
                ? DebugSavestateCommandKind.Save
                : DebugSavestateCommandKind.Load);
        return true;
    }
}

internal readonly record struct DebugSavestateCommand(
    int Slot,
    DebugSavestateCommandKind Kind);

internal enum DebugSavestateCommandKind
{
    Load,
    Save
}
