using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateMoonlitEnemyAdmissionRom()
    {
        foreach (bool orbCase in new[] { true, false })
        foreach (bool batched in new[] { false, true })
        foreach (int free in new[] { 0, 1 })
        foreach (int interactionFree in new[] { 0, 1 })
        {
            ReinitializeGameplayForValidation(); int room = orbCase ? 0x56 : 0x5e;
            _saveData.SetRoomFlag(4, room, OracleSaveData.RoomFlagItem, false); LoadValidationRoom(4, room);
            _inventory.SetScriptedEquippedItems(0, 0); _player.WarpTo(orbCase ? new(88, 136) : new(152, 40)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position), "Moonlit admission regression must approach over the original room floor.");
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            var parent = _entities.Entities<MoonlitGrottoArmosEventRoomEntity>().Single();
            FailIf(_entities.InteractionSlot(parent) != 2, "Moonlit INTERAC$21 must occupy source placement slot $d2.");
            if (orbCase)
            {
                _inventory.GiveTreasure(TreasureId.Sword, 1); _inventory.EquipA(TreasureId.Sword);
                var orb = _entities.Entities<DungeonOrbRoomEntity>().Single();
                StepGameplayUpdates(1, Vector2.Zero, ["attack"], ["attack"], batched);
                for (int update = 0; update < 12 && !orb.PendingHit; update++) StepGameplayUpdates(1, Vector2.Zero, ["attack"], [], batched);
                FailIf(!orb.PendingHit || orb.IsOn, "The real Sword must publish the orb's pending hit before the next PART pass.");
            }
            else
            {
                while (_player.Position.Y > 33) StepGameplayUpdates(1, Vector2.Up, ["move_up"], [], batched);
                FailIf(_entities.ActiveTriggers != 0, "The button approach must stop before contact to stage capacity.");
            }
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 4);
            var seed = _random.CaptureState();
            var native = new SomariaRom(_saveData, seed, _currentRoom, 0, (int)_player.Position.X, (int)_player.Position.Y);
            native[0xcc8a] = 4; native[0xd240] = 1; native[0xd241] = 0x21; native[0xd242] = (byte)(orbCase ? 0x0a : 0x0c); native[0xd244] = 1;
            foreach (int slot in InteractionAdmissionPool.Values)
            {
                int at = 0xd040 + slot * 256;
                if (at == 0xd240) continue;
                native[at] = 1; native[at + 1] = 5; native[at + 2] = 0x80;
            }
            FillInteractionAdmissionPool(native, interactionFree);
            int observedWatchers = -1, observedSlot = -1;
            // Observe admission from the next physical slot, before the new
            // higher-slot watcher can execute its own script. This isolates
            // allocation from the existing small-key script-yield mismatch.
            var prior = InteractionAdmissionPool.First(pair => pair.Value == 3).Key;
            _entities.RetireEntity(prior);
            var observerNode = new PuzzlePuffEffect(); observerNode.Initialize(new(216, 152), SoundId.MusNone);
            _entities.AddEntity(new InteractionSlotValidationEntity(observerNode, _ =>
            {
                Node2D? watcher = orbCase ? _entities.Entities<EnemyClearChestRoomEntity>().SingleOrDefault() :
                    _entities.Entities<DungeonRewardRoomEntity>().SingleOrDefault();
                observedWatchers = watcher is null ? 0 : 1;
                observedSlot = watcher is null ? -1 : _entities.InteractionSlot(watcher);
            }));
            foreach (int slot in F8Enemies.Values) { native[0xd080 + slot * 256] = 1; native[0xd084 + slot * 256] = 8; }
            native[0xcec1] = native[0xcec9] = native[0xceca] = 0; native[0xced0] = 0xaa;
            F8Fill(native, free);
            if (orbCase) StepGameplayUpdates(1, Vector2.Zero, batched: batched);
            else for (int update = 0; update < 8 && _runtimeState.ReadWramByte(OracleRuntimeState.ArmosTriggerAddress) == 0; update++)
                StepGameplayUpdates(1, Vector2.Up, ["move_up"], [], batched);
            if (orbCase) native[0xcdd2] = 0x10; else native[0xcca0] = 1;
            native.AdvanceEnemies(_entities.FrameCounter); native.AdvanceInteractions(_entities.FrameCounter);
            var sourceWatchers = NativeAdmissionChildren(native, 0x12, orbCase ? 2 : 1);
            FailIf(observedWatchers != interactionFree || sourceWatchers.Length != interactionFree,
                $"Moonlit INTERAC$21/$12 must retain the parent allocation during checked watcher creation: orb={orbCase}, free={interactionFree}, watchers={observedWatchers}/{sourceWatchers.Length}.");
            if (interactionFree == 1)
            {
                FailIf(observedSlot != (sourceWatchers.Single() >> 8) - 0xd0,
                    "Moonlit watcher must claim the same lowest available physical interaction slot as the original parser.");
            }
            DungeonRewardRoomEntity? keyWatcher = !orbCase && interactionFree == 1 && free == 0
                ? _entities.Entities<DungeonRewardRoomEntity>().Single() : null;
            if (keyWatcher is not null)
            {
                FailIf(keyWatcher.Finished || _entities.Entities<GroundTreasurePickup>().Count != 0 ||
                    NativeAdmissionChildren(native, 0x60, 0x30).Length != 0,
                    "The Moonlit $12:$01 watcher must yield at checknoenemies on birth, even when ENEMY allocation failed.");
                int watcherAt = sourceWatchers.Single(), pointer = native.Word(watcherAt + 0x18);
                _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 6); native[0xcc8a] = 6;
                StepGameplayUpdates(2, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    native.AdvanceEnemies(_entities.FrameCounter); native.AdvanceInteractions(_entities.FrameCounter);
                    FailIf(keyWatcher.Finished || _entities.Entities<GroundTreasurePickup>().Count != 0 ||
                        NativeAdmissionChildren(native, 0x60, 0x30).Length != 0 || native.Word(watcherAt + 0x18) != pointer,
                        "An initialized small-key watcher must retain its yielded command under INTERACTION mask $02.");
                });
                _runtimeState.SetWramByte(WramAddress.wDisabledObjects, 4); native[0xcc8a] = 4;
            }
            int SourceCount() => Enumerable.Range(0, 16).Count(slot => native[0xd080 + slot * 256] != 0 && native[0xd081 + slot * 256] == 0x1d);
            FailIf(!parent.Finished || _entities.Entities<ArmosSpawnerRoomEntity>().Count != free || SourceCount() != free ||
                _entities.Entities<ArmosSpawnerRoomEntity>().Any(scanner => scanner.State != 0) ||
                _runtimeState.ReadWramByte(OracleRuntimeState.ArmosTriggerAddress) != 1 || _random.Calls != seed.Calls,
                "Nested object opcode7 must skip a full ENEMY pool without initialization RNG, preserving the controller's trigger/deletion continuation.");
            StepGameplayUpdates(1, Vector2.Zero, batched: batched); native.AdvanceEnemies(_entities.FrameCounter); native.AdvanceInteractions(_entities.FrameCounter);
            FailIf(_entities.Entities<ArmosSpawnerRoomEntity>().Any(scanner => scanner.State != 1) || _random.Calls - seed.Calls != native.RandomCalls,
                "An admitted Moonlit ENEMY$1d must wait for the next ENEMY phase before state0 RNG.");
            if (keyWatcher is not null)
            {
                int watcherAt = sourceWatchers.Single();
                var key = _entities.Entities<GroundTreasurePickup>().Single();
                int keyAt = NativeAdmissionChildren(native, 0x60, 0x30).Single();
                FailIf(keyWatcher.Finished || native[watcherAt] == 0 || native[keyAt + 3] != 1 ||
                    key.State != PickupState.Initializing || native[keyAt + 4] != 0 ||
                    _entities.InteractionSlot(key) != (keyAt >> 8) - 0xd0 || keyAt >= watcherAt,
                    "spawnitem must allocate the falling key in the released earlier slot, then yield while retaining its watcher.");
                StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                native.AdvanceEnemies(_entities.FrameCounter); native.AdvanceInteractions(_entities.FrameCounter);
                FailIf(!keyWatcher.Finished || native[watcherAt] != 0 || native[keyAt + 4] != 1 || key.State != PickupState.Spawning ||
                    _entities.Entities<GroundTreasurePickup>().Count != 1,
                    "The earlier key initializes on the next pass before the later watcher executes scriptend.");
            }
            if (free == 0)
            {
                _entities.RetireEntity(F8Enemies.First(pair => pair.Key is EnemyAiSlotReservation).Key);
                StepGameplayUpdates(2, Vector2.Zero, batched: batched);
                FailIf(_entities.Entities<ArmosSpawnerRoomEntity>().Count != 0, "Released capacity must not replay the consumed nested object list.");
            }
            _entities.Clear();
        }
        ReinitializeGameplayForValidation();
        GD.Print("Compared reachable Moonlit orb/button opcode7 ENEMY admission and next-pass RNG against the original controller/parser.");
    }
}
