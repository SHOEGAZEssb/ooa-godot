using Godot;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCrownOrbPlacements()
    {
        // PART_ORB $03:$00 source placements. Isolate their projectile
        // consumer from the separately checked shooter item parent.
        var seeds = new SeedSatchelDatabase();
        FailIf(!seeds.TryGet(0x20, out var ember), "Missing Ember Seed data.");
        foreach (var test in new (int Room, int Packed, Vector2 Origin, Vector2I Direction)[]
        {
            (0x9a, 0x5a, new(216, 88), Vector2I.Left),
            (0xa1, 0x35, new(120, 56), Vector2I.Left),
            (0xa2, 0x47, new(200, 72), Vector2I.Left),
            (0xa8, 0x36, new(104, 72), Vector2I.Up),
            (0xaa, 0x57, new(120, 120), Vector2I.Up),
            (0xb0, 0x57, new(168, 88), Vector2I.Left),
            (0xb9, 0x63, new(88, 104), Vector2I.Left)
        })
        {
            _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress, 0);
            LoadValidationRoom(4, test.Room);
            // Keep the source-placed orb and room geometry, while excluding
            // enemy interception from this isolated projectile-consumer check.
            var placement = new DungeonMechanicDatabase().GetRoomRecords(4, test.Room).Single(r => r.Id == 3);
            _entities.Clear();
            var orb = new DungeonOrbRoomEntity(placement, new DungeonMechanicDatabase(),
                new DungeonInteractionVisualDatabase().Visual("grotto-orb"), _currentRoom,
                _runtimeState, () => (long)_animationTicks, _sound.PlaySound);
            typeof(RoomEntityManager).GetMethod("AddEntity", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_entities, [orb]);
            _player.WarpTo(test.Origin);
            _player.ApplyInteractionInvincibility(240);
            FailIf(_collision.Collides(_player.Position), $"Crown4:{test.Room:x2} orb approach must start on real floor.");
            StepGameplayUpdates(2, test.Direction);
            FailIf(_collision.Collides(_player.Position), $"Crown4:{test.Room:x2} orb approach must remain outside solid geometry.");
            FailIf(orb.Position != new Vector2((test.Packed & 15) * 16 + 8, (test.Packed >> 4) * 16 + 8) || orb.ToggleMask != 1,
                "Crown orb must retain its source position and toggle bit0.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                bool expected = repeat == 0;
                _player.ApplyInteractionInvincibility(240);
                int angle = test.Direction == Vector2I.Left ? 6 : 0;
                _entities.Spawn<EmberSeedEffect>(new EmberSeedSpawn(_player.Position, test.Direction, ember, 4, SeedLaunchKind.Shooter, angle));
                for (int i = 0; orb.IsOn != expected && i < 80; i++) StepGameplayUpdates(1, Vector2.Zero);
                FailIf(orb.IsOn != expected, $"Crown4:{test.Room:x2} orb must accept a projectile through its actual geometry, repeat{repeat}; Link={_player.Position}, lockout={orb.HitLockout}, pending={orb.PendingHit}.");
                StepGameplayUpdates(8, Vector2.Zero, batched: true);
                FailIf(_entities.FloorToggle!.Active ||
                    _runtimeState.ReadWramByte(OracleRuntimeState.LastToggleBlocksStateAddress) != (expected ? 1 : 0),
                    $"Crown4:{test.Room:x2} orb must complete the shared floor toggle.");
                StepGameplayUpdates(32, Vector2.Zero);
            }
        }
        _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress, 0);
        LoadValidationRoom(0, 0x60);
    }
}
