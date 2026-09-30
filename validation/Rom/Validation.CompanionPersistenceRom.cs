using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCompanionPersistenceRom()
    {
        foreach (bool batched in new[] { false, true })
        foreach (int id in new[] { 0x0b, 0x0c, 0x0d })
        for (int direction = 0; direction < 4; direction++)
        {
            var (actor, rom) = PrepareMountedCompanionRom(id, direction);
            StepCompanionRom(actor, rom, 1, Vector2.Zero, batched);
            int update = 0;
            StepGameplayUpdates(40, Vector2.Zero, ["item"], ["item"], batched, () =>
            {
                rom.Update(0xff, update++ == 0 ? (byte)2 : (byte)0, 2);
                for (int address = 0xcc24; address <= 0xcc28; address++)
                    FailIf(_runtimeState.ReadWramByte(address) != rom[address],
                        $"Companion ${id:x2} dismount update {update}, direction={direction}: remembered byte ${address:x4} differs.");
            });
            var remembered = CompanionRuntimeState.ReadRemembered(_runtimeState);
            FailIf(remembered.Id != id, "Dismount persistence fixture did not record its companion.");
            var checkpoint = new LinkCollisionRom();
            checkpoint[0xcc2d] = 0; checkpoint[0xcc30] = 0x2a; checkpoint[0xcc2c] = 0xd0;
            checkpoint[0xd008] = (byte)direction;
            checkpoint[0xd00b] = (byte)_player.Position.Y; checkpoint[0xd00d] = (byte)_player.Position.X;
            for (int address = 0xcc24; address <= 0xcc28; address++) checkpoint[address] = rom[address];
            checkpoint.Call(0x1100); // bank0:setDeathRespawnPoint
            _saveData.SetDeathRespawnPoint(0, 0x2a, 0, direction, (int)_player.Position.Y, (int)_player.Position.X, remembered, 0xd0);
            foreach (int address in new[] { 0xc631, 0xc632, 0xc633, 0xc634, 0xc636, 0xc637 })
                FailIf(_saveData.ReadWramByte(address) != checkpoint[address], $"Companion death checkpoint byte ${address:x4} differs.");

            LoadValidationRoom(0, 0x2b);
            FailIf(_entities.EntityAdapters<IRoomEntity>().Any(e => e is IRoomInitializedCompanion), "A remembered companion followed Link into a different room.");
            FailIf(CompanionRuntimeState.ReadRemembered(_runtimeState) != remembered, "Leaving the room changed remembered coordinates.");
            // Recreate live remembered state from the checkpoint, then use
            // the native room initializer to establish expected re-entry.
            CompanionRuntimeState.ForgetRemembered(_runtimeState);
            CompanionRuntimeState.RestoreRememberedFromDeathRespawn(_runtimeState, _saveData);
            FailIf(CompanionRuntimeState.ReadRemembered(_runtimeState) != remembered, "Checkpoint restore lost remembered companion state.");
            var spawn = new CompanionSpawnRom(_world.LoadRoom(0, 0x2a), 0, new(8, 8));
            for (int address = 0xcc24; address <= 0xcc28; address++) spawn.Memory[address] = checkpoint[address switch
                { 0xcc24 => 0xc631, 0xcc25 => 0xc632, 0xcc26 => 0xc633, 0xcc27 => 0xc636, _ => 0xc637 }];
            spawn.Remembered();
            LoadValidationRoom(0, 0x2a);
            _player.WarpTo(new(8, 8));
            StepGameplayUpdates(2, Vector2.Zero, batched: batched);
            var restored = _entities.EntityAdapters<IRoomEntity>().Where(e => e is IRoomInitializedCompanion).ToArray();
            FailIf(restored.Length != spawn.Memory[0xd100] || restored.Length != 1 ||
                restored[0].Node.Position != new Vector2(spawn.Memory[0xd10d], spawn.Memory[0xd10b]),
                $"Companion ${id:x2} restored a different room position from native loadRememberedCompanion.");
            // A second entry must still yield exactly one owner.
            LoadValidationRoom(0, 0x2b); LoadValidationRoom(0, 0x2a);
            FailIf(_entities.EntityAdapters<IRoomEntity>().Count(e => e is IRoomInitializedCompanion) != 1,
                "Repeated companion re-entry duplicated or lost the slot.");
        }
    }

    private void ValidateCompanionRememberedEligibilityRom()
    {
        ReinitializeGameplayForValidation(); PrepareCompanionFidelityRoom();
        foreach (int id in new[] { 0, 0x0b, 0x0c, 0x0d })
        foreach (int group in new[] { 0, 1 })
        foreach (int room in new[] { 0x2a, 0x2b })
        foreach (int occupied in new[] { 0, 0x0b, 0x0c, 0x0d })
        {
            foreach (int animal in new[] { 0x0b, 0x0c, 0x0d }) CompanionRuntimeState.Clear(_runtimeState, animal);
            CompanionRuntimeState.Remember(_runtimeState, id, group, room, new(72, 64));
            if (occupied != 0) CompanionRuntimeState.Begin(_runtimeState, occupied, 0x2a, new(40, 40), 1);
            var native = new CompanionSpawnRom(_currentRoom, 0, new(8, 8));
            for (int address = 0xcc24; address <= 0xcc28; address++) native.Memory[address] = _runtimeState.ReadWramByte(address);
            native.Memory[0xd100] = occupied == 0 ? (byte)0 : (byte)1; native.Memory[0xd101] = (byte)occupied;
            native.Memory[0xd10b] = native.Memory[0xd10d] = 40;
            native.Remembered();
            _entities.LoadRoom(0, _currentRoom);
            var actors = _entities.EntityAdapters<IRoomEntity>().Where(e => e is IRoomInitializedCompanion).ToArray();
            FailIf(actors.Length != native.Memory[0xd100], $"Remembered ${id:x2} at {group:x}:{room:x2}, slot=${occupied:x2}: allocation differs.");
            if (actors.Length != 0)
                FailIf(actors[0].Node.Position != new Vector2(native.Memory[0xd10d], native.Memory[0xd10b]), "Remembered slot overwrote an existing companion's position.");
            for (int address = 0xcc24; address <= 0xcc28; address++)
                FailIf(_runtimeState.ReadWramByte(address) != native.Memory[address], "Room-entry eligibility changed remembered bytes.");
        }
    }
}
