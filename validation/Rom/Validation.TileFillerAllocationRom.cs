using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareTileFillerAllocationRom()
    {
        foreach (int room in new[] { 0x6f, 0x87 })
        foreach (int free in new[] { 0, 1, 2 })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, room); _entities.Clear();
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, 24, 24);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            // Retained initialized ENEMY capacity isolates the interaction
            // parser. Native enemy state0 timing has separate comparisons.
            SomariaPrivate<HashSet<int>>(_entities, "_reservedEnemySlots").UnionWith(Enumerable.Range(0,16));
            for (int slot = 0; slot < 16; slot++)
            {
                int address = (0xd0+slot)*256+0x80;
                rom[address] = 2; rom[address+1] = 8; rom[address+4] = 8;
            }
            for (int slot = 2; slot < 16-free; slot++)
            {
                _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(224,144), SoundId.MusNone, AlwaysUpdates:false));
                int address = (0xd0+slot)*256+0x40;
                rom[address] = 2; rom[address+1] = 5; rom[address+4] = 1; rom[address+6] = 60;
                rom[address+0xb] = 144; rom[address+0xd] = 224;
            }
            // Execute the original room parser with retained outgoing capacity.
            // Enemy execution and room-edge scrolling are excluded here.
            var frontend = SomariaPrivate<FrontendRom>(rom, "_rom");
            rom[0xcc05] = 0xff; rom[0xcd00] = 4; frontend.ApplyRoomTileSubstitutions();
            rom[0xcd00] = 8; frontend.Call(0x55b7, 0x12);
            _entities.BeginScreenTransition(4, _currentRoom, Vector2.Zero, _player);
            rom.AdvanceInteractions(_entities.FrameCounter);
            var fillers = _entities.Entities<TileFillerRoomEntity>();
            var chests = _entities.Entities<DungeonPuzzleChestRoomEntity>();
            int[] native = Enumerable.Range(0xd2,14).Select(page => page*256+0x40)
                .Where(address => rom[address] != 0 && (rom[address+1] == 0x25 || rom[address+1] == 0x21 && rom[address+2] == 0x11)).ToArray();
            FailIf(fillers.Count != (free > 0 ? 1 : 0) || chests.Count != (free > 1 ? 1 : 0) ||
                fillers.Count+chests.Count != native.Length,
                $"Original tile-filler parser$4:${room:x2}, free={free}: filler/chest count={fillers.Count}/{chests.Count}, native={native.Length}.");
            if (free > 0)
            {
                int start = room == 0x6f ? 0x5d : 0x92;
                FailIf(_entities.InteractionSlot(fillers.Single()) != (native[0]>>8)-0xd0 ||
                    rom[native[0]+1] != 0x25 || rom[native[0]+4] != 0 || !fillers.Single().UpdatesDuringDialogue ||
                    _currentRoom.Layout[start] != 0x9f || rom[0xcf00+start] != 0x9f,
                    "Scroll-mode$08 must retain the allocated filler pending without writing yellow floor or creating a puff.");
            }
            if (free > 1)
                FailIf(_entities.InteractionSlot(chests.Single()) != (native[1]>>8)-0xd0 ||
                    rom[native[1]+1] != 0x21 || rom[native[1]+2] != 0x11 || chests.Single().Finished,
                    "The original no-blue chest must allocate after the filler and remain unsolved during preload.");
            var random = _random.CaptureState();
            FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls || rom.Sounds.Any(),
                $"Tile-filler parser$4:${room:x2}, free={free}: RNG=${random.Rng1:x2}:${random.Rng2:x2}/${rom[0xff94]:x2}:${rom[0xff95]:x2}, calls={random.Calls-seed.Calls}/{rom.RandomCalls}, cues=[{string.Join(',',rom.Sounds.Select(cue=>$"${cue:x2}"))}].");
            _entities.FinishScreenTransition();
            FailIf(_entities.Entities<TileFillerRoomEntity>().Count != fillers.Count ||
                _entities.Entities<DungeonPuzzleChestRoomEntity>().Count != chests.Count,
                "Finishing preload must not recreate object rows skipped by a full pool.");
        }
    }
}
