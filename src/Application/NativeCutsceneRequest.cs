using System;

namespace Oracles;

internal static class NativeCutsceneRequest
{
    // include/wram.s: wCutsceneIndex aliases wThreadStateBuffer+$0f.
    internal const int IndexAddress = 0xc2ef;

    internal static bool TrySelect(OracleRuntimeState memory, Action<int> selectHandler)
    {
        int request = memory.ReadField("wCutsceneTrigger");
        if (request == 0) return false;
        int index = request & 0x7f;
        // bank1.setCutsceneIndexIfCutsceneTriggerSet, after the ordinary HUD.
        memory.SetWramByte(IndexAddress, (byte)index);
        memory.SetField("wCutsceneTrigger", 0);
        memory.SetField("wCutsceneState", 0);
        selectHandler(index);
        return true;
    }
}
