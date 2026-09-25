using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSmogSetupReset()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batch in new[] { false,true })
        {
            void Step(int count = 1, Vector2? direction = null) =>
                StepGameplayUpdates(count, direction ?? Vector2.Zero, [], [], batched: batch);
            _saveData.SetRoomFlag(4,0xbf,0x80,false);
            _saveData.SetRoomFlag(4,0xbf,0x40,true);
            _saveData.SetRoomFlag(4,0xbf,0x20,false);
            LoadValidationRoom(4,0xbf); _player.WarpTo(new(120,88)); Step();
            var entity = ((List<IRoomEntity>)typeof(RoomEntityManager).GetField("_activeEntities",flags)!.GetValue(_entities)!)
                .OfType<SmogEncounterRoomEntity>().Single();
            var controller = entity.Controller;
            // Begin at the isolated phase-zero generation boundary, after
            // intro removal/repositioning. No fight or dungeon traversal.
            typeof(SmogEncounterController).GetProperty("State",flags)!.SetValue(controller,5);
            typeof(SmogEncounterController).GetProperty("SpawnIndex",flags)!.SetValue(controller,0);
            Step(); Step(49);
            FailIf(controller.State != 6 || controller.Counter != 1 || _entities.RoomEnemyCount != 1 ||
                !_entities.PlayerUpdatesFrozen,
                "Nine phase-zero writes consume45 updates; four more must still wait for the source terminator.");
            foreach (int packed in new[] { 0x37,0x46,0x47,0x48,0x76,0x77,0x78,0x87 })
            {
                var terrain = _currentRoom.GetTerrainInfo(new((packed & 15)*16+8,(packed >> 4)*16+8));
                FailIf(terrain.Tile != 0x1d || terrain.Collision == 0,
                    $"Phase-zero source tile${packed:x2} must become solid block$1d through the live tile owner.");
            }
            Step(); Step(4);
            FailIf(controller.State != 7 || controller.Counter != 1 || _entities.RoomEnemyCount != 1,
                "State7 must inherit the terminator's five-update delay before cloud allocation.");
            Step();
            var clouds = _entities.Entities<SmogCharacter>().Where(enemy => enemy.SubId == 2).ToArray();
            FailIf(controller.State != 8 || clouds.Length != 2 || clouds.Any(enemy => enemy.State != 0) ||
                clouds[0].Position != new Vector2(104,56) || clouds[1].Position != new Vector2(136,136) ||
                _saveData.HasRoomFlag(4,0xbf,0x40) || !_entities.PlayerUpdatesFrozen,
                "Phase-zero allocation must preserve source positions/order, clear reset flag and leave children for the next enemy pass.");
            Vector2 before = _player.Position;
            Step(1,Vector2.Right);
            FailIf(_player.Position != before || _entities.PlayerUpdatesFrozen || clouds.Any(enemy => enemy.State != 8),
                "Small-cloud initialization releases Link after his already-frozen player pass.");
            Step(1,Vector2.Right);
            FailIf(_player.Position.X <= before.X,"Link input must resume on the update after the small-cloud handoff.");

            typeof(InventoryState).GetProperty(nameof(InventoryState.MaxHealthQuarters))!.SetValue(_inventory,20);
            typeof(InventoryState).GetProperty(nameof(InventoryState.HealthQuarters))!.SetValue(_inventory,16);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                _player.WarpTo(new(24,40));
                FailIf(_currentRoom.IsSolid(_player.Position),"Reset approach must start on actual floor below the button.");
                for (int i = 0; i < 16 && controller.State == 8; i++)
                {
                    Step(2,Vector2.Up);
                    var terrain = _currentRoom.GetTerrainInfo(_player.Position);
                    // The handler replaces the button with solid tile$11
                    // beneath Link on activation, then freezes and clears it.
                    // Check traversal while state8 still owns the approach.
                    FailIf(controller.State == 8 && _currentRoom.IsSolid(_player.Position),
                        $"Reset approach collision at {_player.Position}, tile${terrain.Tile:x2}/mask${terrain.Collision:x2}.");
                }
                FailIf(controller.State != 10 || !_saveData.HasRoomFlag(4,0xbf,0x40) ||
                    _inventory.HealthQuarters != 12 - attempt * 4 || !_entities.PlayerUpdatesFrozen,
                    "Walking onto Smog's button must start cleanup once, charge4 and retain phase zero.");
                FailIf(_currentRoom.GetMetatile(new(24,24)) != 0x11 ||
                    Math.Abs(_player.Position.X-24) + Math.Abs(_player.Position.Y-20) >= 4,
                    "Smog's unchanged-button fallback must activate inside the source Manhattan radius and install pressed tile$11.");
                Step(2);
                FailIf(_entities.RoomEnemyCount != 1 || controller.Phase != 0,
                    "Reset must delete small clouds through their own enemy updates without advancing the phase.");
                if (attempt == 1) break;
                for (int i = 0; i < 350 && controller.State != 8; i++) Step(2);
                FailIf(controller.State != 8 || controller.Phase != 0 || _entities.RoomEnemyCount != 3,
                    "Cleanup/reposition must restore the same phase before a repeated reset.");
                Step(2);
            }
            for (int i = 0; i < 350 && controller.State != 2; i++) Step();
            FailIf(controller.State != 2,"Reset cleanup must reach the isolated player-lift boundary.");
            Step(3);
            FailIf(_player.ScriptedZHigh != 0xfd || !_entities.PlayerUpdatesFrozen,
                "Reset lift must raise frozen Link by three high-Z bytes before cancellation.");
            LoadValidationRoom(0,0x60);
            FailIf(_entities.PlayerUpdatesFrozen || _entities.PlayerMenusDisabled || _player.ScriptedZHigh != 0 ||
                (int)typeof(Player).GetField("_cutsceneDrawZFixed",flags)!.GetValue(_player)! != 0,
                "Leaving during reset lift must release the Link/menu lock and reset logical/presentation height.");
        }
        GD.Print("Validated isolated Smog phase setup, player handoff and repeated reset through actual button geometry, with single/batched gameplay updates.");
    }
}
