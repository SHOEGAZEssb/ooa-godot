using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareCircularPlatformAllocationRom()
    {
        // Execute the original 6:$2b parser with a retained outgoing prefix.
        // The preceding INTERAC$20:$03 owns a real slot before all three $a4s.
        foreach (int free in new[] { 0, 1, 2, 3, 4 })
        {
            var rom = PrepareSideViewRom(6, 0x2b, new(24.25f, 8.5f));
            // Completed-room entry excludes Head Thwomp's enemy dispatch;
            // the uncollected reward script and all three platforms still parse.
            _saveData.SetRoomFlag(4, 0x2b, OracleSaveData.RoomFlag80);
            for (int address = 0xc5b0; address < 0xcb00; address++)
                rom[address] = _saveData.ReadWramByte(address);
            var seed = _random.CaptureState();
            for (int slot = 2; slot < 16 - free; slot++)
            {
                _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(224, 144), SoundId.MusNone));
                int address = (0xd0 + slot) * 256 + 0x40;
                rom[address] = 2; rom[address + 1] = 5; rom[address + 2] = 0x80;
                rom[address + 0xb] = 144; rom[address + 0xd] = 224;
            }
            var frontend = SomariaPrivate<FrontendRom>(rom, "_rom");
            rom[0xcc05] = 0xff; rom[0xcd00] = 4;
            frontend.ApplyRoomTileSubstitutions();
            rom[0xcd00] = 8;
            frontend.Call(0x55b7, 0x12);
            _entities.BeginScreenTransition(6, _currentRoom, Vector2.Zero, _player);
            int[] native = Enumerable.Range(0xd2, 14).Select(page => page * 256 + 0x40)
                .Where(address => rom[address] == 1 && rom[address + 1] is 0x20 or 0xa4).ToArray();
            var source = _entities.Entities<HeadThwompRewardScript>().Cast<IRoomEntity>()
                .Concat(_entities.Entities<CircularSideScrollPlatformRoomEntity>())
                .OrderBy(actor => _entities.InteractionSlot(actor.Node)).ToArray();
            FailIf(native.Length != Math.Min(free, 4) || source.Length != native.Length,
                $"6:$2b original INTERAC$20/$a4 parser free={free}: native count={native.Length}, runtime count={source.Length}.");
            for (int index = 0; index < native.Length; index++)
            {
                int address = native[index];
                FailIf(_entities.InteractionSlot(source[index].Node) != (address >> 8) - 0xd0 ||
                    rom[address + 1] != (index == 0 ? 0x20 : 0xa4) ||
                    rom[address + 2] != (index == 0 ? 3 : index - 1),
                    $"6:$2b free={free}: reward script must consume the first available slot before the source-ordered circular platforms.");
                if (source[index] is CircularSideScrollPlatformRoomEntity platform)
                    FailIf(platform.UpdatesDuringDialogue || platform.Counter != 7 ||
                        platform.PrecisePosition != new Vector2[] { new(120, 33), new(173, 86), new(120, 139) }[index - 1],
                        "Incoming INTERAC$a4 must complete state0 preload exactly once without moving.");
            }
            var parsed = _random.CaptureState();
            FailIf(parsed.Rng1 != rom[0xff94] || parsed.Rng2 != rom[0xff95] ||
                parsed.Calls - seed.Calls != rom.RandomCalls,
                "6:$2b failed interaction allocations must preserve original full room-parse RNG consumption.");
            _entities.FinishScreenTransition();
            FailIf(_entities.Entities<CircularSideScrollPlatformRoomEntity>().Count != Math.Max(0, free - 1),
                "Releasing outgoing capacity must not recreate skipped circular-platform placements.");
        }
    }
}
