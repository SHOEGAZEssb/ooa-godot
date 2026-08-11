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
        _shooterAngle = AngleForDirection(player.FacingVector);
        _shooterAimCounter = _shooter.AimLockout;
        _shooterPostShotCounter = 0;
        _shooterFired = false;
        UpdateShooterAngle(movementInput);
        return true;
    }

    public bool UpdateShooter(
        Player player,
        Vector2 movementInput,
        bool primaryHeld,
        bool secondaryHeld)
    {
        if (!_shooterActive)
            return false;
        if (_shooterFired)
        {
            _shooterPostShotCounter--;
            if (_shooterPostShotCounter == 0)
                ClearShooter();
            return true;
        }

        bool held = _shooterPrimaryButton ? primaryHeld : secondaryHeld;
        if (!held)
        {
            FireShooter(player);
            return true;
        }
        if (_shooterAimCounter > 0)
            _shooterAimCounter--;
        UpdateShooterAngle(movementInput);
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

    private void UpdateShooterAngle(Vector2 input)
    {
        if (input.LengthSquared() <= 0.01f)
            return;
        int requested = AngleForInput(input);
        if (_shooterAimCounter == 0 && requested != _shooterAngle)
        {
            int clockwise = (requested - _shooterAngle + 8) & 7;
            _shooterAngle = (_shooterAngle + (clockwise is > 0 and < 4 ? 1 : -1)) & 7;
            _shooterAimCounter = _shooter.AimLockout;
        }
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
                    "non-bounce-dungeon-tiles", "source"],
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
        var record = new SeedShooterRecord(
            row.HexByte(0), row.HexByte(1), row.HexByte(2),
            row.UnsignedDecimal(3), row.UnsignedDecimal(4),
            row.UnsignedDecimal(5), row.HexByte(6), offsets,
            nonBounce, row.RequiredString(9));
        if (record.Item != InventoryState.ItemShooter || record.SubId != 0x63 ||
            record.SpeedRaw != 0x78 || record.Bounces != 3 ||
            record.AimLockout != 16 || record.PostShotWait != 12 ||
            record.Sound != 0xcb || offsets.Length != 8)
        {
            throw new System.InvalidOperationException(
                "Imported ITEM_SHOOTER contract is incomplete.");
        }
        return record;
    }
}
