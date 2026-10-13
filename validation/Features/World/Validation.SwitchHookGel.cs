using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSwitchHookSplitGels()
    {
        FailIf(!SwitchHookCollisionDatabase.Shared.EnemyEnabled(0x43) ||
            SwitchHookCollisionDatabase.Shared.Effect(0x33) != 0x0b,
            "ENEMY_GEL $43 mode $33 must enable hook column $0d and damage effect $0b.");
        foreach (bool batched in new[] { false, true })
        foreach (int level in new[] { 1, 2 })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0x91);
            _entities.Clear();
            _inventory.GiveTreasure(TreasureId.SwitchHook, level);
            _inventory.EquipA(TreasureId.SwitchHook);
            void Step(int count = 1, bool fire = false, Vector2 movement = default) =>
                StepGameplayUpdates(count, movement, fire ? ["attack"] : [], fire ? ["attack"] : [], batched);
            _player.WarpTo(new(120, 128));
            Step(16, movement: Vector2.Up);
            FailIf(_player.Position != new Vector2(120, 112) ||
                Enumerable.Range(80, 33).Any(y => Enumerable.Range(114, 13).Any(x => _currentRoom.IsSolid(new(x, y)))),
                "Split Gel hook fixture must approach through unchanged $4:$91 entrance geometry.");
            for (int repetition = 0; repetition < 2; repetition++)
            {
                _player.Face(Vector2I.Up);
                FailIf(!_entities.TrySpawnEnemy(0x34, 1, new(120, 80), "Switch Hook split-Gel regression", out string error), error);
                var zol = _entities.Entities<ZolCharacter>().Single();
                Step(fire: true);
                var firstHook = _entities.SwitchHook!.Item!;
                for (int i = 0; zol.Health == 3 && i < 30; i++) Step();
                FailIf(zol.Health != 1, "$34:$01 hook damage must leave the source 3-HP red Zol alive to split.");
                for (int i = 0; _entities.Entities<GelCharacter>().Count == 0 && i < 25; i++) Step();
                var gels = _entities.Entities<GelCharacter>().ToArray();
                FailIf(gels.Length != 2 || gels.Any(g => g.Health != 1) ||
                    !gels.Select(g => g.Name.ToString()).ToHashSet().SetEquals(new[] { "SplitGelLeft", "SplitGelRight" }),
                    "$34:$01 must create both native $43:$00 children through its split handler.");
                for (int i = 0; _player.IsUsingSwitchHook && i < 80; i++) Step();
                FailIf(!firstHook.Finished || _player.IsUsingSwitchHook, "Zol hit must release its hook parent before firing at its Gels.");
                int sounds = _sound.PlayRequestsFor(SoundId.SndDamageEnemy);
                int count = _entities.RoomEnemyCount;
                Step(fire: true);
                var hook = _entities.SwitchHook.Item!;
                FailIf(ReferenceEquals(hook, firstHook), "Completed Zol hook must permit a new item for its split Gels.");
                for (int i = 0; gels.All(g => g.Health == 1) && i < 30; i++) Step();
                // Both +/-4 X children overlap the same native hook. The item
                // retains collision until its next dispatch, so later slots hit too.
                FailIf(gels.Any(g => g.Health != 0 || g.IsDead || !g.NativeHitPending ||
                        !g.PendingKnockbackDeath || g.CollisionEnabled || g.InvincibilityCounter != 32 || g.KnockbackCounter != 0) ||
                    hook.State != 1 || !hook.CollisionEnabled || _entities.SwitchHook.ExchangeActive ||
                    _sound.PlayRequestsFor(SoundId.SndDamageEnemy) != sounds + 2 || _entities.RoomEnemyCount != count,
                    "Both dynamic $43 Gels must take $fe damage/$20 invincibility/zero recoil after ENEMY dispatch, without immediate death or exchange.");
                int[] counters = gels.Select(g => g.Counter1).ToArray();
                Vector2[] positions = gels.Select(g => g.Position).ToArray();
                var seed = _random.CaptureState();
                int rng1 = seed.Rng1, rng2 = seed.Rng2, calls = seed.Calls;
                var expectedStates = gels.Select(g => g.State).ToArray();
                int[] expectedCounters = counters.Select(c => c - 1).ToArray();
                // gel_state8 still runs on JUST_HIT. A zero wait consumes one
                // global RNG call per child and selects $0a/$30 or $09/$08.
                for (int i = 0; i < gels.Length; i++)
                {
                    FailIf(expectedStates[i] != GelState.Waiting, "$43 split children must still be in state $08 at hook contact.");
                    if (counters[i] != 1) continue;
                    rng2 = ((((rng2 << 8) | rng1) * 3) & 0xffff) >> 8;
                    rng1 = (rng1 + rng2) & 0xff; calls++;
                    expectedStates[i] = (rng1 & 7) == 0 ? GelState.Shaking : GelState.Inching;
                    expectedCounters[i] = (rng1 & 7) == 0 ? 48 : 8;
                }
                Step();
                FailIf(hook.State != 2 || gels.Where((g, i) => g.IsDead || g.NativeHitPending ||
                        g.InvincibilityCounter != 31 || g.KnockbackCounter != 0 || g.State != expectedStates[i] ||
                        g.Counter1 != expectedCounters[i] || g.Position != positions[i]).Any() ||
                    _random.Calls != calls || _random.CaptureState().Rng1 != rng1 || _random.CaptureState().Rng2 != rng2,
                    "$43 JUST_HIT must advance normal waiting AI while the next ITEM pass retracts the hook: " +
                    $"L{level} repeat={repetition} hook={hook.State}; " + string.Join("; ", gels.Select((g,i) =>
                        $"{g.Name} dead={g.IsDead}, pending={g.NativeHitPending}, inv={g.InvincibilityCounter}, recoil={g.KnockbackCounter}, state={g.State}, counter={g.Counter1}/{counters[i]}, position={g.Position}/{positions[i]}")));
                Step();
                FailIf(_entities.Entities<GelCharacter>().Count != 0 || _entities.Entities<EnemyDeathPuffEffect>().Count != 2,
                    "$43 health-zero dispatch must replace both split Gels with their native death puffs one update after JUST_HIT.");
                for (int i = 0; _player.IsUsingSwitchHook && i < 80; i++) Step();
                Step(24);
                FailIf(!hook.Finished || _entities.SwitchHook.ExchangeActive || _player.Position != new Vector2(120, 112) ||
                    _entities.RoomEnemyCount != 0 || _entities.Entities<EnemyDeathPuffEffect>().Count != 0,
                    "Gel hook completion must retain Link's position and finish the counted death-puff lifecycle before repeated use.");
            }
        }
    }
}
