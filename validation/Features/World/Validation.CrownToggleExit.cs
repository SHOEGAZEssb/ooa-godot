using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownToggleExit()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress, 0);
            _saveData.SetRoomFlag(4, 0x9a, 8, true);
            LoadValidationRoom(4, 0x9a);
            _player.ApplicationUpdateOwned = true;
            // Isolate the source orb and opened left door from enemy attacks.
            var data = new DungeonMechanicDatabase();
            var placement = data.GetRoomRecords(4, 0x9a).Single(r => r.Id == 3);
            _entities.Clear();
            var orb = new DungeonOrbRoomEntity(placement, data,
                new DungeonInteractionVisualDatabase().Visual("grotto-orb"), _currentRoom,
                _runtimeState, () => (long)_animationTicks, _sound.PlaySound);
            typeof(RoomEntityManager).GetMethod("AddEntity", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_entities, [orb]);
            _player.WarpTo(new(8, 88));
            StepGameplayUpdates(1, Vector2.Zero);
            var toggle = _entities.FloorToggle!;
            FailIf(_collision.Collides(_player.Position) || !_rooms.TryGetNeighbor(Vector2I.Left, out _),
                "Crown4:9a exit fixture must use the actual open doorway and floor-layout neighbor.");
            StepGameplayUpdates(2, Vector2.Left, batched: batched);
            FailIf(IsTransitioning || _player.Position.X != 6,
                $"Door approach must stop immediately before the exit threshold: {_player.Position}.");
            // Publish a native pending collision before the parts pass. Its
            // consumer and Link's edge movement then share one object update.
            FailIf(!orb.ApplySwordHit(orb.CollisionBounds, orb.Position, 1,
                EnemyKnockbackStrength.Normal, new List<RoomEntitySpawn>()),
                "The initialized source orb must accept the staged collision.");
            StepGameplayUpdates(1, Vector2.Left);
            FailIf(IsTransitioning || toggle.State != 0 || !orb.IsOn,
                "cutscene01 must select the toggle before getNextActiveRoom starts the simultaneous exit.");
            StepGameplayUpdates(7, Vector2.Left, batched: batched);
            FailIf(IsTransitioning || toggle.Counter != 1,
                "The exit must remain deferred throughout the toggle delay.");
            StepGameplayUpdates(1, Vector2.Left);
            FailIf(IsTransitioning || toggle.Active,
                "cutscene02's completion update runs objects but does not dispatch cutscene01's exit check.");
            StepGameplayUpdates(1, Vector2.Left);
            FailIf(!IsTransitioning,
                "The next cutscene01 update must accept the still-requested exit.");
        }
        ReinitializeGameplayForValidation();
    }
}
