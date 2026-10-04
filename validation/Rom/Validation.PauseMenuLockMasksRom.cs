using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePauseMenuLockMasksRom()
    {
        // b2_updateMenus checks IntroDone before these two independent bytes.
        // INTERAC$33 writes wMenuDisabled=$01; script disablemenu writes $80.
        // Toggle floors write wDisableLinkCollisionsAndMenu=$ff. Both bytes
        // persist across menu dispatches until their respective owner clears.
        int hostCase1 = 0;
        foreach (bool introDone in new[] { false, true })
        foreach (int keys in new[] { 4, 8, 12 })
        foreach (int value in new[] { 1, 0x80, 0xff })
        foreach (int locks in new[] { 1, 2, 3 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            var application = new ApplicationValidationFixture(this);
            application.ResetGameplay();
            if (introDone) _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
            LoadValidationRoom(0, 0x45);
            var rom = new MenuRom(_saveData, _currentRoom);
            var sounds = _sound.AttachPlayRequestAudit();
            void SetLocks(int mask)
            {
                if ((mask & 1) != 0) _entities.LockSmogLinkAndMenu();
                else _entities.ReleaseSmogLinkAndMenu();
                if ((mask & 2) != 0) _entities.DisableLinkCollisionsAndMenu();
                else _entities.EnableLinkCollisionsAndMenu();
                rom[0xcc02] = (byte)((mask & 1) != 0 ? value : 0);
                rom[0xcbca] = (byte)((mask & 2) != 0 ? value : 0);
            }
            void Step(int count = 1, int pressed = 0, int held = 0)
            {
                int edge = pressed;
                application.Step(count, Vector2.Zero, MenuRomActions(held), MenuRomActions(pressed), batched, () =>
                {
                    rom.AdvancePalette();
                    rom.Update(edge, held, _saveData.ReadWramByte(0xc622));
                    edge = 0;
                    FailIf(_menuLifecycle.IsActive != (rom[0xcbcb] != 0),
                        $"Menu locks=${locks:x2} value=${value:x2} keys=${keys:x2}: native eligibility differs.");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds),
                        $"Menu locks=${locks:x2} value=${value:x2} keys=${keys:x2}: IntroDone/error/open/close sound priority differs.");
                });
            }
            SetLocks(locks);
            Step(4, keys, keys);
            FailIf(_menuLifecycle.IsActive || sounds.RequestsFor(0x5a) != (introDone ? 0 : 1),
                "The two menu locks must reject opening after the native IntroDone cue gate.");
            if (locks == 3)
            {
                SetLocks(2);
                Step(1, keys, keys);
                FailIf(_menuLifecycle.IsActive, "Clearing only wMenuDisabled released the separate collision/menu lock.");
            }
            SetLocks(0);
            Step(3, held: keys); // Releasing a lock cannot invent another edge.
            FailIf(_menuLifecycle.IsActive, "Menu opened from an old held edge after lock release.");
            _saveData.SetGlobalFlag(GlobalFlag.IntroDone);
            rom[0xc6d1] |= 1 << 2; // GLOBALFLAG_INTRO_DONE $0a.
            Step(1, keys, keys);
            FailIf(!_menuLifecycle.IsActive, "A fresh unlocked Start/Select edge did not open its native menu.");
            SetLocks(locks); // Active dispatch precedes every eligibility gate.
            Step(22);
            Step(1, keys == 8 ? 8 : 2, keys == 8 ? 8 : 2);
            Step(22);
            FailIf(_menuLifecycle.IsActive || _gameplayPause.IsLeased,
                "An active menu failed to close while both eligibility locks remained set.");
            SetLocks(0);
        }
        GD.Print("Validated clean-US independent menu-disable masks, IntroDone cue precedence, held-edge release and active-menu bypass with split/batched application updates.");
    }
}
