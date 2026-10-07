using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareDungeonStateConsumersRom()
    {
        int fixture = 0;
        foreach (var test in new[] { (Room:0x32,Sub:2),(Room:0x43,Sub:6),(Room:0x3b,Sub:7),(Room:0x2f,Sub:8),(Room:0x42,Sub:4),(Room:0x72,Sub:7),(Room:0x78,Sub:8) })
        foreach (int gate in new[] { 0,1,2 }) // Normal, text, interaction mask.
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            bool frozen = gate != 0;
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,test.Room); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            var record = (test.Room is 0x72 or 0x78 ? new SkullDungeonDatabase().GetRoomRecords(4,test.Room) :
                new WingDungeonDatabase().GetRoomRecords(4,test.Room)).Single(row => row.Id == 0x21 && row.SubId == test.Sub);
            Vector2 safeFloor = Enumerable.Range(1,_currentRoom.HeightInTiles-2).SelectMany(y =>
                Enumerable.Range(1,_currentRoom.WidthInTiles-2).Select(x => new Vector2(x*16+8,y*16+8)))
                .First(point => point.DistanceTo(record.Position) > 48 && !_collision.Collides(point) &&
                    _currentRoom.GetTerrainInfo(point+Vector2.Down*5).Hazard == HazardType.None);
            _player.WarpTo(safeFloor); _player.Face(Vector2I.Up);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,(int)safeFloor.X,(int)safeFloor.Y);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            var data = new DungeonInteractionDatabase();
            var puzzle = new ColoredCubePuzzleState(_runtimeState,data.Constant("red-pushable-block"));
            puzzle.CubeColor = 0x55; puzzle.CubePosition = 0x33;
            rom[0xccad] = 0x55; rom[0xccae] = 0x33;
            var actor = new DungeonStateController(record,() => _entities.ActiveRoom,data,puzzle,_runtimeState,_entities.SetTrigger);
            _entities.AddEntity(actor);
            int address = (0xd0+_entities.InteractionSlot(actor))*256+0x40;
            rom[address] = 1; rom[address+1] = 0x21; rom[address+2] = (byte)test.Sub;
            rom[address+0xb] = (byte)record.Y; rom[address+0xd] = (byte)record.X;
            FailIf(puzzle.CubeColor != 0x55 || puzzle.CubePosition != 0x33,
                "Cube/floor controller allocation must not execute state0 before the original object pass.");
            var sounds = _sound.AttachPlayRequestAudit();
            var disabled = _entities.InitializedObjectsDisabledSource;
            if (gate == 1) { _dialogue.ShowMessage("Declared dungeon signal.",100); rom[0xcba0] = 1; }
            if (gate == 2) { _entities.InitializedObjectsDisabledSource = () => true; rom[0xcc8a] = 2; }
            int packed = test.Sub == 2 ? 0x5a : test.Sub == 7 ? record.Y :
                ((record.Y&0xf0) | (record.X>>4));
            int expectedLast = 0, expectedColor = 0, expectedPosition = 0;
            int update = 0;
            void Step(int input)
            {
                _runtimeState.SetWramByte(OracleRuntimeState.SwitchStateAddress,0xa5); rom[0xcdd3] = 0xa5;
                for (int bit = 0; bit < 8; bit++) _entities.SetTrigger(bit,true);
                rom[0xcca0] = 0xff;
                if (test.Sub is 2 or 4 or 7)
                {
                    _currentRoom.SetPositionTileAndCollision(new((packed&15)*16+8,(packed>>4)*16+8),(byte)input,null,0);
                    rom.CopyRoom(_currentRoom);
                }
                else { puzzle.CubeColor = input; rom[0xccad] = (byte)input; }
                int expectedTrigger = test.Sub == 2 ? input == 0xad ? 1 : 0 :
                    test.Sub == 6 ? input == 0x80 ? 1 : 0 : 0xff;
                int expectedSwitch = 0xa5;
                if (test.Sub == 7 && input is >= 0xad and <= 0xaf)
                    expectedSwitch = input == 0xaf ? 0xa5|record.X : 0xa5&~record.X;
                if (test.Sub == 8 && (input&0x80) != 0)
                    expectedSwitch = (input&0x7f) == 2 ? 0xa5|record.X : 0xa5&~record.X;
                if (test.Sub == 4)
                {
                    if (update == 0)
                    { expectedLast = input; expectedColor = ((input-0xad)&255)|0x80; expectedPosition = 0x57; }
                    else if (!frozen && input is >= 0xad and <= 0xaf && input != expectedLast)
                    { expectedLast = input; expectedColor = (input-0xad)|0x80; }
                }
                StepSomariaMotionRom(rom,1,batched,afterUpdate:() =>
                {
                    update++;
                    FailIf(_entities.ActiveTriggers != rom[0xcca0] || _entities.ActiveTriggers != expectedTrigger ||
                        _runtimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != rom[0xcdd3] || rom[0xcdd3] != expectedSwitch ||
                        puzzle.CubeColor != rom[0xccad] || puzzle.CubePosition != rom[0xccae] ||
                        !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                        $"Dungeon$21:${test.Sub:x2}, room=${test.Room:x2}, gate={gate}, update{update}: shared signal/cube/flags/cues differ.");
                    if (test.Sub == 4)
                        FailIf(rom[address+4] != 1 || SomariaPrivate<int>(actor,"_lastTile") != rom[address+3] ||
                            rom[address+3] != expectedLast || puzzle.CubeColor != expectedColor || puzzle.CubePosition != expectedPosition,
                            "Cube color state0 must sample live dispatch data with byte subtraction; later updates freeze or accept only changed valid colors.");
                    if (test.Sub == 8) FailIf(puzzle.CubeColor != (input&0x7f),"Cube switch sensor must consume only the pending high bit.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls,
                        "Dungeon state consumers must preserve shared RNG.");
                });
            }
            try
            {
                foreach (int input in new[] { 0x00,0x7f,0x80,0x81,0x82,0x83,0xac,0xad,0xae,0xaf,0xb0,0xda,0xff,0xad,0xaf }) Step(input);
                if (test.Sub == 4 && gate != 0)
                {
                    _dialogue.Close(); rom[0xcba0] = 0; _entities.InitializedObjectsDisabledSource = disabled; rom[0xcc8a] = 0;
                    frozen = false;
                    Step(0xae);
                }
            }
            finally { _dialogue.Close(); _entities.InitializedObjectsDisabledSource = disabled; }
        }
    }
}
