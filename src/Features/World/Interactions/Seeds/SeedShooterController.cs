
namespace Oracles;

/// <summary>The ITEM_SHOOTER ($0f) parent, separate from the shared Satchel parent.</summary>
public sealed class SeedShooterController
{
    private readonly InventoryState _inventory;
    private readonly RoomEntityManager _entities;
    private readonly SeedSatchelDatabase _database;
    private readonly RoomSession _rooms;
    private readonly Action<int> _playSound;
    private readonly SeedShooterRecord _shooter;
    private bool _shooterActive;
    private int _shooterAngle;
    private int _shooterAimCounter;
    private int _shooterPostShotCounter;
    private bool _shooterFired;

    public bool ShooterActive => _shooterActive;
    internal int ShooterAngle => _shooterAngle;
    internal int ShooterAimCounter => _shooterAimCounter;
    internal int ShooterPostShotCounter => _shooterPostShotCounter;

    public SeedShooterController(InventoryState inventory, RoomEntityManager entities,
        SeedSatchelDatabase database, RoomSession rooms, Action<int> playSound)
    {
        if (rooms.AssetRoot != entities.AssetRoot || rooms.AssetRoot != database.AssetRoot ||
            rooms.AssetRoot != inventory.AssetRoot || !ReferenceEquals(rooms.RuntimeState, entities.RuntimeState) ||
            !ReferenceEquals(rooms.RuntimeState, inventory.RuntimeState) || !ReferenceEquals(rooms.SaveData, inventory.SaveData))
            throw new ArgumentException("Shooter input requires the selected room, inventory and runtime owners.");
        _inventory = inventory;
        _entities = entities;
        _database = database;
        _rooms = rooms;
        _playSound = playSound;
        _shooter = SeedShooterRecord.Load(rooms.AssetRoot);
    }

    public bool TryBeginShooter(
        Player player,
        bool primaryButton,
        Vector2 movementInput)
    {
        if (_shooterActive ||
            !_inventory.HasSelectedShooterSeed())
        {
            return false;
        }
        _shooterActive = true;
        player.NotifyParentItemAnimationStarted(TreasureId.Shooter);
        ParentItemButtons.Set(_inventory.RuntimeState, 2, primaryButton ? 1 : 2);
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

        // Shooter state 1 calls clearSelfIfNoSeeds on every parent update,
        // before checking held input or turning the aiming animation.
        if (!_inventory.HasSelectedShooterSeed())
        {
            ClearShooter();
            return true;
        }
        bool held = ParentItemButtons.IsHeld(_inventory.RuntimeState, 2, primaryHeld, secondaryHeld);
        if (!held)
        {
            FireShooter(player);
            return true;
        }
        if (UpdateShooterAngle(movementInput, directionJustPressed))
            player.QueueRedraw();
        // seedsParent @determineBaseAnimation republishes priority4 while
        // aiming; release and the post-shot wait retain a cart-cleared claim.
        _inventory.RuntimeState.SetWramByte(0xd23f, 4);
        return true;
    }

    public void InterruptShooter() => ClearShooter();

    private void FireShooter(Player player)
    {
        // parentItemCode_shooter checks wIsSeedShooterInUse on release,
        // independently of the selected child ID and Satchel subid $00.
        if (_entities.HasActiveShooterSeed)
        {
            ClearShooter();
            return;
        }
        int seedItem = TreasureId.EmberSeeds +
            _inventory.ShooterSelectedSeeds;
        if (!_database.TryGet(seedItem, out SeedRecord record))
        {
            throw new InvalidOperationException(
                $"{_database.AssetRoot}metadata/seed_satchel.tsv: missing Shooter child ITEM ${seedItem:x2}.");
        }
        // Unlike Satchel, parentItemCode_shooter does not test the carry flag
        // from itemCreateChildWithID: a full pool still consumes ammo and enters
        // the twelve-update post-shot state, without creating a projectile.
        if (_entities.DynamicItemSlotAvailable)
            _entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(
                player.PrecisePosition,
                DirectionForAngle(_shooterAngle),
                record,
                _rooms.ActiveGroup,
                SeedLaunchKind.Shooter,
                _shooterAngle,
                player.ItemCreationZFixed));
        else
            _entities.WriteReservedBraceletAngle(_shooterAngle * 4);
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
        if (_shooterActive) ParentItemAnimationState.Clear(_inventory.RuntimeState, 2, TreasureId.Shooter);
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
        ObjectAngle.Up => Vector2I.Up,
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
