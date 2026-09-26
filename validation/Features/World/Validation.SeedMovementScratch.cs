using Godot;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSeedMovementScratch()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        foreach (bool batch in new[] { false, true })
        foreach (bool shooter in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0xa1);
            _entities.Clear();
            _player.ApplicationUpdateOwned = true;
            _player.WarpTo(new(120, 40));
            FailIf(!new SeedSatchelDatabase().TryGet(ItemId.EmberSeed, out var record), "Missing ITEM$20.");
            // seeds.s rightward offsets: Satchel (+4,+1), shooter (+12,+5).
            var seed = _entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(
                shooter ? new(108, 35) : new(116, 39), Vector2I.Right, record, 4,
                shooter ? SeedLaunchKind.Shooter : SeedLaunchKind.Satchel, 2));
            byte[] expected = [0xa5, 0xa5, 0xa5, 0xa5];
            void Stage()
            {
                for (int i = 0; i < 4; i++) _runtimeState.SetWramByte(0xcec0 + i, expected[i]);
            }
            Stage();
            int observations = 0;
            var observer = new ItemPhaseValidationEntity(() =>
            {
                for (int i = 0; i < 4; i++)
                    FailIf(_runtimeState.ReadWramByte(0xcec0 + i) != expected[i],
                        $"ITEM$20 shooter={shooter}: scratch ${0xcec0 + i:x4} differs after item movement.");
                observations++;
            });
            typeof(RoomEntityManager).GetMethod("RegisterEnemySlot", flags)!.Invoke(_entities, [observer, 0]);
            typeof(RoomEntityManager).GetMethod("AddEntity", flags)!.Invoke(_entities, [observer]);
            StepGameplayUpdates(1, Vector2.Zero, batched: batch);
            FailIf(seed.State != EmberState.Flying || seed.Position != new Vector2(120, 40),
                "Seed initialization must preserve scratch and source position offsets.");
            // Source chooses SPEED_0c0 for Satchel and SPEED_300 for shooter.
            expected = shooter ? [0, 0, 0, 3] : [0, 0, 0xc0, 0];
            StepGameplayUpdates(2, Vector2.Zero, batched: batch);
            FailIf(seed.PrecisePosition != new Vector2(shooter ? 126 : 121.5f, 40),
                "Both moving seed updates must publish and apply the source velocity.");
            seed.QueueNativeCollision(new(true, SeedHitResult.Consume, true));
            expected = [0xff, 0xa5, 0xa5, 0xa5];
            Stage();
            StepGameplayUpdates(2, Vector2.Zero, batched: batch);
            FailIf(!seed.Finished || observations != 5,
                "Consumed seeds must stop before movement and preserve scratch on later updates.");
        }
        ReinitializeGameplayForValidation();
    }
}
