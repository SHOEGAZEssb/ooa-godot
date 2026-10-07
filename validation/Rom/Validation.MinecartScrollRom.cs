using Godot;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareMinecartScrollRom()
    {
        int fixture = 0;
        foreach (var c in new[] {
            (Room:0x89,Switch:0,X:120,Y:144,Direction:2,Destination:0x8f),
            (Room:0x89,Switch:4,X:152,Y:144,Direction:2,Destination:0x8f),
            (Room:0x8f,Switch:0,X:40,Y:144,Direction:2,Destination:0x92),
            (Room:0x8f,Switch:8,X:120,Y:24,Direction:0,Destination:0x89) })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation();
            _runtimeState.SetWramByte(OracleRuntimeState.SwitchStateAddress,(byte)(0x80|c.Switch));
            LoadValidationRoom(4,c.Room); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            Vector2 start = new(c.X,c.Y);
            // Declare a mounted cart on the original edge track. The
            // reachable mounting producer has separate native coverage.
            MinecartRuntimeState.Reset(_runtimeState,[]);
            MinecartRuntimeState.BeginRide(_runtimeState,0,c.Room,start,c.Direction);
            var cart = new MinecartRoomEntity(new ActiveMinecart(-1,c.Room,c.Y,c.X,c.Direction,true),
                _currentRoom,new DungeonInteractionDatabase(),_runtimeState,
                new DungeonInteractionVisualDatabase().Visual("minecart"),_sound.PlaySound);
            _entities.AddEntity(cart);
            _player.WarpTo(start+new Vector2(0.25f,0.5f)); _player.Face(c.Direction == 2 ? Vector2I.Down : Vector2I.Up);
            _player.FinishMinecartMount(start,c.Direction,0);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,c.Direction,c.X,c.Y) {CompanionDispatchEnabled = true};
            rom[0xd00a] = 0x80; rom[0xd00c] = 0x40;
            rom.InitializeLinkGameplay(); rom[0xd004] = 0; rom[0xd009] = 0;
            rom[0xcdd3] = (byte)(0x80|c.Switch);
            rom[0xd100] = 3; rom[0xd101] = 0x0a;
            rom[0xd108] = (byte)c.Direction; rom[0xd109] = (byte)(c.Direction*8);
            rom[0xd10b] = (byte)c.Y; rom[0xd10d] = (byte)c.X;
            rom.CreateMenuView().LoadDungeon(4);
            rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter);
            var frontend = SomariaPrivate<FrontendRom>(rom,"_rom");
            rom[0xcd04] = 2; rom[0xcd0c] = 234; rom[0xcd0d] = 169;
            var graphics = new ScreenTransitionGraphicsDatabase();
            int sourceUnique = graphics.ForTileset(_currentRoom.TilesetId).Unique |
                (_currentRoom.LoadsUniqueGraphicsAfterScroll ? 0x80 : 0);
            var sounds = _sound.AttachPlayRequestAudit();
            int update = 0;
            void Compare(string stage)
            {
                // World positions may cross zero; native XY wraps in its
                // unsigned 16-bit fixed-point storage during that same exit.
                static Vector2 NativePoint(Vector2 point) => new(
                    ((int)(point.X*256)&0xffff)/256f,((int)(point.Y*256)&0xffff)/256f);
                Vector2 nativeCart = new(rom.Word(0xd10c)/256f,rom.Word(0xd10a)/256f);
                Vector2 nativeLink = new(rom.Word(0xd00c)/256f,rom.Word(0xd00a)/256f);
                FailIf(NativePoint(cart.ScreenTransitionPosition) != nativeCart || cart.Direction != rom[0xd108] ||
                    NativePoint(_player.PrecisePosition) != nativeLink || !_player.MinecartRideActive ||
                    !sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Minecart room${c.Room:x2}, {stage}, batch={batch}, update{++update}: cart={cart.ScreenTransitionPosition}/{nativeCart}, Link={_player.PrecisePosition}/{nativeLink}, direction={cart.Direction}/{rom[0xd108]}, cues=[{string.Join(',',sounds.Requests)}]/[{string.Join(',',rom.Sounds)}].");
            }
            for (int travel = 0; !_transitions.ScrollActive && travel < 40; travel++)
                StepGameplayUpdates(1,Vector2.Zero,batched:batch,afterUpdate:()=> {
                    rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter-1,
                        afterInteractions:()=>frontend.Call(0x4000,1));
                    rom.AdvanceTileGraphics(); rom.CheckTileWarps(); rom.SelectNextActiveRoom();
                    Compare("exit approach");
                });
            FailIf(!_transitions.ScrollActive || _currentRoom.Id != c.Destination || rom[0xcc30] != c.Destination || rom[0xcd02] != c.Direction,
                $"Mounted cart must open the original shutter and request imported adjacency$4:${c.Room:x2} -> $4:${c.Destination:x2}.");
            int unique = graphics.ForTileset(_currentRoom.TilesetId).Unique |
                (_currentRoom.LoadsUniqueGraphicsAfterScroll ? 0x80 : 0);
            var scroll = new ScrollRom(true,c.Direction,rom.Word(0xd00c),rom.Word(0xd00a),unique,sourceUnique);
            scroll[0xcc2c] = 0xd1; scroll[0xcc39] = 4;
            for (int address = 0xd000; address < 0xd140; address++) scroll[address] = rom[address];
            // Execute original incoming substitutions/parser/state0. Enemy
            // dispatch/pixels are excluded from this companion handoff check.
            rom.SetOutgoingInteractions(); rom.ClearRoomVariables(scrolling:true); rom.CopyRoom(_currentRoom);
            for (int packed = 0; packed < 0xb0; packed++)
                rom[0xcf00+packed] = _currentRoom.GetOriginalMetatile(new((packed&15)*16+8,(packed>>4)*16+8));
            rom[0xcd00] = 8; rom[0xcc05] = 0xff;
            frontend.LoadRoomTileset(); frontend.ApplyRoomTileSubstitutions(); frontend.Call(0x55b7,0x12);
            rom.AdvanceInteractions(_entities.FrameCounter);
            void CompareDoorAllocations()
            {
                var doors = _entities.Entities<MinecartShutterRoomEntity>().OrderBy(_entities.InteractionSlot).ToArray();
                int[] slots = Enumerable.Range(0xd2,14).Select(page=>page*256+0x40)
                    .Where(address=>(rom[address]&3) == 1 && rom[address+1] == 0x1e &&
                        (rom[address+2] == 0 || rom[address+2] is >= 0x0c and <= 0x0f)).ToArray();
                FailIf(doors.Length != slots.Length,
                    $"Minecart incoming shutter allocation room${c.Destination:x2}, direction{c.Direction}: source={doors.Length}, ROM={slots.Length}.");
                for (int index = 0; index < slots.Length; index++)
                    FailIf(_entities.InteractionSlot(doors[index]) != (slots[index]>>8)-0xd0 ||
                        // INTERAC$1e's scroll gate holds even state0. Its
                        // packed input remains in yh until initialization.
                        doors[index].PackedPosition != rom[slots[index]+(rom[slots[index]+4] == 0 ? 0xb : 0x3e)] ||
                        (doors[index].State == MinecartShutterState.Initialize) != (rom[slots[index]+4] == 0) ||
                        SomariaPrivate<int>(doors[index],"_counter") != rom[slots[index]+6],
                        $"Incoming minecart shutters room${c.Destination:x2}: slot={_entities.InteractionSlot(doors[index])}/${slots[index]:x4}, packed=${doors[index].PackedPosition:x2}/${rom[slots[index]+0x3e]:x2}, counter={SomariaPrivate<int>(doors[index],"_counter")}/{rom[slots[index]+6]}, state={doors[index].State}/${rom[slots[index]+4]:x2}, sub=${rom[slots[index]+2]:x2}, XY={rom[slots[index]+0xd]},{rom[slots[index]+0xb]}.");
            }
            CompareDoorAllocations();
            void Step(int count) => StepGameplayUpdates(count,Vector2.Zero,batched:batch,afterUpdate:()=> {
                bool frozen = scroll[0xcd04] != 2;
                rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter-1,afterInteractions:()=> {
                    if (frozen)
                    {
                        scroll.Update();
                        for (int address = 0xd10a; address < 0xd10e; address++) rom[address] = scroll[address];
                        if (scroll[0xcd04] == 2)
                        {
                            rom.ClearOutgoingInteractions(); rom[0xcd00] = 1;
                        }
                    }
                });
                Compare(frozen ? "scroll" : "arrival");
                CompareDoorAllocations();
                FailIf(_transitions.ScrollActive != (scroll[0xcd04] != 2) || cart.Direction != c.Direction,
                    "Mounted cart must retain direction and freeze movement until the native scroll finisher completes.");
            });
            Step(_transitions.ScrollTotalFrames); Step(3);
            FailIf(_transitions.ScrollActive || !_player.MinecartRideActive || _currentRoom.Id != c.Destination,
                "Completed minecart scrolling must retain the companion owner and resume on the original destination track.");
        }
    }
}
