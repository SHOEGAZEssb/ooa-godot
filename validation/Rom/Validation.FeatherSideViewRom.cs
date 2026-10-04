using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private FeatherRom PrepareFeatherSideViewRom(bool primary, float fraction = 0, bool ice = false)
    {
        ReinitializeGameplayForValidation();
        LoadValidationRoom(6, 0x97);
        _entities.Clear();
        _inventory.GiveTreasure(TreasureId.Feather, 1);
        _inventory.EquipA(primary ? TreasureId.Feather : 0);
        _inventory.EquipB(primary ? 0 : TreasureId.Feather);
        for (int y = 8; y < _currentRoom.Height; y += 16)
        for (int x = 8; x < _currentRoom.Width; x += 16)
            _currentRoom.SetPositionTileAndCollision(new(x, y), y >= 136 ? (byte)(ice ? 0x20 : 0x26) : (byte)0,
                y >= 136 ? (byte)0x0f : (byte)0, 0);
        _player.WarpTo(new(104 + fraction, 121 + fraction));
        FeatherRom rom = new(_currentRoom, _player.PrecisePosition, primary);
        StepFeatherRom(rom, 16, batched: false, angle: 8, primary: primary);
        FailIf(_player.SideScrollAirborne || _currentRoom.IsSolid(_player.Position),
            "Feather sideview fixture must approach along reachable floor geometry.");
        return rom;
    }

    private void ValidateFeatherSideViewRom()
    {
        int hostCase1 = 0;
        foreach (bool primary in new[] { false, true })
        foreach (float fraction in new[] { 0f, 255 / 256f })
        foreach (int turn in new[] { 8, 24, 0xff })
        foreach (bool ice in new[] { false, true })
        foreach (bool ceiling in new[] { false, true })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            FeatherRom rom = PrepareFeatherSideViewRom(primary, fraction, ice);
            if (ceiling)
            {
                for (int x = 8; x < _currentRoom.Width; x += 16)
                    _currentRoom.SetPositionTileAndCollision(new(x, 104), 0x26, 0x0f, 0);
                rom.CopyRoom(_currentRoom);
            }
            var sounds = _sound.AttachPlayRequestAudit();
            StepFeatherRom(rom, 1, batched, 8, held: true, pressed: true, primary: primary);
            FailIf(!rom.Airborne || rom.SpeedZ != -0x20c,
                "Sideview Feather must integrate launch -$0230 then apply gravity $24.");
            StepFeatherRom(rom, 15, batched, turn, held: true, primary: primary);
            FailIf(rom.SpeedZ != 0x10 || !rom.Airborne,
                "Sideview Feather's sixteenth update must cross the speedZ sign boundary.");
            FailIf(ceiling && (rom[0xd033] & 0xc0) == 0,
                "Sideview ceiling fixture must reach the native upward wall probes before the apex.");
            int pausedY = rom.Word(0xd00a);
            _dialogue.ShowMessage("Sideview Feather pause.", _player.Position.Y);
            rom[0xcba0] = 1;
            StepFeatherRom(rom, 7, batched, turn, held: true, pressed: true, primary: primary);
            FailIf(rom.Word(0xd00a) != pausedY, "Dialogue must freeze a sideview Feather arc.");
            _dialogue.Close();
            rom[0xcba0] = 0;
            StepFeatherRom(rom, 24, batched, turn, held: true, primary: primary);
            FailIf(rom.Airborne, "Sideview Feather must finish on its actual floor.");
            FailIf(!sounds.Requests.Where(id => id is SoundId.SndJump or SoundId.SndLand).SequenceEqual(rom.Sounds),
                "Sideview Feather jump/land requests must match native order.");
            StepFeatherRom(rom, 1, batched, primary: primary);
            StepFeatherRom(rom, 40, batched, held: true, pressed: true, primary: primary);
        }
        GD.Print("Validated executed-ROM sideview Feather launch, gravity, steering/release, ceiling collision, normal/ice floor landing, modal pause and repeated A/B use in split/batched gameplay updates.");
    }
}
