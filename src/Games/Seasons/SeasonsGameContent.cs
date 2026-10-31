namespace Oracles;

// Supported US Seasons content and its isolated persistence boundary.
internal static class SeasonsGameContent
{
    internal static OracleRuntimeState CreateContentRuntimeState()
    {
        var state = new OracleRuntimeState(OracleGame.Seasons);
        // bank1.s:initializeSeedTreeRefillData starts tree/event bits $02-$07 set.
        state.SetField("wSeedTreeRefilledBitset", 0xfc);
        return state;
    }
    internal const string AssetRoot = "res://assets/oracle/seasons/";
    internal const string PreviewRoot = AssetRoot + "preview/";
    internal const string SavePath = "user://oracle_of_seasons.sav";
    internal static string SaveSlotPath(int slot) => slot switch {
        0 => SavePath, 1 => "user://oracle_of_seasons_2.sav", 2 => "user://oracle_of_seasons_3.sav",
        _ => throw new System.ArgumentOutOfRangeException(nameof(slot)) };
}
