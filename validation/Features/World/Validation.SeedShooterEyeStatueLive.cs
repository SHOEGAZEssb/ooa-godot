using Godot;
using System;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSeedShooterEyeStatueLive()
    {
        var data = new SeedShooterEyeStatueDatabase();
        for (int i = 0; i < 32; i++)
            FailIf(data.HitLockout(i) != (i is >= 0x1a and <= 0x1e ? -28 : (int?)null),
                $"PART $46 collision ${i:x2} must match the independently traced active mask and effect31.");
        foreach (bool batch in new[] { false,true })
        {
            void Step(int count = 1) =>
                StepGameplayUpdates(count, Vector2.Zero, [], [], batched: batch);
            LoadValidationRoom(4,0xba);
            _player.WarpTo(new(120,88));
            FailIf(_currentRoom.IsSolid(_player.Position),"Crown eye-statue shot must originate on real floor.");
            var eyes = _entities.Entities<SeedShooterEyeStatueRoomEntity>();
            FailIf(eyes.Count != 3 || eyes.Any(eye => eye.Visible),"Crown must create three initially hidden PART $46 eyes.");
            var eye = eyes.Single(eye => eye.Position == new Vector2(120,56));
            Step();
            var seeds = new SeedSatchelDatabase();
            FailIf(!seeds.TryGet(ItemId.EmberSeed,out var seed),"Missing Ember Seed fixture.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                var projectile = _entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(_player.Position,Vector2I.Up,seed,4,SeedLaunchKind.Shooter));
                for (int i = 0; !eye.PendingHit && i < 16; i++) Step();
                FailIf(!eye.PendingHit || eye.Counter != 0 || eye.Invincibility != -28 || !projectile.CollisionEnabled,
                    "PART $46 effect31 must queue its hit and signed lockout without changing the flying seed.");
                Step();
                FailIf(!eye.Visible || eye.Counter != 0x2c || eye.Invincibility != -27 || (_entities.ActiveTriggers & 1) == 0,
                    "Eye activation must consume pending status after updating signed invincibility.");
                var text = _entities.TextActiveSource;
                try
                {
                    _entities.TextActiveSource = () => true;
                    Step(10);
                    FailIf(eye.Counter != 0x2c || eye.Invincibility != -27,"Text must freeze initialized PART $46 counters.");
                }
                finally { _entities.TextActiveSource = text; }
                Step(43);
                FailIf(!eye.Visible || eye.Counter != 1 || eye.Invincibility != 0,"Eye must remain active through its last counter update.");
                Step();
                FailIf(eye.Visible || eye.Counter != 0 || (_entities.ActiveTriggers & 1) != 0,"Eye expiry must clear trigger0 and hide on zero.");
            }
        }
        LoadValidationRoom(0,0x60);
        GD.Print("Validated Crown PART $46 live seed contact, pending hit, signed lockout, text freeze, exact expiry and repeated activation.");
    }
}
