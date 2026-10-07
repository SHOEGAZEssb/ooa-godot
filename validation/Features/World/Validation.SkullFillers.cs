using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullDungeonFillers()
    {
        var data = new SkullDungeonDatabase();
        foreach (var (room, y, x, cy, cx) in new[] { (0x6f, 0x58, 0xd8, 0x68, 0x78), (0x87, 0x98, 0x28, 0x68, 0x58) })
        {
            var records = data.GetRoomRecords(4, room);
            FailIf(records.Count != 2 || records[0] is not { Order: 0, Id: InteractionId.TileFiller, SubId: 0 } || records[0].X != x || records[0].Y != y ||
                records[1] is not { Order: 1, Id: InteractionId.DungeonEvents, SubId: 0x11, Predicate: DungeonObjectCondition.Always } || records[1].X != cx || records[1].Y != cy,
                $"4:${room:x2} lost source tile-filler/chest placements or order.");
        }
        CompareTileFillerRom();
        CompareTileFillerAllocationRom();
        static Vector2 Point(int packed) => new((packed & 15)*16+8, (packed >> 4)*16+8);
        foreach (var (room, count, chest) in new[] { (0x6f, 105, 0x67), (0x87, 54, 0x65) })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, room);
            FailIf(_currentRoom.Layout.Count(tile => tile == 0x9f) != count,
                $"Original room$4:${room:x2} lost its independent blue-floor count{count}.");
            _inventory.GiveTreasure(TreasureId.Sword, 2); _inventory.EquipA(TreasureId.Sword);
            _player.WarpTo(Point(chest+16));
            FailIf(_collision.Collides(_player.Position), "Fill chest must be approached from its original southern floor.");
            // Declare completed floor writes; the ROM comparison covers filling.
            // Keep the original chest collection and room re-entry handoff.
            _player.SetBraceletLiftCollisionsDisabled(true);
            for (int wait = 0; wait < 8 && _entities.Entities<EnemyCharacter>().Any(enemy => enemy.Health > 0); wait++)
            {
                _entities.ApplySwordHit(new Rect2(Vector2.Zero, new Vector2(_currentRoom.Width, _currentRoom.Height)),
                    _player.Position, damage:0x7f);
                StepGameplayUpdates(40, Vector2.Zero, batched:true);
            }
            FailIf(_entities.Entities<EnemyCharacter>().Any(enemy => enemy.Health > 0),
                "Could not establish the independent cleared-enemy chest fixture.");
            _player.SetBraceletLiftCollisionsDisabled(false);
            for (int packed = 0; packed < 0xb0; packed++)
                if (_currentRoom.Layout[packed] == 0x9f) _currentRoom.Layout[packed] = 0x9d;
            StepGameplayUpdates(1, Vector2.Zero, batched:true);
            FailIf(_currentRoom.Layout[chest] != 0xf1 || _entities.Entities<DungeonPuzzleChestRoomEntity>().Count != 0,
                "Completed fill fixture must create its original chest.");
            StepGameplayUpdates(6, Vector2.Up, batched:true);
            ApproachTileWall(true);
            StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched:true);
            StepGameplayUpdates(60, Vector2.Zero, batched:true);
            FailIf(!_saveData.HasRoomFlag(4, room, OracleSaveData.RoomFlagItem),
                $"4:${room:x2} fill chest could not be collected through actual movement and A input.");
            _dialogue.Close(); _player.EndCutsceneControl(); _player.EndGetItemTwoHandPose();
            LoadValidationRoom(4, 0x91); LoadValidationRoom(4, room);
            StepGameplayUpdates(1, Vector2.Zero, batched:true);
            FailIf(_entities.Entities<DungeonPuzzleChestRoomEntity>().Count != 0 ||
                _entities.Entities<TileFillerRoomEntity>().Count != 1,
                "Collected fill chest must remain suppressed while the independent filler reloads.");
        }
        GD.Print("Validated ROM-backed tile-filler updates, queue/puff capacity and no-blue chest predicate; retained source placement/layout goldens, reachable collection and room re-entry.");
    }
}
