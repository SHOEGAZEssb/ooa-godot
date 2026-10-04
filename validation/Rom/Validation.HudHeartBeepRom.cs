using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateHudHeartBeepMountedRom()
    {
        int hostCase3 = 0;
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        foreach (bool batched in RomHostSchedules(hostCase3++))
        {
            var (actor, companion) = PrepareMountedCompanionRom(id, 1);
            StepGameplayUpdates(1, Vector2.Zero, afterUpdate: () => companion.Update(0xff));
            FailIf(!_player.CompanionRideActive || companion[0xd001] != 9,
                "Mounted warning fixture must retain SPECIALOBJECT_LINK_RIDING_ANIMAL $09.");
            _saveData.SetGlobalFlag(GlobalFlag.PregameIntroDone); _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
            _inventory.ApplyDamage(_inventory.HealthQuarters - 1); companion[0xc6aa] = 1;
            _saveData.WriteWramByte(0xc622, 63);
            var menu = new MenuRom(_saveData, _currentRoom);
            var sounds = _sound.AttachPlayRequestAudit();
            StepGameplayUpdates(65, Vector2.Zero, batched: batched, afterUpdate: () =>
            {
                // updateMenus observes the preceding special-object ID; the
                // original species and rider-copy pass execute afterwards.
                menu[0xd001] = companion[0xd001];
                menu.Update(0, 0, _saveData.ReadWramByte(0xc622)); companion.Update(0xff);
                CompareCompanionMotion(actor, companion, $"Warning mounted companion=${id:x2} batch={batched}");
                FailIf(!sounds.Requests.Where(sound => sound == 0x60).SequenceEqual(menu.Sounds.Where(sound => sound == 0x60)),
                    $"Mounted companion ${id:x2}: native $09 ID must permit the ordinary low-health warning.");
            });
            FailIf(sounds.RequestsFor(0x60) != 2 || !_player.CompanionRideActive,
                "Mounted Link must preserve two global warning boundaries without losing his companion.");
        }
        GD.Print("Validated clean-US low-health warning with mounted Ricky/Dimitri/Moosh, original $09 rider ID, native species/motion/rider copy and two global cue boundaries through split/batched gameplay.");
    }

    private void ValidateHudHeartBeepRecoveryRom()
    {
        int hostCase2 = 0;
        foreach (int maximum in new[] { 12, 56, 57, 64 })
        foreach (bool damage in new[] { false, true })
        foreach (bool hidden in new[] { false, true })
        foreach (int frame in new[] { 64, 192 })
        foreach (bool batched in RomHostSchedules(hostCase2++))
        {
            int threshold = (maximum + 3) / 4;
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(0xc6aa, (byte)(threshold + (damage ? 1 : 0)));
            save.WriteWramByte(0xc6ab, (byte)maximum);
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone); save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save); LoadValidationRoom(0, 0x06); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0); _player.WarpTo(new Vector2(24, 24));
            _saveData.WriteWramByte(0xc622, (byte)(frame - 1));
            var rom = new MenuRom(_saveData, _currentRoom);
            if (hidden) { _hud.HideStatusBar(); rom[0xcbe7] = 0x77; }
            var sounds = _sound.AttachPlayRequestAudit();
            if (damage) _inventory.ApplyDamage(1); else _inventory.Heal(1);
            _inventory.AddRupees(3); rom.CopySave(_saveData);
            int update = 0;
            void Step(int count)
            {
                StepGameplayUpdates(count, Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    int phase = _saveData.ReadWramByte(0xc622);
                    rom.Update(0, 0, phase); rom.UpdateHud(phase);
                    string context = $"Warning recovery maximum=${maximum:x2} damage={damage} hidden={hidden} first-frame=${frame:x2} update={++update} batch={batched}";
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds), context + $": live warning/display recovery cue order differs: runtime={string.Join(',', sounds.Requests)}, native={string.Join(',', rom.Sounds)}.");
                    int money = (rom[0xcbe6] & 15) * 100 + (rom[0xcbe5] >> 4) * 10 + (rom[0xcbe5] & 15);
                    FailIf(_statusBar.DisplayedHealth != rom[0xcbe4] || _statusBar.DisplayedRupees != money,
                        context + ": trailing HUD health/money differs.");
                });
            }
            Step(1);
            FailIf(sounds.RequestsFor(0x60) != (damage ? 1 : 0),
                "Warning must read live health before displayed-health and rupee updates, even with the HUD hidden.");
            if (damage) _inventory.Heal(1); else _inventory.ApplyDamage(1);
            rom.CopySave(_saveData); Step(64);
            FailIf(sounds.RequestsFor(0x60) != 1, "Changing live health did not cross the next native warning boundary.");
            if (hidden) { _hud.ShowStatusBar(); rom[0xcbe7] = 0; }
            _inventory.Heal(1); rom.CopySave(_saveData); Step(64);
            FailIf(sounds.RequestsFor(0x60) != 1, "Trailing displayed health must not replay a warning after live healing.");
        }
        GD.Print("Validated clean-US low-health warning reads live health before delayed displayed-health/rupee updates, hidden HUD retains warning cadence while freezing its display, full cue order and recovery through split/batched gameplay.");
    }

    private void ValidateHudHeartBeepRom()
    {
        int hostCase1 = 0;
        foreach (int maximum in new[] { 12, 56, 57, 64 })
        foreach (int health in new[] { 1, (maximum + 3) / 4, (maximum + 3) / 4 + 1 })
        foreach (int gate in new[] { 0, 1, 2, 3, 4 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            var save = OracleSaveData.CreateStandardGame();
            save.WriteWramByte(0xc6aa, (byte)health); save.WriteWramByte(0xc6ab, (byte)maximum);
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);
            if (gate != 3) save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save); LoadValidationRoom(0, 0x06); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0); _player.WarpTo(new Vector2(24, 24));
            FailIf(_collision.Collides(_player.Position), "Heart-beep fixture floor is not reachable.");
            _saveData.WriteWramByte(0xc622, 62);
            var rom = new MenuRom(_saveData, _currentRoom);
            if (gate == 1) { _entities.LockSmogLinkAndMenu(); rom[0xcc02] = 0x80; }
            if (gate == 2) { _entities.DisableLinkCollisionsAndMenu(); rom[0xcbca] = 0x80; }
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0;
            static bool Relevant(int sound) => sound is 0x60 or 0x54 or 0x55 or 0x5a;
            void Step(int count = 1, int pressed = 0, int held = 0)
            {
                int edge = pressed;
                StepGameplayUpdates(count, Vector2.Zero, MenuRomActions(held), MenuRomActions(pressed), batched, () =>
                {
                    rom.AdvancePalette(); rom.Update(edge, held, _saveData.ReadWramByte(0xc622)); edge = 0;
                    string context = $"Heart beep maximum=${maximum:x2} health=${health:x2} gate={gate} update={++update} batch={batched}";
                    FailIf(!sounds.Requests.Where(Relevant).SequenceEqual(rom.Sounds.Where(Relevant)),
                        context + $": heart/menu cue order differs: runtime={string.Join(',', sounds.Requests.Where(Relevant))}, native={string.Join(',', rom.Sounds.Where(Relevant))}.");
                    FailIf(_menuLifecycle.IsActive != (rom[0xcbcb] != 0), context + ": menu ownership differs.");
                });
            }
            Step(); // Global frame $3f is silent.
            int keys = gate is 3 or 4 ? 12 : 0;
            Step(1, keys, keys); // $40: native cue precedes an accepted menu.
            Step(65, held: keys); // $80: active menu or eligibility gates suppress it.
            int expected = health <= (maximum + 3) / 4 ? gate == 0 ? 2 : gate is 3 or 4 ? 1 : 0 : 0;
            FailIf(sounds.RequestsFor(0x60) != expected,
                "Native low-health boundary or global $40-frame cadence changed.");
            if (gate == 4)
            {
                Step(1, 2, 2); Step(22); // Save/Quit cancel, ordinary pause release.
                Step(40); // Global $c0 resumes the original warning phase.
                FailIf(_menuLifecycle.IsActive || sounds.RequestsFor(0x60) != (health <= (maximum + 3) / 4 ? 2 : 0),
                    "Closing a menu must resume low-health warnings on the retained global phase.");
            }
        }
        GD.Print("Validated clean-US low-health arithmetic at $38/$39 compression thresholds, exact global $40/$80/$c0 cadence, independent disable bytes, IntroDone error-before-warning precedence, accepted chord ordering and post-menu warning resumption in split/batched gameplay.");
    }
}
