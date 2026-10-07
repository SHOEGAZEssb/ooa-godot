using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareDungeonStateScrollRom()
    {
        int fixture = 0;
        foreach (var test in new[] { (Room:0x32,Id:0x21,Sub:2),(Room:0x43,Id:0x21,Sub:6),
            (Room:0x3b,Id:0x21,Sub:7),(Room:0x2f,Id:0x21,Sub:8),(Room:0x42,Id:0x21,Sub:4),
            (Room:0x2f,Id:0x1b,Sub:4),(Room:0x3b,Id:0x1b,Sub:0x25),
            (Room:0x72,Id:0x1b,Sub:0),(Room:0x78,Id:0x1b,Sub:1) })
        foreach (bool initialized in new[] { false,true })
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            if (initialized && test.Id == 0x21 && test.Sub != 4) continue;
            // Place the original handler records on the independently tested
            // reachable $79 exit. Sealed source rooms have additional door
            // producers outside this current-buffer/eligibility comparison.
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x79); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            var exit = FindOriginalPatternExitRom();
            _player.WarpTo(exit.Start+new Vector2(0.25f,0.5f)); _player.Face(exit.Direction);
            int angle = exit.Direction == Vector2I.Up ? 0 : exit.Direction == Vector2I.Right ? 8 : exit.Direction == Vector2I.Down ? 16 : 24;
            var rom = new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,angle/8,(int)exit.Start.X,(int)exit.Start.Y);
            rom.Word(0xd00a,(int)exit.Start.Y*256+128); rom.Word(0xd00c,(int)exit.Start.X*256+64);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            StepSomariaMotionRom(rom,9,batched,angle);
            var record = (test.Room < 0x70 ? new WingDungeonDatabase().GetRoomRecords(4,test.Room) :
                new SkullDungeonDatabase().GetRoomRecords(4,test.Room)).Single(row => row.Id == test.Id && row.SubId == test.Sub);
            var data = new DungeonInteractionDatabase();
            var puzzle = new ColoredCubePuzzleState(_runtimeState,data.Constant("red-pushable-block"));
            IRoomEntity actor = test.Id == 0x1b ? new MinecartGateRoomEntity(record,() => _entities.ActiveRoom,_runtimeState,
                new DungeonInteractionVisualDatabase().Visual("minecart-gate"),_entities.OnSoundRequested,_entities.OnRoomTileChanged,() => 0) :
                new DungeonStateController(record,() => _entities.ActiveRoom,data,puzzle,_runtimeState,_entities.SetTrigger);
            _entities.AddEntity(actor);
            int address = (0xd0+_entities.InteractionSlot(actor.Node))*256+0x40;
            rom[address] = 1; rom[address+1] = (byte)test.Id; rom[address+2] = (byte)test.Sub;
            rom[address+0xb] = (byte)record.Y; rom[address+0xd] = (byte)record.X;
            if (initialized) StepSomariaMotionRom(rom,1,batched);
            _transitions.BeginScroll(_player,exit.Direction,exit.Target);
            rom.SetOutgoingInteractions(); rom.ClearRoomVariables(true); rom[0xcc30] = (byte)exit.Target; rom[0xcd00] = 8;
            // Incoming parsing/occupancy and Link scroll coordinates are
            // declared inputs; execute the retained native physical handlers.
            foreach (var pair in SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_interactionSlots"))
            {
                if (ReferenceEquals(pair.Key,actor)) continue;
                int slot = (0xd0+pair.Value)*256+0x40;
                rom[slot] = 1; rom[slot+1] = 5; rom[slot+4] = 1;
            }
            rom.Word(0xd00a,(int)_player.Position.Y*256); rom.Word(0xd00c,(int)_player.Position.X*256);
            var seed = _random.CaptureState(); int calls = rom.RandomCalls;
            rom[0xff94] = seed.Rng1; rom[0xff95] = seed.Rng2;
            var sounds = _sound.AttachPlayRequestAudit(); int cueStart = rom.Sounds.Count;
            int packed = test.Sub == 2 && test.Id == 0x21 ? 0x5a : test.Sub == 7 && test.Id == 0x21 ? record.Y :
                ((record.Y&0xf0)|(record.X>>4));
            int update = 0;
            void Input(int tile,int color)
            {
                _currentRoom.SetPositionTileAndCollision(new((packed&15)*16+8,(packed>>4)*16+8),(byte)tile,null,0);
                puzzle.CubeColor = color; puzzle.CubePosition = 0x33; rom[0xccad] = (byte)color; rom[0xccae] = 0x33;
                _runtimeState.SetWramByte(OracleRuntimeState.SwitchStateAddress,0xa5); rom[0xcdd3] = 0xa5;
                for (int bit = 0; bit < 8; bit++) _entities.SetTrigger(bit,true);
                rom[0xcca0] = 0xff; rom.CopyRoom(_currentRoom);
            }
            void Step(int count = 1) => StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:() =>
            {
                rom.AdvanceInteractions(_entities.FrameCounter); update++;
                if (!_transitions.ScrollActive) { rom.ClearOutgoingInteractions(); rom[0xcd00] = 1; }
                bool alive = SomariaPrivate<List<IRoomEntity>>(_entities,"_outgoingEntities").Contains(actor);
                FailIf(alive != (rom[address] != 0) || _entities.ActiveTriggers != rom[0xcca0] ||
                    _runtimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != rom[0xcdd3] ||
                    puzzle.CubeColor != rom[0xccad] || puzzle.CubePosition != rom[0xccae] ||
                    !sounds.Requests.SequenceEqual(rom.Sounds.Skip(cueStart)),
                    $"Dungeon${test.Id:x2}:${test.Sub:x2}, initialized={initialized}, scroll update{update}: lifetime/shared signals/cues differ.");
                if (alive && actor is MinecartGateRoomEntity gate) CompareMinecartGateStateRom(gate,rom,address,"retained scroll state");
                if (alive && actor is DungeonStateController && test.Sub == 4)
                    FailIf(SomariaPrivate<int>(actor,"_lastTile") != rom[address+3],"Outgoing cube-color state0 samples the destination exactly once, then freezes its tile latch.");
                for (int y = 0; y < _currentRoom.HeightInTiles; y++)
                for (int x = 0; x < _currentRoom.WidthInTiles; x++)
                {
                    var point = new Vector2(x*16+8,y*16+8); int p = y*16+x;
                    FailIf(_currentRoom.GetMetatile(point) != rom[0xcf00+p] || _currentRoom.GetTerrainInfo(point).Collision != rom[0xce00+p] ||
                        _currentRoom.GetUnderlyingMetatile(point) != rom.Underlying(p),
                        $"Retained dungeon handler current-room buffers differ at${p:x2}.");
                }
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls-calls,
                    "Retained dungeon state dispatch must preserve shared RNG.");
            });
            Input(0xad,0x80); Step(); Input(0xaf,0x82); Step();
            Input(0xda,0x7f); Step(); Step(_transitions.ScrollTotalFrames-3);
            FailIf(_transitions.ScrollActive || SomariaPrivate<List<IRoomEntity>>(_entities,"_outgoingEntities").Contains(actor),
                "Scroll completion must discard retained state consumers and gates.");
        }
    }
}
