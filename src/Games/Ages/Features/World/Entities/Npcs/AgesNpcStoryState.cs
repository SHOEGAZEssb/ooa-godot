using System;

namespace Oracles;

// object_code/ages/interactions/miscMan2.s:getGameProgress_1/_2 and zora.s.
internal static class AgesNpcStoryState
{
    internal static int GetState(NpcStoryStateKind kind, OracleSaveData save)
    {
        if (save.Game != OracleGame.Ages)
            throw new NotSupportedException($"Ages NPC state selector {kind} cannot read a {save.Game} save.");
        return kind switch
        {
            NpcStoryStateKind.AgesGameProgress1 => GetGameProgress1(save),
            NpcStoryStateKind.AgesGameProgress2 => GetGameProgress2(save),
            NpcStoryStateKind.AgesZoraWorld => !save.HasGlobalFlag(AgesGlobalFlag.KingZoraCured) ? 0 :
                save.HasGlobalFlag(AgesGlobalFlag.WaterPollutionFixed) ? 2 : 1,
            NpcStoryStateKind.AgesWaterPollution => save.HasGlobalFlag(AgesGlobalFlag.WaterPollutionFixed) ? 1 : 0,
            _ => throw new InvalidOperationException($"Unhandled Ages NPC story state {kind}.")
        };
    }

    internal static int GetGameProgress1(OracleSaveData save)
    {
        if (save.Game != OracleGame.Ages) throw new NotSupportedException("getGameProgress_1 is an Ages NPC selector.");
        if (save.HasGlobalFlag(AgesGlobalFlag.FinishedGame)) return 5;
        if (save.HasGlobalFlag(AgesGlobalFlag.SawTwinrovaBeforeEndgame)) return 4;
        byte essences = save.ReadField("wEssencesObtained");
        if (essences == 0) return 0;
        int highest = HighestSetBit(essences);
        if (highest >= 6) return 3;
        if (save.HasGlobalFlag(AgesGlobalFlag.SavedNayru)) return 2;
        return highest >= 2 ? 1 : 0;
    }

    internal static int GetGameProgress2(OracleSaveData save)
    {
        if (save.Game != OracleGame.Ages) throw new NotSupportedException("getGameProgress_2 is an Ages NPC selector.");
        if (save.HasGlobalFlag(AgesGlobalFlag.FinishedGame)) return 7;
        if (save.IsLinkedGame && save.HasRoomFlag(4, 0xfc, OracleSaveData.RoomFlag80)) return 6;
        if (save.HasGlobalFlag(AgesGlobalFlag.SawTwinrovaBeforeEndgame)) return 5;
        byte essences = save.ReadField("wEssencesObtained");
        if (essences == 0) return 0;
        int highest = HighestSetBit(essences);
        if (highest >= 6) return 4;
        if (save.HasGlobalFlag(AgesGlobalFlag.SavedNayru)) return 3;
        return highest >= 3 ? 2 : highest >= 1 ? 1 : 0;
    }

    private static int HighestSetBit(byte value)
    {
        int bit = 7;
        while ((value & (1 << bit)) == 0) bit--;
        return bit;
    }
}
