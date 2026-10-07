using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareCrownPatternChestRom()
    {
        int fixture = 0;
        foreach (var test in new (int Room, int Sub, int Chest, int[] Positions, int[] Tiles)[] {
            (0x9b, 0x13, 0x54, [0x47,0x48,0x49,0x57,0x59,0x67,0x68,0x69], [0x2c,0x2d,0x2e,0x2c,0x2d,0x2e,0x2c,0x2e]),
            (0x9e, 0x14, 0x47, [0x45,0x49], [0x2a,0x2a]),
            (0xa5, 0x15, 0x53, [0x54,0x62,0x33,0x52,0x44,0x73], [0x2c,0x2c,0x2d,0x2d,0x2e,0x2e]) })
        foreach (int mode in new[] { 0, 1, 2 }) // Ordinary, full outgoing prefix, collected item.
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation();
            if (mode == 2) _saveData.SetRoomFlag(4, test.Room, OracleSaveData.RoomFlagItem);
            LoadValidationRoom(4, test.Room); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            static Vector2 Point(int packed) => new((packed & 15) * 16 + 8, (packed >> 4) * 16 + 8);
            var record = new CrownDungeonDatabase().GetRoomRecords(4, test.Room)
                .Single(row => row.Kind == DungeonObjectKind.TilePatternChest);
            FailIf(record.Id != 0x21 || record.SubId != test.Sub || record.Order != 0 ||
                _currentRoom.GetPackedPosition(record.Position) != test.Chest,
                $"4:${test.Room:x2} must preserve its original INTERAC$21:${test.Sub:x2} chest placement.");
            var data = new DungeonChestPatternDatabase();
            int start = Enumerable.Range(0, 0xb0).First(packed =>
                !test.Positions.Contains(packed) && packed != test.Chest &&
                _currentRoom.GetMetatile(Point(packed)) == 0xa0 && !_collision.Collides(Point(packed)));
            _player.WarpTo(Point(start)); _player.Face(Vector2I.Up);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 0, (int)_player.Position.X, (int)_player.Position.Y);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            if (mode == 1)
                for (int slot = 2; slot < 15; slot++)
                {
                    // Declare frozen outgoing capacity; prefix behavior is excluded.
                    _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(224,144), SoundId.MusNone, AlwaysUpdates:false));
                    int address = (0xd0 + slot) * 256 + 0x40;
                    rom[address] = 2; rom[address + 1] = 5; rom[address + 4] = 1; rom[address + 6] = 60;
                    rom[address + 0xb] = 144; rom[address + 0xd] = 224;
                }
            var chest = new DungeonPuzzleChestRoomEntity(record, _currentRoom,
                () => data.Matches(test.Sub, _currentRoom), () => _saveData.HasRoomFlag(_rooms.ActiveGroup, _rooms.CurrentRoom.Id, OracleSaveData.RoomFlagItem), 0xf1,
                _entities.OnSoundRequested, _entities.OnRoomTileChanged, () => 0, _entities.TryCreatePuzzlePuff, _entities.IsOutgoingEntity,
                () => _rooms.TrySetTile((byte)test.Chest, 0xf1));
            _entities.AddEntity(chest);
            int sourceSlot = (0xd0 + _entities.InteractionSlot(chest)) * 256 + 0x40;
            rom[sourceSlot] = 1; rom[sourceSlot + 1] = 0x21; rom[sourceSlot + 2] = (byte)test.Sub;
            rom[sourceSlot + 0xb] = (byte)record.Y; rom[sourceSlot + 0xd] = (byte)record.X;
            var sounds = _sound.AttachPlayRequestAudit();
            _dialogue.ShowMessage("Pending chest predicate.", 100); rom[0xcba0] = 1;
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched:batched, afterUpdate:() =>
            {
                rom.UpdateGameplay(0, 0, 0xff, _entities.FrameCounter); rom.AdvanceTileGraphics();
                CompareSomariaMotionRom(rom, $"Crown pattern$21:${test.Sub:x2}, mode={mode}, batch={batched}");
                bool nativeFinished = rom[sourceSlot] == 0 || rom[sourceSlot + 1] != 0x21;
                int nativePuffs = Enumerable.Range(0xd2,14).Count(page => rom[page*256+0x40] != 0 && rom[page*256+0x41] == 5);
                FailIf(chest.Finished != nativeFinished || _entities.Entities<PuzzlePuffEffect>().Count != nativePuffs ||
                    _currentRoom.GetMetatile(Point(test.Chest)) != rom[0xcf00 + test.Chest] ||
                    (byte)_currentRoom.GetTerrainInfo(Point(test.Chest)).Collision != rom[0xce00 + test.Chest] ||
                    !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                    $"Crown pattern$21:${test.Sub:x2}, mode={mode}: deleted={chest.Finished}/{nativeFinished}, puffs={_entities.Entities<PuzzlePuffEffect>().Count}/{nativePuffs}, cues=[{string.Join(',',sounds.Requests.Select(cue => $"${cue:x2}"))}]/[{string.Join(',',rom.Sounds.Select(cue => $"${cue:x2}"))}].");
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls,
                    "Crown chest predicate/allocation must preserve shared RNG.");
            });
            void Tile(int position, int tile)
            {
                _currentRoom.SetPositionTileAndCollision(Point(position), (byte)tile, null, 0);
                rom.CopyRoom(_currentRoom);
            }
            for (int index = 0; index < test.Positions.Length; index++) Tile(test.Positions[index], test.Tiles[index]);
            if (mode == 0)
                for (int index = 0; index < test.Positions.Length; index++)
                {
                    int minimum = test.Sub == 0x13 ? 0x2c : test.Tiles[index];
                    int maximum = test.Sub == 0x13 ? 0x2e : test.Tiles[index];
                    foreach (int invalid in new[] { minimum - 1, maximum + 1 })
                    {
                        Tile(test.Positions[index], invalid); Step();
                        FailIf(chest.Finished, "Every original pattern cell must reject both tile-range boundaries.");
                    }
                    Tile(test.Positions[index], test.Tiles[index]);
                }
            Step(); Step(2);
            FailIf(!chest.Finished || mode != 2 && _currentRoom.GetMetatile(Point(test.Chest)) != 0xf1,
                "Complete pattern must publish one chest, while collected-item entry deletes without solving.");
            _dialogue.Close(); rom[0xcba0] = 0;
        }
        CompareCrownPatternChestScrollRom();
        GD.Print("Validated executed-ROM Crown $21:$13-$15 predicates, every cell/range boundary, state0 text admission, chest tile/collision publication, puff capacity/order, collected-item and outgoing deletion, re-entry and cues/RNG through split/batched gameplay.");
    }

    private void CompareCrownPatternChestScrollRom()
    {
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4, 0xa5); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(220.25f, 88.5f)); _player.Face(Vector2I.Right);
            FailIf(_collision.Collides(_player.Position), "Crown chest cancellation must approach the original room$4:$a5 exit floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData, seed, _currentRoom, 1, 220, 88);
            rom.Word(0xd00a, 88*256+128); rom.Word(0xd00c, 220*256+64);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            StepSomariaMotionRom(rom, 9, batched, 8);
            var record = new CrownDungeonDatabase().GetRoomRecords(4, 0xa5)
                .Single(row => row.Kind == DungeonObjectKind.TilePatternChest);
            var data = new DungeonChestPatternDatabase();
            var chest = new DungeonPuzzleChestRoomEntity(record, _currentRoom,
                () => data.Matches(0x15, _currentRoom), () => _saveData.HasRoomFlag(_rooms.ActiveGroup, _rooms.CurrentRoom.Id, OracleSaveData.RoomFlagItem), 0xf1,
                _entities.OnSoundRequested, _entities.OnRoomTileChanged, () => 0, _entities.TryCreatePuzzlePuff, _entities.IsOutgoingEntity,
                () => _rooms.TrySetTile(0x53, 0xf1));
            _entities.AddEntity(chest);
            int slot = (0xd0 + _entities.InteractionSlot(chest))*256 + 0x40;
            rom[slot] = 1; rom[slot+1] = 0x21; rom[slot+2] = 0x15;
            rom[slot+0xb] = (byte)record.Y; rom[slot+0xd] = (byte)record.X;
            StepSomariaMotionRom(rom, 1, batched);
            FailIf(chest.Finished || rom[slot] != 1 || rom[slot+4] != 0,
                "Unsolved Crown pattern chest must remain in original state$00.");
            FailIf(!_rooms.TryGetNeighbor(Vector2I.Right, out int target) || target != 0xa6,
                "Crown chest cancellation must retain original adjacency$4:$a5 -> $4:$a6.");
            // Native incoming parsing and Link scroll coordinates are covered
            // separately. Compare this outgoing handler's ordered object pass.
            _transitions.BeginScroll(_player, Vector2I.Right, target);
            rom.SetOutgoingInteractions(); rom[0xcd00] = 8;
            var sounds = _sound.AttachPlayRequestAudit();
            void Step(int count = 1) => StepGameplayUpdates(count, Vector2.Zero, batched:batched, afterUpdate:() =>
            {
                rom.AdvanceInteractions(_entities.FrameCounter);
                if (!_transitions.ScrollActive) rom.ClearOutgoingInteractions();
                bool alive = rom[slot] != 0;
                FailIf(chest.Finished == alive || _entities.OutgoingEntities<DungeonPuzzleChestRoomEntity>().Count != (alive ? 1 : 0) ||
                    sounds.Requests.Any() || rom.Sounds.Any(),
                    $"Crown chest outgoing$21:$15, batch={batched}: deleted={chest.Finished}/{!alive}, outgoing count/cues differ.");
            });
            Step();
            FailIf(!chest.Finished, "Enabled$02 Crown chest must delete on the first frozen outgoing object pass.");
            Step(_transitions.ScrollTotalFrames-1);
            FailIf(_transitions.ScrollActive, "Crown chest cancellation must finish the actual scroll.");
            LoadValidationRoom(4, 0xa5);
            FailIf(_entities.Entities<DungeonPuzzleChestRoomEntity>().Count != 1 ||
                _currentRoom.Layout[0x53] == 0xf1 || _saveData.HasRoomFlag(4, 0xa5, OracleSaveData.RoomFlagItem),
                "Unsolved Crown chest re-entry must construct a fresh original controller without a chest or collected flag.");
        }
    }
}
