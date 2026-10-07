using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareColoredFloorScrollRom()
    {
        int fixture = 0;
        foreach (int kind in new[] { 0,1,2,3 }) // $15 parent/landing child; $22 parent/worker.
        foreach (bool initialized in new[] { false,true })
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x79); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            var selected = FindOriginalPatternExitRom();
            _player.WarpTo(selected.Start+new Vector2(0.25f,0.5f)); _player.Face(selected.Direction);
            int angle = selected.Direction == Vector2I.Up ? 0 : selected.Direction == Vector2I.Right ? 8 : selected.Direction == Vector2I.Down ? 16 : 24;
            var rom = new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,angle/8,(int)selected.Start.X,(int)selected.Start.Y);
            rom.Word(0xd00a,(int)selected.Start.Y*256+128); rom.Word(0xd00c,(int)selected.Start.X*256+64);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            StepSomariaMotionRom(rom,9,batched,angle);
            var data = new DungeonInteractionDatabase();
            var record = new SkullDungeonDatabase().GetRoomRecords(4,0x71).Single(row => row.Id == 0x22);
            IRoomEntity actor;
            if (kind == 0) actor = new ToggleFloorRoomEntity(_currentRoom,data,() => _runtimeState.ReadWramByte(WramAddress.wActiveTilePos),
                _entities.TryCreateFloorToggleTile,() => _entities.Entities<ToggleFloorTileRoomEntity>().Count,_entities.IsOutgoingEntity,
                _entities.OnRoomTileChanged,() => 0);
            else if (kind == 1) actor = new ToggleFloorTileRoomEntity(0x43,initialized ? ToggleFloorRoomEntity.LinkTilePosition(_player) : (byte)0,
                0xad,() => _entities.ActiveRoom,_rooms.TrySetTile,_entities.OnSoundRequested);
            else if (kind == 2) actor = new FloorColorChangerRoomEntity(record,() => _entities.ActiveRoom,data,_entities.TryCreateFloorColorWorker,
                () => _entities.Entities<FloorColorWorkerRoomEntity>().Count);
            else actor = new FloorColorWorkerRoomEntity(new(120,88),0x9e,data,() => _entities.ActiveRoom,_rooms.TrySetTile,_random,_runtimeState);
            _entities.AddEntity(actor);
            int address = (0xd0+_entities.InteractionSlot(actor.Node))*256+0x40;
            rom[address] = 1; rom[address+1] = (byte)(kind < 2 ? 0x15 : 0x22); rom[address+2] = (byte)(kind is 1 or 3 ? 1 : 0);
            if (kind == 1) { rom[address+3] = 0x43; rom[address+0x30] = initialized ? ToggleFloorRoomEntity.LinkTilePosition(_player) : (byte)0; }
            if (kind >= 2) { rom[address+0xb] = 0x58; rom[address+0xd] = 0x78; if (kind == 3) rom[address+3] = 0x9e; }
            if (initialized && kind != 1) StepSomariaMotionRom(rom,1,batched);
            _transitions.BeginScroll(_player,selected.Direction,selected.Target);
            var seed = _random.CaptureState(); int nativeCalls = rom.RandomCalls;
            rom[0xff94] = seed.Rng1; rom[0xff95] = seed.Rng2;
            rom.SetOutgoingInteractions(); rom.ClearRoomVariables(true);
            rom[0xcc30] = (byte)selected.Target; rom[0xcd00] = 8;
            // Incoming producers and scroll coordinates are declared inputs.
            // Retained state0 handlers still execute the original dispatcher
            // against the destination's current shared tile buffers.
            foreach (var pair in SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_interactionSlots"))
            {
                if (ReferenceEquals(pair.Key,actor)) continue;
                int slot = (0xd0+pair.Value)*256+0x40;
                rom[slot] = 1; rom[slot+1] = 5; rom[slot+4] = 1;
            }
            _currentRoom.SetPositionTileAndCollision(new(56,72),0xad,null,0);
            _currentRoom.SetPositionTileAndCollision(new(120,88),0xae,null,0);
            rom.CopyRoom(_currentRoom);
            // Neither original owner reads Link's scroll position after the
            // child's first dispatch; retain that captured ground input.
            rom.Word(0xd00a,(int)_player.Position.Y*256); rom.Word(0xd00c,(int)_player.Position.X*256);
            var sounds = _sound.AttachPlayRequestAudit(); int cueStart = rom.Sounds.Count;
            int firstTile = _currentRoom.Layout[0x43], firstUnderlying = _currentRoom.GetUnderlyingStorageMetatile(0x43);
            int count = 0;
            void Step(int updates = 1) => StepGameplayUpdates(updates,Vector2.Zero,batched:batched,afterUpdate:() =>
            {
                rom.AdvanceInteractions(_entities.FrameCounter); count++;
                if (!_transitions.ScrollActive) { rom.ClearOutgoingInteractions(); rom[0xcd00] = 1; }
                bool alive = SomariaPrivate<List<IRoomEntity>>(_entities,"_outgoingEntities").Contains(actor);
                FailIf(alive != (rom[address] != 0) || _currentRoom.Layout[0x43] != rom[0xcf43] ||
                    _currentRoom.GetUnderlyingStorageMetatile(0x43) != rom.Underlying(0x43) ||
                    !sounds.Requests.SequenceEqual(rom.Sounds.Skip(cueStart)),
                    $"Floor scroll kind={kind}, initialized={initialized}, update{count}: lifetime/current-room writes/cues differ.");
                if (alive && kind == 2)
                    FailIf(SomariaPrivate<int>(actor,"_lastControlTile") != rom[address+3],"Retained floor controller must preserve its native color latch.");
                if (alive && kind == 3)
                    FailIf(((FloorColorWorkerRoomEntity)actor).Index != rom[address+6],"Initialized floor worker must retain its counter throughout scrolling.");
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                    random.Calls-seed.Calls != rom.RandomCalls-nativeCalls,$"Floor scroll kind={kind}, initialized={initialized}, update{count}: RNG schedule differs.");
            });
            Step();
            if (kind == 1)
                FailIf(_currentRoom.Layout[0x43] != (initialized ? firstTile : 0xae) ||
                    _currentRoom.GetUnderlyingStorageMetatile(0x43) != (initialized ? firstUnderlying : 0xae),
                    "The outgoing landing child must cancel on takeoff or write the destination despite enabled02.");
            FailIf(rom.RandomCalls-nativeCalls != (kind == 3 && !initialized ? 256 : 0),
                "Only an outgoing state-zero color worker initializes its shared permutation during scrolling.");
            Step(_transitions.ScrollTotalFrames-1);
            FailIf(_transitions.ScrollActive || SomariaPrivate<List<IRoomEntity>>(_entities,"_outgoingEntities").Contains(actor),
                "Scroll completion must release all retained floor owners.");
            LoadValidationRoom(4,0x71);
            FailIf(_entities.Entities<ToggleFloorRoomEntity>().Count != 1 || _entities.Entities<FloorColorChangerRoomEntity>().Count != 1 ||
                _entities.Entities<ToggleFloorTileRoomEntity>().Count != 0 || _entities.Entities<FloorColorWorkerRoomEntity>().Count != 0,
                "Room re-entry must recreate placed controllers without old physical children.");
        }
    }
}
