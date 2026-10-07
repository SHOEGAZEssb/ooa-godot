using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePauseMenuToggleGatesRom() => RunPauseMenuToggleGatesRom(false);
    private void ValidateHudHeartBeepToggleRom() => RunPauseMenuToggleGatesRom(true);

    private void RunPauseMenuToggleGatesRom(bool lowHealthWarning)
    {
        int hostCase1 = 0;
        foreach (bool introDone in new[] { false, true })
        foreach (int keys in new[] { 4, 8, 12 })
        foreach (int elapsed in new[] { 0, 1, 6, 7 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            ReinitializeGameplayForValidation();
            if (lowHealthWarning)
            {
                _inventory.ApplyDamage(_inventory.HealthQuarters - 1); _statusBar.SynchronizeHealth();
                _saveData.WriteWramByte(0xc622, (byte)(63 - elapsed));
            }
            _saveData.SetGlobalFlag(GlobalFlag.PregameIntroDone);
            if (introDone) _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
            _runtimeState.SetWramByte(0xcdd2, 0);
            LoadValidationRoom(4, 0xa1); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0); _player.WarpTo(new Vector2(120, 56));
            FailIf(_collision.Collides(_player.Position), "Toggle menu fixture floor is not reachable.");
            var rom = new MenuRom(_saveData, _currentRoom);
            rom.LoadRoomTileset(); rom.CopyToggleRoom(_currentRoom);
            FailIf(rom[0xcc39] != 5, "Crown toggle fixture missed native dungeon $05 selection.");
            rom.LoadDungeon(5);
            var toggle = _entities.FloorToggle!;
            // Declared completed orb publication/selection at the preceding
            // cutscene01 tail. The native cutscene caller executes thereafter.
            _runtimeState.SetWramByte(0xcdd2, 1); toggle.CheckAfterObjects();
            FailIf(!toggle.Active, "Toggle source publication did not select its runtime cutscene.");
            rom[0xcdd2] = 1; rom[0xc2ef] = 2;
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0;
            void Step(int count = 1, int pressed = 0, int held = 0)
            {
                int edge = pressed;
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(held), MenuRomActions(pressed), batched, () =>
                {
                    rom.AdvancePalette();
                    if (rom[0xc2ef] == 2) rom.AdvanceToggleCutscene();
                    else rom.Update(edge, held, _saveData.ReadWramByte(0xc622));
                    edge = 0;
                    string context = $"Toggle menu gates intro={introDone} keys=${keys:x2} elapsed={elapsed} update={++update} batch={batched}";
                    FailIf(_menuLifecycle.IsActive != (rom[0xcbcb] != 0), context + ": menu dispatch differs from native cutscene caller.");
                    FailIf(toggle.Active != (rom[0xc2ef] == 2) || toggle.Active &&
                        (toggle.State != rom[0xcc03] || toggle.Counter != rom[0xcbb4]),
                        context + $": cutscene state/counter differs: runtime={toggle.State}/{toggle.Counter}, native=${rom[0xc2ef]:x2}/{rom[0xcc03]}/{rom[0xcbb4]}.");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),
                        context + $": cutscene/IntroDone/menu cue order differs: runtime={string.Join(',', sounds.Requests)}, native={string.Join(',', rom.Sounds)}.");
                });
            }
            if (elapsed != 0) Step(elapsed);
            Step(1, keys, keys);
            FailIf(_menuLifecycle.IsActive || sounds.RequestsFor(0x5a) != 0,
                "Toggle cutscene updates, including completion, must bypass normal-menu and IntroDone dispatch.");
            if (lowHealthWarning) FailIf(sounds.RequestsFor(0x60) != 0,
                "The toggle cutscene caller must bypass the global $40 warning, including its completion update.");
            Step(9 - update, held: keys);
            FailIf(toggle.Active || _menuLifecycle.IsActive,
                "Completing the toggle must not reuse an old held menu edge.");
            Step(); Step(1, keys, keys);
            FailIf(_menuLifecycle.IsActive != introDone || sounds.RequestsFor(0x5a) != (introDone ? 0 : 1),
                "The next normal cutscene must restore fresh-edge menu/IntroDone eligibility.");
            if (_menuLifecycle.IsActive)
            {
                Step(22); int cancel = keys == 8 ? 8 : 2;
                Step(1, cancel, cancel); Step(22);
            }
            if (lowHealthWarning)
            {
                Step((128 - _saveData.ReadWramByte(0xc622) + 256) & 0xff);
                FailIf(sounds.RequestsFor(0x60) != 1 || _menuLifecycle.IsActive,
                    "The next global $80 warning must resume after toggle completion and menu cancellation.");
                Step(); Step(1, keys, keys);
                if (_menuLifecycle.IsActive)
                {
                    Step(22); int cancel = keys == 8 ? 8 : 2;
                    Step(1, cancel, cancel); Step(22);
                }
            }
        }
        GD.Print("Validated clean-US toggle-floor cutscene caller bypasses menus/IntroDone on initialization, delay and completion; native state/counters, full cue order, held/fresh edges and closing through split/batched gameplay. Orb contact and tile graphics are outside this fixture.");
    }
}
