using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownToggleStair()
    {
        foreach (bool batched in new[] { false, true })
        for (int repeat = 0; repeat < 2; repeat++)
        {
            ReinitializeGameplayForValidation();
            _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress, 0);
            LoadValidationRoom(4, 0xa1);
            _player.ApplicationUpdateOwned = true;
            var data = new DungeonMechanicDatabase();
            var placement = data.GetRoomRecords(4, 0xa1).Single(r => r.Id == 3);
            _entities.Clear();
            var orb = new DungeonOrbRoomEntity(placement, data,
                new DungeonInteractionVisualDatabase().Visual("grotto-orb"), _currentRoom,
                _runtimeState, () => (long)_animationTicks, _sound.PlaySound);
            typeof(RoomEntityManager).GetMethod("AddEntity", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_entities, [orb]);
            // Approach the actual $17 stair from its southern floor. With
            // standing Y+4, Y=$1a is outside the source's centering window;
            // Y=$19 is the first accepted position when walking north.
            _player.WarpTo(new(120, 40));
            FailIf(_collision.Collides(_player.Position) || _currentRoom.GetMetatile(new(120, 24)) != 0x44,
                "Crown4:a1 stair fixture must retain source geometry.");
            StepGameplayUpdates(14, Vector2.Up, batched: batched);
            FailIf(IsTransitioning || _player.Position != new Vector2(120, 26),
                $"Stair approach must remain outside the warp centering window: {_player.Position}.");
            FailIf(!orb.ApplySwordHit(orb.CollisionBounds, orb.Position, 1,
                EnemyKnockbackStrength.Normal, new List<RoomEntitySpawn>()),
                "The source orb must accept the staged pending collision.");
            int sounds = _sound.PlayRequestsFor(OracleSoundEngine.SndEnterCave);
            StepGameplayUpdates(1, Vector2.Up);
            var toggle = _entities.FloorToggle!;
            FailIf(IsTransitioning || toggle.State != 0 || !orb.IsOn ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndEnterCave) != sounds,
                "Toggle selection must precede func_60e9's stair lookup and its entrance sound.");
            StepGameplayUpdates(8, Vector2.Zero, batched: batched);
            FailIf(IsTransitioning || toggle.Active ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndEnterCave) != sounds,
                "The stair must remain inactive through cutscene02's completion object update.");
            StepGameplayUpdates(1, Vector2.Zero);
            FailIf(!IsTransitioning ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndEnterCave) != sounds + 1,
                "The next cutscene01 update must select the stair and play its one entrance sound.");
            for (int i = 0; IsTransitioning && i < 160; i++) StepGameplayUpdates(1, Vector2.Zero);
            FailIf(IsTransitioning || _rooms.ActiveGroup != 4 || _currentRoom.Id != 0xb1 ||
                _player.Position != new Vector2(120, 24),
                "The deferred stair must preserve Crown4:b1/$17 arrival.");
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            FailIf(IsTransitioning, "The destination stair must not immediately return Link.");
        }
        ReinitializeGameplayForValidation();
    }
}
