using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateLinkOnSpawnedChest()
    {
        var seeds = new SeedSatchelDatabase();
        FailIf(!seeds.TryGet(ItemId.EmberSeed, out var seed), "Missing chest fixture seed.");
        Vector2 chest = new(120, 88); // objects/ages chest in room $4:$ba, packed $57.
        foreach (bool batched in new[] { false, true })
        {
            void Step(int count = 1, Vector2 move = default) =>
                StepGameplayUpdates(count, move, [], [], batched: batched);
            _saveData.SetRoomFlag(4, 0xba, 0x20, false);
            LoadValidationRoom(4, 0xba);
            _player.WarpTo(new(120, 120));
            Step();
            // Walk onto the future chest through the actual south floor.
            for (int i = 0; _player.Position.Y > 88 && i < 64; i++) Step(move: Vector2.Up);
            FailIf(_player.Position.Y != 88 || _currentRoom.IsSolid(_player.Position),
                "$4:$ba Link must reach the future $57 chest on open floor.");
            var script = _entities.Entities<DungeonTriggerChestScriptRoomEntity>().Single();
            foreach (var eye in _entities.Entities<SeedShooterEyeStatueRoomEntity>())
                FailIf(!eye.ApplySeedCollision(eye.CollisionBounds, eye.Position, seed,
                    ItemCollisionType.MysterySeed, new List<RoomEntitySpawn>()).Contact,
                    "$4:$ba eye must accept the chest-trigger fixture hit.");
            Step();
            FailIf(script.Counter != 15, "$4:$ba chest must start its source 15-update wait.");
            Step(14);
            FailIf(_currentRoom.GetMetatile(chest) == 0xf1 ||
                _runtimeState.ReadWramByte(WramAddress.wLinkOnChest) != 0,
                "$4:$ba collision escape must not start before the chest exists.");
            Step();
            FailIf(_currentRoom.GetMetatile(chest) != 0xf1 ||
                _currentRoom.GetTerrainInfo(chest).Collision != 0x0f ||
                _runtimeState.ReadWramByte(WramAddress.wLinkOnChest) != 0,
                "$4:$ba interaction-pass chest write must remain solid until the next Link pass.");
            Step();
            FailIf(_player.Position != chest || _currentRoom.GetTerrainInfo(chest).Collision != 0 ||
                _runtimeState.ReadWramByte(WramAddress.wLinkOnChest) != 0x57 ||
                _currentRoom.GetMetatile(chest) != 0xf1,
                "checkAndUpdateLinkOnChest must clear shared collision at $57 without moving Link or changing tile $f1.");
            Step(4);
            FailIf(_currentRoom.GetTerrainInfo(chest).Collision != 0,
                "Idle Link must retain the chest collision opening.");
            // y+$05 leaves $57 at y=$5b, before Link's center leaves it.
            while (_player.Position.Y < 91) Step(move: Vector2.Down);
            FailIf(_currentRoom.GetTerrainInfo(chest).Collision != 0,
                "$57 collision must remain open during the movement that leaves its feet probe.");
            Step(move: Vector2.Down);
            FailIf(_currentRoom.GetTerrainInfo(chest).Collision != 0x0f ||
                _runtimeState.ReadWramByte(WramAddress.wLinkOnChest) != 0,
                "$57 collision must restore on the following Link update at feet y=$60.");
            Step(12, Vector2.Down);
            Step(24, Vector2.Up);
            FailIf(_player.Position.Y < 98 || _currentRoom.GetTerrainInfo(chest).Collision != 0x0f,
                $"Returning from the south must stop outside the restored $57 chest (Link={_player.Position}, collision=${_currentRoom.GetTerrainInfo(chest).Collision:x2}).");
            int keys = _inventory.GetDungeonSmallKeys(5);
            FailIf(!TryInteract(_player), "Escaping $57 must leave its chest reachable from the south.");
            Step(32);
            FailIf(_inventory.GetDungeonSmallKeys(5) != keys + 1 || !_saveData.HasRoomFlag(4, 0xba, 0x20),
                "Escaped $4:$ba chest must still grant one small key and persist its item flag.");
            _dialogue.Close();
            Step();
            FailIf(TryInteract(_player), "Opened $4:$ba chest must not award its treasure again.");
            LoadValidationRoom(4, 0xba);
            Step();
            FailIf(_runtimeState.ReadWramByte(WramAddress.wLinkOnChest) != 0 ||
                _entities.Entities<DungeonTriggerChestScriptRoomEntity>().Count != 0,
                "Collected $4:$ba chest must retain persistence without stale collision-escape state.");
        }
        // Exercise the shared tile rule in another real room, independent of
        // which script installs $f1. Tile mutations here are fixture inputs.
        foreach (int roomId in new[] { 0xba, 0xbc })
        foreach (Vector2 direction in new[] { Vector2.Up, Vector2.Right, Vector2.Down, Vector2.Left })
        {
            _saveData.SetRoomFlag(4, roomId, 0x20, false);
            LoadValidationRoom(4, roomId);
            _player.WarpTo(new(120, 104));
            for (int i = 0; _player.Position.Y > 88 && i < 40; i++)
                StepGameplayUpdates(1, Vector2.Up);
            FailIf(_player.Position != chest || _currentRoom.IsSolid(chest),
                $"$4:${roomId:x2} chest fixture must approach $57 on real floor.");
            if (roomId == 0xbc)
            {
                // Keep this room's retractable chest active through its own
                // four pressure buttons; otherwise its writer removes $f1.
                foreach (int packed in new[] { 0x46, 0x48, 0x68 })
                    _currentRoom.SetPositionTileAndCollision(new((packed & 15) * 16 + 8,
                        (packed >> 4) * 16 + 8), 0xa0, null, 0);
                foreach (int packed in new[] { 0x34, 0x3a, 0x74, 0x7a })
                    _currentRoom.SetPositionTileAndCollision(new((packed & 15) * 16 + 8,
                        (packed >> 4) * 16 + 8), 0x2a, null, 0);
                StepGameplayUpdates(1, Vector2.Zero);
                FailIf(_currentRoom.GetMetatile(chest) != 0xf1,
                    "$4:$bc four-button event must install its native chest under Link.");
            }
            else
                FailIf(!_rooms.TrySetTile(0x57, 0xf1), "Fixture chest write must fit the native queue.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(_currentRoom.GetTerrainInfo(chest).Collision != 0,
                $"$4:${roomId:x2} shared chest rule must open collision.");
            StepGameplayUpdates(24, direction, batched: true);
            FailIf(_player.Position.DistanceTo(chest) < 12 ||
                _currentRoom.GetTerrainInfo(chest).Collision != 0x0f ||
                _runtimeState.ReadWramByte(WramAddress.wLinkOnChest) != 0,
                $"$4:${roomId:x2} must escape $57 toward {direction} and restore collision (Link={_player.Position}).");
        }

        _saveData.SetRoomFlag(4, 0xba, 0x20, false);
        LoadValidationRoom(4, 0xba);
        _player.WarpTo(chest); // Still ordinary floor; the chest is installed afterward.
        _rooms.TrySetTile(0x57, 0xf1);
        StepGameplayUpdates(1, Vector2.Zero);
        // Airborne tile sampling retains $cc99/$cc9a, even over another tile.
        _collision.UpdateLinkOnChest(new(120, 104), airborne: true);
        FailIf(_runtimeState.ReadWramByte(WramAddress.wActiveTilePos) != 0x57 ||
            _currentRoom.GetTerrainInfo(chest).Collision != 0,
            "Airborne Link must retain the previous active chest tile.");
        // setTile can overwrite that collision while Link remains on the tile.
        _rooms.TrySetTile(0x57, 0xa0);
        StepGameplayUpdates(1, Vector2.Zero);
        FailIf(_runtimeState.ReadWramByte(WramAddress.wLinkOnChest) != 0x57,
            "Replacing a chest must retain $cc9f until the active position changes.");
        _collision.UpdateLinkOnChest(new(120, 104), airborne: false);
        FailIf(_currentRoom.GetTerrainInfo(chest).Collision != 0 ||
            _runtimeState.ReadWramByte(WramAddress.wLinkOnChest) != 0,
            "Departure must restore the replacement floor's collision, not the former chest's.");
        _rooms.TrySetTile(0x57, 0xf1);
        StepGameplayUpdates(1, Vector2.Zero);
        _rooms.GetRoom(4, 0xbc); // Preload must not clear the outgoing active-tile state.
        FailIf(_runtimeState.ReadWramByte(WramAddress.wLinkOnChest) != 0x57,
            "Preload must preserve $cc9f until the room actually changes.");
        LoadValidationRoom(4, 0xba);
        FailIf(_runtimeState.ReadWramByte(WramAddress.wLinkOnChest) != 0 ||
            _runtimeState.ReadWramByte(WramAddress.wActiveTilePos) != 0 ||
            _currentRoom.GetMetatile(chest) == 0xf1,
            "Full room reload must discard the uncollected chest and its collision escape state.");
        LoadValidationRoom(0, 0x60);
        GD.Print("Validated spawned-chest escape, shared collision, feet boundary, repeat approach and collection in single/batched gameplay.");
    }
}
