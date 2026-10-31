namespace Oracles;

/// <summary>
/// ITEM_SEED_SATCHEL ($19) parent-item allocation and BCD consumption. The
/// child must be allocated before decNumActiveSeeds changes WRAM.
/// </summary>
public sealed class SeedSatchelController
{
    private readonly InventoryState _inventory;
    private readonly RoomEntityManager _entities;
    private readonly SeedSatchelDatabase _database;
    private readonly RoomSession _rooms;
    public PegasusSeedState Pegasus { get; }

    public SeedSatchelController(
        InventoryState inventory,
        RoomEntityManager entities,
        SeedSatchelDatabase database,
        RoomSession rooms)
    {
        if (rooms.AssetRoot != entities.AssetRoot || rooms.AssetRoot != database.AssetRoot ||
            rooms.AssetRoot != inventory.AssetRoot || !ReferenceEquals(rooms.RuntimeState, entities.RuntimeState) ||
            !ReferenceEquals(rooms.RuntimeState, inventory.RuntimeState) || !ReferenceEquals(rooms.SaveData, inventory.SaveData) ||
            !ReferenceEquals(entities.Inventory, inventory))
            throw new System.ArgumentException("Satchel input requires the selected room, inventory and runtime owners.");
        _inventory = inventory;
        _entities = entities;
        _database = database;
        _rooms = rooms;
        Pegasus = new PegasusSeedState(inventory, entities);
    }

    public int TryUse(Player player)
    {
        // The selected parent branch rejects these before selecting a seed;
        // Pegasus uses the same gate as physical seeds.
        if (_database.RejectsUnderwater && player.World.Underwater ||
            _database.RejectsRaft && _entities.RaftSpecialObjectPresent ||
            player.TopDownSwimming || player.SideScrollSwimming)
            return 0;
        int seedItem = TreasureId.EmberSeeds +
            _inventory.SatchelSelectedSeeds;
        if (seedItem == ItemId.PegasusSeed)
        {
            Pegasus.TryUse();
            return 0; // This branch clears the parent without immobilizing Link.
        }
        if (_entities.HasActiveSeed(seedItem, SeedLaunchKind.Satchel) ||
            !_inventory.HasSelectedSatchelSeed() || !_entities.DynamicItemSlotAvailable)
        {
            return 0;
        }
        if (!_database.TryGet(seedItem, out SeedRecord record))
        {
            throw new System.InvalidOperationException(
                $"{_database.AssetRoot}metadata/seed_satchel.tsv: missing Satchel child ITEM ${seedItem:x2}.");
        }

        _entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(
            player.PrecisePosition, player.FacingVector, record, _rooms.ActiveGroup,
            // itemCreateChildWithID copies all eight direction/position bytes,
            // including both Z bytes before Link's following gravity update.
            LinkZFixed: player.ItemCreationZFixed));
        if (!_inventory.TryConsumeSelectedSatchelSeed(out int consumed) ||
            consumed != seedItem)
        {
            throw new System.InvalidOperationException(
                $"Satchel child ${seedItem:x2} was allocated without its selected BCD seed count.");
        }
        return record.LinkFrames;
    }
}
