namespace oracleofages;

// Save-independent source tables. Each session receives its own bundle; no
// room identity, palette, live tile state, save reference or RNG lives here.
public sealed class RoomSessionResources
{
    internal SingleTileChangeDatabase SingleTileChanges { get; } = new();
    internal RoomTileChangeDatabase TileChanges { get; } = new();
    internal DungeonKeyDoorDatabase KeyDoors { get; } = new();
    internal StandardTileSubstitutionDatabase StandardTileSubstitutions { get; } = new();
    internal SwitchTileReplacementDatabase SwitchTiles { get; } = new();
    internal DungeonToggleTileDatabase ToggleTiles { get; } = new();
    internal JabuWaterTileDatabase JabuWaterTiles { get; } = new();
    internal GashaSpotDatabase GashaSpots { get; } = new();
    internal DungeonMapDatabase DungeonMaps { get; } = new();
}
