using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareOrbBridgeScrollRom()
    {
        int fixture = 0;
        foreach (bool completed in new[] { false,true })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(2,0x9e,OracleSaveData.RoomFlag40,!completed);
            _saveData.SetRoomFlag(4,0xa6,OracleSaveData.RoomFlag40,completed);
            LoadValidationRoom(4,0xa5); _entities.Clear();
            _inventory.EquipA(0); _inventory.EquipB(0);
            _player.WarpTo(new(220.25f,88.5f)); _player.Face(Vector2I.Right);
            FailIf(_collision.Collides(_player.Position),"Orb-controller scroll must approach the original room$4:$a5 exit floor.");
            var rom = new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,1,220,88);
            rom.Word(0xd00a,88*256+128); rom.Word(0xd00c,220*256+64);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            StepSomariaMotionRom(rom,9,batch,8);
            var record = new DungeonMechanicDatabase().GetRoomRecords(2,0x9e).Single(row => row.Id == 0xdc);
            // Declare this source handler pending after the outgoing object
            // pass, opposing its source room flag and the live destination.
            var controller = new OrbBridgeControllerRoomEntity(record,_saveData,_runtimeState,_sound.PlaySound,
                SoundId.SndSolvePuzzle,_rooms,_entities.TryCreateBridgeSpawner);
            _entities.AddEntity(controller);
            rom[0xd240] = 1; rom[0xd241] = 0xdc; rom[0xd242] = 0x12; rom[0xd24b] = 0x13;
            FailIf(!_rooms.TryGetNeighbor(Vector2I.Right,out int target) || target != 0xa6,"Original Crown scroll adjacency must be$a5 -> $a6.");
            _transitions.BeginScroll(_player,Vector2I.Right,target);
            rom.SetOutgoingInteractions(); rom[0xcd00] = 8; rom[0xcc30] = 0xa6;
            rom[0xc9a6] = (byte)_saveData.GetRoomFlags(4,0xa6); // Destination load's visited bit.
            const BindingFlags flags = BindingFlags.Instance|BindingFlags.NonPublic;
            var slots = (Dictionary<IRoomEntity,int>)typeof(RoomEntityManager).GetField("_partSlots",flags)!.GetValue(_entities)!;
            // Incoming parsing has separate coverage. Represent its physical
            // PART allocations with initialized inert native reservations.
            foreach (int index in slots.Values)
            {
                int address = 0xd0c0+index*0x100;
                rom[address] = 1; rom[address+1] = 0x0a; rom[address+4] = 1;
            }
            int bridgeSlot = Enumerable.Range(0,16).First(index => !slots.Values.Contains(index));
            int bridgeAddress = 0xd0c0+bridgeSlot*0x100;
            _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress,0x80); rom[0xcdd2] = 0x80;
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1) => StepGameplayUpdates(count,Vector2.Zero,batched:batch,afterUpdate:() => {
                rom.AdvanceParts(_entities.FrameCounter); rom.AdvanceInteractions(_entities.FrameCounter);
                if (!_transitions.ScrollActive) { rom.ClearOutgoingInteractions(); rom.ClearOutgoingParts(); rom[0xcd00] = 4; }
                var part = _entities.Entities<BridgeSpawnerRoomEntity>().SingleOrDefault();
                bool nativePart = rom[bridgeAddress] != 0 && rom[bridgeAddress+1] == 0x0c;
                FailIf(_entities.OutgoingEntities<Node2D>().Contains(controller.Node) != (rom[0xd240] != 0) ||
                    (part != null) != nativePart || _saveData.GetRoomFlags(4,0xa6) != rom[0xc9a6] ||
                    !sounds.Requests.SequenceEqual(rom.Sounds),
                    $"Orb-controller scroll completed={completed}, batch={batch}, update{++update}: controller={controller.Finished}/{rom[0xd240]:x2}, PART={part != null}/{rom[bridgeAddress]:x2}:{rom[bridgeAddress+1]:x2} slot={bridgeSlot}, flags={_saveData.GetRoomFlags(4,0xa6):x2}/{rom[0xc9a6]:x2}, cues=[{string.Join(',',sounds.Requests)}]/[{string.Join(',',rom.Sounds)}].");
                if (part != null) FailIf(SomariaPrivate<int>(part,"_counter") != rom[bridgeAddress+6] ||
                    SomariaPrivate<bool>(part,"_initialized") != (rom[bridgeAddress+4] != 0),
                    "Pending bridge must initialize once during scrolling and retain its state1 counter until scrolling completes.");
            });
            Step();
            FailIf(!controller.Finished || (!completed && rom[bridgeAddress+4] != 0),
                "INTERAC$dc:$12 must read the active destination flag and allocate after the scrolling PART pass.");
            Step(_transitions.ScrollTotalFrames-1);
            FailIf(_transitions.ScrollActive || !completed && rom[bridgeAddress+6] != 7,
                "Scrolling must retain a new bridge's counter$07 after its pending initialization update.");
            Step(2);
        }
    }
}
