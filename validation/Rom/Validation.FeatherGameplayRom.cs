using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateFeatherGameplayRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (bool primary in new[] { false, true })
        foreach (int pauseAfter in new[] { 1, 9, 15, 24 })
        {
            FeatherRom rom = PrepareFeatherRom(primary);
            StepFeatherRom(rom, pauseAfter, batched, held: true, pressed: true, primary: primary);
            int beforeZ = rom.ZFixed;
            _dialogue.ShowMessage("Feather pause.", _player.Position.Y);
            rom[0xcba0] = 1;
            StepFeatherRom(rom, 7, batched, 8, held: true, pressed: true, primary: primary);
            FailIf(rom.ZFixed != beforeZ, "Dialogue must freeze the native Feather arc before item dispatch.");
            _dialogue.Close();
            rom[0xcba0] = 0;
            // Another real button edge while airborne cannot restart the arc.
            StepFeatherRom(rom, 31 - pauseAfter, batched, held: true, pressed: true, primary: primary);
            FailIf(rom.Airborne, "Resuming Feather after a modal pause must retain its original landing update.");
            StepFeatherRom(rom, 3, batched, held: true, primary: primary);
            StepFeatherRom(rom, 1, batched, primary: primary);
            StepFeatherRom(rom, 31, batched, held: true, pressed: true, primary: primary);
        }
        foreach (bool batched in new[] { false, true })
        foreach (bool primary in new[] { false, true })
        foreach (int cancelAfter in new[] { 1, 14, 30 })
        {
            FeatherRom rom = PrepareFeatherRom(primary);
            StepFeatherRom(rom, cancelAfter, batched, held: true, pressed: true, primary: primary);
            LoadValidationRoom(4, 0xa8);
            _entities.Clear();
            for (int y = 8; y < _currentRoom.Height; y += 16)
            for (int x = 8; x < _currentRoom.Width; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0xa0, 0, 0);
            _player.WarpTo(new(120, 88));
            _player.Face(Vector2I.Up);
            rom.PutLinkOnGround();
            rom.CopyRoom(_currentRoom);
            rom.Word(0xd00c, 120 * 256);
            rom.Word(0xd00a, 88 * 256);
            CompareFeatherRom(rom, "Feather room discard");
            StepFeatherRom(rom, 2, batched, primary: primary);
            StepFeatherRom(rom, 31, batched, held: true, pressed: true, primary: primary);
        }
        foreach (bool batched in new[] { false, true })
        foreach (bool primary in new[] { false, true })
        {
            FeatherRom rom = PrepareFeatherRom(primary);
            // Declared carry ownership isolates the Feather parent gate; the
            // Bracelet suite independently exercises reachable pickup/lifting.
            _player.BeginCarriedObjectPose();
            rom[0xcc5a] = 0x83;
            StepFeatherRom(rom, 1, batched, held: true, pressed: true, primary: primary);
            FailIf(rom.Airborne || _player.TopDownAirborne, "Feather must reject launch while holding an object.");
            _player.EndCarriedObjectPose();
            rom[0xcc5a] = 0;
            StepFeatherRom(rom, 2, batched, held: true, primary: primary);
            FailIf(rom.Airborne, "A rejected Feather press must not retry on held input after the gate clears.");
            StepFeatherRom(rom, 1, batched, primary: primary);
            StepFeatherRom(rom, 31, batched, held: true, pressed: true, primary: primary);
        }
        CompareFeatherHazardsRom();
        GD.Print("Validated executed-ROM Feather A/B, midair/rejected input edges, dialogue pause/resume, room cancellation, repeat launch and terrain handoff through individual/batched gameplay updates.");
    }
}
