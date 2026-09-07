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
        _inventory.GiveTreasure(InventoryState.ItemFlute, 0x0d);
        LoadValidationRoom(0, 0x36);
        foreach (int companion in new[] { 0x0b, 0x0c, 0x0d, 0x0b })
        {
            OracleRoomData before = _rooms.CurrentRoom;
            _debugFlagMenu.OpenImmediatelyForValidation();
            _debugFlagScreen.SelectTreasureForValidation(
                $"TREASURE_OBJECT_FLUTE_{companion - 0x0b:x2}");
            _debugFlagScreen.ActivateSelection();
            FailIf(_inventory.AnimalCompanion != companion ||
                _saveData.ReadWramByte(0xc610) != companion ||
                !_inventory.HasTreasure(InventoryState.ItemFlute),
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
        static Vector2 Point(int packed) => new((packed & 15) * 16 + 8, (packed >> 4) * 16 + 8);
        void Step(int count = 1)
        {
            for (int i = 0; i < count; i++)
                _entities.Update(1.0 / 60, _player);
        }
        var shooter = SeedShooterRecord.Load();
        var ember = new SeedSatchelDatabase().Ember;
        var data = new DungeonMechanicDatabase();
        _saveData.SetRoomFlag(0, 0x54, 0x40, false);
        _saveData.SetRoomFlag(0, 0x54, OracleSaveData.RoomFlagItem, false);
        _runtimeState.SetWramByte(OracleRuntimeState.SwitchStateAddress, 0xff);
        LoadValidationRoom(0, 0x54);
        _player.WarpTo(Point(0x61));
        OracleRoomData room = _currentRoom;
        FailIf(_runtimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != 0,
            "Room 0:54 must clear wSwitchState before parsing $6b:$0f.");
        Step();
        FailIf(_entities.Entities<DungeonSwitchRoomEntity>() is not [{ PackedPosition: 0x68, SwitchMask: 1 }],
            "Room 0:54 $6b:$0f did not allocate PART_SWITCH $05:$01 at $68.");
        EmberSeedEffect Shoot() => _entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(
            Point(0x61) - shooter.Offsets[2], Vector2I.Right, ember, 0,
            LaunchKind: SeedLaunchKind.Shooter, Angle: 2));
        EmberSeedEffect seed = Shoot();
        bool crossedDown = false;
        bool crossedUp = false;
        for (int i = 0; i < 110 && !seed.Finished; i++)
        {
            Step();
            FailIf(seed.BouncesRemaining != 3 || seed.Angle != 2,
                $"Room 0:54 seed bounced at {seed.PrecisePosition} before burning tree $ce.");
            if (seed.PrecisePosition.X is >= 48 and < 96)
            {
                crossedDown = true;
                FailIf(seed.ShooterElevation != 1,
                    "Room 0:54 $0b must increase elevation once, retaining it across water.");
            }
            if (seed.PrecisePosition.X >= 112)
            {
                crossedUp = true;
                FailIf(seed.ShooterElevation != 0,
                    "Room 0:54 $0a must consume the previous descent exactly once.");
            }
        }
        FailIf(!crossedDown || !crossedUp || !seed.Finished || room.GetMetatile(Point(0x67)) == 0xce,
            "Room 0:54 shooter seed did not cross both cliffs and burn the tree.");
        FailIf(_saveData.HasRoomFlag(0, 0x54, 0x40),
            "Burning room 0:54's tree must not activate its switch.");

        Shoot();
        for (int i = 0; i < 60 && !_saveData.HasRoomFlag(0, 0x54, 0x40); i++)
            Step();
        var controller = _entities.Entities<NuunBridgeRoomEntity>().Single();
        FailIf(!_saveData.HasRoomFlag(0, 0x54, 0x40) || !controller.DisablesMovement ||
            _entities.Entities<DungeonSwitchRoomEntity>().Count != 0 || room.GetMetatile(Point(0x68)) != 0,
            "Room 0:54 seed hit must latch $40, delete the switch and start the restricted sequence.");
        _sound.ClearPlayRequestAudit();
        for (int t = 1; t <= 169; t++)
        {
            Step();
            int doorSounds = t >= 126 ? 3 : t >= 84 ? 2 : t >= 42 ? 1 : 0;
            FailIf(_sound.PlayRequestsFor(data.DoorSound) != doorSounds ||
                _sound.PlayRequestsFor(data.SolveSound) != (t >= 168 ? 1 : 0),
                $"Room 0:54 simple script has wrong sound/yield timing at update {t}.");
            FailIf(room.GetMetatile(Point(0x43)) != (t >= 85 ? 0x1d : 0xfa) ||
                room.GetMetatile(Point(0x44)) != (t >= 127 ? 0x1d : 0xf4) ||
                room.GetMetatile(Point(0x53)) != (t >= 85 ? 0x1e : t >= 43 ? 0xf4 : 0xfa) ||
                room.GetMetatile(Point(0x54)) != (t >= 127 ? 0x1e : 0xfa),
                $"Room 0:54 bridge tile stages changed at the wrong boundary, update {t}.");
            FailIf(controller.DisablesMovement != (t < 169),
                $"Room 0:54 restrictions ended at the wrong boundary, update {t}.");
        }
        LoadValidationRoom(0, 0x55);
        LoadValidationRoom(0, 0x54);
        Step(2);
        FailIf(_entities.Entities<NuunBridgeRoomEntity>().Count != 0 ||
            _entities.Entities<DungeonSwitchRoomEntity>().Count != 0 ||
            new[] { 0x43, 0x44, 0x45 }.Any(p => _currentRoom.GetMetatile(Point(p)) != 0x1d) ||
            new[] { 0x53, 0x54, 0x55 }.Any(p => _currentRoom.GetMetatile(Point(p)) != 0x1e) ||
            _currentRoom.GetMetatile(Point(0x68)) != 0x9e,
            "Room 0:54 re-entry must restore the complete bridge and suppress the one-shot switch.");

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
        GD.Print("Validated room 0:54 seed cliff elevation/cache, tree burning, one-shot switch, 169-update bridge script and re-entry.");
    }
}
