namespace oracleofages;

// Source symbols retain their clean-US/Ages values. Keep IDs distinct from table indices,
// masks and counters; byte-oriented asset/state APIs intentionally continue to use integers.
public static class ItemId
{
    // constants/common/items.s: ITEM_NONE
    public const int None = 0x00;
    // constants/common/items.s: ITEM_SHIELD
    public const int Shield = 0x01;
    // constants/common/items.s: ITEM_CANE_OF_SOMARIA
    public const int CaneOfSomaria = 0x04;
    // constants/common/items.s: ITEM_SWORD
    public const int Sword = 0x05;
    // constants/common/items.s: ITEM_BOOMERANG
    public const int Boomerang = 0x06;
    // constants/common/items.s: ITEM_HARP
    public const int Harp = 0x11;
    // constants/common/items.s: ITEM_FEATHER
    public const int Feather = 0x17;
    // constants/common/items.s: ITEM_SEED_SATCHEL
    public const int SeedSatchel = 0x19;
    // constants/common/items.s: ITEM_DUST
    public const int Dust = 0x1a;
    // constants/common/items.s: ITEM_EMBER_SEED
    public const int EmberSeed = 0x20;
    // constants/common/items.s: ITEM_SCENT_SEED
    public const int ScentSeed = 0x21;
    // constants/common/items.s: ITEM_PEGASUS_SEED
    public const int PegasusSeed = 0x22;
    // constants/common/items.s: ITEM_GALE_SEED
    public const int GaleSeed = 0x23;
    // constants/common/items.s: ITEM_MYSTERY_SEED
    public const int MysterySeed = 0x24;
}
