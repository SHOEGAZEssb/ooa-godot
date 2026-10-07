using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareBlueFlameChestRom()
    {
        int fixture = 0;
        foreach (int mode in new[] { 0, 1, 2, 3, 4 }) // Ordinary, collected, full pool, full queue, ignored color bits.
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation();
            if (mode == 1) _saveData.SetRoomFlag(4,0x90,OracleSaveData.RoomFlagItem);
            LoadValidationRoom(4,0x90);
            var writeChest = SomariaPrivate<Action>(_entities.Entities<DungeonPuzzleChestRoomEntity>().Single(),"_queuedWrite");
            _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(200,136)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position), "Blue-flame chest fixture must keep Link on original room$4:$90 floor.");
            var record = new SkullDungeonDatabase().GetRoomRecords(4,0x90).Single(row => row.Kind == DungeonObjectKind.BlueFlameChest);
            FailIf(record.Id != 0x21 || record.SubId != 0x12 || _currentRoom.GetPackedPosition(record.Position) != 0x84,
                "Blue-flame chest requires its original $21:$12 placement at $84.");
            var declared = new DeclaredColoredCubeState(_runtimeState); _entities.AddEntity(declared);
            var cube = declared.ColoredCubePuzzleState;
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,200,136);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            if (mode == 2)
                for (int slot = 2; slot < 15; slot++)
                {
                    _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(224,144),SoundId.MusNone,AlwaysUpdates:false));
                    int address = (0xd0+slot)*256+0x40;
                    rom[address] = 2; rom[address+1] = 5; rom[address+4] = 1; rom[address+6] = 60;
                    rom[address+0xb] = 144; rom[address+0xd] = 224;
                }
            var chest = new DungeonPuzzleChestRoomEntity(record,_currentRoom,
                () => (cube.CubeColor & 0x83) == 0x82,() => _saveData.HasRoomFlag(_rooms.ActiveGroup,_rooms.CurrentRoom.Id,OracleSaveData.RoomFlagItem),0xf1,
                _entities.OnSoundRequested,_entities.OnRoomTileChanged,() => 0,
                _entities.TryCreatePuzzlePuff,_entities.IsOutgoingEntity,writeChest);
            _entities.AddEntity(chest);
            int addressChest = (0xd0+_entities.InteractionSlot(chest))*256+0x40;
            rom[addressChest] = 1; rom[addressChest+1] = 0x21; rom[addressChest+2] = 0x12;
            rom[addressChest+0xb] = (byte)record.Y; rom[addressChest+0xd] = (byte)record.X;
            var sounds = _sound.AttachPlayRequestAudit();
            _dialogue.ShowMessage("Declared cube-color producer.",100); rom[0xcba0] = 1;
            void Color(int color) { cube.CubeColor = color; rom[0xccad] = (byte)color; }
            void Step(int count = 1) => StepSomariaMotionRom(rom,count,batched,afterUpdate:() =>
            {
                rom.AdvanceTileGraphics();
                int puffs = Enumerable.Range(0xd2,14).Count(page => rom[page*256+0x40] != 0 && rom[page*256+0x41] == 5);
                FailIf(chest.Finished != (rom[addressChest] == 0 || rom[addressChest+1] != 0x21) ||
                    _entities.Entities<PuzzlePuffEffect>().Count != puffs ||
                    _rooms.PendingTileGraphics != ((rom[0xcce0]-rom[0xccdf])&31) ||
                    !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                    $"Blue-flame chest$21:$12, mode={mode}, batch={batched}: lifetime/puff/queue/cues differ.");
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls,
                    "Blue-flame chest must preserve shared RNG.");
            });
            if (mode == 0 || mode == 4)
                foreach (int color in new[] { 0,1,2,3,0x80,0x81,0x83,0x84,0x85,0x87,0x7e })
                {
                    Color(color); Step();
                    FailIf(chest.Finished,"Blue-flame chest must require bit7 and low color bits$02.");
                }
            if (mode == 3)
                for (int index = 0; index < 31; index++)
                {
                    FailIf(!_rooms.TrySetTile(0x11,0xa0),"Blue-flame queue fixture requires31 accepted writes.");
                    rom.SetTile(0x11,0xa0);
                }
            Color(mode == 4 ? 0x86 : 0x82); Step(); Step(32);
            FailIf(!chest.Finished || (_currentRoom.Layout[0x84] == 0xf1) != (mode != 1 && mode != 3) ||
                sounds.Requests.Count(cue => cue == SoundId.SndSolvePuzzle) != (mode == 1 ? 0 : 1),
                "Shared blue-flame chest must solve once, including queue rejection, ignored color bits and collected-item deletion.");
            _dialogue.Close(); LoadValidationRoom(4,0x91);
        }
        CompareBlueFlameChestScrollRom();
    }

    private void CompareBlueFlameChestScrollRom()
    {
        int fixture = 0;
        foreach (int destinationColor in new[] { 0x82,0 })
        foreach (bool collectedDestination in new[] { false, true })
        foreach (bool fullQueue in destinationColor == 0x82 && !collectedDestination ? new[] { false,true } : new[] { false })
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x90);
            // Keep the actual factory's write callback while isolating cube,
            // enemy and incoming room producers from this handler comparison.
            var writeChest = SomariaPrivate<Action>(_entities.Entities<DungeonPuzzleChestRoomEntity>().Single(),"_queuedWrite");
            _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            int exitColumn = Enumerable.Range(1,13).First(column =>
                !_collision.Collides(new(column*16+8,24)) && !_collision.Collides(new(column*16+8,8)));
            int startX = exitColumn*16+8;
            _player.WarpTo(new(startX+0.25f,24.5f)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position), "Blue-flame cancellation must approach room$4:$90's original northern exit floor.");
            var rom = new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,startX,24);
            rom.Word(0xd00a,24*256+128); rom.Word(0xd00c,startX*256+64);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            StepSomariaMotionRom(rom,9,batched,0);
            var record = new SkullDungeonDatabase().GetRoomRecords(4,0x90).Single(row => row.Kind == DungeonObjectKind.BlueFlameChest);
            var declared = new DeclaredColoredCubeState(_runtimeState); _entities.AddEntity(declared);
            var cube = declared.ColoredCubePuzzleState;
            var outgoingRoom = _currentRoom;
            var chest = new DungeonPuzzleChestRoomEntity(record,outgoingRoom,
                () => (cube.CubeColor & 0x83) == 0x82,() => _saveData.HasRoomFlag(_rooms.ActiveGroup,_rooms.CurrentRoom.Id,OracleSaveData.RoomFlagItem),0xf1,
                _entities.OnSoundRequested,_entities.OnRoomTileChanged,() => 0,
                _entities.TryCreatePuzzlePuff,_entities.IsOutgoingEntity,writeChest);
            _entities.AddEntity(chest);
            int slot = (0xd0+_entities.InteractionSlot(chest))*256+0x40;
            rom[slot] = 1; rom[slot+1] = 0x21; rom[slot+2] = 0x12;
            rom[slot+0xb] = (byte)record.Y; rom[slot+0xd] = (byte)record.X;
            // Declare a completed sensor write after the last outgoing pass.
            // Room clearing must supersede it before this pending handler runs.
            cube.CubeColor = 0x82; rom[0xccad] = 0x82;
            _saveData.SetRoomFlag(4,0x8b,OracleSaveData.RoomFlagItem,collectedDestination);
            rom[0xc98b] = _saveData.GetRoomFlags(4,0x8b);
            FailIf(!_rooms.TryGetNeighbor(Vector2I.Up,out int target) || target != 0x8b,
                "Blue-flame scroll must preserve imported adjacency$4:$90 -> $4:$8b.");
            var sounds = _sound.AttachPlayRequestAudit();
            _transitions.BeginScroll(_player,Vector2I.Up,target);
            rom.SetOutgoingInteractions(); rom.ClearRoomVariables(true);
            rom.CopyRoom(_currentRoom); rom[0xcc30] = 0x8b; rom[0xcd00] = 8;
            foreach (var pair in SomariaPrivate<System.Collections.Generic.Dictionary<IRoomEntity,int>>(_entities,"_interactionSlots"))
            {
                if (ReferenceEquals(pair.Key,chest)) continue;
                int occupied = (0xd0+pair.Value)*256+0x40;
                rom[occupied] = 1; rom[occupied+1] = 0; rom[occupied+4] = 1;
            }
            var seed = _random.CaptureState(); int calls = rom.RandomCalls;
            rom[0xff94] = seed.Rng1; rom[0xff95] = seed.Rng2;
            byte oldTile = outgoingRoom.Layout[0x84], incomingTile = _currentRoom.Layout[0x84];
            FailIf(cube.CubeColor != 0 || rom[0xccad] != 0,"Scroll activation must clear the preceding cube signal before any retained handler.");
            if (fullQueue) for (int index = 0; index < 31; index++)
            {
                FailIf(!_rooms.TrySetTile(0x11,0xa0),"Outgoing chest fixture requires31 accepted queue entries.");
                rom.SetTile(0x11,0xa0);
            }
            cube.CubeColor = destinationColor; rom[0xccad] = (byte)destinationColor;
            bool solved = destinationColor == 0x82 && !collectedDestination;
            // Incoming parsing and Link scroll coordinates are excluded. The
            // actual host loop still exercises this outgoing handler's lifetime.
            void Step(int count = 1) => StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:() =>
            {
                rom.AdvanceInteractions(_entities.FrameCounter);
                if (!_transitions.ScrollActive) rom.ClearOutgoingInteractions();
                bool alive = rom[slot] != 0 && rom[slot+1] == 0x21;
                var puffs = _entities.Entities<PuzzlePuffEffect>().OrderBy(puff => _entities.InteractionSlot(puff)).ToArray();
                int[] nativePuffs = Enumerable.Range(0xd2,14).Select(page => page*256+0x40)
                    .Where(address => rom[address] != 0 && rom[address+1] == 5).ToArray();
                FailIf(_transitions.ScrollActive && chest.Finished == alive || _entities.OutgoingEntities<DungeonPuzzleChestRoomEntity>().Count != (alive ? 1 : 0) ||
                    puffs.Length != nativePuffs.Length || _rooms.PendingTileGraphics != ((rom[0xcce0]-rom[0xccdf])&31) ||
                    !sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Blue-flame outgoing$21:$12, color=${destinationColor:x2}, destination item={collectedDestination}, queue={fullQueue}, batch={batched}: lifetime/puff/queue/cues differ.");
                for (int index = 0; index < puffs.Length; index++)
                {
                    int address = nativePuffs[index]; var puff = puffs[index];
                    FailIf(_entities.InteractionSlot(puff) != (address>>8)-0xd0 || puff.Position != record.Position ||
                        puff.Position != new Vector2(rom[address+0xd],rom[address+0xb]) || puff.Initialized != (rom[address+4] != 0) ||
                        puff.Visible != ((rom[address+0x1a]&0x80) != 0) || puff.Initialized &&
                        (puff.CurrentParameter != rom[address+0x21] || SomariaPrivate<int>(puff,"_animationCounter") != rom[address+0x20]),
                        "Outgoing chest puff must allocate before parent deletion in physical order and retain native state0/animation lifetime.");
                }
                for (int y = 0; y < _currentRoom.HeightInTiles; y++) for (int x = 0; x < _currentRoom.WidthInTiles; x++)
                {
                    var point = new Vector2(x*16+8,y*16+8); int packed = y*16+x;
                    FailIf(_currentRoom.GetMetatile(point) != rom[0xcf00+packed] || _currentRoom.GetTerrainInfo(point).Collision != rom[0xce00+packed] ||
                        _currentRoom.GetUnderlyingMetatile(point) != rom.Underlying(packed),$"Outgoing chest current-room buffers differ at${packed:x2}.");
                }
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls-calls ||
                    outgoingRoom.Layout[0x84] != oldTile || _saveData.HasRoomFlag(4,0x90,OracleSaveData.RoomFlagItem) ||
                    _saveData.HasRoomFlag(4,0x8b,OracleSaveData.RoomFlagItem) != collectedDestination,
                    "Outgoing chest must preserve old-room terrain, persistent item flags and shared RNG.");
            });
            Step();
            FailIf(chest.Finished != (solved || collectedDestination) || cube.CubeColor != rom[0xccad] ||
                _currentRoom.Layout[0x84] != (solved && !fullQueue ? 0xf1 : incomingTile) ||
                sounds.Requests.Count(cue => cue == SoundId.SndSolvePuzzle) != (solved ? 1 : 0) ||
                sounds.Requests.Count(cue => cue == SoundId.SndPoof) != (solved ? 1 : 0),
                "Outgoing $12 must use destination cube color/item flags and current-room queued tile writes, including rejection, before puff allocation/deletion.");
            Step(_transitions.ScrollTotalFrames-1);
            FailIf(_transitions.ScrollActive || _entities.OutgoingEntities<DungeonPuzzleChestRoomEntity>().Count != 0,
                "Unsolved outgoing $12 must retire at actual scroll completion.");
        }
    }
}
