using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareFloorColorWorkersRom()
    {
        int fixture = 0;
        foreach (int mode in new[] { 0,1,2,3,4 }) // Ordinary, text, interaction mask, graphics queue, full pool.
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x71); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(200,120)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position),"Floor worker Link requires original room$04:$71 geometry.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,200,120);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            var data = new DungeonInteractionDatabase();
            var record = new SkullDungeonDatabase().GetRoomRecords(4,0x71).Single(row => row.Id == 0x22);
            var sounds = _sound.AttachPlayRequestAudit();
            var disabled = _entities.InitializedObjectsDisabledSource;
            if (mode == 4)
                for (int index = 0; index < 13; index++)
                {
                    _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(224,144),SoundId.MusNone));
                    int slot = (0xd2+index)*256+0x40;
                    rom[slot] = 1; rom[slot+1] = 5; rom[slot+2] = 0x80; rom[slot+0xb] = 144; rom[slot+0xd] = 224;
                }
            FloorColorChangerRoomEntity? parent = null;
            int parentAddress = 0;
            if (mode is 0 or 3 or 4)
            {
                parent = new(record,() => _entities.ActiveRoom,data,_entities.TryCreateFloorColorWorker,
                    () => _entities.Entities<FloorColorWorkerRoomEntity>().Count);
                _entities.AddEntity(parent);
                parentAddress = (0xd0+_entities.InteractionSlot(parent))*256+0x40;
                rom[parentAddress] = 1; rom[parentAddress+1] = 0x22;
                rom[parentAddress+0xb] = 0x58; rom[parentAddress+0xd] = 0x78;
            }
            int update = 0;
            void Step(int count = 1) => StepSomariaMotionRom(rom,count,batched,afterUpdate:() =>
            {
                rom.AdvanceTileGraphics(); update++;
                int[] native = Enumerable.Range(0xd2,14).Select(page => page*256+0x40)
                    .Where(slot => rom[slot] != 0 && rom[slot+1] == 0x22 && rom[slot+2] == 1).ToArray();
                var workers = _entities.Entities<FloorColorWorkerRoomEntity>();
                FailIf(workers.Count != native.Length || parent is not null &&
                    SomariaPrivate<int>(parent,"_lastControlTile") != rom[parentAddress+3],
                    $"Floor$22 update{update}, mode={mode}: physical worker count or parent color latch differs.");
                foreach (var worker in workers)
                {
                    int slot = (0xd0+_entities.InteractionSlot(worker))*256+0x40;
                    FailIf(!native.Contains(slot) || (worker.UpdatesDuringDialogue ? 0 : 1) != rom[slot+4] ||
                        !worker.UpdatesDuringDialogue && worker.Index != rom[slot+6],
                        $"Floor worker update{update}: slot/state/counter differs at ${slot>>8:x2}.");
                }
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls ||
                    !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds) ||
                    _rooms.PendingTileGraphics != ((rom[0xcce0]-rom[0xccdf])&31),
                    $"Floor$22 update{update}, mode={mode}: RNG, queue or cues differ.");
                if (rom.RandomCalls > 0)
                    for (int index = 0; index < 256; index++)
                        FailIf(random.PlacementBuffer[index] != rom.FloorPermutation(index) ||
                            _runtimeState.ReadWramByte(WramAddress.wBigBuffer+index) != rom[0xc300+index],
                            $"Floor worker shared buffer differs at ${index:x2}.");
                // Right-column padding is a real underlying byte, excluded
                // from the ordinary playable-grid comparison.
                for (int row = 1; row < 10; row++)
                    FailIf(_currentRoom.GetUnderlyingStorageMetatile(row*16+15) != rom.Underlying(row*16+15),
                        $"Floor worker padding differs on row ${row:x2}.");
            });
            void Tile(int tile)
            {
                _currentRoom.SetPositionTileAndCollision(new(120,88),(byte)tile,null,0); rom.CopyRoom(_currentRoom);
            }
            void AddWorker(int target)
            {
                FailIf(!_entities.TryCreateFloorColorWorker(new(120,88),(byte)target),"Declared floor worker requires an available physical slot.");
                var worker = _entities.Entities<FloorColorWorkerRoomEntity>().Single();
                int slot = (0xd0+_entities.InteractionSlot(worker))*256+0x40;
                rom[slot] = 1; rom[slot+1] = 0x22; rom[slot+2] = 1; rom[slot+3] = (byte)target;
                rom[slot+0xb] = 0x58; rom[slot+0xd] = 0x78;
            }
            try
            {
                Step(); Tile(0xae);
                if (mode == 1) { _dialogue.ShowMessage("Declared floor worker.",100); rom[0xcba0] = 1; AddWorker(0x9e); }
                if (mode == 2) { _entities.InitializedObjectsDisabledSource = () => true; rom[0xcc8a] = 2; AddWorker(0x9e); }
                if (mode == 3)
                    for (int index = 0; index < 31; index++)
                    { FailIf(!_rooms.TrySetTile(0x11,0x9d),"Floor-worker fixture requires31 queued writes."); rom.SetTile(0x11,0x9d); }
                Step();
                if (mode == 4)
                {
                    FailIf(parent!.WorkerCount != 0 || rom.RandomCalls != 0,"Full pool must latch the changed color without a worker or RNG.");
                    Step(35);
                    FailIf(parent.WorkerCount != 0 || rom.RandomCalls != 0,"Freed lower slots must not retry an unchanged control color.");
                    Tile(0xaf); Step();
                    FailIf(parent.WorkerCount != 1 || !_entities.Entities<FloorColorWorkerRoomEntity>().Single().UpdatesDuringDialogue || rom.RandomCalls != 0,
                        "A new color must allocate into an already-visited lower slot and defer initialization.");
                    Step();
                }
                FailIf(rom.RandomCalls != 256,"One physical worker must generate exactly256 random values without resetting the placement cursor.");
                if (mode is 1 or 2)
                {
                    int remaining = _entities.Entities<FloorColorWorkerRoomEntity>().Single().Index;
                    Step(3);
                    FailIf(_entities.Entities<FloorColorWorkerRoomEntity>().Single().Index != remaining,
                        "State0 must initialize under text/mask; initialized state1 must freeze.");
                    _dialogue.Close(); rom[0xcba0] = 0; _entities.InitializedObjectsDisabledSource = disabled; rom[0xcc8a] = 0;
                }
                Step(62);
                FailIf(_entities.Entities<FloorColorWorkerRoomEntity>().Count != 1,"Worker must survive through its63rd dispatch.");
                Step();
                FailIf(_entities.Entities<FloorColorWorkerRoomEntity>().Count != 0 || rom.RandomCalls != 256,
                    "Update64 must consume all256 entries and delete without additional RNG.");

                // A worker reads current wBigBuffer, and keeps running while
                // Somaria replaces its control tile. These literal entries
                // include every range/edge boundary and the accepted padding.
                Tile(mode == 4 ? 0xaf : 0xae); AddWorker(0x9e); Step();
                byte[] probes = [0x00,0x01,0x10,0x11,0x1f,0x57,0x9e,0x9f,0xa1,0xff];
                for (int index = 0; index < 256; index++)
                { byte value = probes[index%probes.Length]; _runtimeState.SetWramByte(WramAddress.wBigBuffer+index,value); rom[0xc300+index] = value; }
                Tile(0xda); Step(3);
                FailIf(_entities.Entities<FloorColorWorkerRoomEntity>().Count != 1,"Somaria must preserve the physical worker.");
                Tile(0xad); Step();
                // The normal parent may create its new red worker before the
                // older yellow worker observes the changed tile and deletes.
                FailIf(_entities.Entities<FloorColorWorkerRoomEntity>().Any(worker => SomariaPrivate<byte>(worker,"_targetTile") == 0x9e),
                    "Changed control color must cancel the older worker in the same interaction pass.");
                Step(2);
            }
            finally { _dialogue.Close(); _entities.InitializedObjectsDisabledSource = disabled; }
        }
    }
}
