using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateNativeForcedMovementAdmissionRom()
    {
        var gates = new (string Field, int Address, byte Value, bool BlocksRequest)[]
        {
            ("wDisabledObjects", 0xcc8a, 1, false),
            ("wDisabledObjects", 0xcc8a, 2, false),
            ("wDisabledObjects", 0xcc8a, 0x20, false),
            ("wDisabledObjects", 0xcc8a, 0x80, false),
            ("wDisabledObjects", 0xcc8a, 0xff, false),
            ("wScrollMode", 0xcd00, 1, false),
            ("wScrollMode", 0xcd00, 2, true),
            ("wScrollMode", 0xcd00, 8, true),
            ("wScrollMode", 0xcd00, 0x80, false),
            ("wPaletteThread_mode", 0xc4ab, 1, true),
            ("wPaletteThread_mode", 0xc4ab, 0xff, true),
            ("wTextIsActive", 0xcba0, 1, false),
            ("wTextIsActive", 0xcba0, 0x80, false),
            ("wTextIsActive", 0xcba0, 0xff, false)
        };
        foreach (var gate in gates)
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(4, 0xfc, 0x80);
            LoadValidationRoom(4, 0xfc); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(120.25f, 88.5f)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position), "Link state$0b requires room$4:fc's actual central floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, 120, 88)
                { CompanionDispatchEnabled = true };
            rom.InitializeLinkGameplay();
            rom[0xd00c] = 0x40; rom[0xd00a] = 0x80;
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0;
            void SetGate(bool enabled)
            {
                byte value = enabled ? gate.Value : (byte)0;
                _runtimeState.SetField(gate.Field, value); rom[gate.Address] = value;
            }
            void Step(int count, bool inputDown = false)
            {
                StepGameplayUpdates(count, inputDown ? Vector2.Down : Vector2.Zero, batched: batched, afterUpdate: () =>
                {
                    rom.UpdateGameplay(0, inputDown ? 0x80 : 0, inputDown ? 0x10 : 0xff, ++update);
                    string context = $"Link state$0b {gate.Field}=${gate.Value:x2} update{update} batch={batched}";
                    FailIf(_player.PrecisePosition != new Vector2(rom.Word(0xd00c) / 256f, rom.Word(0xd00a) / 256f) ||
                        CarriedObjectMotion.DirectionIndex(_player.FacingVector) != rom[0xd008] ||
                        _player.NativeForcedMovementActive != (rom[0xd004] == 0x0b) ||
                        _runtimeState.ReadField("wLinkForceState") != rom[0xcc4f],
                        $"{context}: request/selected state, facing or fixed position differs (XY={_player.PrecisePosition}/{rom.Word(0xd00c) / 256f},{rom.Word(0xd00a) / 256f}, native=${rom[0xd004]:x2}:${rom[0xd005]:x2}).");
                    if (rom[0xd004] == 0x0b && rom[0xd005] == 1)
                        FailIf(_runtimeState.ReadWramByte(0xd006) != rom[0xd006],
                            $"{context}: selected state$0b countdown differs.");
                    FailIf(!sounds.Requests.SequenceEqual(rom.Sounds) || RandomDiffers(seed, rom[0xff94], rom[0xff95], rom.RandomCalls),
                        $"{context}: sounds/shared RNG differ.");
                });
            }
            for (int repeat = 0; repeat < 2; repeat++)
            {
                _player.RequestNativeForcedMovement(14, 0, 0);
                rom[0xd008] = 0; rom[0xd009] = 0; rom[0xcc4f] = 0x0b; rom[0xcc51] = 14;
                Vector2 before = _player.PrecisePosition;
                SetGate(true); Step(3, inputDown: true);
                FailIf(gate.BlocksRequest && (_player.PrecisePosition != before || rom[0xcc4f] != 0x0b),
                    "Palette/scroll must leave the force-state request pending without normal input movement.");
                SetGate(false); Step(1);
                SetGate(true); Step(4, inputDown: true);
                SetGate(false); Step(14);
                FailIf(_player.NativeForcedMovementActive || rom[0xd004] != 1,
                    "Selected Link state$0b must finish and permit another request after every source gate.");
            }
        }
        GD.Print("Validated native shared Link state$0b pending/selected admission, exact countdown and repeated requests across14 source gates and both host schedules.");
    }
}
