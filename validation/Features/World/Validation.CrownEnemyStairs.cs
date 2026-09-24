using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownEnemyStairs()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var data = new DungeonMechanicDatabase();
        foreach (int freePuffSlots in new[] { 0, 1, 4 })
        {
            _saveData.SetRoomFlag(4, 0xab, 0x80, false);
            LoadValidationRoom(4, 0xab);
            _entities.Clear();
            _player.WarpTo(new(120, 136));
            // Isolate the scan/map/allocation rule from enemy combat. Keep
            // position$00 hidden: the source loop never examines that byte.
            _currentRoom.SetPositionTileAndCollision(new(120, 24), 0xa0, null, (long)_animationTicks);
            for (int p = 0; p <= 4; p++)
                _currentRoom.SetPositionTileAndCollision(new(p * 16 + 8, 8),
                    (byte)(p == 0 ? 0x40 : 0x3f + p), null, (long)_animationTicks);
            var puff = typeof(RoomEntityManager).GetMethod("TryCreatePuzzlePuff", flags)!
                .CreateDelegate<Func<Vector2, bool>>(_entities);
            var scan = new EnemyClearStairsRoomEntity(data.GetRoomRecords(4, 0xab).Single(),
                _currentRoom, data, _saveData, () => 0, () => (long)_animationTicks,
                _sound.PlaySound, puff);
            typeof(RoomEntityManager).GetMethod("AddEntity", flags)!.Invoke(_entities, [scan]);
            for (int i = 0; i < 13 - freePuffSlots; i++)
                _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(24, 136), 0));
            _entities.Update(1.0 / 60, _player);
            byte[] expected = [0x40, 0x46, 0x47, 0x44, 0x45];
            for (int p = 0; p <= 4; p++)
                FailIf(_currentRoom.GetMetatile(new(p * 16 + 8, 8)) != expected[p],
                    "Enemy-clear stair scan must preserve$00 and map$40..43 to$46,$47,$44,$45 even with no puff slots.");
            var spawned = _entities.Entities<PuzzlePuffEffect>().Where(effect => effect.Position.Y == 8)
                .OrderBy(effect => _entities.InteractionSlot(effect)).ToArray();
            FailIf(spawned.Length != freePuffSlots || !_saveData.HasRoomFlag(4, 0xab, 0x80),
                "Stair tile/flag changes must not depend on puff allocation success.");
            for (int i = 0; i < spawned.Length; i++)
                FailIf(spawned[i].Position.X != (4 - i) * 16 + 8 || spawned[i].ElapsedUpdates != 1,
                    "Stair puffs must allocate in descending tile order and initialize later in the same interaction pass.");
            _entities.Update(1.0 / 60, _player);
            FailIf(_entities.Entities<EnemyClearStairsRoomEntity>().Count != 0,
                "Even a full-pool stair reveal must delete its controller on the following update.");
        }
        foreach (bool batch in new[] { false, true })
        {
            _saveData.SetRoomFlag(4, 0xab, 0x80, false);
            LoadValidationRoom(4, 0xab);
            _player.WarpTo(new(120, 136));
            _player.ApplyInteractionInvincibility(240);
            void Step(int n = 1) => StepGameplayUpdates(n, Vector2.Zero, batched: batch);
            Vector2 stairs = new(120, 24);
            var controller = _entities.Entities<EnemyClearStairsRoomEntity>().Single();
            FailIf(_currentRoom.GetMetatile(stairs) != 0x43 || _entities.RoomEnemyCount != 4 ||
                _entities.InteractionSlot(controller) != 2,
                "Crown4:ab must import its state0 stair controller in native slot$d2 and four counted Moblins.");
            Step(2);
            FailIf(_currentRoom.GetMetatile(stairs) != 0x43 || _saveData.HasRoomFlag(4, 0xab, 0x80),
                "INTERAC$12:$04 must not reveal the staircase while enemies remain.");
            foreach (var enemy in _entities.Entities<ArrowMoblinCharacter>())
                FailIf(!enemy.TakeSwordHit(enemy.Position + Vector2.Down * 16, 99),
                    "Stair fixture must deliver lethal hits through the shared enemy damage owner.");
            _sound.ClearPlayRequestAudit();
            for (int i = 0; !_saveData.HasRoomFlag(4, 0xab, 0x80) && i < 120; i++) Step();
            FailIf(_entities.RoomEnemyCount != 0 || !_saveData.HasRoomFlag(4, 0xab, 0x80) ||
                _currentRoom.GetMetatile(stairs) != 0x45 || controller.Finished ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 1 ||
                _entities.Entities<PuzzlePuffEffect>().Count(puff => puff.Position == stairs) != 1,
                "The enemy-clear interaction pass must reveal$43->$45, set flag80 and create one puff before deleting.");
            Step();
            FailIf(_entities.Entities<EnemyClearStairsRoomEntity>().Count != 0 ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndSolvePuzzle) != 1,
                "The flagged stair controller must delete on the next update without repeating its reward.");
            LoadValidationRoom(0, 0x60);
            LoadValidationRoom(4, 0xab);
            FailIf(_currentRoom.GetMetatile(stairs) != 0x45,
                "Standard roomflag80 tile substitution must retain the revealed Crown staircase on re-entry.");
            _player.WarpTo(new(120, 40));
            FailIf(_collision.Collides(_player.Position), "Revealed stair approach must start on real adjacent floor.");
            Step();
            FailIf(_entities.Entities<EnemyClearStairsRoomEntity>().Count != 0,
                "A reloaded flagged stair controller must delete without waiting for enemies.");
            for (int i = 0; !_transitions.IsTransitioning && i < 30; i++) StepGameplayUpdates(1, Vector2.Up);
            FailIf(!_transitions.IsTransitioning, "The revealed staircase must be reachable through ordinary movement.");
            for (int i = 0; _rooms.CurrentRoom.Id == 0xab && i < 120; i++) Step();
            FailIf(_rooms.ActiveGroup != 4 || _rooms.CurrentRoom.Id != 0x9d || _player.Position != stairs,
                "Crown4:ab's revealed staircase must return to4:9d/$17 through the floor-switch rule.");
            for (int i = 0; _transitions.IsTransitioning && i < 120; i++) Step();
            FailIf(_transitions.IsTransitioning, "Revealed stair transition must finish.");
            LoadValidationRoom(0, 0x60);
        }
    }
}
