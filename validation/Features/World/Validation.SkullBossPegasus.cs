using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullBossPegasus()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var input = (ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput", flags)!.GetValue(this)!;
        var scheduler = (ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates", flags)!.GetValue(this)!;
        var update = (Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate", flags)!.CreateDelegate(typeof(Action), this);
        OracleSaveData.TryDeserialize(_saveData.Serialize(), out var initialSave);
        var random = CaptureOracleRandomForValidation();
        var entityState = _entities.CaptureDebugState();
        var results = new Dictionary<bool, (int Seeds, int Health, int RandomCalls, Vector2 Position)>();
        foreach (bool armos in new[] { false, true })
        foreach (bool batch in new[] { false, true })
        {
            void Step(int count = 1, Vector2 movement = default, bool attack = false)
            {
                input.CaptureForValidation(attack ? ["attack"] : [], attack ? ["attack"] : [], movement);
                if (batch) scheduler.Advance(count / 60.0, update);
                else for (int i = 0; i < count; i++) scheduler.Advance(1.0 / 60, update);
            }
            _saveData.RestoreFrom(initialSave!);
            RestoreOracleRandomForValidation(random);
            _entities.RestoreDebugStateBeforeRoomParse(entityState);
            typeof(InventoryState).GetMethod("LoadFromSaveData", flags)!.Invoke(_inventory, null);
            _seedSatchel.Pegasus.Clear();
            _seedSatchel.InterruptShooter();
            while (_inventory.MaxHealthQuarters < 56) _inventory.GiveTreasure(TreasureDatabase.TreasureHeartContainer, 4);
            _inventory.RefillHealth();
            _inventory.GiveTreasure(0x19, 1);
            _inventory.GiveTreasure(0x0f, 1);
            _inventory.GiveTreasure(0x22, 0x20);
            _inventory.SelectShooterSeeds(2);
            _inventory.EquipA(InventoryState.ItemShooter);
            _saveData.SetRoomFlag(4, armos ? 0x80 : 0x6b, 0xff, false);
            LoadValidationRoom(4, armos ? 0x86 : 0x6c);
            _entities.RestoreDebugStateAfterRoomParse(entityState);
            _inventory.GiveTreasure(new TreasureDatabase().GetObject("TREASURE_OBJECT_BOSS_KEY_03"));
            _player.WarpTo(armos ? new Vector2(120, 40) : new Vector2(24, 88));
            FailIf(_collision.Collides(_player.Position), "Boss Pegasus approach must start on the adjacent room's clear corridor.");
            for (int i = 0; !IsTransitioning && i < 100; i++) Step(movement: armos ? Vector2.Up : Vector2.Left);
            FailIf(!IsTransitioning, "Boss Pegasus approach did not start its native scroll.");
            for (int i = 0; IsTransitioning && i < 160; i++) Step();
            FailIf(IsTransitioning || _currentRoom.Id != (armos ? 0x80 : 0x6b), "Boss Pegasus entry did not finish scrolling.");
            if (armos)
            {
                for (int i = 0; !_dialogue.IsOpen && i < 200; i++) Step();
                FailIf(!_dialogue.IsOpen, "Armos Pegasus fixture missed the landing dialogue.");
                for (int i = 0; _dialogue.IsOpen && i < 500; i++) Step(attack: i % 12 == 0);
                FailIf(_dialogue.IsOpen, "Armos landing dialogue did not close through actual A input.");
                Step();
            }
            else
            {
                for (int i = 0; _entities.Entities<EyesoarActor>().Single(a => !a.IsChild).State < 10 && i < 150; i++) Step();
            }
            FailIf(_entities.PlayerMovementDisabled, "Boss entry did not release Link before shooting.");
            EnemyCharacter[] actors = armos ? _entities.Entities<ArmosWarriorActor>().Cast<EnemyCharacter>().ToArray() :
                _entities.Entities<EyesoarActor>().Cast<EnemyCharacter>().ToArray();
            var health = actors.Select(a => a.Health).ToArray();
            var sounds = new List<int>();
            _entities.SoundRequested += sounds.Add;
            try
            {
                for (int shot = 0; shot < 2; shot++)
                {
                    Step(movement: armos ? Vector2.Up : Vector2.Left, attack: true);
                    Step();
                    var seed = _entities.Entities<EmberSeedEffect>().Single(s => !s.Finished);
                    EnemyCharacter? target = null;
                    for (int i = 0; !seed.HasPendingNativeCollision && seed.State == EmberState.Flying && i < 160; i++)
                    {
                        Step();
                    }
                    // The post-object pass writes the pending hit; ITEM state1
                    // consumes it on the following update, after scrolling/text gates.
                    if (seed.HasPendingNativeCollision)
                        target = actors.FirstOrDefault(a => a.InvincibilityCounter == 0 &&
                            (a is EyesoarActor eye ? eye.JustHit : a is ArmosWarriorActor warrior && warrior.JustHit && warrior.CollisionMode != 0x60) &&
                            RoomEntityManager.ObjectCollisionXYOverlaps(a.CollisionBounds, seed.CollisionBounds) &&
                            RoomEntityManager.ObjectCollisionZOverlaps(a is EyesoarActor e ? e.ZFixed >> 8 : ((ArmosWarriorActor)a).ZFixed >> 8,
                                seed.ZFixed >> 8, 7));
                    FailIf(target is null || seed.State != EmberState.Flying || seed.AnimationFrame != 0 || seed.CollisionEnabled,
                        "Pegasus effect20 must disable collision and leave a pending hit without advancing ITEM state1 in the post-object pass.");
                    Step();
                    FailIf(target is null || seed.State != EmberState.Dissipating || seed.AnimationFrame != 1 || seed.CollisionEnabled ||
                        !actors.Select(a => a.Health).SequenceEqual(health) || target.InvincibilityCounter != 0,
                        $"Boss Pegasus impact: armos={armos}, batch={batch}, shot={shot}, target={target?.Name}, seed={seed.State}/{seed.AnimationFrame} at {seed.Position}, Link={_player.Position}.");
                    Step(8);
                    FailIf(seed.Finished || seed.AnimationFrame != 3, "Boss-absorbed Pegasus must retain its effect through update8.");
                    Step();
                    FailIf(!seed.Finished || _entities.HasActiveShooterSeed || _seedSatchel.Pegasus.Active,
                        "Boss-absorbed Pegasus must release the Shooter on update9 without activating Link's speed timer.");
                    Step(4);
                }
            }
            finally { _entities.SoundRequested -= sounds.Add; }
            FailIf(sounds.Contains(OracleSoundEngine.SndDamageEnemy) || sounds.Contains(OracleSoundEngine.SndBossDamage) ||
                sounds.Count(s => s == OracleSoundEngine.SndLightTorch) != 2,
                "Boss Pegasus effect20 must play only the seed activation sound, never a damage/stun sound.");
            var result = (_inventory.PegasusSeeds, _inventory.HealthQuarters, _entities.RandomCalls, _player.Position);
            FailIf(results.TryGetValue(armos, out var prior) && prior != result,
                $"Single and batched boss Pegasus routes differ: {prior} / {result}.");
            results[armos] = result;

            // Independently exercise every native collision mode and both
            // signs of invincibility. These are collision-dispatch fixtures;
            // the shots above cover the actual item/enemy update handoff.
            foreach (var actor in actors)
            {
                var type = actor.GetType();
                var mode = type.GetProperty("CollisionMode", flags)!;
                var justHit = type.GetField("_justHit", flags)!;
                var spawns = new List<RoomEntitySpawn>();
                ISeedCollisionTarget receiver = actor is EyesoarActor eye ? new EyesoarRoomEntity(eye) :
                    new ArmosWarriorRoomEntity((ArmosWarriorActor)actor, ((ArmosWarriorActor)actor).Entry);
                int[] modes = actor is EyesoarActor eyesoar ? eyesoar.IsChild ? [0x15] : [0x6d, 0x4c] :
                    ((ArmosWarriorActor)actor).SubId switch { 1 => [0x60, 0x44], 2 => [0x61], _ => [0x62] };
                foreach (int collisionMode in modes)
                foreach (int invincibility in new[] { -1, 1, 0 })
                {
                    mode.SetValue(actor, collisionMode);
                    justHit.SetValue(actor, false);
                    actor.InvincibilityCounter = invincibility;
                    int before = actor.Health;
                    var hitbox = new Rect2(actor.CollisionBounds.GetCenter() - Vector2.One * 4, Vector2.One * 8);
                    var response = receiver.ApplySeedCollision(hitbox, actor.Position, new SeedSatchelDatabase().Pegasus, 0x1d, spawns);
                    bool accepts = collisionMode != 0x60 && invincibility == 0;
                    FailIf(response.Contact != (invincibility == 0) || response.Effect != (accepts ? SeedHitResult.Activate : SeedHitResult.None) ||
                        response.DisableCollision != accepts || actor.Health != before ||
                        actor.InvincibilityCounter != invincibility || (bool)justHit.GetValue(actor)! != accepts || spawns.Count != 0,
                        $"Pegasus collision mode${collisionMode:x2}, inv={invincibility} must preserve health/invincibility and write JUST_HIT only on effect20.");
                    if (accepts)
                        FailIf(receiver.ApplySeedCollision(hitbox, actor.Position, new SeedSatchelDatabase().Pegasus, 0x1d, spawns).Contact,
                            "A second item collision must reject the boss's pending JUST_HIT signal.");
                }
            }
            LoadValidationRoom(4, armos ? 0x86 : 0x6c);
            FailIf(_entities.HasActiveShooterSeed, "Leaving the boss room must clear Pegasus projectile ownership.");
        }
    }
}
