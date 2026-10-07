using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateNuunCompanionLayouts()
    {
        int[] nuunRooms = [0x16, 0x17, 0x26, 0x27, 0x35, 0x36, 0x37];
        var world = new OracleWorldData();
        byte[] packs = Godot.FileAccess.GetFileAsBytes("res://assets/oracle/groups/roomPacksPresent.bin");
        FailIf(!Enumerable.Range(0, 256).Where(room => packs[room] == 0x7f).SequenceEqual(nuunRooms),
            "Nuun room pack $7f membership diverged from bank0.loadRoomMusic/checkTilesetOverride.");
        foreach (int room in nuunRooms)
        {
            var variants = new List<OracleRoomData>();
            foreach (int companion in new[] { 0x0b, 0x0c, 0x0d, 0 })
            {
                int layoutGroup = companion == 0x0b ? 0 : companion == 0x0c ? 1 : 3;
                byte[] expected = Godot.FileAccess.GetFileAsBytes($"res://assets/oracle/rooms/small/room{layoutGroup:x2}{room:x2}.bin");
                OracleRoomData loaded = world.LoadRoom(0, room, 0, companion);
                FailIf(!loaded.Layout.SequenceEqual(expected) || loaded.Group != 0 || loaded.Id != room ||
                    loaded.TilesetId != world.GetTilesetId(0, room),
                    $"Nuun room 0:{room:x2}, companion ${companion:x2}, must use layout group ${layoutGroup:x2} with present room identity/tileset.");
                if (companion != 0) variants.Add(loaded);
                else FailIf(!ReferenceEquals(loaded, variants[2]), "Unassigned Nuun layout did not use the source Moosh fallback.");
            }
            FailIf(ReferenceEquals(variants[0], variants[1]) || ReferenceEquals(variants[1], variants[2]),
                $"Nuun room 0:{room:x2} cached different companion layouts as one mutable room.");
            byte original = variants[0].Layout[0];
            byte dimitri = variants[1].Layout[0], moosh = variants[2].Layout[0];
            variants[0].Layout[0] ^= 0xff;
            FailIf(variants[1].Layout[0] != dimitri || variants[2].Layout[0] != moosh,
                "Nuun tile mutation leaked between companion layouts.");
            variants[0].Layout[0] = original;
            FailIf(!ReferenceEquals(world.LoadRoom(0, room, 0, 0x0b), variants[0]),
                "Returning to Ricky's Nuun layout did not preserve its own cached room.");
            FailIf(!ReferenceEquals(world.LoadRoom(1, room, 1, 0x0b), world.LoadRoom(1, room, 1, 0x0d)),
                "Nuun override changed a past-overworld room.");
        }
        FailIf(!ReferenceEquals(world.LoadRoom(0, 0x34, 0, 0x0b), world.LoadRoom(0, 0x34, 0, 0x0d)),
            "Nuun override escaped room pack $7f.");

        foreach (bool linked in new[] { false, true })
        foreach (int companion in new[] { 0x0b, 0x0c, 0x0d })
        {
            _saveData.SetLinkedGame(linked);
            _inventory.AssignAnimalCompanion(companion);
            LoadValidationRoom(0, 0x36);
            var outgoing = _rooms.CurrentRoom;
            byte[] before = (byte[])outgoing.Layout.Clone();
            var expected = _rooms.GetRoom(0, 0x35);
            int layoutGroup = companion == 0x0b ? 0 : companion == 0x0c ? 1 : 3;
            FailIf(!expected.Layout.SequenceEqual(Godot.FileAccess.GetFileAsBytes(
                $"res://assets/oracle/rooms/small/room{layoutGroup:x2}35.bin")),
                $"RoomSession ignored companion ${companion:x2} when preloading Nuun 0:35.");
            _player.WarpTo(new Vector2(1, 0x48), recordSafe: false);
            _transitions.BeginScroll(_player, Vector2I.Left, 0x35);
            for (int frame = 0; frame < 60 && _transitions.ScrollActive; frame++)
            {
                _transitions.UpdateScroll(1.0 / 60.0);
                _entities.Update(1.0 / 60.0, _player);
                _roomEvents.Update(1.0 / 60.0);
            }
            FailIf(_transitions.ScrollActive || !ReferenceEquals(_rooms.CurrentRoom, expected) ||
                !outgoing.Layout.SequenceEqual(before),
                $"Nuun companion ${companion:x2} scrolling replaced the selected terrain or mutated the outgoing room.");
            FailIf(!OracleSaveData.TryDeserialize(_saveData.Serialize(), out var saved) || saved is null,
                "Nuun save-image fixture could not reload.");
            var restored = new RoomSession(0, 0x35, () => 0, () => { }, saved!, countAsRoomEntry: false);
            FailIf(!restored.CurrentRoom.Layout.SequenceEqual(expected.Layout),
                $"Nuun companion ${companion:x2} terrain did not survive save/reload.");
        }
        // Exercise the actual F1 item browser and close boundary, without a warp
        // or room re-entry masking a stale displayed layout.
        _inventory.GiveTreasure(TreasureId.Flute, 0x0d);
        LoadValidationRoom(0, 0x36);
        foreach (int companion in new[] { 0x0b, 0x0c, 0x0d, 0x0b })
        {
            OracleRoomData before = _rooms.CurrentRoom;
            _debugFlagMenu.OpenImmediatelyForValidation();
            _debugFlagScreen.SelectTreasureForValidation(
                $"TREASURE_OBJECT_FLUTE_{companion - 0x0b:x2}");
            _debugFlagScreen.ActivateSelection();
            FailIf(_inventory.AnimalCompanion != companion ||
                _saveData.ReadWramByte(WramAddress.wAnimalCompanion) != companion ||
                !_inventory.HasTreasure(TreasureId.Flute),
                $"F1 did not switch an owned flute directly to companion ${companion:x2}.");
            FailIf(!ReferenceEquals(before, _rooms.CurrentRoom),
                "F1 reloaded terrain while the debug modal still owned gameplay.");
            _debugFlagMenu.CloseImmediatelyForValidation();
            int layoutGroup = companion == 0x0b ? 0 : companion == 0x0c ? 1 : 3;
            FailIf(!_rooms.CurrentRoom.Layout.SequenceEqual(Godot.FileAccess.GetFileAsBytes(
                    $"res://assets/oracle/rooms/small/room{layoutGroup:x2}36.bin")) ||
                ReferenceEquals(before, _rooms.CurrentRoom) || _debugFlagMenu.IsActive,
                $"Closing F1 did not refresh Nuun 0:36 for companion ${companion:x2}.");
        }
        GD.Print("Validated all seven Nuun rooms, three companion layouts, F1 switching, unassigned fallback, cache isolation, scrolling and linked/unlinked save reload.");
    }

    private void ValidateRoom054SeedCliffsAndBridge()
    {
        var shooter = SeedShooterRecord.Load();
        var ember = new SeedSatchelDatabase().Ember;

        // An uphill face without an earlier descent must bounce and retain
        // the original byte underflow; the cache must not erase that state.
        var testRoom = new RoomSession(0, 0x54, () => 0, () => { }, OracleSaveData.CreateStandardGame()).CurrentRoom;
        var uphill = new EmberSeedEffect();
        uphill.Initialize(ember, testRoom, new BreakableTileDatabase(),
            new Vector2(96, 104) - shooter.Offsets[2], Vector2I.Right,
            _ => { }, (_, _) => { }, () => { }, () => 0, _ => null, null, 0,
            launchKind: SeedLaunchKind.Shooter, angle: 2);
        var spawns = new List<RoomEntitySpawn>();
        uphill.UpdateFrame(1, spawns);
        uphill.UpdateFrame(2, spawns);
        FailIf(uphill.Angle != 6 || uphill.BouncesRemaining != 2 || uphill.ShooterElevation != 0xff,
            "itemCheckCanPassSolidTile must bounce on an unpaired $0a ascent and retain var3e=$ff.");
        uphill.Free();
        foreach ((byte tile, int angle, byte elevation, int finalAngle, int bounces) in
            new (byte, int, byte, int, int)[] { (0x0b, 1, 1, 1, 3), (0x0b, 3, 1, 3, 3), (0x0a, 1, 0xff, 5, 2) })
        {
            Vector2 start = new(40, 104);
            testRoom.SetPositionTileAndCollision(start, tile, 0x0f, 0);
            var diagonal = new EmberSeedEffect();
            diagonal.Initialize(ember, testRoom, new BreakableTileDatabase(),
                start - shooter.Offsets[angle], Vector2I.Right,
                _ => { }, (_, _) => { }, () => { }, () => 0, _ => null, null, 0,
                launchKind: SeedLaunchKind.Shooter, angle: angle);
            diagonal.UpdateFrame(1, spawns);
            diagonal.UpdateFrame(2, spawns);
            FailIf(diagonal.ShooterElevation != elevation || diagonal.Angle != finalAngle ||
                diagonal.BouncesRemaining != bounces,
                $"Seed diagonal {angle} on tile ${tile:x2} must predict both probes and commit elevation once.");
            if (elevation == 1)
            {
                diagonal.UpdateFrame(3, spawns);
                FailIf(diagonal.ShooterElevation != 1 || diagonal.BouncesRemaining != 3,
                    "Repeated diagonal probes in one cliff metatile must not increment elevation again.");
            }
            diagonal.Free();
        }
        GD.Print("Validated seed cliff byte underflow, diagonal elevation and repeated-metatile probe caching; the switch/bridge timeline is covered by executed ROM.");
    }
}
