namespace Oracles;

internal static class NativeScriptInputControl
{
    // scripting.s:scriptCmd_disableInput / scriptCmd_enableInput and
    // func_0c_4177. Keep the normal Link object; the shared mask gates it.
    internal static void SetEnabled(OracleRuntimeState memory, Player player, bool enabled)
    {
        memory.SetField("wDisabledObjects", enabled ? (byte)0 : (byte)0x81);
        memory.SetField("wMenuDisabled", enabled ? (byte)0 : (byte)0x80);
        if (enabled) return;
        player.ClearNativeItemParents();
        player.DropHeldItemsForScript();
        player.ApplyInteractionInvincibility(0x80);
    }
}
