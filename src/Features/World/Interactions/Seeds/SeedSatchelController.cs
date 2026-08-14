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
    private readonly System.Action<int> _playSound;
    private readonly SeedShooterRecord _shooter;
    private bool _shooterActive;
    private bool _shooterPrimaryButton;
    private int _shooterAngle;
    private int _shooterAimCounter;
    private int _shooterPostShotCounter;
    private bool _shooterFired;

    public bool ShooterActive => _shooterActive;
    internal int ShooterAngle => _shooterAngle;
    internal int ShooterAimCounter => _shooterAimCounter;
    internal int ShooterPostShotCounter => _shooterPostShotCounter;

    public SeedSatchelController(
        InventoryState inventory,
        RoomEntityManager entities,
        SeedSatchelDatabase database,
        RoomSession rooms,
        System.Action<int>? playSound = null)
    {
        _inventory = inventory;
        _entities = entities;
        _database = database;
        _rooms = rooms;
        _playSound = playSound ?? (_ => { });
        _shooter = SeedShooterRecord.Load();
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
                "the imported active-seed slice does not include it.");
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

    public bool TryBeginShooter(
        Player player,
        bool primaryButton,
        Vector2 movementInput)
    {
        if (_shooterActive || _entities.HasActiveSeedProjectile ||
            !_inventory.HasSelectedShooterSeed())
        {
            return false;
        }
        _shooterActive = true;
        _shooterPrimaryButton = primaryButton;
        _shooterAngle = movementInput.LengthSquared() > 0.01f
            ? AngleForInput(movementInput)
            : AngleForDirection(player.FacingVector);
        _shooterAimCounter = _shooter.AimLockout;
        _shooterPostShotCounter = 0;
        _shooterFired = false;
        player.FaceShooterDirection(DirectionForAngle(_shooterAngle));
        return true;
    }

    public bool UpdateShooter(
        Player player,
        Vector2 movementInput,
        bool primaryHeld,
        bool secondaryHeld,
        bool directionJustPressed = false)
    {
        if (!_shooterActive)
            return false;
        if (_shooterFired)
        {
            _shooterPostShotCounter--;
            if (_shooterPostShotCounter == 0)
            {
                player.FaceShooterDirection(DirectionForAngle(_shooterAngle));
                ClearShooter();
            }
            return true;
        }

        bool held = _shooterPrimaryButton ? primaryHeld : secondaryHeld;
        if (!held)
        {
            FireShooter(player);
            return true;
        }
        if (UpdateShooterAngle(movementInput, directionJustPressed))
            player.QueueRedraw();
        return true;
    }

    public void InterruptShooter() => ClearShooter();

    private void FireShooter(Player player)
    {
        int seedItem = TreasureDatabase.TreasureEmberSeeds +
            _inventory.ShooterSelectedSeeds;
        if (!_database.TryGet(seedItem, out SeedRecord record))
        {
            GD.PushError(
                $"Unsupported active Shooter child ITEM ${seedItem:x2}; " +
                "the imported active-seed slice does not include it.");
            ClearShooter();
            return;
        }
        _entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(
            player.Position,
            DirectionForAngle(_shooterAngle),
            record,
            _rooms.ActiveGroup,
            SeedLaunchKind.Shooter,
            _shooterAngle));
        if (!_inventory.TryConsumeSelectedShooterSeed(out int consumed) ||
            consumed != seedItem)
        {
            throw new System.InvalidOperationException(
                $"Shooter child ${seedItem:x2} was allocated without its selected BCD seed count.");
        }
        _playSound(_shooter.Sound);
        _shooterFired = true;
        _shooterPostShotCounter = _shooter.PostShotWait;
    }

    private bool UpdateShooterAngle(Vector2 input, bool directionJustPressed)
    {
        if (!directionJustPressed)
        {
            _shooterAimCounter = (_shooterAimCounter - 1) & 0xff;
            if (_shooterAimCounter != 0)
                return false;
        }
        if (input.LengthSquared() <= 0.01f)
            return false;
        int requested = AngleForInput(input);
        if (requested != _shooterAngle)
        {
            int clockwise = (requested - _shooterAngle + 8) & 7;
            _shooterAngle = (_shooterAngle + (clockwise is > 0 and < 4 ? 1 : -1)) & 7;
            _shooterAimCounter = _shooter.AimLockout;
            return true;
        }
        return false;
    }

    private void ClearShooter()
    {
        _shooterActive = false;
        _shooterFired = false;
        _shooterAimCounter = 0;
        _shooterPostShotCounter = 0;
    }

    private static int AngleForDirection(Vector2I direction) =>
        direction == Vector2I.Up ? 0 :
        direction == Vector2I.Right ? 2 :
        direction == Vector2I.Down ? 4 :
        direction == Vector2I.Left ? 6 :
        throw new System.ArgumentOutOfRangeException(nameof(direction));

    private static int AngleForInput(Vector2 input)
    {
        double radians = System.Math.Atan2(input.X, -input.Y);
        return (int)System.Math.Round(radians / (System.Math.PI / 4.0),
            System.MidpointRounding.AwayFromZero) & 7;
    }

    private static Vector2I DirectionForAngle(int angle) => angle switch
    {
        0 => Vector2I.Up,
        1 => new Vector2I(1, -1),
        2 => Vector2I.Right,
        3 => Vector2I.One,
        4 => Vector2I.Down,
        5 => new Vector2I(-1, 1),
        6 => Vector2I.Left,
        7 => new Vector2I(-1, -1),
        _ => throw new System.ArgumentOutOfRangeException(nameof(angle))
    };
}

