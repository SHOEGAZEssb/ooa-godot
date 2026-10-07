using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareFloorPatternTriggerRom()
    {
        // Literal dungeonEvents.s $0f table; floor-change producers are declared.
        byte[][] pattern = [[0x43,0x45,0x64],[0x54,0x63,0x65],[0x44,0x53,0x55]];
        int fixture = 0;
        foreach (int gate in new[] { 0,1,2 }) // Normal, text, DISABLE_INTERACTIONS.
        foreach (bool batched in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x79); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(168,120)); _player.Face(Vector2I.Up);
            FailIf(_collision.Collides(_player.Position),"Pattern trigger Link must remain on original room$4:$79 floor.");
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,0,168,120);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            var trigger = new DungeonPatternTriggerRoomEntity(_currentRoom,0xad,
                new SkullDungeonDatabase().Pattern(0x0f),_entities.SetTrigger,_entities.IsOutgoingEntity);
            _entities.AddEntity(trigger);
            int address = (0xd0+_entities.InteractionSlot(trigger))*256+0x40;
            rom[address] = 1; rom[address+1] = 0x21; rom[address+2] = 0x0f;
            var disabled = _entities.InitializedObjectsDisabledSource;
            var sounds = _sound.AttachPlayRequestAudit();
            if (gate == 1) { _dialogue.ShowMessage("Declared floor pattern.",100); rom[0xcba0] = 1; }
            if (gate == 2) { _entities.InitializedObjectsDisabledSource = () => true; rom[0xcc8a] = 2; }
            void Tile(int packed,int tile)
            {
                _currentRoom.SetPositionTileAndCollision(new((packed&15)*16+8,(packed>>4)*16+8),(byte)tile,null,0);
                rom.CopyRoom(_currentRoom);
            }
            void Step(int expected,int count = 1)
            {
                for (int bit = 0; bit < 8; bit++) _entities.SetTrigger(bit,true);
                rom[0xcca0] = 0xff;
                StepSomariaMotionRom(rom,count,batched,afterUpdate:() =>
                {
                    FailIf(trigger.Finished || rom[address] != 1 || rom[address+4] != 0 ||
                        _entities.ActiveTriggers != rom[0xcca0] || _entities.ActiveTriggers != expected ||
                        !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                        $"Floor pattern$21:$0f, gate={gate}, batch={batched}: state0/whole-byte trigger/cues differ.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls,
                        "Floor pattern predicate must preserve shared RNG.");
                });
            }
            try
            {
                for (int color = 0; color < 3; color++)
                    foreach (byte packed in pattern[color]) Tile(packed,0xad+color);
                Step(1,2);
                for (int color = 0; color < 3; color++)
                    foreach (byte packed in pattern[color])
                    {
                        foreach (int wrong in new[] { 0xad+(color+1)%3,0xad+(color+2)%3,0xda })
                        {
                            Tile(packed,wrong); Step(0);
                        }
                        Tile(packed,0xad+color); Step(1);
                    }
                Tile(0x43,0xae); Step(0,2); Tile(0x43,0xad); Step(1,2);
            }
            finally { _entities.InitializedObjectsDisabledSource = disabled; _dialogue.Close(); }
        }
        CompareFloorPatternTriggerScrollRom();
    }

    private void CompareFloorPatternTriggerScrollRom()
    {
        foreach (bool batched in new[] { false,true })
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(4,0x79); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            var selected = FindOriginalPatternExitRom();
            _player.WarpTo(selected.Start+new Vector2(0.25f,0.5f)); _player.Face(selected.Direction);
            int nativeDirection = selected.Direction == Vector2I.Up ? 0 : selected.Direction == Vector2I.Right ? 8 : selected.Direction == Vector2I.Down ? 16 : 24;
            var rom = new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,nativeDirection/8,(int)selected.Start.X,(int)selected.Start.Y);
            rom.Word(0xd00a,(int)selected.Start.Y*256+128); rom.Word(0xd00c,(int)selected.Start.X*256+64);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            StepSomariaMotionRom(rom,9,batched,nativeDirection);
            var trigger = new DungeonPatternTriggerRoomEntity(_currentRoom,0xad,
                new SkullDungeonDatabase().Pattern(0x0f),_entities.SetTrigger,_entities.IsOutgoingEntity);
            _entities.AddEntity(trigger);
            int address = (0xd0+_entities.InteractionSlot(trigger))*256+0x40;
            rom[address] = 1; rom[address+1] = 0x21; rom[address+2] = 0x0f;
            _transitions.BeginScroll(_player,selected.Direction,selected.Target);
            rom.SetOutgoingInteractions(); rom[0xcd00] = 8;
            // Incoming parser/Link scroll coordinates are excluded. A shared
            // signal published after activation must survive outgoing deletion.
            _entities.SetTrigger(7,true); rom[0xcca0] = 0x80;
            StepGameplayUpdates(1,Vector2.Zero,batched:batched,afterUpdate:() =>
            {
                rom.AdvanceInteractions(_entities.FrameCounter);
                FailIf(!trigger.Finished || rom[address] != 0 ||
                    _entities.OutgoingEntities<DungeonPatternTriggerRoomEntity>().Count != 0 ||
                    _entities.ActiveTriggers != rom[0xcca0],
                    "Outgoing enabled$02 pattern trigger must delete before replacing the incoming trigger byte.");
            });
            StepGameplayUpdates(_transitions.ScrollTotalFrames-1,Vector2.Zero,batched:batched);
            FailIf(_transitions.ScrollActive,"Pattern trigger cancellation must finish the actual scroll.");
            LoadValidationRoom(4,0x79);
            FailIf(_entities.Entities<DungeonPatternTriggerRoomEntity>().Count != 1,
                "Uncollected pattern trigger must recreate on original-room re-entry.");
        }
    }

    private (Vector2I Direction,Vector2 Start,int Target) FindOriginalPatternExitRom()
    {
        (Vector2I Direction,Vector2 Start,int Target)? exit = null;
        foreach (Vector2I direction in new[] { Vector2I.Right,Vector2I.Up,Vector2I.Down,Vector2I.Left })
        {
            if (!_rooms.TryGetNeighbor(direction,out int target)) continue;
            bool horizontal = direction.X != 0;
            int length = horizontal ? _currentRoom.Height : _currentRoom.Width;
            for (int coordinate = 24; coordinate < length-16; coordinate += 16)
            {
                Vector2 start = horizontal
                    ? new(direction.X > 0 ? _currentRoom.Width-20 : 20,coordinate)
                    : new(coordinate,direction.Y > 0 ? _currentRoom.Height-20 : 20);
                if (Enumerable.Range(0,13).Any(offset => _collision.Collides(start+(Vector2)direction*offset))) continue;
                exit = (direction,start,target); break;
            }
            if (exit.HasValue) break;
        }
        FailIf(!exit.HasValue,"Pattern cancellation needs a reachable original room exit.");
        return exit!.Value;
    }
}
