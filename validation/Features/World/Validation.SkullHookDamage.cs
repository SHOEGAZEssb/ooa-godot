using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSkullHookDamage()
    {
        var collisions = SwitchHookCollisionDatabase.Shared;
        FailIf(collisions.Effect(EnemyCollisionMode.Zol) != 0x0b || collisions.Effect(EnemyCollisionMode.FireKeese) != 8 ||
            collisions.Effect(EnemyCollisionMode.PeahatVulnerable) != 0x0b || collisions.Effect(EnemyCollisionMode.Peahat) != 0x1c || collisions.Effect(EnemyCollisionMode.Moldorm) != 8,
            "Skull ordinary enemies lost source hook effects $0b/$08/$1c.");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Step(int count = 1, bool fire = false, Vector2 movement = default) =>
            StepGameplayUpdates(count, movement, fire ? ["attack"] : [], fire ? ["attack"] : [], batched: true);
        _inventory.GiveTreasure(TreasureId.SwitchHook, 1);
        _inventory.EquipA(TreasureId.SwitchHook);
        var random = CaptureOracleRandomForValidation();
        foreach (bool batch in new[] { false, true })
        foreach (string scenario in new[] { "red", "red-lethal", "green", "fire", "moldorm", "peahat", "peahat-air" })
        {
            RestoreOracleRandomForValidation(random);
            LoadValidationRoom(4, 0x91);
            _player.WarpTo(new Vector2(120, 128));
            _player.SetBraceletLiftCollisionsDisabled(true);
            for (int i = 0; _player.Position.Y > 112 && i < 30; i++) Step(movement: Vector2.Up);
            FailIf(_player.Position.Y != 112 || Enumerable.Range(80, 33).Any(y =>
                Enumerable.Range(115, 11).Any(x => _currentRoom.IsSolid(new Vector2(x, y)))),
                "Ordinary enemy hook approach must follow the actual entrance floor.");
            int id = scenario.StartsWith("peahat") ? 0x3e : scenario == "fire" ? 0x39 : scenario == "moldorm" ? 0x4f : 0x34;
            int subid = scenario.StartsWith("red") ? 1 : 0;
            FailIf(!_entities.TrySpawnEnemy(id, subid, new Vector2(120, id == 0x4f ? 96 : 80), "Skull hook collision regression", out string error), error);
            if (id == 0x4f) Step(); // Native spawner allocates head and two tail ENEMY slots.
            EnemyCharacter enemy = id switch
            {
                0x34 => _entities.Entities<ZolCharacter>().Single(),
                0x39 => _entities.Entities<FireKeeseCharacter>().Single(),
                0x3e => _entities.Entities<PeahatCharacter>().Single(),
                _ => _entities.Entities<MoldormCharacter>().Single()
            };
            if (scenario == "red-lethal") enemy.Health = 1;
            if (scenario == "green")
            {
                for (int i = 0; !enemy.CollisionEnabled && i < 100; i++) Step();
                FailIf(!enemy.CollisionEnabled, "Green Zol must emerge naturally before the hook can collide.");
            }
            if (enemy is FireKeeseCharacter fireBat)
            {
                // First shoot past a native high-flying bat: its Z must exclude the hook.
                Step(fire: true);
                var highHook = _entities.SwitchHook!.Item!;
                for (int i = 0; !highHook.Finished && i < 80; i++) Step();
                FailIf(fireBat.Health != 2 || !highHook.Finished, "Hook must pass beneath Fire Keese at native flight height.");
                // Isolate the source torch-relighting state, whose Z=-6 permits item collisions.
                fireBat.Position = new Vector2(120, 80);
                typeof(FireKeeseCharacter).GetField("<ZFixed>k__BackingField", flags)!.SetValue(fireBat, -6 * 256);
                typeof(FireKeeseCharacter).GetField("<State>k__BackingField", flags)!.SetValue(fireBat, 10);
                typeof(FireKeeseCharacter).GetField("<Counter>k__BackingField", flags)!.SetValue(fireBat, 60);
            }
            if (scenario == "peahat-air")
            {
                var peahat = (PeahatCharacter)enemy;
                for (int i = 0; peahat.ZHigh != -1 && i < 160; i++) Step();
                FailIf(peahat.ZHigh != -1 || peahat.CollisionMode != 0x2e,
                    "Peahat takeoff must retain ground collision mode on its first Z=-1 update.");
                Step();
                FailIf(peahat.CollisionMode != 0x58, "Peahat must adopt flight mode on the following update.");
                for (int i = 0; peahat.State != PeahatState.Flying && i < 100; i++) Step();
                peahat.Position = new Vector2(120, 80);
            }
            int hp = enemy.Health;
            Step(fire: true);
            var hook = _entities.SwitchHook!.Item!;
            for (int i = 0; enemy.InvincibilityCounter == 0 && hook.State != 2 && !hook.Finished && i < 60; i++) Step();
            if (scenario == "peahat-air")
            {
                var peahat = (PeahatCharacter)enemy;
                FailIf(hook.State != 2 || enemy.Health != hp || enemy.InvincibilityCounter != 0 ||
                    enemy.KnockbackCounter != 0 || peahat.State != PeahatState.Flying,
                    "Flying Peahat must retract the hook without damage, recoil, or stopping its normal state.");
            }
            else
            {
                bool recoil = id is 0x39 or 0x4f;
                FailIf(enemy.Health != Math.Max(0, hp - 2) || enemy.IsDead || hook.State != 1 ||
                    enemy.InvincibilityCounter != (recoil ? 16 : 32) || enemy.KnockbackCounter != (recoil ? 8 : 0),
                    $"{scenario}: hook must apply native damage $fe with the collision profile; HP={enemy.Health}, inv={enemy.InvincibilityCounter}, recoil={enemy.KnockbackCounter}, hook={hook.State}.");
                Vector2 hit = enemy.Position;
                Step();
                FailIf(enemy.Position != hit || enemy.IsDead || hook.State != 2 ||
                    enemy.InvincibilityCounter != (recoil ? 15 : 31) || enemy.KnockbackCounter != (recoil ? 8 : 0),
                    $"{scenario}: JUST_HIT must precede movement, recoil, and health-zero dispatch.");
                if (scenario.StartsWith("red"))
                {
                    var zol = (ZolCharacter)enemy;
                    FailIf(zol.State != ZolState.RedSplitting, "Red Zol must select splitting on the JUST_HIT update.");
                    Step();
                    if (scenario == "red-lethal")
                        FailIf(!zol.IsDead || _entities.Entities<EnemyDeathPuffEffect>().Count == 0 ||
                            _entities.Entities<GelCharacter>().Count != 0,
                            "Lethal red Zol hook damage must reach enemyDie before spawning split Gels.");
                    else
                    {
                        FailIf(zol.State != ZolState.RedSplitDelay || zol.Counter2 != 18 || zol.Visible || zol.CollisionEnabled,
                            "Surviving red Zol must hide and begin its eighteen-update split delay.");
                        if (batch) Step(17); else for (int i = 0; i < 17; i++) Step();
                        FailIf(zol.Counter2 != 1 || _entities.Entities<GelCharacter>().Count != 0, "Zol split early.");
                        Step();
                        var gels = _entities.Entities<GelCharacter>();
                        FailIf(!zol.IsDead || gels.Count != 2 || !gels.Any(g => g.Position == hit + Vector2.Right * 4) ||
                            !gels.Any(g => g.Position == hit + Vector2.Left * 4),
                            "Zol must replace itself with two Gels at source X offsets +4/-4 at counter zero.");
                    }
                }
                else if (recoil)
                {
                    if (batch) Step(8); else for (int i = 0; i < 8; i++) Step();
                    FailIf(enemy.KnockbackCounter != 0 || enemy.Position.DistanceTo(hit) < 14 ||
                        enemy is FireKeeseCharacter { Lit: false },
                        $"{scenario}: source low recoil must finish eight movements without shedding fire.");
                }
            }
            for (int i = 0; !hook.Finished && i < 80; i++) Step();
            FailIf(!hook.Finished || _entities.SwitchHook.ExchangeActive || _player.Position != new Vector2(120, 112),
                $"{scenario}: ordinary damage must release the parent without exchanging Link.");
            _player.Face(Vector2I.Down);
            Step(fire: true);
            var repeated = _entities.SwitchHook.Item;
            FailIf(repeated is null || ReferenceEquals(repeated, hook), $"{scenario}: completed hook action did not permit a second shot.");
            for (int i = 0; !repeated!.Finished && i < 80; i++) Step();
            FailIf(!repeated!.Finished, $"{scenario}: second hook action did not complete.");
        }
    }
}
