using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareTileFillerRom()
    {
        int fixture = 0;
        foreach (int room in new[] { 0x87, 0x6f })
        foreach (int mode in new[] { 2, 0, 1 }) // Full graphics queue, ordinary, full interaction pool.
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, room); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Feather, 1);
            _inventory.EquipA(TreasureId.Feather); _inventory.EquipB(0);
            static Vector2 Point(int packed) => new((packed & 15)*16+8, (packed >> 4)*16+8);
            var records = new SkullDungeonDatabase().GetRoomRecords(4, room);
            var record = records.Single(row => row.Kind == DungeonObjectKind.TileFiller);
            var chestRecord = records.Single(row => row.Kind == DungeonObjectKind.FloorFillChest);
            int start = room == 0x87 ? 0x92 : 0x5d;
            _player.WarpTo(Point(start)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position), "Tile filler must begin on its original blue floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, (int)_player.Position.X, (int)_player.Position.Y);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            if (mode == 1)
                for (int slot = 2; slot < 14; slot++)
                {
                    // Declare frozen retained capacity; its execution is excluded.
                    _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(224,144), SoundId.MusNone, AlwaysUpdates:false));
                    int address = (0xd0+slot)*256+0x40;
                    rom[address] = 2; rom[address+1] = 5; rom[address+4] = 1; rom[address+6] = 60;
                    rom[address+0xb] = 144; rom[address+0xd] = 224;
                }
            var data = new DungeonInteractionDatabase();
            var filler = new TileFillerRoomEntity(record, _currentRoom, data,
                _entities.OnSoundRequested, _rooms.TrySetTile, _entities.TryCreatePuzzlePuff);
            var chest = new DungeonPuzzleChestRoomEntity(chestRecord, _currentRoom,
                () => !_currentRoom.Layout.Contains((byte)0x9f), () => _saveData.HasRoomFlag(_rooms.ActiveGroup, _rooms.CurrentRoom.Id, OracleSaveData.RoomFlagItem), 0xf1,
                _entities.OnSoundRequested, _entities.OnRoomTileChanged, () => 0,
                _entities.TryCreatePuzzlePuff, _entities.IsOutgoingEntity,
                () => _rooms.TrySetTile((byte)_currentRoom.GetPackedPosition(chestRecord.Position), 0xf1));
            _entities.AddEntity(filler); _entities.AddEntity(chest);
            int fillerSlot = (mode == 1 ? 0xde : 0xd2)*256+0x40, chestSlot = fillerSlot+256;
            FailIf(_entities.InteractionSlot(filler) != (fillerSlot>>8)-0xd0 ||
                _entities.InteractionSlot(chest) != (chestSlot>>8)-0xd0,
                "Original tile filler must consume its physical slot before the following chest controller.");
            rom[fillerSlot] = 1; rom[fillerSlot+1] = 0x25;
            rom[fillerSlot+0xb] = (byte)record.Y; rom[fillerSlot+0xd] = (byte)record.X;
            rom[chestSlot] = 1; rom[chestSlot+1] = 0x21; rom[chestSlot+2] = 0x11;
            rom[chestSlot+0xb] = (byte)chestRecord.Y; rom[chestSlot+0xd] = (byte)chestRecord.X;
            var sounds = _sound.AttachPlayRequestAudit();
            _dialogue.ShowMessage("Tile-filler dispatch pause.", 100); rom[0xcba0] = 1;
            if (mode == 2)
                for (int index = 0; index < 31; index++)
                {
                    FailIf(!_rooms.TrySetTile(0x11, 0x9f), "Tile-filler fixture requires31 accepted queue entries.");
                    rom.SetTile(0x11, 0x9f);
                }
            int update = 0;
            void Step(int count = 1, int angle = 0xff, int held = 0, int pressed = 0) => StepSomariaMotionRom(rom, count, batched, angle, held, pressed, afterUpdate:() =>
            {
                rom.AdvanceTileGraphics();
                string context = $"Tile filler$25 room$4:${room:x2}, mode={mode}, batch={batched}, update{++update}";
                int[] nativePuffs = Enumerable.Range(0xd2,14).Select(page => page*256+0x40)
                    .Where(address => rom[address] != 0 && rom[address+1] == 5).ToArray();
                var puffs = _entities.Entities<PuzzlePuffEffect>().OrderBy(_entities.InteractionSlot).ToArray();
                FailIf(filler.Endpoint != rom[fillerSlot+0x30] || filler.UpdatesDuringDialogue != (rom[fillerSlot+4] == 0) ||
                    Enumerable.Range(0, 0xb0).Any(index => _currentRoom.Layout[index] != rom[0xcf00+index]) ||
                    _rooms.PendingTileGraphics != ((rom[0xcce0]-rom[0xccdf])&31) ||
                    puffs.Length != nativePuffs.Length || chest.Finished != (rom[chestSlot] == 0 || rom[chestSlot+1] != 0x21) ||
                    !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                    context + $": endpoint=${filler.Endpoint:x2}/${rom[fillerSlot+0x30]:x2}, start=${_currentRoom.Layout[start]:x2}/${rom[0xcf00+start]:x2}, queue={_rooms.PendingTileGraphics}/{((rom[0xcce0]-rom[0xccdf])&31)}, puffs={puffs.Length}/{nativePuffs.Length}, chest={chest.Finished}/{rom[chestSlot]==0}, cues=[{string.Join(',',sounds.Requests.Select(cue=>$"${cue:x2}"))}]/[{string.Join(',',rom.Sounds.Select(cue=>$"${cue:x2}"))}].");
                for (int index = 0; index < puffs.Length; index++)
                    FailIf(_entities.InteractionSlot(puffs[index]) != (nativePuffs[index]>>8)-0xd0 ||
                        puffs[index].Position != new Vector2(rom[nativePuffs[index]+0xd],rom[nativePuffs[index]+0xb]),
                        context + ": physical puff allocation/position differs.");
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls,
                    context + ": shared RNG differs.");
            });
            Step(); Step(3);
            if (mode != 0)
            {
                Step(28);
                FailIf(mode == 2 && _currentRoom.Layout[start] != 0x9f,
                    "Rejected state0 yellow write must not retry after queue drain.");
                _dialogue.Close(); LoadValidationRoom(4, 0x91); continue;
            }
            _dialogue.Close(); rom[0xcba0] = 0;
            void Walk(int packed)
            {
                Vector2 target = Point(packed);
                for (int wait = 0; _player.Position.DistanceTo(target) > 0.8f && wait < 40; wait++)
                {
                    Vector2 delta = target-_player.Position;
                    Step(angle:Math.Abs(delta.X)>0.8f ? (delta.X>0 ? 8 : 24) : (delta.Y>0 ? 16 : 0));
                }
                FailIf(_player.Position.DistanceTo(target)>0.8f, $"Tile-filler bounded route could not reach ${packed:x2}.");
            }
            int adjacent = room == 0x87 ? 0x91 : 0x4d;
            Walk(adjacent);
            FailIf(filler.Endpoint != adjacent || _currentRoom.Layout[start] != 0x9d,
                "Adjacent original blue floor must publish red then yellow and one seed cue.");
            Walk(start);
            FailIf(filler.Endpoint != adjacent, "A revisited red tile must not move the yellow endpoint.");
            int nonadjacent = room == 0x87 ? 0x82 : 0x5c;
            Walk(nonadjacent);
            FailIf(filler.Endpoint != adjacent, "A diagonal blue tile must not move the endpoint.");
            Walk(start); Walk(adjacent);
            int next = room == 0x87 ? 0x81 : 0x3d;
            Step(angle:0, held:1, pressed:1);
            for (int wait = 0; filler.Endpoint != next && wait < 16; wait++) Step(angle:0);
            FailIf(filler.Endpoint != next || !_player.TopDownAirborne,
                "Native filler has no airborne gate when Link crosses onto adjacent blue floor.");
            Step(32); Walk(next);
            if (room == 0x6f)
                foreach (int packed in new[] { 0x3c, 0x4c, 0x5c, 0x6c, 0x6d }) Walk(packed);
            Step(28);
            FailIf(_entities.Entities<PuzzlePuffEffect>().Count != 0 || chest.Finished,
                "Initialization puff must expire independently while remaining blue cells block the chest.");
            // Declare the other completed floor writes after actual movement.
            // This isolates findTileInRoom's storage scan, not a full-room walk.
            for (int packed = 0; packed < 0xb0; packed++)
                if (_currentRoom.Layout[packed] == 0x9f) _currentRoom.Layout[packed] = 0x9d;
            rom.CopyRoom(_currentRoom);
            byte padding = _currentRoom.Layout[0xaf];
            _currentRoom.Layout[0xaf] = 0x9f; rom[0xcfaf] = 0x9f;
            Step(); FailIf(chest.Finished, "The final storage-padding column must still block the no-blue chest.");
            _currentRoom.Layout[0xaf] = padding; rom[0xcfaf] = padding;
            byte first = _currentRoom.Layout[0];
            _currentRoom.Layout[0] = 0x9f; rom.CopyRoom(_currentRoom);
            Step(); FailIf(chest.Finished, "findTileInRoom must include its lowest position$00.");
            _currentRoom.Layout[0] = first; rom.CopyRoom(_currentRoom);
            if (room == 0x6f)
                for (int index = 0; index < 31; index++)
                {
                    FailIf(!_rooms.TrySetTile(0x11,0x9d), "Chest rejection fixture requires31 accepted queue entries.");
                    rom.SetTile(0x11,0x9d);
                }
            Step();
            int chestPosition = room == 0x87 ? 0x65 : 0x67;
            FailIf(!chest.Finished || (_currentRoom.Layout[chestPosition] == 0xf1) != (room == 0x87),
                "No-blue chest must attempt one queued tile write before puff/deletion, including full-queue rejection.");
            Step(32);
            FailIf(_entities.Entities<PuzzlePuffEffect>().Count != 0 ||
                sounds.Requests.Count(cue => cue == SoundId.SndSolvePuzzle) != 1,
                "Completed or rejected chest must not retry its write/cue after queue drain and puff expiry.");
            LoadValidationRoom(4, 0x91);
            FailIf(_entities.Entities<TileFillerRoomEntity>().Count != 0, "Room cancellation must release the filler.");
        }
    }
}
