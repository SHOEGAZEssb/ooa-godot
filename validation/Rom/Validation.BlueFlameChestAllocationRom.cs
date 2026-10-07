using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareBlueFlameChestAllocationRom()
    {
        foreach (bool collected in new[] { true, false })
        foreach (int free in new[] { 3, 0, 1, 2, 4, 5, 6, 7 })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(4,0x90,OracleSaveData.RoomFlagItem,collected);
            LoadValidationRoom(4,0x90); _entities.Clear();
            // Actual scroll activation clears shared room variables before
            // parsing. The isolated manager call below does not own that clear.
            _rooms.SetLoadedRoom(4,_currentRoom);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,200,136);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            // Declare frozen outgoing ENEMY capacity; exclude enemy producers.
            SomariaPrivate<HashSet<int>>(_entities,"_reservedEnemySlots").UnionWith(Enumerable.Range(0,16));
            for (int slot = 0; slot < 16; slot++)
            {
                int address = (0xd0+slot)*256+0x80;
                rom[address] = 2; rom[address+1] = 8; rom[address+4] = 8;
            }
            for (int slot = 2; slot < 16-free; slot++)
            {
                _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(224,144),SoundId.MusNone,AlwaysUpdates:false));
                int address = (0xd0+slot)*256+0x40;
                rom[address] = 2; rom[address+1] = 5; rom[address+4] = 1; rom[address+6] = 60;
                rom[address+0xb] = 144; rom[address+0xd] = 224;
            }
            var frontend = SomariaPrivate<FrontendRom>(rom,"_rom");
            rom[0xcc05] = 0xff; rom[0xcd00] = 4; frontend.ApplyRoomTileSubstitutions();
            rom[0xcd00] = 8; frontend.Call(0x55b7,0x12);
            _entities.BeginScreenTransition(4,_currentRoom,Vector2.Zero,_player);
            rom.AdvanceInteractions(_entities.FrameCounter);
            int[] native = Enumerable.Range(0xd2,14).Select(page => page*256+0x40)
                .Where(address => rom[address] != 0 && rom[address+1] != 5).ToArray();
            var actors = SomariaPrivate<List<IRoomEntity>>(_entities,"_activeEntities")
                .Where(entity => entity is ColoredCubeRoomEntity or ColoredCubeSensorRoomEntity or DungeonPuzzleChestRoomEntity or ColoredCubeFlameRoomEntity)
                .OrderBy(entity => _entities.InteractionSlot(entity.Node)).ToArray();
            int expectedCount = free-(collected && free >= 3 ? 1 : 0);
            FailIf(actors.Length != native.Length || actors.Length != expectedCount,
                $"Original cube/chest/flame parser$4:$90, item={collected}, free={free}: actor count={actors.Length}/{native.Length}, expected={expectedCount}.");
            // Original mainData.s order: cube, sensor, chest, then four flames.
            (int Id,int Sub,int X,int Y)[] rows = [(0x19,0,0x68,0x38),(0x21,3,0x48,0x78),(0x21,0x12,0x48,0x88),
                (0x1a,0,0x38,0x5e),(0x1a,0,0x58,0x5e),(0x1a,0,0x38,0x7e),(0x1a,0,0x58,0x7e)];
            for (int index = 0; index < actors.Length; index++)
            {
                int address = native[index], original = (address>>8)-0xd0-(16-free);
                var row = rows[original];
                FailIf(_entities.InteractionSlot(actors[index].Node) != (address>>8)-0xd0 ||
                    rom[address+1] != row.Id || rom[address+2] != row.Sub || rom[address+0xd] != row.X || rom[address+0xb] != row.Y,
                    "Collected chest must allocate in source order before its first-pass deletion; subsequent flames retain their original physical slots.");
            }
            FailIf(_runtimeState.ReadWramByte(WramAddress.wRotatingCubeColor) != rom[0xccad] ||
                _runtimeState.ReadWramByte(WramAddress.wRotatingCubePos) != rom[0xccae],
                $"Cube allocation item={collected}, free={free}: shared color/position=${_runtimeState.ReadWramByte(WramAddress.wRotatingCubeColor):x2}/${_runtimeState.ReadWramByte(WramAddress.wRotatingCubePos):x2}, native=${rom[0xccad]:x2}/${rom[0xccae]:x2}.");
            var random = _random.CaptureState();
            FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls || rom.Sounds.Any(),
                "Cube/chest/flame allocation and pending scroll updates must preserve native parser RNG/cues.");
            _entities.FinishScreenTransition();
            FailIf(_entities.Entities<ColoredCubeFlameRoomEntity>().Count != (free > 3 ? free-3 : 0),
                "Deleting a collected chest must not retry flame rows skipped earlier by the parser.");
        }
    }
}
