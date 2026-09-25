using Godot;
using System;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSmogOwnerEffects()
    {
        var treasures = new TreasureDatabase();
        foreach (int health in new[] { 0,1,8,11,12,13,16,20 })
        {
            var inventory = new InventoryState(treasures);
            typeof(InventoryState).GetProperty(nameof(InventoryState.MaxHealthQuarters))!.SetValue(inventory,20);
            typeof(InventoryState).GetProperty(nameof(InventoryState.HealthQuarters))!.SetValue(inventory,health);
            int healthChanges = 0, changes = 0;
            inventory.HealthChanged += () => healthChanges++;
            inventory.Changed += () => changes++;
            inventory.ApplySmogResetPenalty();
            FailIf(inventory.HealthQuarters != (health >= 12 ? health - 4 : health) ||
                healthChanges != (health >= 12 ? 1 : 0) || changes != healthChanges,
                $"Smog reset at raw health${health:x2} must subtract4 only at >=$0c and publish exactly one inventory/health change.");
            for (int i = 0; i < 10; i++) inventory.ApplySmogResetPenalty();
            FailIf(inventory.HealthQuarters != (health >= 12 ? 8 + health % 4 : health),
                "Repeated reset penalties must stop below$0c without killing Link or refilling health.");
        }

        foreach (bool batch in new[] { false,true })
        {
            _saveData.SetRoomFlag(4,0xbf,0x80,false);
            LoadValidationRoom(4,0xbf);
            void Step(int count) =>
                StepGameplayUpdates(count, Vector2.Zero, [], [], batched: batch);
            Step(1);
            var sentinel = _entities.Entities<SmogCharacter>().Single();
            int kills = _inventory.TotalEnemiesKilled;
            _entities.ReleaseSmogSentinelCount();
            FailIf(_entities.RoomEnemyCount != 0 || sentinel.IsDead || sentinel.SubId != 5 ||
                _inventory.TotalEnemiesKilled != kills || _saveData.HasRoomFlag(4,0xbf,0x80),
                "INTERAC$33 final decrement must release only the retained count, without deleting/defeating the sentinel or setting room flags.");
            Step(3);
            FailIf(_entities.RoomEnemyCount != 0 || _entities.Entities<SmogCharacter>().Single() != sentinel || sentinel.IsDead,
                "Released Smog sentinel must remain an enabled, inert native enemy after subsequent gameplay updates.");
            for (int i = 0; i < 15; i++)
                _entities.Spawn<SmogCharacter>(new SmogEnemySpawn(new(72,72),2));
            bool full = false;
            try { _entities.Spawn<SmogCharacter>(new SmogEnemySpawn(new(72,72),2)); }
            catch (NotSupportedException error) { full = error.Message.Contains("full native pool"); }
            FailIf(!full || _entities.RoomEnemyCount != 15,
                "Uncounted Smog sentinel must retain its ENEMY slot; only15 further allocations fit.");
        }
        LoadValidationRoom(0,0x60);
        GD.Print("Validated Smog reset penalty through inventory ownership and final retained-count release without enemy deletion, kill effects or slot reuse.");
    }
}
