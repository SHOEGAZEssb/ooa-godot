using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSmogControllerLive()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var signal = (BossShutterSignal)typeof(RoomEntityManager).GetField("_bossShutterSignal",flags)!.GetValue(_entities)!;
        foreach (bool batch in new[] { false,true })
        {
            void Step(int count = 1) =>
                StepGameplayUpdates(count, Vector2.Zero, [], [], batched: batch);
            void Reset()
            {
                _saveData.SetRoomFlag(4,0xbf,0x80,false);
                _saveData.SetRoomFlag(4,0xbf,0x40,false);
                LoadValidationRoom(4,0xbf); _player.WarpTo(new(120,136)); Step();
            }
            SmogEncounterRoomEntity PlacedController()
            {
                return ((List<IRoomEntity>)typeof(RoomEntityManager).GetField("_activeEntities",flags)!.GetValue(_entities)!)
                    .OfType<SmogEncounterRoomEntity>().Single();
            }
            _saveData.SetRoomFlag(4,0xbf,0x80,false);
            LoadValidationRoom(4,0xbf);
            var preloadedController = PlacedController();
            var active = (List<IRoomEntity>)typeof(RoomEntityManager).GetField("_activeEntities",flags)!.GetValue(_entities)!;
            active.Remove(preloadedController); active.Insert(0,preloadedController);
            // Reproduce the source object-stream relationship: controller
            // first, sentinel later. Native dispatch still runs ENEMY first.
            typeof(RoomEntityManager).GetMethod("PrepareIncomingEntitiesForScreenTransition",flags)!.Invoke(_entities,[_player]);
            FailIf(_entities.Entities<SmogCharacter>().Single().State != 8 ||
                preloadedController.Controller.State != 0 || preloadedController.Node.Visible ||
                _entities.BossEntrySignal == 0 || _entities.RoomEnemyCount != 1 || !_entities.PlayerUpdatesFrozen,
                "Enemy preload must establish cc93 before an earlier-inserted INTERAC$33 executes; no intro may spawn during entry.");
            Reset();
            var controller = PlacedController();
            Step(2);
            FailIf(controller.Controller.State != 0 || _entities.RoomEnemyCount != 1 || !_entities.PlayerUpdatesFrozen,
                "Bound INTERAC$33 must wait on the live nonzero entry signal while retaining Link's lock.");
            var text = _entities.TextActiveSource;
            try
            {
                _entities.TextActiveSource = () => true;
                signal.Clear(); // Isolate completion of the already-tested shutter signal.
                Step();
                var intro = _entities.Entities<SmogCharacter>().Single(enemy => enemy.SubId == 0);
                FailIf(controller.Controller.State != 1 || intro.State != 0 || _entities.RoomEnemyCount != 2,
                    "State0 controller remains eligible during text and allocates intro after the enemy pass; it must not initialize early.");
            }
            finally { _entities.TextActiveSource = text; }

            Reset(); controller = PlacedController();
            typeof(SmogEncounterController).GetProperty("State",flags)!.SetValue(controller.Controller,8);
            typeof(SmogEncounterController).GetProperty("Position",flags)!.SetValue(controller.Controller,new Vector2I(24,20));
            _currentRoom.SetPositionTileAndCollision(new(24,24),0x11,null,0);
            typeof(InventoryState).GetProperty(nameof(InventoryState.MaxHealthQuarters))!.SetValue(_inventory,20);
            typeof(InventoryState).GetProperty(nameof(InventoryState.HealthQuarters))!.SetValue(_inventory,16);
            _entities.Spawn<SmogCharacter>(new SmogEnemySpawn(new(56,104),2));
            _entities.Spawn<SmogCharacter>(new SmogEnemySpawn(new(136,104),2));
            int splashes = 0;
            void Sound(int id) { if (id == OracleSoundEngine.SndSplash) splashes++; }
            _entities.SoundRequested += Sound;
            try
            {
                Step();
                FailIf(controller.Controller.State != 10 || _inventory.HealthQuarters != 12 ||
                    !_saveData.HasRoomFlag(4,0xbf,0x40) || !_entities.PlayerUpdatesFrozen || splashes != 1,
                    "Bound changed-button reset must write room flag$40, lock Link, subtract4 and request SND_SPLASH once.");
                Step(2);
                FailIf(_entities.RoomEnemyCount != 1 || _inventory.HealthQuarters != 12 || splashes != 1,
                    "Small clouds must consume the controller's live reset flag on later enemy passes without repeating the penalty.");
            }
            finally { _entities.SoundRequested -= Sound; }
            typeof(SmogEncounterController).GetProperty("State",flags)!.SetValue(controller.Controller,9);
            typeof(SmogEncounterController).GetProperty("Phase",flags)!.SetValue(controller.Controller,3);
            Step();
            FailIf(!controller.Finished || _entities.RoomEnemyCount != 0 ||
                _entities.Entities<SmogCharacter>().Single().SubId != 5,
                "Final controller phase must release the live count while retaining the native sentinel.");
        }
        LoadValidationRoom(0,0x60);
        GD.Print("Validated bound Smog controller entry gate, ordered spawn, reset handoff and final count release in isolated single/batched gameplay updates.");
    }
}
