using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullSeedScan()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        OracleSaveData.TryDeserialize(_saveData.Serialize(), out var initialSave);
        var random = CaptureOracleRandomForValidation();
        var entityState = _entities.CaptureDebugState();
        var first = new Dictionary<int, (int Health, int RandomCalls, Vector2 Link, Vector2 Body)>();
        foreach (int mode in new[] { 0, 1, 2 })
        foreach (bool batch in new[] { false, true })
        {
            void Step(int count = 1, Vector2 movement = default, bool attack = false) =>
                StepGameplayUpdates(count, movement, attack ? ["attack"] : [], attack ? ["attack"] : [], batched: batch);
            _saveData.RestoreFrom(initialSave!);
            RestoreOracleRandomForValidation(random);
            _entities.RestoreDebugStateBeforeRoomParse(entityState);
            typeof(InventoryState).GetMethod("LoadFromSaveData", flags)!.Invoke(_inventory, null);
            _seedSatchel.InterruptShooter();
            _inventory.GiveTreasure(TreasureId.SeedSatchel, 1);
            _inventory.GiveTreasure(TreasureId.Shooter, 1);
            _inventory.GiveTreasure(TreasureId.PegasusSeeds, 0x20);
            _inventory.SelectShooterSeeds(2);
            _inventory.EquipA(TreasureId.Shooter);
            _inventory.RefillHealth();
            _saveData.SetRoomFlag(4, 0x80, 0xff, false);
            LoadValidationRoom(4, 0x86);
            _entities.RestoreDebugStateAfterRoomParse(entityState);
            _player.WarpTo(new Vector2(120, 40));
            FailIf(_collision.Collides(_player.Position), "Armos collision scan approach must begin on the real adjacent corridor.");
            for (int i = 0; !IsTransitioning && i < 100; i++) Step(movement: Vector2.Up);
            FailIf(!IsTransitioning, "Armos collision scan route did not start the north scroll.");
            for (int i = 0; IsTransitioning && i < 160; i++) Step();
            for (int i = 0; !_dialogue.IsOpen && i < 200; i++) Step();
            FailIf(!_dialogue.IsOpen, "Armos collision scan route missed its intro dialogue.");
            for (int i = 0; _dialogue.IsOpen && i < 500; i++) Step(attack: i % 12 == 0);
            Step();
            var body = _entities.Entities<ArmosWarriorActor>().Single(a => a.SubId == 1);
            var shield = _entities.Entities<ArmosWarriorActor>().Single(a => a.SubId == 2);
            int health = _inventory.HealthQuarters;
            // Walk into the body's future retreat path, to its left so Link
            // stays outside the shield's offset box. No actor is repositioned.
            Step(11, Vector2.Left);
            Step(12, Vector2.Up);
            FailIf(_player.Position != new Vector2(109, 133) || _collision.Collides(_player.Position),
                $"Armos scan approach must reach actual floor109,133: {_player.Position}.");
            for (int i = 0; (body.Substate != 4 || body.Counter > 4) && i < 100; i++) Step();
            FailIf(body.Position != new Vector2(120, 121) || body.Counter != 4 || _inventory.HealthQuarters != health,
                $"Armos retreat source66 half-pixel updates must reach120,121 before Link contact: {body.Position}/{body.Counter}, Link={_player.Position}, hp={_inventory.HealthQuarters}/{health}.");
            Step(movement: new Vector2(1, -1), attack: true);
            FailIf(_inventory.HealthQuarters != health || Player.EnemyCollisionOverlaps(_player.Position, body.CollisionBounds),
                "The aim update must still be outside the body's contact interval.");
            Step();
            var seed = _entities.Entities<EmberSeedEffect>().Single(s => !s.Finished);
            FailIf(!Player.EnemyCollisionOverlaps(_player.Position, body.CollisionBounds) ||
                Player.EnemyCollisionOverlaps(_player.Position, shield.CollisionBounds) || !seed.HasPendingNativeCollision ||
                seed.State != EmberState.Flying || seed.CollisionEnabled || seed.AnimationFrame != 0 ||
                body.JustHit || !shield.JustHit || body.Health != 10 || shield.Health != 3 || _inventory.HealthQuarters != health,
                $"Effect00 must skip only this body's Link scan, preserve its bytes and let the later shield absorb: body={body.Position}/{body.JustHit}, shield={shield.Position}/{shield.JustHit}, seed={seed.Position}/{seed.State}/{seed.HasPendingNativeCollision}, Link={_player.Position}, hp={_inventory.HealthQuarters}/{health}.");
            if (mode == 2)
            {
                var sounds = new List<int>();
                _entities.SoundRequested += sounds.Add;
                try { LoadValidationRoom(4, 0x86); Step(3); }
                finally { _entities.SoundRequested -= sounds.Add; }
                FailIf(_entities.HasActiveShooterSeed || _entities.Entities<EmberSeedEffect>().Count != 0 ||
                    sounds.Contains(SoundId.SndLightTorch),
                    "Room replacement must discard an unconsumed native collision without playing its future activation sound.");
                continue;
            }
            if (mode == 1)
            {
                _dialogue.ShowMessage("Pending seed hit.", _player.Position.Y);
                var frozen = (seed.ElapsedFrames, body.Counter, body.Position, shield.JustHit);
                Step(24);
                FailIf(!seed.HasPendingNativeCollision || seed.AnimationFrame != 0 || seed.State != EmberState.Flying ||
                    frozen != (seed.ElapsedFrames, body.Counter, body.Position, shield.JustHit) || _inventory.HealthQuarters != health,
                    "Text must retain the pending seed hit and the shield's JUST_HIT signal without advancing either object or repeating contact.");
                for (int i = 0; _dialogue.IsOpen && i < 180; i++) Step(attack: i % 12 == 0);
                FailIf(_dialogue.IsOpen, "Pending collision text did not close with actual A input.");
            }
            if (seed.HasPendingNativeCollision) Step();
            FailIf(seed.HasPendingNativeCollision || seed.State != EmberState.Dissipating || seed.AnimationFrame != 1 ||
                _inventory.HealthQuarters != health - 2 || !body.JustHit || shield.JustHit,
                "On the next update the item must consume its hit, and the body's ordinary Link collision must resume without a stale scan-suppression flag.");
            Step(8);
            FailIf(seed.Finished, "Pending Pegasus effect must survive eight subsequent item updates.");
            Step();
            FailIf(!seed.Finished || _entities.HasActiveShooterSeed, "Pending Pegasus effect must end on its ninth subsequent update.");
            var result = (_inventory.HealthQuarters, _entities.RandomCalls, _player.Position, body.Position);
            FailIf(first.TryGetValue(mode, out var previous) && previous != result, $"Single/batched native seed scan mode{mode} differs: {previous}/{result}.");
            first[mode] = result;
        }
    }
}
