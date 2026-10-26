namespace Oracles;

// Supported US Seasons content and its isolated persistence boundary.
internal static class SeasonsGameContent
{
    internal const string AssetRoot = "res://assets/oracle/seasons/";
    internal const string PreviewRoot = AssetRoot + "preview/";
    internal const string SavePath = "user://oracle_of_seasons.sav";
    internal static string SaveSlotPath(int slot) => slot switch {
        0 => SavePath, 1 => "user://oracle_of_seasons_2.sav", 2 => "user://oracle_of_seasons_3.sav",
        _ => throw new System.ArgumentOutOfRangeException(nameof(slot)) };
}