internal readonly record struct SeedShooterRecord(
    int Item,
    int SubId,
    int SpeedRaw,
    int Bounces,
    int AimLockout,
    int PostShotWait,
    int Sound,
    Vector2I[] Offsets,
    int[] NonBounceDungeonTiles,
    byte[][] ItemPassableTiles,
    string WeaponSprite,
    int WeaponVramTileBase,
    int WeaponPalette,
    bool WeaponSourceGrayscaleInverted,
    string[] WeaponOam,
    string Source)
{
    internal static SeedShooterRecord Load()
    {
        GeneratedTable table = GeneratedTable.Load(
            "res://assets/oracle/metadata/seed_shooter.tsv",
            new GeneratedTableSchema(
                "seed shooter",
                GeneratedTableKeySemantics.Unique,
                ["item", "subid", "speed-raw", "bounces", "aim-lockout",
                    "post-shot-wait", "sound", "offsets",
                    "non-bounce-dungeon-tiles", "item-passable-tiles",
                    "weapon-sprite", "weapon-vram-tile-base",
                    "weapon-palette", "weapon-source-grayscale-inverted",
                    "weapon-oam", "source"],
                ["item"],
                headerRequired: true));
        if (table.Rows.Count != 1)
            throw new System.InvalidOperationException("Expected one ITEM_SHOOTER record.");
        GeneratedTableRow row = table.Rows[0];
        Vector2I[] offsets = System.Array.ConvertAll(
            row.RequiredString(7).Split(';'), value =>
            {
                string[] pair = value.Split(',');
                if (pair.Length != 2 ||
                    !int.TryParse(pair[0], out int y) ||
                    !int.TryParse(pair[1], out int x))
                {
                    throw row.Invalid(7, "eight y,x offset pairs");
                }
                return new Vector2I(x, y);
            });
        int[] nonBounce = System.Array.ConvertAll(
            row.RequiredString(8).Split(','), value =>
                System.Convert.ToInt32(value, 16));
        byte[][] passableTiles = ParseItemPassableTiles(
            row.RequiredString(9), row);
        string[] weaponOam = row.RequiredString(14).Split('|');
        var record = new SeedShooterRecord(
            row.HexByte(0), row.HexByte(1), row.HexByte(2),
            row.UnsignedDecimal(3), row.UnsignedDecimal(4),
            row.UnsignedDecimal(5), row.HexByte(6), offsets,
            nonBounce, passableTiles, row.RequiredString(10),
            row.HexByte(11), row.HexByte(12), row.Boolean01(13), weaponOam,
            row.RequiredString(15));
        if (record.Item != InventoryState.ItemShooter || record.SubId != 0x63 ||
            record.SpeedRaw != 0x78 || record.Bounces != 3 ||
            record.AimLockout != 16 || record.PostShotWait != 12 ||
            record.Sound != 0xcb || offsets.Length != 8 ||
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

    private static byte[][] ParseItemPassableTiles(
        string encoded,
        GeneratedTableRow row)
    {
        string[] groups = encoded.Split(';');
        if (groups.Length != 6)
            throw row.Invalid(9, "six collision-set tile lists");
        var result = new byte[groups.Length][];
        for (int index = 0; index < groups.Length; index++)
        {
            string[] pair = groups[index].Split(':');
            if (pair.Length != 2 ||
                !int.TryParse(pair[0], out int collisionSet) ||
                collisionSet != index)
            {
                throw row.Invalid(
                    9, "ordered collision-set:tile-list entries");
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
                    throw row.Invalid(9, "decimal byte tile lists");
                }
            }
        }
        return result;
    }
}
