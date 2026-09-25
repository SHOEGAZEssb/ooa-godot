using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePegasusProjectiles()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        OracleSaveData.TryDeserialize(_saveData.Serialize(), out var initialSave);
        var random = CaptureOracleRandomForValidation();
        var entityState = _entities.CaptureDebugState();
        (int Seeds, int Stun, int RandomCalls, Vector2 Position)? first = null;
        var record = new SeedSatchelDatabase().Pegasus;
        FailIf(record.SeedItem != 0x22 || record.TileBase != 0x16 || record.Palette != 1 || record.Collision != 0x9d ||
            record.Damage != 0xff || record.CollisionRadiusX != 4 || record.CollisionRadiusY != 4 ||
            record.CollisionEffectTileBase != 0x18 || record.CollisionEffectOamFlags != 9 || record.CollisionEffectCounter != 0 ||
            record.CollisionEffectSound != OracleSoundEngine.SndLightTorch,
            "Pegasus projectile lost itemData/attributes or seeds.s @data's own $09/$18/$00 effect row.");
        var animation = OracleGraphicsCache.GetAnimationDefinition(record.Animation);
        FailIf(!animation.Frames.Select(f => (f.Duration, f.Parameter)).SequenceEqual(new[] { (1, 0), (3, 0), (3, 0), (3, 0), (127, 255) }),
            "ITEM22 must retain the shared itemAnimation1e829 timing and terminal $ff parameter.");
        FailIf(!animation.Frames.Select(f => f.EncodedOam).SequenceEqual(new[] {
            "8,4,0,0", "8,0,4,0;8,8,4,96", "8,0,0,0;8,8,0,32",
            "8,0,2,0;8,8,2,32", "8,0,2,0;8,8,2,32" }),
            "ITEM22 must resolve all four shared OAM pointers and retain the terminal composition.");
        foreach (bool batch in new[] { false, true })
        {
            void Step(int count = 1, Vector2 movement = default, bool attack = false) =>
                StepGameplayUpdates(count, movement, attack ? ["attack"] : [], attack ? ["attack"] : [], batched: batch);
            _saveData.RestoreFrom(initialSave!);
            RestoreOracleRandomForValidation(random);
            _entities.RestoreDebugStateBeforeRoomParse(entityState);
            typeof(InventoryState).GetMethod("LoadFromSaveData", flags)!.Invoke(_inventory, null);
            _seedSatchel.Pegasus.Clear();
            _seedSatchel.InterruptShooter();
            _inventory.GiveTreasure(0x19, 1);
            _inventory.GiveTreasure(0x0f, 1);
            _inventory.GiveTreasure(0x22, 0x20);
            _inventory.SelectShooterSeeds(2);
            _inventory.EquipA(InventoryState.ItemShooter);
            LoadValidationRoom(4, 0x91);
            _entities.RestoreDebugStateAfterRoomParse(entityState);
            _player.WarpTo(new Vector2(120, 144));
            Step(32, Vector2.Up);
            Step(movement: Vector2.Right, attack: true);
            FailIf(_inventory.PegasusSeeds != 0x20, "Shooter must wait for release before consuming Pegasus.");
            Step();
            var seed = _entities.Entities<EmberSeedEffect>().Single();
            FailIf(seed.SeedItem != 0x22 || seed.LaunchKind != SeedLaunchKind.Shooter || seed.Position != new Vector2(132, 117) ||
                seed.ZFixed != -512 || seed.BouncesRemaining != 3 || seed.AnimationFrame != 0 || _inventory.PegasusSeeds != 0x19 ||
                _seedSatchel.Pegasus.Active,
                "Released Pegasus projectile must initialize at the native shooter offset without activating Link's speed timer.");
            Vector2 previous = seed.Position;
            Step();
            FailIf(seed.Position != previous + Vector2.Right * 3 || seed.AnimationFrame != 0,
                "Pegasus flight must use SPEED_300 and retain its initial animation frame.");
            for (int i = 0; seed.State == EmberState.Flying && i < 300; i++) Step();
            FailIf(seed.State != EmberState.Dissipating || seed.BouncesRemaining != 0 || seed.AnimationFrame != 1 || seed.CollisionEnabled || seed.ScentTarget.HasValue,
                "Third terrain bounce must enter Pegasus state3, advance animation once, and disable collision without Scent attraction.");
            Step(8);
            FailIf(seed.Finished || seed.AnimationFrame != 3, "Pegasus effect ended before its ninth state3 update.");
            Step();
            FailIf(!seed.Finished || _entities.HasActiveShooterSeed, "The terminal $ff frame must delete the seed and release Shooter ownership.");

            // Shoot the first native 4:6e Gibdo from a clear floor position;
            // retain its original spawn, movement, RNG, and collision geometry.
            _seedSatchel.InterruptShooter();
            LoadValidationRoom(4, 0x6e);
            _player.WarpTo(new Vector2(124, 88));
            FailIf(_collision.Collides(_player.Position) || _currentRoom.GetTerrainInfo(_player.Position).Hazard != HazardType.None,
                "Gibdo projectile fixture must begin on actual clear floor.");
            var gibdo = _entities.Entities<GibdoCharacter>()[0];
            Step(movement: Vector2.Up, attack: true);
            Step();
            seed = _entities.Entities<EmberSeedEffect>().Single();
            int health = gibdo.Health;
            var sounds = new List<int>();
            _entities.SoundRequested += sounds.Add;
            Vector2 hitPosition = default;
            for (int i = 0; !seed.HasPendingNativeCollision && seed.State == EmberState.Flying && i < 80; i++) Step();
            FailIf(!seed.HasPendingNativeCollision || seed.State != EmberState.Flying || seed.AnimationFrame != 0 ||
                gibdo.InvincibilityCounter != -16 || gibdo.StunCounter != 240 || gibdo.Health != health,
                "Pegasus must write ENEMYDMG_38 after movement without consuming the new stun or pending ITEM hit in that collision pass.");
            hitPosition = gibdo.Position;
            Step();
            _entities.SoundRequested -= sounds.Add;
            FailIf(seed.State != EmberState.Dissipating || seed.AnimationFrame != 1 || gibdo.Health != health ||
                gibdo.InvincibilityCounter != -15 || gibdo.StunCounter != 240 - (_entities.FrameCounter & 1) || gibdo.Position != hitPosition,
                $"The next item/enemy update must consume the Pegasus hit and first stun update exactly once: seed={seed.State}/{seed.AnimationFrame}, inv={gibdo.InvincibilityCounter}, stun={gibdo.StunCounter}, health={gibdo.Health}, position={gibdo.Position}/{hitPosition}.");
            FailIf(!sounds.SequenceEqual(new[] { OracleSoundEngine.SndDamageEnemy, OracleSoundEngine.SndLightTorch }),
                "Pegasus stun must play ENEMYDMG_38's enemy sound before the projectile's state3 activation sound.");
            Step(8);
            FailIf(seed.Finished || seed.AnimationFrame != 3 || _entities.ActiveScentSeedTarget().HasValue,
                "A stunning Pegasus must retain its non-attracting state3 graphic through update8.");
            Step();
            FailIf(!seed.Finished || gibdo.StunCounter < 230 || gibdo.Health != health,
                "Deleting the Pegasus effect must leave the enemy's independent stun running.");
            Step(7);
            Step(movement: Vector2.Up, attack: true);
            Step();
            seed = _entities.Entities<EmberSeedEffect>().Single(s => !s.Finished);
            FailIf(seed.State != EmberState.Flying || _inventory.PegasusSeeds != 0x17,
                $"Repeat shot initialization: batch={batch}, state={seed.State}, frame={seed.ElapsedFrames}, seeds={_inventory.PegasusSeeds:x2}, pos={seed.Position}.");
            // Unsigned source comparison (enemyZ-itemZ+7)<14 accepts
            // differences -7..+6. Exercise its lower boundary with XY overlap.
            gibdo.ZFixed = -10 * 256;
            for (int i = 0; seed.Position.Y > gibdo.Position.Y + 4 && i < 20; i++)
            {
                // Hold this controlled collision-height boundary while the
                // real stunned actor now integrates its source gravity.
                gibdo.ZFixed = -10 * 256;
                typeof(GibdoCharacter).GetField("_speedZ", flags)!.SetValue(gibdo, 0);
                Step();
            }
            FailIf(seed.State != EmberState.Flying || !RoomEntityManager.ObjectCollisionXYOverlaps(gibdo.CollisionBounds, seed.CollisionBounds) ||
                gibdo.StunCounter >= 239,
                $"Pegasus height boundary: batch={batch}, seed={seed.State} {seed.Position} Z={seed.ZFixed}, elapsed={seed.ElapsedFrames}, bounces={seed.BouncesRemaining}, enemy={gibdo.Position} Z={gibdo.ZFixed}, stun={gibdo.StunCounter}.");
            gibdo.ZFixed = -9 * 256;
            for (int i = 0; seed.State == EmberState.Flying && i < 20; i++) Step();
            FailIf(seed.State != EmberState.Dissipating || gibdo.StunCounter < 239 || gibdo.Health != health ||
                _inventory.PegasusSeeds != 0x17,
                "After its invulnerability ends, the same Gibdo must accept a second real shot and refresh its stun without damage.");
            var result = (_inventory.PegasusSeeds, gibdo.StunCounter, _entities.RandomCalls, gibdo.Position);
            FailIf(first is { } previousResult && previousResult != result,
                $"Single and batched Pegasus projectile routes differ: {first} / {result}.");
            first = result;
            LoadValidationRoom(4, 0x6e);
            FailIf(_entities.HasActiveShooterSeed || _entities.Entities<GibdoCharacter>().Any(g => g.StunCounter != 0),
                "Room replacement must clear projectile ownership and the old enemies' stun state.");
        }
    }
}
