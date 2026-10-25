using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateCuccoReplacementRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int parentSlot in new[] { 0, 15 })
        foreach (int free in new[] { 0, 1 })
        foreach (bool elevated in new[] { false, true })
        {
            F8Setup();
            var data = EnemyDatabase.Shared; var resources = new RoomEntityResources(_rooms);
            var source = data.GetRoomObjects(2, 0xcf).Single();
            var cucco = new CuccoCharacter();
            cucco.Initialize(data.ImportedEnemy(0x36), data.ImportedEnemy(0x3b), _currentRoom,
                new(80.75f, 72.5f), _random, resources.Bracelet.Data, resources.Bomb.Data,
                _sound.PlaySound, _ => { }, (_, _, _, _) => { });
            var parent = new CuccoRoomEntity(cucco, source);
            _entities.RegisterEnemySlot(parent, parentSlot); _entities.AddEntity(parent);
            F1Set(cucco, "_state", CuccoState.Transforming); F1Set(cucco, "_transformationCounter", 2);
            var carried = SomariaPrivate<CarriedObjectMotion>(cucco, "_carried");
            carried.ZFixed = elevated ? -0x580 : 0; carried.SpeedZ = -0x123;
            F1Set(cucco, "_carried", carried);
            var native = F8Native(0x36, 0, parentSlot);
            F8Parent(native, parentSlot); // Original ordinary Cucco state 0.
            F8Fill(native, free);
            int at = 0xd080 + parentSlot * 256;
            native.Word(at + 10, 0x4880); native.Word(at + 12, 0x50c0);
            native.Word(at + 14, elevated ? 0xfa80 : 0); native.Word(at + 0x14, 0xfedd);
            native[at + 4] = 0x0b; native[at + 0x31] = 0x33;
            native[at + 0x16] = 0x40; native[at + 0x17] = 0xd2;
            native[0xd240] = 1; native[0xd241] = 5; native[0xd242] = 2;
            // The fixture declares an already-transforming parent rather than
            // replaying its initialization/Mystery Seed history. Start both
            // RNG streams from the same seed at that declared boundary.
            var handoffSeed = _random.CaptureState();
            native[0xff94] = handoffSeed.Rng1; native[0xff95] = handoffSeed.Rng2;
            int originalNativeCalls = native.RandomCalls; long originalCalls = _random.Calls;
            void Step(int count) => StepGameplayUpdates(count, Vector2.Zero, batched: batched,
                afterUpdate: () => F8Parent(native, parentSlot));

            Step(1);
            FailIf(!ReferenceEquals(_entities.ResolveEnemySlot(parentSlot), parent) ||
                _entities.Entities<BabyCuccoCharacter>().Count != 0 || native[at + 1] != 0x36 || native[at + 4] != 0x0b,
                "Cucco $36 state $0b must retain its slot while the related puff's terminal bit is clear.");
            native[0xd261] = 0x80; // Original puff animParameter terminal bit.
            Step(1);
            var baby = _entities.Entities<BabyCuccoCharacter>().Single();
            int z = elevated ? -0x600 : 0;
            FailIf(_entities.ResolveEnemySlot(parentSlot) is not BabyCuccoRoomEntity replacement ||
                !ReferenceEquals(replacement.Node, baby) || F8Enemies.Count != 16 - free ||
                baby.State != BabyCuccoState.Uninitialized || baby.Visible ||
                baby.Position != new Vector2(80, 72) || baby.ZFixed != z || baby.SpeedZ != 0 ||
                baby.InvincibilityCounter != 0 || _entities.RoomEnemyCount != 1 || native[0xcdd1] != 1 ||
                native[at] != 1 || native[at + 1] != 0x33 || native[at + 2] != 0 || native[at + 4] != 0 ||
                native.Word(at + 10) != 0x4800 || native.Word(at + 12) != 0x5000 ||
                native.Word(at + 14) != (elevated ? 0xfa00 : 0) || native.Word(at + 0x14) != 0 ||
                _random.Calls != originalCalls || native.RandomCalls != originalNativeCalls,
                $"Cucco objectReplaceWithID must retain slot ${parentSlot:x2} with free={free}, clear fractions/speed, copy high XYZ and defer initialization without RNG/count changes.");
            FailIf(_entities.Entities<CuccoCharacter>().Count != 0 || !cucco.IsQueuedForDeletion(),
                "Cucco replacement must retire the prior node through the owning manager without a death outcome.");
            Step(1);
            // enemyStandardUpdate draws var3d on the next dispatch, after the
            // same-slot replacement, before babyCucco_state_uninitialized.
            var initializedRandom = _random.CaptureState();
            FailIf(baby.State != BabyCuccoState.Following || !baby.Visible || native[at + 4] != 8 ||
                !ReferenceEquals(_entities.ResolveEnemySlot(parentSlot)?.Node, baby) || baby.ZFixed != z ||
                _entities.RoomEnemyCount != 1 || native.RandomCalls != originalNativeCalls + 1 ||
                initializedRandom.Calls != originalCalls + 1 ||
                initializedRandom.Rng1 != native[0xff94] || initializedRandom.Rng2 != native[0xff95],
                $"Baby slot=${parentSlot:x2}, free={free}, elevated={elevated}: state={baby.State}/{native[at + 4]}, visible={baby.Visible}, Z={baby.ZFixed}/{z}, RNG draws={initializedRandom.Calls - originalCalls}/{native.RandomCalls - originalNativeCalls}; next dispatch must initialize once with the source var3d draw.");
            Step(2);
            FailIf(_entities.Entities<BabyCuccoCharacter>().Count != 1 ||
                !ReferenceEquals(_entities.ResolveEnemySlot(parentSlot)?.Node, baby) || native[at + 1] != 0x33 ||
                _entities.RoomEnemyCount != native[0xcdd1] || _entities.EntityAdapters<KillPuffRoomEntity>().Any(),
                "Subsequent enemy updates must retain the replacement and avoid a deletion/drop outcome.");
            _entities.Clear();
            FailIf(F8Enemies.Count != 0 || _entities.RoomEnemyCount != 0,
                "Replacement cancellation must release every native ENEMY reservation.");
        }
        F8Setup();
        var enemies = EnemyDatabase.Shared; var roomResources = new RoomEntityResources(_rooms);
        var adult = new CuccoCharacter();
        adult.Initialize(enemies.ImportedEnemy(0x36), enemies.ImportedEnemy(0x3b), _currentRoom,
            new(80, 72), _random, roomResources.Bracelet.Data, roomResources.Bomb.Data,
            _sound.PlaySound, _ => { }, (_, _, _, _) => { });
        _entities.AddEntity(new CuccoRoomEntity(adult, enemies.GetRoomObjects(2, 0xcf).Single()));
        F1Set(adult, "_state", CuccoState.Transforming); F1Set(adult, "_transformationCounter", 2);
        StepGameplayUpdates(1, Vector2.Zero); _entities.Clear(); StepGameplayUpdates(2, Vector2.Zero);
        FailIf(_entities.Entities<BabyCuccoCharacter>().Count != 0 || F8Enemies.Count != 0,
            "Cancelling before the terminal handoff must not create a delayed replacement.");
        LoadValidationRoom(2, 0xcf);
        FailIf(_entities.Entities<CuccoCharacter>().Count != 2 || _entities.Entities<BabyCuccoCharacter>().Count != 0,
            "Re-entry must restore the original placed Cuccos without a stale transformation request.");
        ReinitializeGameplayForValidation();
        GD.Print("Compared Cucco same-slot replacement against original objectReplaceWithID at full/one-free capacity, both slot ends and XYZ copy boundaries.");
    }
}
