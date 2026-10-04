using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidatePauseMenuGatesRom()
    {
        int hostCase1 = 0;
        foreach (int pressed in new[] { 4, 8, 12 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            var application = new ApplicationValidationFixture(this);
            application.ResetGameplay();
            LoadValidationRoom(0, 0x45);
            var rom = new MenuRom(_saveData, _currentRoom);
            var sounds = _sound.AttachPlayRequestAudit();
            void Step(int count = 1)
            {
                int edge = pressed;
                application.Step(count, Vector2.Zero, MenuRomActions(pressed), MenuRomActions(pressed), batched, () =>
                {
                    rom.Update(edge, pressed, _saveData.ReadWramByte(0xc622));
                    edge = 0;
                    FailIf(_menuLifecycle.IsActive != (rom[0xcbcb] != 0),
                        "Start/Select gate disagrees with native menu dispatch.");
                    FailIf(!sounds.Requests.Where(sound => sound == 0x5a).SequenceEqual(rom.Sounds),
                        "Start/Select/chord changed IntroDone SND_ERROR $5a count or priority.");
                });
            }
            Step(4);
            FailIf(sounds.RequestsFor(0x5a) != 1, "IntroDone gate must request one SND_ERROR $5a.");
            _dialogue.ShowMessage("Blocked", 0);
            rom[0xcba0] = 1;
            Step();
            _dialogue.Close();
            rom[0xcba0] = 0;
            Step();
            FailIf(sounds.RequestsFor(0x5a) != 2, "Closing text did not restore the original menu gate.");
        }
        GD.Print("Validated clean-US IntroDone Start/Select/chord error count, active-text precedence and post-text gate restoration through split/batched application updates.");
    }
}
