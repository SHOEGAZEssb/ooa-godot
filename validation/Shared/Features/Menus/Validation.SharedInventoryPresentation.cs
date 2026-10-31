using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSharedInventoryPresentation()
    {
        var ages = InventoryPresentationDatabase.For(AgesGameContent.AssetRoot);
        var seasons = InventoryPresentationDatabase.For(SeasonsGameContent.AssetRoot);
        // bank2.s:subscreen1TreasureData, selected ROM_SEASONS branch. Keep
        // the repeated Star Ore row and overlapping jewel coordinates.
        int[] ids = [0x54, 0x2e, 0x2f, 0x41, 0x36, 0x34, 0x42, 0x48, 0x43,
            0x49, 0x58, 0x4a, 0x4b, 0x4c, 0x4d, 0x4e, 0x4f, 0x45, 0x46,
            0x45, 0x47, 0x44, 0x50, 0x52, 0x51, 0x53, 0x2d];
        int[] positions = [0x01, 0x01, 0x04, 0x07, 0x0a, 0x0d, 0x31, 0x34, 0x34,
            0x27, 0x47, 0x37, 0x3a, 0x3d, 0x3e, 0x4d, 0x4e, 0x61, 0x61,
            0x61, 0x61, 0x61, 0x64, 0x64, 0x67, 0x6a, 0x6d];
        FailIf(!seasons.PassiveTreasures.Select(row => row.TreasureId).SequenceEqual(ids) ||
            !seasons.PassiveTreasures.Select(row => row.Position).SequenceEqual(positions),
            "Seasons subscreen1TreasureData must retain its 27 source rows, duplicates and jewel coordinates.");
        FailIf(ages.PassiveTreasures.Count != 32 || ReferenceEquals(ages, seasons) ||
            !ReferenceEquals(seasons, InventoryPresentationDatabase.For(SeasonsGameContent.AssetRoot)),
            "Inventory presentation caches must bind each game's own source shape and pack.");
        FailIf(!seasons.InventoryMakuSeed.Select(part => part.Attributes).SequenceEqual(new[] { 0x0c, 0x2c, 0x0f, 0x0f }) ||
            !ages.InventoryMakuSeed.Select(part => part.Attributes).SequenceEqual(new[] { 0x0f, 0x2f, 0x0b, 0x0b }),
            "inventoryMenuDrawSprites@makuSeedSprite uses distinct native palette selections in Ages and Seasons.");
        FailIf(!seasons.InventoryItemSlots.Select(row => row.TilemapOffset).SequenceEqual(new[] {
                0x063, 0x067, 0x06b, 0x06f, 0x0c3, 0x0c7, 0x0cb, 0x0cf,
                0x123, 0x127, 0x12b, 0x12f, 0x183, 0x187, 0x18b, 0x18f }) ||
            !seasons.InventoryItemSubmenu(2).Positions.Select(row => row.RawX).SequenceEqual(new[] { 7, 11 }) ||
            !seasons.InventoryItemSubmenu(5).Positions.Select(row => row.RawX).SequenceEqual(new[] { 3, 6, 9, 12, 15 }) ||
            !seasons.InventorySeedSubmenuSprites.Select(row => row.Tile).SequenceEqual(new[] { 6, 8, 10, 12, 14 }),
            "Seasons must retain native item slots, overlapping table_5ae5 suffixes and five seed OAM pointers.");
        var ring = RingPresentationDatabase.For(SeasonsGameContent.AssetRoot);
        FailIf(!ring.RingBoxOffsets.Select(row => row.XOffset).SequenceEqual(new[] { 0x38, 0x50, 0x68, 0x80, 0x98 }) ||
            ring.RingOam("page-arrows")[1].Attributes != 0x24 ||
            ReferenceEquals(ring, RingPresentationDatabase.For(AgesGameContent.AssetRoot)),
            "Shared ring presentation must load Seasons' own ordered OAM without an Ages map or Harp dependency.");
        // Native loadEquippedItemSpriteData compares $84 in Ages, $86 in
        // Seasons; loadItemIconGfx's $a3/+2 Harp branch is Ages-only.
        foreach (int sprite in new[] { 0x83, 0x84, 0x85, 0x86 })
        foreach (int palette in Enumerable.Range(0, 6))
        {
            FailIf(ItemIconAtlas.EquippedLeftPalette(OracleGame.Ages, sprite, palette) !=
                    (sprite < 0x84 ? ((palette - 3) | 1) & 7 : palette) ||
                ItemIconAtlas.EquippedLeftPalette(OracleGame.Seasons, sprite, palette) !=
                    (sprite < 0x86 ? ((palette - 3) | 1) & 7 : palette),
                $"bank2.s: equipped icon ${sprite:x2} must use the selected game's palette boundary.");
        }
        foreach (int sprite in new[] { 0xa3, 0xa4, 0xa7, 0xa8, 0xab, 0xac })
            FailIf(ItemIconAtlas.EquippedSprite(OracleGame.Ages, sprite) != sprite + 2 ||
                ItemIconAtlas.EquippedSprite(OracleGame.Seasons, sprite) != sprite,
                "Only Ages shifts the equipped Harp's source index to its smaller pair.");
    }
}
