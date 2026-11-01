using System.Numerics;
using static Oracles.SeasonsGlobalFlag;

namespace Oracles;

internal static class SeasonsNpcStoryState
{
    // miscNpcs.s:checkNPCStage, including linked-game precedence and noncontiguous essences.
    internal static int HoronStage(OracleSaveData save,OracleRuntimeState memory)
    {
        if (save.HasGlobalFlag(FinishedGame)) return 10;
        int essences = save.ReadField("wEssencesObtained");
        if (!save.HasTreasure(TreasureId.Essence)) return save.HasGlobalFlag(GnarledKeyGiven) ? 1 : 0;
        int highest = BitOperations.Log2((uint)essences), count = BitOperations.PopCount((uint)essences);
        if (memory.ReadField("wIsLinkedGame")!=0)
        {
            if (save.HasGlobalFlag(ZeldaKidnappedSeen)) return 8;
            if (save.HasGlobalFlag(GotMakuSeed)) return 7;
            if (save.HasGlobalFlag(ZeldaVillagersSeen)) return 6;
            if (highest != 0 && count < 5) return save.HasGlobalFlag(ZeldaSavedFromVire) ? 3 : 9;
        }
        return highest >= 7 ? 5 : count >= 5 ? 4 : highest >= 1 ? 3 : 2;
    }
    internal static int SunkenStage(OracleSaveData save)
    {
        if (save.HasGlobalFlag(FinishedGame)) return 4;
        if (save.HasGlobalFlag(ZeldaKidnappedSeen)) return 3;
        int essences = save.HasTreasure(TreasureId.Essence)?save.ReadField("wEssencesObtained"):0;
        return (essences & 0x80) != 0 ? 2 : (essences & 8) != 0 ? 1 : 0;
    }
}
