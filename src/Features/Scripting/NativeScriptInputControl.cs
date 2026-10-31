namespace Oracles;

internal sealed class NativeScriptInputControl(OracleRuntimeState memory)
{
    private ulong? _objectsWrite;
    private ulong? _menuWrite;
    private ulong? _transitionsWrite;
    private ulong? _linkMenuWrite;
    // A granted treasure can replace the mask while the script still awaits enableinput.
    internal bool LinkDisabled => _objectsWrite.HasValue &&
        (memory.ReadField("wDisabledObjects") & 0x81) != 0;

    internal void SetEnabled(Player player, bool enabled)
    {
        SetEnabled(memory, player, enabled);
        _objectsWrite = enabled ? null : memory.ObjectsDisabledWriteVersion;
        _menuWrite = enabled ? null : memory.MenuDisabledWriteVersion;
    }

    internal void SetDisabledObjects(byte value)
    {
        memory.SetField("wDisabledObjects", value);
        _objectsWrite = value == 0 ? null : memory.ObjectsDisabledWriteVersion;
    }

    internal void SetMenuEnabled(Player player, bool enabled)
    {
        SetMenuEnabled(memory, player, enabled);
        _menuWrite = enabled ? null : memory.MenuDisabledWriteVersion;
    }

    internal void SetMenuDisabled(byte value)
    {
        memory.SetField("wMenuDisabled", value);
        _menuWrite = value == 0 ? null : memory.MenuDisabledWriteVersion;
    }

    internal void SetScreenTransitionsDisabled(byte value)
    {
        memory.SetField("wDisableScreenTransitions", value);
        _transitionsWrite = value == 0 ? null : memory.ScreenTransitionsDisabledWriteVersion;
    }

    internal void SetLinkCollisionsAndMenuDisabled(byte value)
    {
        memory.SetField("wDisableLinkCollisionsAndMenu",value);
        _linkMenuWrite=value==0?null:memory.LinkCollisionsAndMenuDisabledWriteVersion;
    }

    internal void Cancel()
    {
        if (_objectsWrite == memory.ObjectsDisabledWriteVersion) memory.SetField("wDisabledObjects", 0);
        if (_menuWrite == memory.MenuDisabledWriteVersion) memory.SetField("wMenuDisabled", 0);
        if (_transitionsWrite == memory.ScreenTransitionsDisabledWriteVersion) memory.SetField("wDisableScreenTransitions", 0);
        if(_linkMenuWrite==memory.LinkCollisionsAndMenuDisabledWriteVersion)memory.SetField("wDisableLinkCollisionsAndMenu",0);
        Relinquish();
    }

    internal void Relinquish() => _objectsWrite = _menuWrite = _transitionsWrite = _linkMenuWrite = null;

    // scripting.s:scriptCmd_disableInput / scriptCmd_enableInput and
    // func_0c_4177. Keep the normal Link object; the shared mask gates it.
    internal static void SetEnabled(OracleRuntimeState memory, Player player, bool enabled)
    {
        memory.SetField("wDisabledObjects", enabled ? (byte)0 : (byte)0x81);
        SetMenuEnabled(memory, player, enabled);
    }

    internal static void SetMenuEnabled(OracleRuntimeState memory, Player player, bool enabled)
    {
        memory.SetField("wMenuDisabled", enabled ? (byte)0 : (byte)0x80);
        if (enabled) return;
        player.ClearNativeItemParents();
        player.DropHeldItemsForScript();
        player.ApplyInteractionInvincibility(0x80);
    }
}
