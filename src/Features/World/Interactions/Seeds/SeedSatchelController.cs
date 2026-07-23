using Godot;

namespace oracleofages;

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

    public SeedSatchelController(
        InventoryState inventory,
        RoomEntityManager entities,
        SeedSatchelDatabase database,
        RoomSession rooms)
    {
        _inventory = inventory;
        _entities = entities;
        _database = database;
        _rooms = rooms;
    }

    public int TryUse(Player player)
    {
        if (_entities.HasActiveSeedProjectile ||
            !_inventory.HasSelectedSatchelSeed())
        {
            return 0;
        }
        int seedItem = TreasureDatabase.TreasureEmberSeeds +
            _inventory.SatchelSelectedSeeds;
        if (!_database.TryGet(seedItem, out SeedRecord record))
        {
            GD.PushError(
                $"Unsupported active Satchel child ITEM ${seedItem:x2}; " +
                "only imported object_code/common/items/seeds.s:itemCode20 is enabled.");
            return 0;
        }

        _entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(
            player.Position, player.FacingVector, record, _rooms.ActiveGroup));
        if (!_inventory.TryConsumeSelectedSatchelSeed(out int consumed) ||
            consumed != seedItem)
        {
            throw new System.InvalidOperationException(
                $"Satchel child ${seedItem:x2} was allocated without its selected BCD seed count.");
        }
        return record.LinkFrames;
    }
}
