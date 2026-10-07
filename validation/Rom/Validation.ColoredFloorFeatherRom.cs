using Godot;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareColoredFloorFeatherRom()
    {
        int fixture = 0;
        foreach (int room in new[] { 0x79,0x7b,0x71,0x3b })
        foreach (bool fullPool in new[] { false,true })
        foreach (bool wetLanding in room == 0x7b && !fullPool ? new[] { false,true } : new[] { false })
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            if (room is 0x71 or 0x3b && fullPool) continue;
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,room); _entities.Clear();
            _inventory.GiveTreasure(TreasureId.Feather,1); _inventory.EquipA(TreasureId.Feather); _inventory.EquipB(0);
            var data = new DungeonInteractionDatabase();
            byte[][] pattern = room == 0x79 ? [[0x43,0x45,0x64],[0x54,0x63,0x65],[0x44,0x53,0x55]] :
                room == 0x7b ? [[0x54,0x58],[],[0x55,0x57]] : room == 0x3b ? [[0x79],[],[]] : [[],[0x57],[]];
            int parentAddress = 0;
            var parent = new ToggleFloorRoomEntity(_currentRoom,data,() => _runtimeState.ReadWramByte(WramAddress.wActiveTilePos),
                _entities.TryCreateFloorToggleTile,() => _entities.Entities<ToggleFloorTileRoomEntity>().Count,_entities.IsOutgoingEntity,
                _entities.OnRoomTileChanged,() => 0);
            for (int color = 0; color < 3; color++) foreach (byte packed in pattern[color])
                _currentRoom.SetPositionTileAndCollision(new((packed&15)*16+8,(packed>>4)*16+8),(byte)(0xad+color),null,0);
            if (room == 0x3b)
            {
                // The original direction-$02 gate writes $05/$00 when
                // closed. Include its actual geometry in the approach search;
                // the native/port state0 below repeats these literal writes.
                _currentRoom.SetPositionTileAndCollision(new(168,120),_currentRoom.GetMetatile(new(168,120)),0x05,0,preserveRenderedTile:true);
                _currentRoom.SetPositionTileAndCollision(new(184,120),0x5e,0x00,0,preserveRenderedTile:true);
            }
            (int Target,int Color,Vector2 Origin,Vector2 Direction)? approach = null;
            for (int color = 0; color < 3 && !approach.HasValue; color++) foreach (byte packed in pattern[color])
            {
                Vector2 point = new((packed&15)*16+8,(packed>>4)*16+8);
                foreach (Vector2 direction in new[] { Vector2.Right,Vector2.Left,Vector2.Down,Vector2.Up })
                {
                    Vector2 origin = point+Vector2.Up-direction*16;
                    Vector2 side = new(-direction.Y,direction.X);
                    if (!Enumerable.Range(0,24).All(offset => Enumerable.Range(-5,11).All(lateral =>
                    {
                        Vector2 probe = origin+direction*offset+side*lateral;
                        return probe.X >= 8 && probe.Y >= 8 && probe.X < _currentRoom.Width-8 && probe.Y < _currentRoom.Height-8 &&
                            !_currentRoom.IsSolid(probe) && _currentRoom.GetTerrainInfo(probe).Hazard == HazardType.None;
                    }))) continue;
                    approach = (packed,color,origin,direction); break;
                }
                if (approach.HasValue) break;
            }
            if (room == 0x3b)
            {
                // $7a's free left half is reachable beside the closed gate;
                // center $a0 avoids its solid right half. Ground contact is
                // checked here and every air/landing update against the ROM.
                approach = (0x79,0,new Vector2(160,119),Vector2.Left);
                FailIf(_collision.Collides(new(160,119.5f)),"Room$3b's original gate must admit the Feather takeoff beside its free left half.");
            }
            FailIf(!approach.HasValue,$"Colored-floor Feather in room $04:${room:x2} must approach through original room collision geometry.");
            var selected = approach!.Value;
            Vector2 fraction = new(room == 0x3b ? 0 : 0.25f,0.5f);
            _player.WarpTo(selected.Origin+fraction); _player.Face((Vector2I)selected.Direction);
            FailIf(_collision.Collides(_player.Position),"Feather approach must start outside the actual original gate/floor collision geometry.");
            int angle = selected.Direction == Vector2.Up ? 0 : selected.Direction == Vector2.Right ? 8 : selected.Direction == Vector2.Down ? 16 : 24;
            int beforeTile = 0xad+(selected.Color+2)%3;
            _currentRoom.SetPositionTileAndCollision(new((selected.Target&15)*16+8,(selected.Target>>4)*16+8),(byte)beforeTile,null,0);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,angle/8,(int)selected.Origin.X,(int)selected.Origin.Y);
            rom.Word(0xd00a,(int)selected.Origin.Y*256+128); rom.Word(0xd00c,(int)selected.Origin.X*256+(int)(fraction.X*256));
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            if (fullPool)
                for (int index = 0; index < 13; index++)
                {
                    _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(224,144),SoundId.MusNone));
                    int address = (0xd2+index)*256+0x40;
                    rom[address] = 1; rom[address+1] = 5; rom[address+2] = 0x80; rom[address+0xb] = 144; rom[address+0xd] = 224;
                }
            if (!fullPool && room == 0x7b)
            {
                var record = new SkullDungeonDatabase().GetRoomRecords(4,room).Single(row => row.SubId == 0x10);
                var request = new GroundTreasureGrantRequest(4,room,0,0x58,0x68,"TREASURE_OBJECT_SMALL_KEY_01",record.Source)
                { SpawnMode = 2,GrabMode = 2,SpawnDelayFrames = 40,BounceCount = 2,Gravity = 0x10,BounceSpeed = -0xaa,
                    SpawnSound = SoundId.SndSolvePuzzle,LandingSound = SoundId.SndDropEssence,InitialZAboveScreen = true };
                var key = new DungeonPatternKeyRoomEntity(record,() => _entities.ActiveRoom,0xad,new SkullDungeonDatabase().Pattern(0x10),request,
                    () => _entities.ActiveRoomHasFlag(_saveData,OracleSaveData.RoomFlagItem),_entities.TryCreateGroundTreasure,_entities.IsOutgoingEntity);
                _entities.AddEntity(key); rom[0xd240] = 1; rom[0xd241] = 0x21; rom[0xd242] = 0x10; rom[0xd24b] = 0x58; rom[0xd24d] = 0x68;
            }
            if (room == 0x71)
            {
                var record = new SkullDungeonDatabase().GetRoomRecords(4,room).Single(row => row.Id == 0x22);
                _entities.AddEntity(new FloorColorChangerRoomEntity(record,() => _entities.ActiveRoom,data,_entities.TryCreateFloorColorWorker,
                    () => _entities.Entities<FloorColorWorkerRoomEntity>().Count));
                rom[0xd240] = 1; rom[0xd241] = 0x22; rom[0xd24b] = 0x58; rom[0xd24d] = 0x78;
            }
            _entities.AddEntity(parent);
            parentAddress = (0xd0+_entities.InteractionSlot(parent))*256+0x40;
            rom[parentAddress] = 1; rom[parentAddress+1] = 0x15;
            MinecartGateRoomEntity? gate = null;
            if (room == 0x3b)
            {
                var records = new WingDungeonDatabase().GetRoomRecords(4,room);
                var sensor = records.Single(row => row.Id == 0x21 && row.SubId == 7);
                var puzzle = new ColoredCubePuzzleState(_runtimeState,data.Constant("red-pushable-block"));
                _entities.AddEntity(new DungeonStateController(sensor,() => _entities.ActiveRoom,data,puzzle,_runtimeState,_entities.SetTrigger));
                rom[0xd340] = 1; rom[0xd341] = 0x21; rom[0xd342] = 7; rom[0xd34b] = (byte)sensor.Y; rom[0xd34d] = (byte)sensor.X;
                var record = records.Single(row => row.Id == 0x1b);
                gate = new MinecartGateRoomEntity(record,() => _entities.ActiveRoom,_runtimeState,
                    new DungeonInteractionVisualDatabase().Visual("minecart-gate"),_entities.OnSoundRequested,_entities.OnRoomTileChanged,() => 0);
                _entities.AddEntity(gate);
                rom[0xd440] = 1; rom[0xd441] = 0x1b; rom[0xd442] = (byte)record.SubId; rom[0xd44b] = (byte)record.Y; rom[0xd44d] = (byte)record.X;
            }
            if (!fullPool && room == 0x79)
            {
                var trigger = new DungeonPatternTriggerRoomEntity(_currentRoom,0xad,new SkullDungeonDatabase().Pattern(0x0f),_entities.SetTrigger,_entities.IsOutgoingEntity);
                _entities.AddEntity(trigger); rom[0xd340] = 1; rom[0xd341] = 0x21; rom[0xd342] = 0x0f;
            }
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,int movement = 0xff,bool jump = false)
            {
                int tick = 0, keys = movement switch { 0 => 0x40,8 => 0x10,16 => 0x80,24 => 0x20,_ => 0 };
                StepGameplayUpdates(count,movement == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(movement),
                    jump ? ["attack"] : [],jump ? ["attack"] : [],batched,afterUpdate:() =>
                {
                    rom.UpdateGameplay(jump && tick++ == 0 ? 1 : 0,keys|(jump ? 1 : 0),movement,_entities.FrameCounter); rom.AdvanceTileGraphics();
                    CompareSomariaMotionRom(rom,$"Colored floor$15 actual Feather update{++update}, room=${room:x2}, full={fullPool}, batch={batched}");
                    ComparePhysicalSplashesRom(rom,$"Colored floor$15 wet={wetLanding}, update{update}");
                    int countNative = Enumerable.Range(0xd2,14).Count(page => rom[page*256+0x40] != 0 && rom[page*256+0x41] == 0x15 && rom[page*256+0x42] == 1);
                    FailIf(parent.PendingCount != countNative || SomariaPrivate<int>(parent,"_lastTilePosition") != rom[parentAddress+0x30] ||
                        _entities.ActiveTriggers != rom[0xcca0] || _rooms.PendingTileGraphics != ((rom[0xcce0]-rom[0xccdf])&31) ||
                        !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                        $"Floor$15 room=${room:x2}, full={fullPool}, update={update}: pending={parent.PendingCount}/{countNative}, var30=${SomariaPrivate<int>(parent,"_lastTilePosition"):x2}/${rom[parentAddress+0x30]:x2}, active=${_runtimeState.ReadWramByte(WramAddress.wActiveTilePos):x2}/${rom[0xcc99]:x2}, cues=[{string.Join(',',sounds.Requests.Where(cue => cue != SoundId.SndText))}]/[{string.Join(',',rom.Sounds)}].");
                    var random = _random.CaptureState();
                    if (gate is not null)
                    {
                        CompareMinecartGateStateRom(gate,rom,0xd440,$"actual room$3b Feather update{update}");
                        FailIf(_runtimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != rom[0xcdd3],
                            "Actual Feather landing must publish the floor signal in physical order before the gate consumes it.");
                    }
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls,
                        "Colored floor Feather must preserve shared RNG.");
                });
            }
            _dialogue.ShowMessage("Pending floor initialization.",100); rom[0xcba0] = 1; Step(3);
            FailIf(rom[parentAddress+4] != 1,"Floor parent must initialize state0 under text, then freeze state1.");
            _dialogue.Close(); rom[0xcba0] = 0; Step();
            Step(movement:angle,jump:true); Step(14,angle);
            FailIf(!_player.TopDownAirborne,"Floor crossing must use the actual Feather jump.");
            // Opposite input during descent brakes Link inside the original
            // narrow floor strip; releasing input alone retains air momentum.
            if (!wetLanding) Step(4,angle^16);
            for (int wait = 0; _player.TopDownAirborne && wait < 80; wait++) Step();
            FailIf(_player.TopDownAirborne || parent.PendingCount != 0 ||
                _currentRoom.GetMetatile(new((selected.Target&15)*16+8,(selected.Target>>4)*16+8)) != (fullPool ? beforeTile : 0xad+selected.Color),
                $"Landing room=${room:x2}, origin={selected.Origin}, XY={_player.Position}, angle={angle}, pending={parent.PendingCount}, active=${_runtimeState.ReadWramByte(WramAddress.wActiveTilePos):x2}, floor=${_currentRoom.GetMetatile(new((selected.Target&15)*16+8,(selected.Target>>4)*16+8)):x2}: consume physical child, or retain after allocation failure.");
            if (!fullPool && room == 0x79)
            {
                FailIf(_entities.ActiveTriggers != 0,"The higher landing child must write after the earlier pattern trigger.");
                Step(); FailIf(_entities.ActiveTriggers != 1,"Pattern trigger must observe the completed floor on the next dispatch.");
            }
            if (!fullPool && room == 0x7b)
            {
                if (wetLanding)
                    FailIf(!_player.IsDrowning || !_entities.Entities<SplashEffect>().Any() ||
                        sounds.Requests.ToList().IndexOf(SoundId.SndGetSeed) >= sounds.Requests.ToList().IndexOf(SoundId.SndSplash),
                        "Real water landing must initialize its higher splash after the earlier landing child publishes Seed, with physical pool ownership.");
                FailIf(_entities.Entities<GroundTreasurePickup>().Count != 0,"The earlier pattern key must precede the higher landing child.");
                Step();
                FailIf(_entities.Entities<GroundTreasurePickup>().Single().State != PickupState.Spawning || rom[0xd441] != 0x60 || rom[0xd444] != 1,
                    "Next-pass pattern completion must create and initialize its higher-slot key in the same pass.");
            }
            if (room == 0x71)
            {
                FailIf(_entities.Entities<FloorColorWorkerRoomEntity>().Count != 0,"The earlier color controller must precede the higher landing child.");
                Step();
                FailIf(_entities.Entities<FloorColorWorkerRoomEntity>().Count != 1 || rom[0xd441] != 0x22 || rom[0xd442] != 1 || rom[0xd444] != 1,
                    "Next-pass floor-color change must create and initialize its higher-slot worker in the same pass.");
            }
            if (room == 0x3b)
            {
                FailIf(gate!.Open,"The higher landing child writes after the earlier floor sensor and gate.");
                Step(); FailIf(!gate.Open || !gate.Animating,"The following pass must open the gate from actual red-floor landing.");
                Step(16);
                FailIf(gate.Animating,"The physical gate must complete its native opening after the Feather landing handoff.");
            }
            Step(4);
            if (room == 0x71)
            {
                // Walk off the control tile, then cross it again through the
                // same original corridor while the older worker is still live.
                Step(20,angle);
                Step(movement:angle^16,jump:true); Step(14,angle^16); Step(4,angle);
                for (int wait = 0; _player.TopDownAirborne && wait < 80; wait++) Step();
                FailIf(_player.TopDownAirborne || _currentRoom.Layout[0x57] != 0xaf,
                    "Repeated Feather crossing must cycle the original control tile to blue.");
                Step();
                FailIf(_entities.Entities<FloorColorWorkerRoomEntity>().Count != 1 ||
                    SomariaPrivate<byte>(_entities.Entities<FloorColorWorkerRoomEntity>().Single(),"_targetTile") != 0x9f,
                    "The new control color must replace the older worker through the live interaction pass.");
                Step(64);
                FailIf(_entities.Entities<FloorColorWorkerRoomEntity>().Count != 0,
                    "The repeated physical floor worker must complete its bounded propagation.");
            }
        }
    }
}
