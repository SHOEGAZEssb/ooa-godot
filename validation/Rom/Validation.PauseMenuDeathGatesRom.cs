using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePauseMenuDeathGatesRom()
    {
        int hostCase1 = 0;
        foreach (bool introDone in new[] { false, true })
        foreach (int keys in new[] { 4, 8, 12 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            _saveData.SetGlobalFlag(GlobalFlag.PregameIntroDone);
            if (introDone) _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
            LoadValidationRoom(0, 0x06); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            var landing = new TimeWarpLandingDatabase();
            Vector2 position = Enumerable.Range(1, 6).SelectMany(y => Enumerable.Range(1, 8)
                .Select(x => new Vector2(x * 16 + 8, y * 16 + 8)))
                .First(point => !_collision.Collides(point) && landing.CanStandOnTile(_currentRoom, point, false) &&
                    !WarpDatabase.IsWarpTile(_currentRoom.ActiveCollisions, _currentRoom.GetMetatile(point)));
            _player.WarpTo(position); _player.Face(Vector2I.Down);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 2, (int)position.X, (int)position.Y);
            rom.InitializeLinkGameplay();
            var menus = rom.CreateMenuView();
            rom[0xcbe4] = rom[0xc6aa]; rom[0xcbe5] = rom[0xc6ad]; rom[0xcbe6] = rom[0xc6ae];
            rom[0xcbe9] = 0xff;
            FailIf(!_player.ApplyDamage(_player.HealthQuarters), "Lethal menu fixture did not publish pending death.");
            rom.ApplyLinkDamage(unchecked((byte)(-2 * rom[0xc6aa])));
            FailIf(!_player.IsDying || rom[0xcdd5] != 0xff || _player.HealthQuarters != rom[0xc6aa],
                "Native linkApplyDamage did not independently publish zero health and $ff death trigger.");
            // linkState01's caller has already selected LINK_STATE_DYING
            // after linkApplyDamage. Resume that state, as the existing shared
            // damage fixture does, without applying the lethal hit a second
            // time through linkState01's unconditional health check.
            rom[0xd004] = 3;
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0;
            void Step(int count = 1, int pressed = 0, int held = 0)
            {
                int edge = pressed;
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(held), MenuRomActions(pressed), batched, () =>
                {
                    // Menus observe pending $ff before standardGameState
                    // consumes it into $e7 and advances Link's death state.
                    menus.Update(edge, held, _saveData.ReadWramByte(0xc622));
                    rom.AdvanceDeathPrelude();
                    rom.UpdateGameplay(edge, held, 0xff, _entities.FrameCounter - 1);
                    menus.UpdateHud(_saveData.ReadWramByte(0xc622)); edge = 0;
                    string context = $"Death menu gates intro={introDone} keys=${keys:x2} batch={batched} update={++update}";
                    FailIf(_menuLifecycle.IsActive || rom[0xcbcb] != 0 || !_player.IsDying || rom[0xcdd5] != 0xe7 ||
                        _player.Position != position || _player.HealthQuarters != rom[0xc6aa] ||
                        _player.DeathAnimationActive != (rom[0xd004] == 3 && rom[0xd005] == 1),
                        context + $": menu={_menuLifecycle.IsActive}/${rom[0xcbcb]:x2}, dying={_player.IsDying}/${rom[0xcdd5]:x2}, position={_player.Position}/{position}, health={_player.HealthQuarters}/{rom[0xc6aa]}, animation={_player.DeathAnimationActive}/${rom[0xd004]:x2}:${rom[0xd005]:x2}.");
                    if (_player.DeathAnimationActive)
                        FailIf(_player.DeathAnimationCounter != rom[0xd020] ||
                            _player.DeathAnimationFrame != rom[0xd031] || _player.DeathSpinLoopsRemaining != rom[0xd006],
                            context + ": menu input changed native death animation/counter/loop boundaries.");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),
                        context + $": death/menu/IntroDone cue order differs: runtime={string.Join(',', sounds.Requests)}, native={string.Join(',', rom.Sounds)}.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                        random.Calls - seed.Calls != rom.RandomCalls, context + ": native death/menu RNG differs.");
                });
            }
            Step(1, keys, keys); Step(15, held: keys); Step();
            // Repeat fresh edges through the complete spin and into collapse,
            // stopping before the separate Game Over menu handoff.
            for (int repeat = 0; repeat < 11; repeat++) { Step(1, keys, keys); Step(15, held: keys); Step(); }
            FailIf(update != 204 || _player.DeathAnimationFrame != 4 || rom[0xcdd6] != 0 ||
                sounds.RequestsFor(SoundId.SndError) != 0 || sounds.RequestsFor(SoundId.SndCtrlSlowFadeOut) != 1,
                "Death gate must precede IntroDone throughout pending death, spin and collapse without starting Game Over early.");
        }
        GD.Print("Validated clean-US pending $ff/consumed $e7 death menu gates, IntroDone precedence, Start/Select/chord held/repeated input, native damage publication, full spin/collapse boundaries, sound/RNG and split/batched gameplay before Game Over.");
    }
}
