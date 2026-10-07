using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ComparePatternKeyScrollRom(bool blockPatterns = false)
    {
        int fixture = 0;
        var cases = blockPatterns ? new[] { (Room:0x64,Sub:0x09,First:0x1d),(Room:0x61,Sub:0x0e,First:0x2a) } :
            new[] { (Room:0x7b,Sub:0x10,First:0xad),(Room:0x2e,Sub:1,First:0xad),(Room:0x42,Sub:5,First:0x2c) };
        foreach (var test in cases)
        foreach (bool collectedDestination in new[] { false,true })
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,test.Room); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            var selected = FindOriginalPatternExitRom();
            _player.WarpTo(selected.Start+new Vector2(0.25f,0.5f)); _player.Face(selected.Direction);
            int angle = selected.Direction == Vector2I.Up ? 0 : selected.Direction == Vector2I.Right ? 8 : selected.Direction == Vector2I.Down ? 16 : 24;
            var rom = new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,angle/8,(int)selected.Start.X,(int)selected.Start.Y);
            rom.Word(0xd00a,(int)selected.Start.Y*256+128); rom.Word(0xd00c,(int)selected.Start.X*256+64);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            StepSomariaMotionRom(rom,9,batched,angle);
            var mechanic = blockPatterns ? new DungeonMechanicDatabase().GetRoomRecords(4,test.Room).Single(row => row.Id == 0x21 && row.SubId == test.Sub) : default;
            var record = blockPatterns ? new DungeonObjectRecord(4,test.Room,mechanic.Order,DungeonObjectKind.FloorPatternKey,0x21,test.Sub,
                mechanic.PackedPosition,mechanic.Parameter,DungeonObjectCondition.Always,$"dungeon_mechanics.tsv:$21:${test.Sub:x2}") :
                (test.Sub == 0x10 ? new SkullDungeonDatabase().GetRoomRecords(4,test.Room) : new WingDungeonDatabase().GetRoomRecords(4,test.Room))
                    .Single(row => row.Id == 0x21 && row.SubId == test.Sub);
            IReadOnlyList<byte>[] pattern = blockPatterns ? test.Sub == 9 ? [[0x3b,0x59,0x5d],[],[]] : [[0x4a],[],[]] :
                test.Sub == 0x10 ? new SkullDungeonDatabase().Pattern(0x10) :
                    Enumerable.Range(0,3).Select(color => new WingDungeonDatabase().Pattern(record.Kind,color)).ToArray();
            var request = new GroundTreasureGrantRequest(4,test.Room,record.Order,record.Y,record.X,"TREASURE_OBJECT_SMALL_KEY_01",record.Source)
            {
                SpawnMode = 2, GrabMode = 2, SpawnDelayFrames = 40, BounceCount = 2, Gravity = 0x10,
                BounceSpeed = -0xaa, SpawnSound = SoundId.SndSolvePuzzle, LandingSound = SoundId.SndDropEssence, InitialZAboveScreen = true
            };
            IRoomEntityLifetime controller;
            if (test.Sub == 9) controller = new DungeonTilePatternFallingKeyRoomEntity(mechanic,new DungeonMechanicDatabase().TilePattern(0x21,9),
                () => _entities.ActiveRoom,request,() => _entities.ActiveRoomHasFlag(_saveData,OracleSaveData.RoomFlagItem),_entities.TryCreateGroundTreasure);
            else if (test.Sub == 0x0e) controller = new MoonlitGrottoFallingKeyRoomEntity(mechanic,new DungeonMechanicDatabase(),() => _entities.ActiveRoom,
                request,() => _entities.ActiveRoomHasFlag(_saveData,OracleSaveData.RoomFlagItem),_entities.TryCreateGroundTreasure);
            else controller = new DungeonPatternKeyRoomEntity(record,() => _entities.ActiveRoom,test.First,pattern,request,
                () => _entities.ActiveRoomHasFlag(_saveData,OracleSaveData.RoomFlagItem),_entities.TryCreateGroundTreasure,_entities.IsOutgoingEntity);
            var owner = (IRoomEntity)controller;
            _entities.AddEntity(owner);
            int parent = (0xd0+_entities.InteractionSlot(owner.Node))*256+0x40;
            rom[parent] = 1; rom[parent+1] = 0x21; rom[parent+2] = (byte)test.Sub;
            rom[parent+0xb] = (byte)record.Y; rom[parent+0xd] = (byte)record.X;
            _saveData.SetRoomFlag(4,selected.Target,OracleSaveData.RoomFlagItem,collectedDestination);
            rom[0xc900+selected.Target] = _saveData.GetRoomFlags(4,selected.Target);
            _transitions.BeginScroll(_player,selected.Direction,selected.Target);
            rom.SetOutgoingInteractions(); rom.ClearRoomVariables(true);
            rom[0xcc30] = (byte)selected.Target; rom[0xcd00] = 8;
            // Incoming producers are excluded. Declare their actual physical
            // occupancy, then their completed floor publication in both views.
            foreach (var pair in SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_interactionSlots"))
            {
                if (ReferenceEquals(pair.Key,owner)) continue;
                int address = (0xd0+pair.Value)*256+0x40;
                rom[address] = 1; rom[address+1] = 5; rom[address+4] = 1;
            }
            for (int color = 0; color < 3; color++) foreach (byte packed in pattern[color])
                _currentRoom.SetPositionTileAndCollision(new((packed&15)*16+8,(packed>>4)*16+8),(byte)(test.First+color),null,0);
            rom.CopyRoom(_currentRoom);
            var sounds = _sound.AttachPlayRequestAudit();
            bool createsKey = test.Sub != 0x10 && !collectedDestination;
            void Step(int count = 1) => StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:() =>
            {
                rom.AdvanceInteractions(_entities.FrameCounter);
                if (!_transitions.ScrollActive) { rom.ClearOutgoingInteractions(); rom[0xcd00] = 1; }
                var keys = _entities.Entities<GroundTreasurePickup>().Where(key => key.Record.Source == record.Source).ToArray();
                int[] native = Enumerable.Range(0xd2,14).Select(page => page*256+0x40)
                    .Where(address => rom[address] != 0 && rom[address+1] == 0x60).ToArray();
                FailIf(!controller.Finished || rom[parent] != 0 || keys.Length != native.Length || keys.Length != (createsKey ? 1 : 0),
                    $"Outgoing pattern$21:${test.Sub:x2}, destination item={collectedDestination}: enabled02/current-room flag/predicate or treasure lifetime differs.");
                if (createsKey)
                {
                    var key = keys.Single(); int address = native.Single();
                    FailIf(key.Record.Group != 4 || key.Record.Room != selected.Target || key.Position != record.Position ||
                        _entities.InteractionSlot(key) != (address>>8)-0xd0 || key.State != PickupState.Spawning || rom[address+4] != 1 ||
                        key.SpawnSubstate != rom[address+5] || key.SpawnCounter != rom[address+6],
                        "A retained state-zero key controller must bind its newly allocated treasure to the destination and freeze its initialized spawn during scrolling.");
                }
            });
            Step(); Step(_transitions.ScrollTotalFrames-1);
            FailIf(_transitions.ScrollActive || sounds.Requests.Any(cue => cue == SoundId.SndSolvePuzzle) || rom.Sounds.Any(),
                "Pattern-key scroll must complete without advancing the initialized falling-key delay.");
            Step();
            FailIf(sounds.Requests.Count(cue => cue == SoundId.SndSolvePuzzle) != (createsKey ? 1 : 0) ||
                rom.Sounds.Count(cue => cue == SoundId.SndSolvePuzzle) != (createsKey ? 1 : 0),
                "The new key must begin its Solve/counter40 substate only after scrolling completes.");
        }
    }
}
