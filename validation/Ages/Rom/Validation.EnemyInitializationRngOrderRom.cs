using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private IRoomEntity CreateInitializationRngEnemy(int id, int order)
    {
        var database = EnemyDatabase.Shared;
        var source = new RoomObjectRecord(4, 0xa1, order, RoomObjectKind.FixedEnemy,
            id, 0, 2, 1, 40, 120, 0x27, 0);
        var factory = new RoomEnemyFactory(_entities, database, new RoomEntityResources(_rooms), _rooms);
        return factory.CreateOrderedEnemy(database.EnemyHandlers.ResolveHandler(source), source,
            _currentRoom, new(120, 40), 0, 0, EnemyPlacementContext.Unrestricted)
            ?? throw new InvalidOperationException($"Missing ordered ENEMY ${id:x2}:$00 factory.");
    }

    private void ValidateEnemyInitializationRngOrderRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (byte rng1 in new byte[] { 0x0d, 0x40 })
        {
            F8Setup();
            _player.WarpTo(new(120, 40));
            _random.RestoreState(_random.CaptureState() with { Rng1 = rng1, Rng2 = 0x37 });
            var seed = _random.CaptureState();
            var native = new SomariaRom(_saveData, seed, _currentRoom, 0, 120, 40);
            int[] ids = [0x09, 0x62, 0x52, 0x38, 0x63, 0x33];
            var actors = new IRoomEntity[ids.Length];
            // Insert in reverse scene order: the actual ENEMY pass must still
            // initialize by physical slot, then leave the next draw to $33.
            for (int slot = ids.Length - 1; slot >= 0; slot--)
            {
                actors[slot] = CreateInitializationRngEnemy(ids[slot], slot);
                _entities.RegisterEnemySlot(actors[slot], slot); _entities.AddEntity(actors[slot]);
                int at = 0xd080 + slot * 256;
                native[at] = 1; native[at + 1] = (byte)ids[slot];
                native[at + 11] = 40; native[at + 13] = 120;
            }
            FailIf(_random.Calls != seed.Calls, "Ordered factories must not draw initialization RNG before dispatch.");
            int update = 0;
            StepGameplayUpdates(2, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                native.AdvanceEnemies(_entities.FrameCounter);
                Check(++update == 1 ? 7 : 8);
            });
            var baby = (BabyCuccoCharacter)actors[5].Node;
            FailIf((int)baby.State != native[0xd584],
                "The next Baby Cucco hop draw must follow six common initializers and Octorok's species draw in physical slot order.");

            void Check(int expected)
            {
                var random = _random.CaptureState();
                FailIf(random.Calls - seed.Calls != expected || native.RandomCalls != expected ||
                    random.Rng1 != native[0xff94] || random.Rng2 != native[0xff95],
                    $"Ordered initialization RNG, seed=${rng1:x2}:$37, expected={expected}: port={random.Calls - seed.Calls}, source={native.RandomCalls}.");
            }
        }

        // Focused preload entry controls complement the real gameplay passes
        // above. They do not claim a complete native room-loading replay.
        foreach (bool batched in new[] { false, true })
        foreach (int id in new[] { 0x38, 0x63 })
        foreach (bool preloadFirst in new[] { false, true })
        {
            F8Setup();
            var seed = _random.CaptureState();
            var actor = CreateInitializationRngEnemy(id, 0);
            _entities.RegisterEnemySlot(actor, 0); _entities.AddEntity(actor);
            var native = F8Native(id, 0, 0);
            var preload = (IScreenTransitionPreloadRoomEntity)actor;
            var spawns = new List<RoomEntitySpawn>();
            var text = _entities.TextActiveSource;
            try
            {
                if (preloadFirst) preload.PrepareForScreenTransition(spawns);
                else StepGameplayUpdates(1, Vector2.Zero, batched: batched);
                native.AdvanceEnemies(_entities.FrameCounter);
                // Repeated preload and restricted gameplay must not reinitialize.
                preload.PrepareForScreenTransition(spawns);
                _entities.TextActiveSource = () => true; native[0xcba0] = 1;
                StepGameplayUpdates(2, Vector2.Zero, batched: batched,
                    afterUpdate: () => native.AdvanceEnemies(_entities.FrameCounter));
                var random = _random.CaptureState();
                FailIf(random.Calls - seed.Calls != 1 || native.RandomCalls != 1 ||
                    random.Rng1 != native[0xff94] || random.Rng2 != native[0xff95] || spawns.Count != 0,
                    $"ENEMY ${id:x2} preloadFirst={preloadFirst} must share the guarded state-zero RNG path and draw exactly once.");
            }
            finally { _entities.TextActiveSource = text; }
        }
        ReinitializeGameplayForValidation();
        GD.Print("Compared six ordered factory initializers including Octorok's two draws and the following Baby Cucco draw for two seeds/both schedules, plus eight guarded fairy/crystal preload controls.");
    }
}
