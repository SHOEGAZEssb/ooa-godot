using System;

namespace Oracles;

/// <summary>
/// Shared evaluation of save-backed state selectors used by imported NPC
/// visibility, dialogue, and position records.
/// </summary>
internal static class NpcStoryState
{
    public static int InteractionKey(int id, int subId) => (id << 8) | subId;

    public static NpcStoryStateKind ParseKind(string value, string context) => value switch
    {
        "game-progress-1" => NpcStoryStateKind.AgesGameProgress1,
        "game-progress-2" => NpcStoryStateKind.AgesGameProgress2,
        "global-flag" => NpcStoryStateKind.GlobalFlag,
        "current-room-flag" => NpcStoryStateKind.CurrentRoomFlag,
        "zora-world" => NpcStoryStateKind.AgesZoraWorld,
        "water-pollution" => NpcStoryStateKind.AgesWaterPollution,
        _ => throw new InvalidOperationException(
            $"Unknown NPC {context} state kind '{value}'.")
    };

    public static int GetState(
        NpcStoryStateKind kind,
        int comparisonValue,
        NpcRecord npc,
        OracleSaveData save) => kind switch
    {
        NpcStoryStateKind.GlobalFlag => save.HasGlobalFlag(comparisonValue)
            ? comparisonValue
            : 0,
        NpcStoryStateKind.CurrentRoomFlag => save.HasRoomFlag(
            npc.Group, npc.Room, (byte)comparisonValue) ? comparisonValue : 0,
        _ => AgesNpcStoryState.GetState(kind, save)
    };

}

internal enum NpcStoryStateKind
{
    AgesGameProgress1,
    AgesGameProgress2,
    GlobalFlag,
    CurrentRoomFlag,
    AgesZoraWorld,
    AgesWaterPollution
}
