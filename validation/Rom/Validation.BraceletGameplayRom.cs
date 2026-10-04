using Godot;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void RepeatBraceletPickupRom(BraceletRom rom, bool batched, bool primary)
    {
        // Reinstall the pot after debris has retired, then approach it again.
        // The player never starts inside the solid tile or carried-object box.
        Vector2 center = new(120, 88);
        _currentRoom.SetPositionTileAndCollision(center, 0x10, 0x0f, 0);
        _currentRoom.SetUnderlyingMetatile(center, 0xa0);
        rom.CopyRoom(_currentRoom);
        _player.WarpTo(new(120, 114));
        _player.Face(Vector2I.Up);
        rom.Word(0xd00c, 120 * 256);
        rom.Word(0xd00a, 114 * 256);
        rom[0xd008] = 0;
        StepGameplayUpdates(24, Vector2.Up, batched: batched, afterUpdate: () => rom.Walk(0));
        rom.Probe();
        FailIf(_player.Position != new Vector2(120, 98) || rom[0xd00b] != 98,
            $"Bracelet repeat pickup must approach through the pot's collision geometry: runtime={_player.Position}, ROM={rom[0xd00d]},{rom[0xd00b]}.");
        StepBraceletRom(rom, 1, batched, held: true, pressed: true, primary: primary);
        StepBraceletRom(rom, 11, batched, 16, held: true, primary: primary);
        StepBraceletRom(rom, 13, batched, primary: primary);
        FailIf(!_bracelet.HoldingTile || !rom.ChildActive, "Bracelet must support another pickup after completion/cancellation.");
    }

    private void CompareBraceletRoomCancellationRom()
    {
        int hostCase2 = 0;
        foreach (bool primary in new[] { false, true })
        foreach (int cancelAfter in new[] { 1, 7, 12, 20 })
        foreach (bool finishLift in new[] { false, true })
        foreach (bool batched in RomHostSchedules(hostCase2++))
        {
            BraceletRom rom = PrepareBraceletRom(primary: primary);
            StepBraceletRom(rom, 1, batched, held: true, pressed: true, primary: primary);
            StepBraceletRom(rom, 11, batched, 16, held: true, primary: primary);
            StepBraceletRom(rom, cancelAfter, batched, primary: primary);
            _dialogue.ShowMessage("Lift pause.", _player.Position.Y);
            rom[0xcba0] = 1;
            StepBraceletRom(rom, 7, batched, primary: primary);
            _dialogue.Close();
            rom[0xcba0] = 0;
            if (finishLift) StepBraceletRom(rom, 20, batched, primary: primary);
            // The actual room replacement requests discard from the owner.
            LoadValidationRoom(4, 0xa8);
            _entities.Clear();
            rom.Call(0, 0x2c43); // dropLinkHeldItem
            rom.Call(6, 0x4878); // clearAllParentItems_body
            rom.Call(0, 0x35e3); // clearItems
            rom.CopyRoom(_currentRoom);
            CompareBraceletRom(rom, "Bracelet room replacement");
            for (int y = 24; y <= 152; y += 16)
            for (int x = 56; x <= 184; x += 16)
                _currentRoom.SetPositionTileAndCollision(new(x, y), 0xa0, 0, 0);
            RepeatBraceletPickupRom(rom, batched, primary);
        }
        GD.Print("Validated executed-ROM Bracelet pause at each lift stage, room cancellation and collision-reachable repeat pickup through A/B and individual/batched gameplay updates.");
    }

    private void CompareBraceletWallImpactRom()
    {
        int hostCase1 = 0;
        foreach (int direction in new[] { 0, 1, 2, 3 })
        foreach (byte tile in new byte[] { 0xff, 0xb0, 0x90 })
        foreach (bool batched in RomHostSchedules(hostCase1++))
        {
            BraceletRom rom = PrepareBraceletRom(direction);
            StepBraceletRom(rom, 1, batched, held: true, pressed: true);
            StepBraceletRom(rom, 11, batched, (direction * 8 + 16) & 0x1f, held: true);
            StepBraceletRom(rom, 20, batched);
            Vector2 facing = OracleObjectMath.StrictCardinalVector(direction * 8);
            Vector2 obstacle = _player.Position + facing * 32;
            _currentRoom.SetPositionTileAndCollision(obstacle, tile, 0x0f, 0);
            rom.CopyRoom(_currentRoom);
            StepBraceletRom(rom, 1, batched, direction * 8, held: true, pressed: true);
            StepBraceletRom(rom, 40, batched);
            FailIf(rom.ChildActive || _bracelet.LiftedObject is not null,
                "A breakable Bracelet tile must retire on native wall impact or ground contact, without bouncing.");
        }
        GD.Print("Validated executed-ROM Bracelet wall/fence/cliff passage, impact deletion and debris position across four directions through individual/batched gameplay updates.");
    }
}
