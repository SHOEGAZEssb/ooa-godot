
namespace Oracles;

internal static class CompanionNativeTerrain
{
    internal static void Update(OracleRuntimeState memory, OracleRoomData room)
    {
        // commonCode.s: companionCheckEnableTerrainEffects follows the species call.
        if (!CompanionRuntimeState.AnyActive(memory)) return;
        memory.SetField("wWarpsDisabled", memory.ReadWramByte(0xd13c));
        bool enabled = (memory.ReadWramByte(0xd10f) & 0x80) != 0;
        if (!enabled)
        {
            var feet = new Vector2(memory.ReadWramByte(0xd10d), unchecked((byte)(memory.ReadWramByte(0xd10b) + 5)));
            enabled = !LinkTerrainEffectDatabase.For(room.AssetRoot).IsPuddle(room.GetMetatile(feet));
            if (enabled) memory.SetWramByte(0xd10f, 0);
        }
        int visible = memory.ReadWramByte(0xd11a);
        memory.SetWramByte(0xd11a, (byte)(enabled ? visible | 0x40 : visible & ~0x40));
    }
}
