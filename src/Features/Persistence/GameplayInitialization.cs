
namespace Oracles;

internal static class GameplayInitialization
{
    internal static void RestoreCheckpoint(OracleSaveData save, OracleRuntimeState memory)
    {
        if (save.Game != memory.Game)
            throw new ArgumentException("initializeGame requires the active game's file and runtime state.");
        DeathRespawnInitialization.Apply(save);
        CompanionRuntimeState.RestoreRememberedFromDeathRespawn(memory, save);
        memory.SetField("wRoomStateModifier", (byte)save.RespawnStateModifier);
        memory.SetField("wIsLinkedGame", save.ReadField("wFileIsLinkedGame"));
        save.ResetHealthIfDepleted();
    }

    internal static bool PregameComplete(OracleSaveData save) =>
        HasNativeFlag(save, "GLOBALFLAG_PREGAME_INTRO_DONE");
    internal static bool LinkSummoned(OracleSaveData save) => HasNativeFlag(save, "GLOBALFLAG_3d");
    private static bool HasNativeFlag(OracleSaveData save, string name) => save.HasGlobalFlag(
        new GlobalFlagDatabase(OracleAssetPack.ForGame(save.Game)).GetId(name));

    internal static void RestorePlayer(OracleSaveData save, Player player, bool protect = true)
    {
        Vector2I facing = save.RespawnFacing switch
        {
            0 => Vector2I.Up, 1 => Vector2I.Right, 2 => Vector2I.Down, _ => Vector2I.Left
        };
        player.Face(facing);
        player.SetLocalRespawnPosition(new(save.RespawnX, save.RespawnY), facing);
        // initializeGame's signed $88 counter gives 120 non-flashing object updates.
        if (protect) player.ApplyInteractionInvincibility(0x78);
    }
}
