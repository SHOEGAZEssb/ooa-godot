using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownEyeChestBoundaries()
    {
        var seeds = new SeedSatchelDatabase();
        FailIf(!seeds.TryGet(ItemId.EmberSeed,out var seed),"Missing Ember Seed fixture.");
        foreach (bool batch in new[] { false,true })
        foreach (int boundary in new[] { 0,1,2 })
        {
            void Step(int count = 1) =>
                StepGameplayUpdates(count, Vector2.Zero, [], [], batched: batch);
            _saveData.SetRoomFlag(4,0xba,0x20,false);
            LoadValidationRoom(4,0xba); _player.WarpTo(new(120,120)); Step();
            var script = _entities.Entities<DungeonTriggerChestScriptRoomEntity>().Single();
            // Isolate the post-collision boundary; the projectile/geometry
            // path is exercised independently by ValidateCrownEyeChest.
            foreach (var eye in _entities.Entities<SeedShooterEyeStatueRoomEntity>())
                FailIf(!eye.ApplySeedCollision(eye.CollisionBounds,eye.Position,seed,ItemCollisionType.MysterySeed,new List<RoomEntitySpawn>()).Contact,
                    "Crown eye boundary fixture must queue an accepted native seed hit.");
            Step();
            FailIf(script.Counter != 15,"All three pending hits must start the chest delay in the same gameplay update.");
            if (boundary == 0)
            {
                Step(5); LoadValidationRoom(0,0x60); Step(20);
                LoadValidationRoom(4,0xba); Step();
                FailIf(_currentRoom.GetMetatile(new(120,88)) == 0xf1 || _entities.ActiveTriggers != 0 ||
                    _entities.Entities<DungeonTriggerChestScriptRoomEntity>().Single().Counter != -1,
                    "Leaving during the chest delay must discard the old script and triggers without a deferred chest write.");
            }
            else if (boundary == 1)
            {
                // stopifitemflagset has already advanced. It is not polled
                // again at the trigger gate or during spawnChestAfterPuff.
                _saveData.SetRoomFlag(4,0xba,0x20,true);
                Step(15);
                FailIf(!script.Finished || _currentRoom.GetMetatile(new(120,88)) != 0xf1,
                    "A later item-flag write must not cancel an already-started chest script.");
                LoadValidationRoom(4,0xba); Step();
                FailIf(_entities.Entities<DungeonTriggerChestScriptRoomEntity>().Count != 0,
                    "The same item flag must stop a newly initialized chest script.");
            }
            else
            {
                Step(14);
                byte tile = _currentRoom.GetMetatile(new(24,24));
                for (int i = 0; i < 31; i++)
                    FailIf(!_rooms.TrySetTile(0x11,tile),"Chest queue fixture must fill all31 native entries.");
                Step();
                FailIf(!script.Finished || _currentRoom.GetMetatile(new(120,88)) == 0xf1 || _rooms.PendingTileGraphics != 27,
                    "Full queue must reject settilehere before the four-entry drain; scriptend must still retire the controller.");
                Step(8);
                FailIf(_rooms.PendingTileGraphics != 0 || _currentRoom.GetMetatile(new(120,88)) == 0xf1,
                    "Chest script must not retry its rejected write after queue capacity becomes available.");
            }
        }
        _saveData.SetRoomFlag(4,0xba,0x20,false);
        LoadValidationRoom(0,0x60);
        GD.Print("Validated Crown eye chest cancellation, one-time item check and full tile-queue rejection in single/batched gameplay.");
    }
}
