using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareFloorPatternKeyRom(bool blockPatterns = false)
    {
        // Literal $21:$01/$05/$09/$0e/$10 predicates; actual $60 allocation, initialization,
        // wait and falling physics execute in the original interaction pass.
        int fixture = 0;
        (int Room,int Sub,int First,int X,int Y,byte[][] Pattern)[] cases = blockPatterns ?
        [ (0x64,0x09,0x1d,0xb8,0x68,[[0x3b,0x59,0x5d],[],[]]), (0x61,0x0e,0x2a,0xb8,0x58,[[0x4a],[],[]]) ] :
        [
            (0x7b,0x10,0xad,0x68,0x58,[[0x54,0x58],[],[0x55,0x57]]),
            (0x2e,0x01,0xad,0x58,0x48,[[],[0x67,0x77],[0x68,0x78]]),
            (0x42,0x05,0x2c,0x78,0x58,[[0x49,0x4b,0x69,0x6b],[0x5a],[0x4a,0x59,0x5b,0x6a]])
        ];
        foreach (var test in cases)
        foreach (int mode in new[] { 0,1,2 }) // Ordinary, full pool, collected.
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation();
            byte[][] pattern = test.Pattern;
            _saveData.SetRoomFlag(4,test.Room,OracleSaveData.RoomFlagItem,mode == 2);
            LoadValidationRoom(4,test.Room); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            var mechanic = blockPatterns ? new DungeonMechanicDatabase().GetRoomRecords(4,test.Room).Single(row => row.Id == 0x21 && row.SubId == test.Sub) : default;
            var record = blockPatterns ? new DungeonObjectRecord(4,test.Room,mechanic.Order,DungeonObjectKind.FloorPatternKey,
                0x21,test.Sub,mechanic.PackedPosition,mechanic.Parameter,DungeonObjectCondition.Always,$"dungeon_mechanics.tsv:$21:${test.Sub:x2}") :
                (test.Sub == 0x10 ? new SkullDungeonDatabase().GetRoomRecords(4,test.Room) : new WingDungeonDatabase().GetRoomRecords(4,test.Room))
                    .Single(row => row.SubId == test.Sub && row.Id == 0x21);
            FailIf(record.X != test.X || record.Y != test.Y,"Falling pattern key must retain its original placement.");
            Vector2 safeFloor = Enumerable.Range(1,_currentRoom.HeightInTiles-2).SelectMany(y =>
                Enumerable.Range(1,_currentRoom.WidthInTiles-2).Select(x => new Vector2(x*16+8,y*16+8)))
                .First(point => point.DistanceTo(record.Position) > 48 && !_collision.Collides(point) &&
                    _currentRoom.GetTerrainInfo(point).Hazard == HazardType.None);
            _player.WarpTo(safeFloor); _player.Face(Vector2I.Up);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,(int)safeFloor.X,(int)safeFloor.Y);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation(); rom[0xcc39] = (byte)_rooms.World.GetDungeonIndex(4,test.Room);
            if (mode == 1)
                for (int index = 0; index < 13; index++)
                {
                    // Actual silent puffs release lower slots through their
                    // own expiry; the controller must keep retrying until then.
                    _entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new(224,144),SoundId.MusNone));
                    int address = (0xd2+index)*256+0x40;
                    rom[address] = 1; rom[address+1] = 5; rom[address+2] = 0x80;
                    rom[address+0xb] = 144; rom[address+0xd] = 224;
                }
            var request = new GroundTreasureGrantRequest(4,test.Room,record.Order,test.Y,test.X,"TREASURE_OBJECT_SMALL_KEY_01",record.Source)
            {
                SpawnMode = 2, GrabMode = 2, SpawnDelayFrames = 40, BounceCount = 2, Gravity = 0x10,
                BounceSpeed = -0xaa, SpawnSound = SoundId.SndSolvePuzzle, LandingSound = SoundId.SndDropEssence,
                InitialZAboveScreen = true
            };
            IRoomEntityLifetime controller;
            if (test.Sub == 0x09)
                controller = new DungeonTilePatternFallingKeyRoomEntity(mechanic,new DungeonMechanicDatabase().TilePattern(0x21,0x09),
                    () => _entities.ActiveRoom,request,() => _entities.ActiveRoomHasFlag(_saveData,OracleSaveData.RoomFlagItem),_entities.TryCreateGroundTreasure);
            else if (test.Sub == 0x0e)
                controller = new MoonlitGrottoFallingKeyRoomEntity(mechanic,new DungeonMechanicDatabase(),() => _entities.ActiveRoom,request,
                    () => _entities.ActiveRoomHasFlag(_saveData,OracleSaveData.RoomFlagItem),_entities.TryCreateGroundTreasure);
            else
            {
                var sourcePattern = test.Sub == 0x10 ? new SkullDungeonDatabase().Pattern(0x10) :
                    Enumerable.Range(0,3).Select(color => new WingDungeonDatabase().Pattern(record.Kind,color)).ToArray();
                controller = new DungeonPatternKeyRoomEntity(record,() => _entities.ActiveRoom,test.First,sourcePattern,request,
                    () => _entities.ActiveRoomHasFlag(_saveData,OracleSaveData.RoomFlagItem),_entities.TryCreateGroundTreasure,_entities.IsOutgoingEntity);
            }
            var owner = (IRoomEntity)controller;
            _entities.AddEntity(owner);
            int parent = (0xd0+_entities.InteractionSlot(owner.Node))*256+0x40;
            rom[parent] = 1; rom[parent+1] = 0x21; rom[parent+2] = (byte)test.Sub;
            rom[parent+0xb] = (byte)test.Y; rom[parent+0xd] = (byte)test.X;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            _dialogue.ShowMessage("Declared floor colors.",100); rom[0xcba0] = 1;
            void Tile(int packed,int tile)
            {
                _currentRoom.SetPositionTileAndCollision(new((packed&15)*16+8,(packed>>4)*16+8),(byte)tile,null,0);
                rom.CopyRoom(_currentRoom);
            }
            void Step(int count = 1) => StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:() =>
            {
                // Camera positioning is an explicit presentation input; room
                // identity, Link dispatch and object execution are live.
                Vector2 screenOrigin = _entities.ToScreen(Vector2.Zero);
                rom[0xffaa] = unchecked((byte)-(int)screenOrigin.Y); rom[0xffac] = unchecked((byte)-(int)screenOrigin.X);
                rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter);
                CompareSomariaMotionRom(rom,$"Pattern key$21:${test.Sub:x2} update{++update}, mode={mode}, batch={batched}");
                int[] nativeKeys = Enumerable.Range(0xd2,14).Select(page => page*256+0x40)
                    .Where(address => rom[address] != 0 && rom[address+1] == 0x60).ToArray();
                var keys = _entities.Entities<GroundTreasurePickup>();
                FailIf(controller.Finished != (rom[parent] == 0 || rom[parent+1] != 0x21) || keys.Count != nativeKeys.Length ||
                    !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                    $"Pattern key update{update}: parent lifetime/treasure allocation/cues differ.");
                if (keys.Count == 1)
                {
                    var key = keys[0]; int address = nativeKeys[0];
                    FailIf(_entities.InteractionSlot(key) != (address>>8)-0xd0 || (int)key.State != rom[address+4] ||
                        key.Position != new Vector2(rom[address+0xd],rom[address+0xb]) ||
                        key.State == PickupState.Spawning && key.SpawnSubstate != rom[address+5] ||
                        key.State == PickupState.Spawning && key.SpawnSubstate == 1 && key.SpawnCounter != rom[address+6] ||
                        unchecked((ushort)key.ZFixed) != rom.Word(address+0xe) || key.SpeedZ != unchecked((short)rom.Word(address+0x14)) ||
                        key.Visible != ((rom[address+0x1a]&0x80) != 0),
                        $"Pattern key update{update}: slot/state={_entities.InteractionSlot(key)}/{key.State}:{key.SpawnSubstate}:{key.SpawnCounter}, native=${address>>8:x2}/{rom[address+4]}:{rom[address+5]}:{rom[address+6]}; z/vz={key.ZFixed}/{key.SpeedZ}, native={unchecked((short)rom.Word(address+0xe))}/{unchecked((short)rom.Word(address+0x14))}, visible={key.Visible}/{rom[address+0x1a]:x2}.");
                }
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls,
                    "Pattern key dispatch/falling must preserve shared RNG.");
            });
            for (int color = 0; color < 3; color++) foreach (byte packed in pattern[color]) Tile(packed,test.First+color);
            if (mode == 0)
                for (int color = 0; color < 3; color++) foreach (byte packed in pattern[color])
                {
                    foreach (int wrong in new[] { test.First+(color+1)%3,test.First+(color+2)%3,0xda })
                    { Tile(packed,wrong); Step(); FailIf(controller.Finished,"Every original key-pattern cell must reject other colors/Somaria."); }
                    Tile(packed,test.First+color);
                }
            Step();
            if (mode == 1)
            {
                FailIf(controller.Finished || _entities.Entities<GroundTreasurePickup>().Count != 0,
                    "Full interaction pool must retain the solved key controller before its own deletion.");
                for (int wait = 0; !controller.Finished && wait < 40; wait++) Step();
                FailIf(!controller.Finished || _entities.Entities<GroundTreasurePickup>().Single().State != PickupState.Initializing,
                    "Puff expiry must allocate the key into an already-visited lower slot, deferring state0 until the next pass.");
            }
            Step(3);
            FailIf(mode != 2 && _entities.Entities<GroundTreasurePickup>().Single().SpawnSubstate != 0,
                "Initialized falling key must remain frozen under text before its Solve/counter40 substate.");
            _dialogue.Close(); rom[0xcba0] = 0;
            if (mode != 2)
            {
                Step();
                FailIf(_entities.Entities<GroundTreasurePickup>().Single().SpawnCounter != 40,"Falling key must begin its exact40-update delay after text.");
                var disabled = _entities.InitializedObjectsDisabledSource;
                _entities.InitializedObjectsDisabledSource = () => true; rom[0xcc8a] = 2;
                try { Step(3); }
                finally { _entities.InitializedObjectsDisabledSource = disabled; rom[0xcc8a] = 0; }
                Step(39);
                FailIf(_entities.Entities<GroundTreasurePickup>().Single().SpawnCounter != 1,"Falling key must stay hidden through counter1.");
                Step(); Step(4);
                _entities.InitializedObjectsDisabledSource = () => true; rom[0xcc8a] = 2;
                try { Step(5); }
                finally { _entities.InitializedObjectsDisabledSource = disabled; rom[0xcc8a] = 0; }
                Step(90);
                FailIf(_entities.Entities<GroundTreasurePickup>().Single().State != PickupState.Waiting ||
                    sounds.Requests.Count(cue => cue == SoundId.SndSolvePuzzle) != 1 ||
                    sounds.Requests.Count(cue => cue == SoundId.SndDropEssence) != 2,
                    "Falling key must complete two native bounces without duplicate spawning.");
            }
            else Step(4);
            LoadValidationRoom(4,0x91);
        }
    }
}
