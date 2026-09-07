using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom29eOrbBridge()
    {
        const double update = 1.0 / 60;
        static Vector2 Point(int packed) => new(
            (packed & 0x0f) * 16 + 8, (packed >> 4) * 16 + 8);
        void Step(int count = 1)
        {
            for (int i = 0; i < count; i++)
                _entities.Update(update, _player);
        }
        var data = new DungeonMechanicDatabase();
        FailIf(data.GetRoomRecords(2, 0x9e).Select(r =>
            (r.Order, r.Id, r.SubId, r.PackedPosition, r.Parameter)).ToArray()
            is not [(0, 0xdc, 0x12, 0x13, 0x0c), (1, 0x03, 0, 0x61, 0)],
            "Room 2:9e lost its ordered $dc:$12 controller and $03:$00 orb.");

        foreach (bool batched in new[] { false, true })
        {
            _saveData.SetRoomFlag(2, 0x9e, 0x40, false);
            _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress, 0xff);
            LoadValidationRoom(2, 0x9e);
            _player.WarpTo(Point(0x71));
            var room = _currentRoom;
            var original = Enumerable.Range(0x13, 6)
                .Select(p => room.GetMetatile(Point(p))).ToArray();
            var orb = _entities.Entities<DungeonOrbRoomEntity>().Single();
            FailIf(orb.Position != Point(0x61) || orb.IsOn || orb.ToggleMask != 1,
                "Room 2:9e tile initialization must reset $cc31 before orb initialization.");
            Step(16);
            FailIf(_saveData.HasRoomFlag(2, 0x9e, 0x40) ||
                _entities.Entities<BridgeSpawnerRoomEntity>().Count != 0,
                "Room 2:9e bridge activated without an orb hit.");
            var spawns = new List<RoomEntitySpawn>();
            orb.ApplySwordHit(orb.CollisionBounds, orb.Position, 1,
                EnemyKnockbackStrength.Normal, spawns);
            _sound.ClearPlayRequestAudit();
            Step();
            FailIf(!_saveData.HasRoomFlag(2, 0x9e, 0x40) ||
                _entities.Entities<OrbBridgeControllerRoomEntity>().Count != 0 ||
                _entities.Entities<BridgeSpawnerRoomEntity>().Count != 1 ||
                _sound.PlayRequestsFor(data.SolveSound) != 1 ||
                room.GetMetatile(Point(0x13)) != original[0],
                "Room 2:9e must set $40 and delete $dc:$12 at allocation, before PART $0c runs.");

            for (int half = 0; half < 12; half++)
            {
                int packed = 0x13 + half / 2;
                int before = room.GetMetatile(Point(packed));
                if (batched) _entities.Update(7 * update, _player);
                else Step(7);
                FailIf(room.GetMetatile(Point(packed)) != before ||
                    _sound.PlayRequestsFor(data.DoorSound) != half,
                    $"Room 2:9e PART $0c half ${half:x2} advanced before update $08.");
                Step();
                FailIf(room.GetMetatile(Point(packed)) != ((half & 1) == 0 ? 0x6e : 0x6d) ||
                    _sound.PlayRequestsFor(data.DoorSound) != half + 1,
                    $"Room 2:9e PART $0c half ${half:x2} has wrong tile, position, or sound.");
                if (half == 3)
                {
                    orb.ApplySwordHit(orb.CollisionBounds, orb.Position, 1,
                        EnemyKnockbackStrength.Normal, spawns);
                    FailIf(orb.IsOn, "Room 2:9e second orb hit did not switch it off.");
                }
            }
            FailIf(_entities.Entities<BridgeSpawnerRoomEntity>().Count != 0 ||
                Enumerable.Range(0x13, 6).Any(p => room.GetMetatile(Point(p)) != 0x6d) ||
                _sound.PlayRequestsFor(data.SolveSound) != 1,
                "Room 2:9e bridge must finish after $60 part updates, even with orb off.");
        }

        // Any nonzero toggle byte activates the handler, not only orb bit 0.
        _saveData.SetRoomFlag(2, 0x9e, 0x40, false);
        LoadValidationRoom(2, 0x9e);
        _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress, 0x80);
        Step();
        FailIf(!_saveData.HasRoomFlag(2, 0x9e, 0x40),
            "Room 2:9e $dc:$12 must test the whole wToggleBlocksState byte.");
        Step(8);
        LoadValidationRoom(2, 0x9f);
        LoadValidationRoom(2, 0x9e);
        _sound.ClearPlayRequestAudit();
        Step(100);
        FailIf(Enumerable.Range(0x13, 6).Any(p => _currentRoom.GetMetatile(Point(p)) != 0x6d) ||
            _entities.Entities<BridgeSpawnerRoomEntity>().Count != 0 ||
            _entities.Entities<OrbBridgeControllerRoomEntity>().Count != 0 ||
            _entities.Entities<DungeonOrbRoomEntity>().Single().IsOn ||
            _sound.PlayRequestsFor(data.SolveSound) != 0 ||
            _sound.PlayRequestsFor(data.DoorSound) != 0,
            "Room 2:9e re-entry after interrupted construction must restore all six tiles silently.");

        FailIf(!OracleSaveData.TryDeserialize(_saveData.Serialize(), out OracleSaveData? restored) ||
            !restored!.HasRoomFlag(2, 0x9e, 0x40),
            "Room 2:9e bridge flag $40 did not survive the explicit save serialization boundary.");
        _saveData.SetRoomFlag(2, 0x9e, 0x40, false);
        _entities.LoadRoom(2, _world.LoadRoom(2, 0x9f));
        OracleRoomData destination = _world.LoadRoom(2, 0x9e);
        _entities.BeginScreenTransition(2, destination, Vector2.Left * destination.Width);
        _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress, 1);
        Step(100);
        FailIf(_saveData.HasRoomFlag(2, 0x9e, 0x40) ||
            _entities.Entities<BridgeSpawnerRoomEntity>().Count != 0,
            "Room 2:9e bridge controller advanced during destination scrolling.");
        _entities.FinishScreenTransition();
        Step();
        FailIf(!_saveData.HasRoomFlag(2, 0x9e, 0x40),
            "Room 2:9e bridge controller did not resume after scrolling.");
        GD.Print("Validated room 2:9e orb, $dc:$12 trigger, twelve $08-update bridge halves, repeat hits and re-entry.");
    }
}
