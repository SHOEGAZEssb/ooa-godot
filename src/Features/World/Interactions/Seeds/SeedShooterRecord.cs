
namespace Oracles;

internal readonly record struct SeedShooterRecord(
    int Item,
    int SpeedRaw,
    int AimLockout,
    int PostShotWait,
    int Sound,
    Vector2I[] Offsets,
    byte[][] NonBounceTiles,
    byte[][] ItemPassableTiles,
    string WeaponSprite,
    int WeaponVramTileBase,
    int WeaponPalette,
    bool WeaponSourceGrayscaleInverted,
    string[] WeaponOam,
    string Source)
{
    internal static SeedShooterRecord Load(string assetRoot)
    {
        GeneratedTable table = GeneratedTable.Load(
            assetRoot + "metadata/seed_shooter.tsv",
            new GeneratedTableSchema(
                "seed shooter",
                GeneratedTableKeySemantics.Unique,
                ["item", "speed-raw", "aim-lockout",
                    "post-shot-wait", "sound", "offsets",
                    "non-bounce-tiles", "item-passable-tiles",
                    "weapon-sprite", "weapon-vram-tile-base",
                    "weapon-palette", "weapon-source-grayscale-inverted",
                    "weapon-oam", "source"],
                ["item"],
                headerRequired: true));
        if (table.Rows.Count != 1)
            throw new System.InvalidOperationException("Expected one ITEM_SHOOTER record.");
        GeneratedTableRow row = table.Rows[0];
        Vector2I[] offsets = System.Array.ConvertAll(
            row.RequiredString(5).Split(';'), value =>
            {
                string[] pair = value.Split(',');
                if (pair.Length != 2 ||
                    !int.TryParse(pair[0], out int y) ||
                    !int.TryParse(pair[1], out int x))
                {
                    throw row.Invalid(5, "eight y,x offset pairs");
                }
                return new Vector2I(x, y);
            });
        byte[][] nonBounce = ParseItemPassableTiles(row.RequiredString(6), row, 6);
        byte[][] passableTiles = ParseItemPassableTiles(
            row.RequiredString(7), row, 7);
        string[] weaponOam = row.RequiredString(12).Split('|');
        var record = new SeedShooterRecord(
            row.HexByte(0), row.HexByte(1),
            row.UnsignedDecimal(2), row.UnsignedDecimal(3), row.HexByte(4), offsets,
            nonBounce, passableTiles, row.RequiredString(8),
            row.HexByte(9), row.HexByte(10), row.Boolean01(11), weaponOam,
            row.RequiredString(13));
        if (record.Item != TreasureId.Shooter ||
            record.SpeedRaw != ObjectSpeed.Speed300 ||
            record.AimLockout != 16 || record.PostShotWait != 12 ||
            record.Sound != SoundId.SndSeedShooter || offsets.Length != 8 ||
            record.NonBounceTiles.Length != 6 ||
            record.NonBounceTiles[0].Length != 9 ||
            record.NonBounceTiles[2].Length != 2 ||
            record.ItemPassableTiles.Length != 6 ||
            record.ItemPassableTiles[0].Length != 2 ||
            record.ItemPassableTiles[1].Length != 3 ||
            record.ItemPassableTiles[2].Length != 16 ||
            record.ItemPassableTiles[3].Length != 0 ||
            record.ItemPassableTiles[4].Length != 2 ||
            record.ItemPassableTiles[5].Length != 16 ||
            record.WeaponSprite != "spr_seed_shooter" ||
            record.WeaponVramTileBase != 0x52 ||
            record.WeaponPalette != 0 ||
            record.WeaponSourceGrayscaleInverted ||
            record.WeaponOam.Length != 8)
        {
            throw new System.InvalidOperationException(
                "Imported ITEM_SHOOTER contract is incomplete.");
        }
        return record;
    }

    internal bool CanPassSolidTile(OracleRoomData room, Vector2 point)
    {
        int collisionSet = room.ActiveCollisions;
        return collisionSet >= 0 && collisionSet < ItemPassableTiles.Length &&
            System.Array.IndexOf(
                ItemPassableTiles[collisionSet],
                room.GetMetatile(point)) >= 0;
    }

    internal bool DoesNotBounce(OracleRoomData room, byte tile) =>
        System.Array.IndexOf(NonBounceTiles[room.ActiveCollisions], tile) >= 0;

    private static byte[][] ParseItemPassableTiles(
        string encoded,
        GeneratedTableRow row,
        int column)
    {
        string[] groups = encoded.Split(';');
        if (groups.Length != 6)
            throw row.Invalid(column, "six collision-set tile lists");
        var result = new byte[groups.Length][];
        for (int index = 0; index < groups.Length; index++)
        {
            string[] pair = groups[index].Split(':');
            if (pair.Length != 2 ||
                !int.TryParse(pair[0], out int collisionSet) ||
                collisionSet != index)
            {
                throw row.Invalid(
                    column, "ordered collision-set:tile-list entries");
            }
            if (pair[1].Length == 0)
            {
                result[index] = [];
                continue;
            }
            string[] tiles = pair[1].Split(',');
            result[index] = new byte[tiles.Length];
            for (int tileIndex = 0; tileIndex < tiles.Length; tileIndex++)
            {
                if (!byte.TryParse(
                        tiles[tileIndex], out result[index][tileIndex]))
                {
                    throw row.Invalid(column, "decimal byte tile lists");
                }
            }
        }
        return result;
    }
}
