using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareSkullRailJunctionRom()
    {
        static Vector2 Point(int packed) => new((packed&15)*16+8,(packed>>4)*16+8);
        Vector2I[] directions = [Vector2I.Up,Vector2I.Right,Vector2I.Down,Vector2I.Left];
        int fixture = 0;
        foreach (var c in new[] {(Room:0x89,Position:0x67,Direction:1,Mask:4),(Room:0x8f,Position:0x52,Direction:3,Mask:8)})
        foreach (bool set in new[] {false,true})
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation();
            byte switches = (byte)(0x80|(set ? c.Mask : 0));
            _runtimeState.SetWramByte(OracleRuntimeState.SwitchStateAddress,switches);
            LoadValidationRoom(4,c.Room); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            Vector2 center = Point(c.Position);
            int tile = c.Room == 0x89 ? set ? 0x5d : 0x5c : set ? 0x5e : 0x59;
            int exit = c.Room == 0x89 ? set ? 1 : 2 : set ? 1 : 2;
            FailIf(_currentRoom.GetMetatile(center) != tile,
                $"Original Skull junction$4:${c.Room:x2}:${c.Position:x2} must reconstruct tile${tile:x2} from switch${switches:x2}.");
            // Declare the mounted companion at this original junction.
            // Reachable push/jump/handoff and repeat boarding have separate
            // native coverage; no room geometry is changed in this fixture.
            MinecartRuntimeState.Reset(_runtimeState,[]);
            MinecartRuntimeState.BeginRide(_runtimeState,0,c.Room,center,c.Direction);
            var cart = new MinecartRoomEntity(new ActiveMinecart(-1,c.Room,(int)center.Y,(int)center.X,c.Direction,true),
                _currentRoom,new DungeonInteractionDatabase(),_runtimeState,
                new DungeonInteractionVisualDatabase().Visual("minecart"),_sound.PlaySound);
            _entities.AddEntity(cart);
            _player.WarpTo(center+new Vector2(0.25f,0.5f)); _player.Face(directions[c.Direction]);
            _player.FinishMinecartMount(center,c.Direction,0);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,c.Direction,(int)center.X,(int)center.Y)
                {CompanionDispatchEnabled = true};
            rom[0xd00a] = 0x80; rom[0xd00c] = 0x40;
            rom.InitializeLinkGameplay(); rom[0xd004] = 0; rom[0xd009] = 0;
            rom[0xcdd3] = switches;
            rom[0xd100] = 3; rom[0xd101] = 0x0a;
            rom[0xd108] = (byte)c.Direction; rom[0xd109] = (byte)(c.Direction*8);
            rom[0xd10b] = (byte)center.Y; rom[0xd10d] = (byte)center.X;
            rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter);
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count) => StepGameplayUpdates(count,Vector2.Zero,batched:batch,afterUpdate:()=> {
                rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter-1); update++;
                FailIf(cart.Position != new Vector2(rom.Word(0xd10c)/256f,rom.Word(0xd10a)/256f) ||
                    cart.Direction != rom[0xd108] || cart.Angle != rom[0xd109] ||
                    _player.PrecisePosition != new Vector2(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f) ||
                    !_player.MinecartRideActive || _runtimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != switches ||
                    rom[0xcdd3] != switches || !sounds.Requests.Where(cue=>cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                    $"Skull rail junction room${c.Room:x2}, switch${switches:x2}, batch={batch}, update{update}: cart={cart.Position}/{rom.Word(0xd10c)/256f},{rom.Word(0xd10a)/256f}, direction={cart.Direction}/{rom[0xd108]}, Link={_player.PrecisePosition}/{rom.Word(0xd00c)/256f},{rom.Word(0xd00a)/256f}.");
                if (update == 1) FailIf(cart.Direction != exit || cart.Position != center+(Vector2)directions[exit],
                    "Original switched junction must select its independent source exit before moving one pixel.");
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls,
                    "Original rail junction dispatch must preserve shared RNG consumption.");
            });
            Step(7);
            _dialogue.ShowGameplayMessage("Rail junction pause",120); rom[0xcba0] = 1;
            Step(3); _dialogue.Close(); rom[0xcba0] = 0;
            Step(26);
        }
    }
}
