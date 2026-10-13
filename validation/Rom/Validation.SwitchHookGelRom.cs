using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSwitchHookGelDamageRom()
    {
        foreach (int health in new[] { 1, 5 })
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            LoadValidationRoom(4, 0x91);
            _entities.Clear();
            _inventory.GiveTreasure(TreasureId.SwitchHook, 1);
            _inventory.EquipA(TreasureId.SwitchHook);
            _player.WarpTo(new(120, 112)); _player.Face(Vector2I.Up);
            var gel = _entities.Spawn<GelCharacter>(new GelSpawn(new(120, 80), "SplitGelLeft"));
            var rom = new EnemyStatusRom(_currentRoom, _saveData, _random.Calls);
            const int address = 0xd080;
            rom[0xcc08] = ValidationRom.LoadCleanUs().Span[0xfdd4b + 0x43 * 4];
            rom[address] = 1; rom[address + 1] = 0x43;
            rom[address + 11] = 80; rom[address + 13] = 120;
            rom.Update(0, _player.Position); // load native properties/animation and initialize state $08
            gel.Health = health; rom[address + 0x29] = (byte)health;
            gel.SetStateForValidation(GelState.Waiting, counter1: 64); rom[address + 6] = 64;
            bool hit = false, frozen = false;
            int sounds = _sound.PlayRequestsFor(SoundId.SndDamageEnemy);
            void Compare()
            {
                rom[0xcba0] = (byte)(frozen ? 1 : 0);
                rom.Update(_entities.FrameCounter, _player.Position);
                if (!hit && gel.NativeHitPending)
                {
                    rom.HitWithSword(0x0d, 2);
                    hit = true;
                    FailIf(rom[address + 0x2a] != 0x8d || rom[address + 0x2b] != 0x20 ||
                        rom[address + 0x2d] != 0 || rom[address + 0x29] != (health == 1 ? 0 : 3) ||
                        rom[0xd72a] != 1,
                        "Clean-US Gel hook effect $0b must publish JUST_HIT $8d, invincibility $20, zero recoil, $fe damage and item collision bit0.");
                }
                if (rom[address] == 0)
                {
                    FailIf(_entities.Entities<GelCharacter>().Contains(gel), "Clean-US Gel enemyDie must retire the same ENEMY update.");
                    return;
                }
                FailIf(gel.Health != rom[address + 0x29] || (gel.InvincibilityCounter & 0xff) != rom[address + 0x2b] ||
                    gel.KnockbackCounter != rom[address + 0x2d] || gel.NativeHitPending != ((rom[address + 0x2a] & 0x80) != 0) ||
                    gel.Counter1 != rom[address + 6] || (int)gel.State != rom[address + 4] ||
                    gel.CollisionEnabled != ((rom[address + 0x24] & 0x80) != 0),
                    $"Clean-US $43 hook collision/status differs: health={health}, counter={gel.Counter1}/{rom[address+6]}, hit={gel.NativeHitPending}/${rom[address+0x2a]:x2}.");
            }
            void Step(int count = 1, bool fire = false) => StepGameplayUpdates(count, Vector2.Zero,
                fire ? ["attack"] : [], fire ? ["attack"] : [], batched, Compare);
            Step(fire: true);
            for (int i = 0; !hit && i < 30; i++) Step();
            FailIf(!hit || _sound.PlayRequestsFor(SoundId.SndDamageEnemy) != sounds + 1,
                "$43 real hook flight must reach the native Gel and request one damage sound.");
            var hook = _entities.SwitchHook!.Item!;
            var text = _entities.TextActiveSource;
            try
            {
                frozen = true; _entities.TextActiveSource = () => true;
                Step(2);
                FailIf(hook.State != 1 || !gel.NativeHitPending, "Text must retain both pending Gel damage and hook retraction.");
            }
            finally { frozen = false; _entities.TextActiveSource = text; }
            Step();
            FailIf(hook.State != 2 || gel.IsDead || gel.Counter1 != rom[address + 6],
                "$43 JUST_HIT must continue AI without early death while the hook retracts.");
            Step();
            for (int i = 0; _player.IsUsingSwitchHook && i < 80; i++) StepGameplayUpdates(1, Vector2.Zero);
            FailIf(!hook.Finished || _entities.SwitchHook.ExchangeActive || _player.Position != new Vector2(120, 112),
                "$43 hook damage must complete without exchanging Link.");
        }
    }
}
