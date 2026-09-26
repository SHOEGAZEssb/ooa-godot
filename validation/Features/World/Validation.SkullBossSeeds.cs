using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullBossSeeds()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        OracleSaveData.TryDeserialize(_saveData.Serialize(), out var initialSave);
        var random = CaptureOracleRandomForValidation();
        var entityState = _entities.CaptureDebugState();
        var results = new Dictionary<(bool Armos, int Item, int Effect), (int Health, int RandomCalls, Vector2 Position)>();
        int damagingHits = 0;
        foreach (bool armos in new[] { false, true })
        foreach (var (item, selected) in new[] { (0x20, 0), (0x21, 1), (0x23, 3), (0x24, 0), (0x24, 1), (0x24, 2), (0x24, 3) })
        foreach (bool batch in new[] { false, true })
        {
            void Step(int count = 1, Vector2 movement = default, bool attack = false) =>
                StepGameplayUpdates(count, movement, attack ? ["attack"] : [], attack ? ["attack"] : [], batched: batch);
            _saveData.RestoreFrom(initialSave!);
            RestoreOracleRandomForValidation(random);
            _entities.RestoreDebugStateBeforeRoomParse(entityState);
            typeof(InventoryState).GetMethod("LoadFromSaveData", flags)!.Invoke(_inventory, null);
            _seedSatchel.InterruptShooter();
            while (_inventory.MaxHealthQuarters < 56) _inventory.GiveTreasure(TreasureId.HeartContainer, 4);
            _inventory.RefillHealth();
            _inventory.GiveTreasure(TreasureId.SeedSatchel, 1);
            _inventory.GiveTreasure(TreasureId.Shooter, 1);
            _inventory.GiveTreasure(item, 0x20);
            _inventory.SelectShooterSeeds(item - 0x20);
            _inventory.EquipA(TreasureId.Shooter);
            _saveData.SetRoomFlag(4, armos ? 0x80 : 0x6b, 0xff, false);
            LoadValidationRoom(4, armos ? 0x86 : 0x6c);
            _entities.RestoreDebugStateAfterRoomParse(entityState);
            _inventory.GiveTreasure(new TreasureDatabase().GetObject("TREASURE_OBJECT_BOSS_KEY_03"));
            _player.WarpTo(armos ? new Vector2(120, 40) : new Vector2(24, 88));
            FailIf(_collision.Collides(_player.Position), "Boss seed route must start on the real adjacent corridor floor.");
            for (int i = 0; !IsTransitioning && i < 100; i++) Step(movement: armos ? Vector2.Up : Vector2.Left);
            FailIf(!IsTransitioning, "Boss seed route did not start its native scroll.");
            for (int i = 0; IsTransitioning && i < 160; i++) Step();
            if (armos)
            {
                for (int i = 0; !_dialogue.IsOpen && i < 200; i++) Step();
                FailIf(!_dialogue.IsOpen, "Armos seed fixture missed its landing dialogue.");
                for (int i = 0; _dialogue.IsOpen && i < 500; i++) Step(attack: i % 12 == 0);
                FailIf(_dialogue.IsOpen, "Armos landing dialogue did not close through A input.");
                Step();
            }
            else for (int i = 0; _entities.Entities<EyesoarActor>().Single(a => !a.IsChild).State < 10 && i < 150; i++) Step();
            EnemyCharacter[] actors = armos ? _entities.Entities<ArmosWarriorActor>().Cast<EnemyCharacter>().ToArray() :
                _entities.Entities<EyesoarActor>().Cast<EnemyCharacter>().ToArray();
            if (!armos) Step(20, Vector2.Up);
            var sounds = new List<int>();
            _entities.SoundRequested += sounds.Add;
            try
            {
                for (int shot = 0; shot < 2; shot++)
                {
                    Step(movement: armos ? Vector2.Up : Vector2.Left, attack: true);
                    if (item == 0x24) SetDiggingRoll(_random, selected);
                    int calls = _random.Calls;
                    var healthBefore = actors.ToDictionary(a => a, a => a.Health);
                    sounds.Clear();
                    Step();
                    var seed = _entities.Entities<EmberSeedEffect>().Single(s => !s.Finished);
                    FailIf(seed.SeedItem != item || seed.CollisionType != 0x1b + selected ||
                        seed.Record.Damage != (item == 0x23 ? 0xff : 0xfe) ||
                        item == 0x24 && (seed.MysteryEffect != selected || _random.Calls != calls + 1),
                        $"Seed init item${item:x2}/selected{selected}/shot{shot}/batch{batch}: actual${seed.SeedItem:x2}, collision${seed.CollisionType:x2}, damage${seed.Record.Damage:x2}, effect{seed.MysteryEffect}, RNG{_random.Calls - calls}.");
                    EnemyCharacter? target = null;
                    int health = 0;
                    for (int i = 0; !seed.HasPendingNativeCollision && seed.State == EmberState.Flying && i < 160; i++)
                    {
                        healthBefore = actors.ToDictionary(a => a, a => a.Health);
                        sounds.Clear();
                        Step();
                    }
                    if (seed.HasPendingNativeCollision)
                    {
                        target = actors.FirstOrDefault(a =>
                            (a is EyesoarActor eye ? eye.JustHit : ((ArmosWarriorActor)a).JustHit && ((ArmosWarriorActor)a).CollisionMode != 0x60) &&
                            RoomEntityManager.ObjectCollisionXYOverlaps(a.CollisionBounds, seed.CollisionBounds) &&
                            RoomEntityManager.ObjectCollisionZOverlaps(a is EyesoarActor e ? e.ZFixed >> 8 : ((ArmosWarriorActor)a).ZFixed >> 8,
                                seed.ZFixed >> 8, 7));
                        health = target is null ? 0 : healthBefore[target];
                    }
                    FailIf(target is null, $"Armos={armos} ITEM${item:x2} selection{selected} shot{shot} missed native actors: seed={seed.Position}/{seed.State}, Link={_player.Position}, dying={_player.IsDying}, actors={string.Join(';', actors.Select(actor=>$"{actor.Position}/{actor.Health}"))}.");
                    bool damages = target is EyesoarActor { IsChild: true } && selected < 2;
                    if (damages) damagingHits++;
                    int damage = damages ? 2 : 0;
                    FailIf(seed.State != EmberState.Flying || seed.AnimationFrame != 0 || seed.SeedItem != item ||
                        seed.CollisionEnabled != damages || target!.Health != health - damage || target.InvincibilityCounter != (damages ? 32 : 0),
                        "The post-object seed collision must write health/invincibility immediately, preserve ITEM state1/ID, and clear collision only for effect20.");
                    Step();
                    EmberState state = selected switch { 0 => EmberState.Burning, 3 => EmberState.Gale, _ => EmberState.Dissipating };
                    int duration = selected switch { 0 => 58, 3 => 50, _ => 9 };
                    int sound = selected switch { 0 or 2 => SoundId.SndLightTorch,
                        1 => SoundId.SndPirateBell, _ => SoundId.SndGaleSeed /* SND_GALE_SEED */ };
                    FailIf(target!.Health != health - damage || target.InvincibilityCounter != (damages ? 31 : 0) ||
                        actors.OfType<EyesoarActor>().Any(a => a.MysteryCounter != 0) || seed.State != state || seed.SeedItem != 0x20 + selected || seed.AnimationFrame != 1 ||
                        seed.Record.Damage != (selected >= 2 ? 0xff : 0xfe) ||
                        seed.CollisionEnabled || selected is 0 or 3 && seed.FlameCounter != duration,
                        $"ITEM${item:x2} selection{selected} must consume its pending hit once before the next enemy dispatch: target={target.Name}, hp={target.Health}/{health}, inv={target.InvincibilityCounter}, seed={seed.State}/{seed.AnimationFrame}/{seed.FlameCounter}.");
                    int[] expectedSounds = damages ? [SoundId.SndDamageEnemy, sound] : [sound];
                    // armosWarrior.s emits SND_SWORDSLASH when the shared
                    // frame is divisible by16. This two-update impact window
                    // can contain that independent sword event after scrolling.
                    int slashes = sounds.Count(value => value == SoundId.SndSwordSlash);
                    FailIf(slashes > 0 && (!armos || slashes != 1 || (_entities.FrameCounter & 15) > 1),
                        "Armos emitted a sword slash outside its global16-update boundary.");
                    FailIf(!sounds.Where(value => value != SoundId.SndSwordSlash).SequenceEqual(expectedSounds),
                        $"Armos={armos} seed${item:x2} selected{selected} impact sounds [{string.Join(',', sounds)}] / [{string.Join(',', expectedSounds)}].");
                    Step(duration - 1);
                    FailIf(seed.Finished, "Selected seed effect ended before its source animation/counter boundary.");
                    Step();
                    FailIf(!seed.Finished || _entities.HasActiveShooterSeed || _entities.ActiveScentSeedTarget().HasValue,
                        "Selected seed effect must release the Shooter at the source boundary without creating landed Scent attraction.");
                    Step(4);
                }
            }
            finally { _entities.SoundRequested -= sounds.Add; }
            int remaining = item switch { 0x20 => _inventory.EmberSeeds, 0x21 => _inventory.ScentSeeds,
                0x23 => _inventory.GaleSeeds, _ => _inventory.MysterySeeds };
            FailIf(remaining != 0x18, "Two boss shots must consume only two seeds of the original type, including Mystery transformations.");
            var result = (_inventory.HealthQuarters, _entities.RandomCalls, _player.Position);
            FailIf(results.TryGetValue((armos, item, selected), out var prior) && prior != result,
                $"Single/batched seed${item:x2} selection{selected} routes diverged: {prior}/{result}.");
            results[(armos, item, selected)] = result;

            // Source collision rows15/4c/6d: Ember and Scent damage only
            // children. Armos mode44 accepts Scent as effect21. Ember, Scent
            // and Mystery carry damage$fe; selected Pegasus/Gale reload
            // damage$ff only after the collision.
            var data = new SeedSatchelDatabase();
            foreach (var actor in actors)
            foreach (int mode in actor is EyesoarActor e ? e.IsChild ? [0x15] : new[] { 0x4c, 0x6d } :
                ((ArmosWarriorActor)actor).SubId switch { 1 => [0x60, 0x44], 2 => [0x61], _ => [0x62] })
            {
                actor.GetType().GetProperty("CollisionMode", flags)!.SetValue(actor, mode);
                actor.GetType().GetField("_justHit", flags)!.SetValue(actor, false);
                bool child = actor is EyesoarActor { IsChild: true };
                actor.Health = child ? 4 : 20;
                actor.InvincibilityCounter = 0;
                if (!actor.CollisionEnabled) continue; // A killed eye is still in its native respawn delay.
                data.TryGet(item, out var record);
                ISeedCollisionTarget adapter = actor is EyesoarActor eye ? new EyesoarRoomEntity(eye) :
                    new ArmosWarriorRoomEntity((ArmosWarriorActor)actor, ((ArmosWarriorActor)actor).Entry);
                var response = adapter.ApplySeedCollision(new Rect2(actor.Position - Vector2.One * 4, Vector2.One * 8),
                    actor.Position, record, 0x1b + selected, new List<RoomEntitySpawn>());
                int damage = child && selected < 2 || mode == 0x44 && selected == 1 ? 2 : 0;
                var expected = mode == 0x60 ? SeedHitResult.None : item == 0x24 ? SeedHitResult.ActivateRandomSeed : SeedHitResult.Activate;
                FailIf(!response.Contact || response.Effect != expected || response.DisableCollision != (mode != 0x60 && damage == 0) ||
                    actor.Health != (child ? 4 : 20) - damage || actor.InvincibilityCounter != (damage > 0 ? 32 : 0),
                    $"Boss mode${mode:x2} seed${item:x2} selection{selected} lost its source damage/effect dispatch.");
            }
            Step(movement: armos ? Vector2.Up : Vector2.Left, attack: true);
            Step();
            FailIf(!_entities.HasActiveShooterSeed, "Cancellation fixture must retain a live third projectile.");
            LoadValidationRoom(4, 0x6c);
            FailIf(_entities.HasActiveShooterSeed || _entities.Entities<EmberSeedEffect>().Count != 0,
                "Room replacement must clear the live boss seed and its Shooter ownership.");
        }
        FailIf(damagingHits == 0, "Actual seed shots must exercise damage to Eyesoar's children, not only harmless absorption.");
    }
}
