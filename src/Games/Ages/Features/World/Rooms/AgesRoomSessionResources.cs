namespace Oracles;

// Session bundle of save-independent source tables; immutable catalogs may be
// shared. No room identity, palette, live tile state, save reference or RNG lives here.
public sealed class AgesRoomSessionResources
{
    internal SingleTileChangeDatabase SingleTileChanges { get; } = new(AgesGameContent.AssetRoot);
    internal PollutedWaterTileDatabase PollutedWaterTiles { get; } = new();
    internal AgesRoomTileChangeDatabase TileChanges { get; } = new(AgesGameContent.AssetRoot);
    internal DungeonKeyDoorDatabase KeyDoors { get; } = new(AgesGameContent.AssetRoot);
    internal StandardTileSubstitutionDatabase StandardTileSubstitutions { get; } = new(AgesGameContent.AssetRoot);
    internal ChestDatabase Chests { get; } = new(AgesGameContent.AssetRoot);
    internal SwitchTileReplacementDatabase SwitchTiles { get; } = new(AgesGameContent.AssetRoot);
    internal DungeonToggleTileDatabase ToggleTiles { get; } = new(AgesGameContent.AssetRoot);
    internal JabuWaterTileDatabase JabuWaterTiles { get; } = new();
    internal GashaSpotDatabase GashaSpots { get; } = new(AgesGameContent.AssetRoot);
    internal DungeonMapDatabase DungeonMaps { get; } = DungeonMapDatabase.For(AgesGameContent.AssetRoot);
}
