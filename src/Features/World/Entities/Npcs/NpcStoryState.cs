using System;

namespace oracleofages;

/// <summary>
/// Shared evaluation of save-backed state selectors used by imported NPC
/// visibility, dialogue, and position records.
/// </summary>
internal static class NpcStoryState
{
    public static int InteractionKey(int id, int subId) => (id << 8) | subId;

    public static NpcStoryStateKind ParseKind(string value, string context) => value switch
    {
        "game-progress-1" => NpcStoryStateKind.GameProgress1,
        "game-progress-2" => NpcStoryStateKind.GameProgress2,
        "current-room-flag" => NpcStoryStateKind.CurrentRoomFlag,
        _ => throw new InvalidOperationException(
            $"Unknown NPC {context} state kind '{value}'.")
    };

    public static int GetState(
        NpcStoryStateKind kind,
        int comparisonValue,
        NpcRecord npc,
        OracleSaveData save) => kind switch
    {
        NpcStoryStateKind.GameProgress1 => GetGameProgress1(save),
        NpcStoryStateKind.GameProgress2 => GetGameProgress2(save),
        NpcStoryStateKind.CurrentRoomFlag => save.HasRoomFlag(
            npc.Group, npc.Room, (byte)comparisonValue) ? comparisonValue : 0,
        _ => throw new InvalidOperationException($"Unhandled NPC story state {kind}.")
    };

    public static int GetGameProgress1(OracleSaveData save)
    {
        if (save.HasGlobalFlag(OracleSaveData.GlobalFlagFinishedGame))
            return 5;
        if (save.HasGlobalFlag(OracleSaveData.GlobalFlagSawTwinrovaBeforeEndgame))
            return 4;

        byte essences = save.ReadWramByte(0xc6bf);
        if (essences == 0)
            return 0;
        int highestEssence = HighestSetBit(essences);
        if (highestEssence >= 6)
            return 3;
        if (save.HasGlobalFlag(OracleSaveData.GlobalFlagSavedNayru))
            return 2;
        return highestEssence >= 2 ? 1 : 0;
    }

    public static int GetGameProgress2(OracleSaveData save)
    {
        if (save.HasGlobalFlag(OracleSaveData.GlobalFlagFinishedGame))
            return 7;
        if (save.IsLinkedGame && save.HasRoomFlag(
            4, 0xfc, OracleSaveData.RoomFlag80))
        {
            return 6;
        }
        if (save.HasGlobalFlag(OracleSaveData.GlobalFlagSawTwinrovaBeforeEndgame))
            return 5;

        byte essences = save.ReadWramByte(0xc6bf);
        if (essences == 0)
            return 0;
        int highestEssence = HighestSetBit(essences);
        if (highestEssence >= 6)
            return 4;
        if (save.HasGlobalFlag(OracleSaveData.GlobalFlagSavedNayru))
            return 3;
        if (highestEssence >= 3)
            return 2;
        return highestEssence >= 1 ? 1 : 0;
    }

    private static int HighestSetBit(byte value)
    {
        int bit = 7;
        while ((value & (1 << bit)) == 0)
            bit--;
        return bit;
    }
}
