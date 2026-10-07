using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareMovingPlatformMotionRom()
    {
        var programs = new MovingPlatformDatabase();
        var data = new DungeonInteractionDatabase();
        var visuals = new DungeonInteractionVisualDatabase();
        int fixture = 0;
        // Six mainData.s placements, plus the distinct dungeon1 programs.
        foreach (var c in new[] {
            (Room:0x6c,Sub:0x03,X:160,Y:88,Dungeon:4), (Room:0x6c,Sub:0x08,X:104,Y:40,Dungeon:4),
            (Room:0x74,Sub:0x11,X:112,Y:64,Dungeon:4), (Room:0x75,Sub:0x1a,X:168,Y:40,Dungeon:4),
            (Room:0x75,Sub:0x23,X:104,Y:72,Dungeon:4), (Room:0x75,Sub:0x29,X:104,Y:144,Dungeon:4),
            (Room:0x6c,Sub:0x03,X:160,Y:88,Dungeon:1), (Room:0x6c,Sub:0x08,X:104,Y:40,Dungeon:1),
            (Room:0x15,Sub:0x05,X:48,Y:144,Dungeon:1) })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,c.Room); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            Vector2 start = c.Room switch {0x6c=>new(40.25f,56.5f),0x74=>new(72.25f,72.5f),0x15=>new(168.25f,104.5f),_=>new(200.25f,40.5f)};
            _player.WarpTo(start); _player.Face(Vector2I.Down);
            FailIf(_collision.Collides(start) || _currentRoom.GetTerrainInfo(start).Hazard != HazardType.None,
                "Platform motion comparison must keep Link on the room's original safe shore.");
            // Dungeon1 cases declare the table selector, excluding the room's
            // dungeon lookup; all script bytes/branches execute native code.
            var platform = new MovingPlatformRoomEntity(visuals.Visual($"platform-{c.Sub&7:x2}"),new(c.X,c.Y),c.Sub,
                data.MovingPlatformCollisionRadii(c.Sub),data,programs.Script(c.Dungeon,c.Sub>>3),
                _entities.PlatformRiding,_entities.ReadPlayingInstrument);
            _entities.AddEntity(platform);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,2,(int)start.X,(int)start.Y);
            rom.Word(0xd00c,(int)(start.X*256)); rom.Word(0xd00a,(int)(start.Y*256));
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = (byte)c.Dungeon;
            rom[0xd240] = 1; rom[0xd241] = 0x79; rom[0xd242] = (byte)c.Sub;
            rom[0xd24b] = (byte)c.Y; rom[0xd24d] = (byte)c.X;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0,scriptStart = 0,instrumentPublication = 0;
            void Step(int count = 1) => StepGameplayUpdates(count,Vector2.Zero,batched:batch,afterUpdate:() => {
                rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter,() => {
                    rom[0xcc8d] = (byte)instrumentPublication; rom[0xcc96] = (byte)instrumentPublication;
                });
                if (scriptStart == 0) scriptStart = rom.Word(0xd258)-2;
                FailIf(platform.PrecisePosition != new Vector2(rom.Word(0xd24c)/256f,rom.Word(0xd24a)/256f) ||
                    platform.Counter != rom[0xd246] || platform.Moving != (rom[0xd245] != 0) || platform.Angle != rom[0xd249] ||
                    rom.Word(0xd258) != scriptStart+platform.Command*2 ||
                    platform.CollisionRadii != new Vector2(rom[0xd267],rom[0xd266]) ||
                    platform.LinkRiding != (rom[0xcc96] == 0xd2) || _entities.PlayerRidingObject != (rom[0xcc96] != 0) ||
                    _player.PrecisePosition != new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f) ||
                    !sounds.Requests.Where(cue=>cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                    $"Platform room${c.Room:x2}, sub${c.Sub:x2}, dungeon${c.Dungeon:x2}, batch={batch}, update{++update}: counter={platform.Counter}/{rom[0xd246]}, command={platform.Command}/${rom.Word(0xd258):x4}, support={_entities.PlayerRidingObject}/{rom[0xcc96]:x2}, XY={platform.PrecisePosition}/{rom.Word(0xd24c)/256f},{rom.Word(0xd24a)/256f}, Link={_player.PrecisePosition}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}.");
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls,
                    "Native platform scripts must retain shared RNG order.");
            });
            _dialogue.ShowGameplayMessage("Pending platform",120); rom[0xcba0] = 1;
            Step(3); _dialogue.Close(); rom[0xcba0] = 0;
            FailIf(platform.Counter != 8 || platform.Command != 1,"State0 must install wait8 under text without claiming Link.");
            var instrument = _entities.PlayingInstrumentSource;
            try
            {
                // Declare the instrument's publication; its parent/menu is
                // separately ROM-backed. Waiting remains eligible; moving holds.
                _entities.PlayingInstrumentSource = () => instrumentPublication; instrumentPublication = 1;
                Step(8); int counter = platform.Counter; Vector2 point = platform.PrecisePosition;
                Step(4);
                FailIf(!platform.Moving || platform.Counter != counter || platform.PrecisePosition != point,
                    "Instrument sentinel must allow wait completion and then retain movement/counter.");
            }
            finally { _entities.PlayingInstrumentSource = instrument; instrumentPublication = 0; }
            _dialogue.ShowGameplayMessage("Platform pause",120); rom[0xcba0] = 0x80;
            Step(3); _dialogue.Close(); rom[0xcba0] = 0;
            int previous = platform.Command; bool looped = false;
            for (int wait = 0; !looped && wait < 550; wait++)
            {
                Step(); looped = platform.Command < previous; previous = platform.Command;
            }
            FailIf(!looped,"Bounded native motion must complete the initial leg and one whole script loop.");
            Step(10);
            LoadValidationRoom(4,0x91);
            FailIf(_entities.Entities<MovingPlatformRoomEntity>().Count != 0 || _entities.PlayerRidingObject,
                "Room replacement must retire the platform and its shared rider publication.");
        }
    }
}
