using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareOrbBridgeAllocationRom()
    {
        foreach (bool batch in new[] { false,true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(2,0x9e,OracleSaveData.RoomFlag40,false);
            LoadValidationRoom(2,0x9e);
            _player.WarpTo(new(72.25f,104.5f)); _player.Face(Vector2I.Left);
            FailIf(_collision.Collides(_player.Position),"Bridge allocation fixture must start on original floor$64.");
            var controller = _entities.Entities<OrbBridgeControllerRoomEntity>().Single();
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,3,72,104) { HostilePartsEnabled = true };
            rom.Word(0xd00a,104*256+128); rom.Word(0xd00c,72*256+64);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xd240] = 1; rom[0xd241] = 0xdc; rom[0xd242] = 0x12; rom[0xd24b] = 0x13;
            rom[0xd0c0] = 1; rom[0xd0c1] = 3; rom[0xd0cb] = 104; rom[0xd0cd] = 24;
            const BindingFlags flags = BindingFlags.Instance|BindingFlags.NonPublic;
            var slots = (Dictionary<IRoomEntity,int>)typeof(RoomEntityManager).GetField("_partSlots",flags)!.GetValue(_entities)!;
            var reservations = new List<EnemyAiSlotReservation>();
            for (int index = 1; index < 16; index++)
            {
                var reservation = new EnemyAiSlotReservation(); reservations.Add(reservation); slots.Add(reservation,index);
                int address = 0xd0c0+index*0x100;
                rom[address] = 1; rom[address+1] = 0x0a; rom[address+4] = 1; // partCodeNil: RET.
            }
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1) => StepGameplayUpdates(count,Vector2.Zero,batched:batch,afterUpdate:() => {
                rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter);
                CompareSomariaMotionRom(rom,$"Bridge allocation batch={batch}, update{++update}");
                rom.AdvanceTileGraphics();
                var part = _entities.Entities<BridgeSpawnerRoomEntity>().SingleOrDefault();
                FailIf(controller.Finished != (rom[0xd240] == 0) ||
                    (part != null) != (rom[0xd1c0] != 0 && rom[0xd1c1] == 0x0c) ||
                    _saveData.GetRoomFlags(2,0x9e) != rom[0xc79e] ||
                    !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                    $"Bridge allocation update{update}: controller={controller.Finished}/{rom[0xd240]:x2}, PART={part != null}/{rom[0xd1c0]:x2}:{rom[0xd1c1]:x2}, flags={_saveData.GetRoomFlags(2,0x9e):x2}/{rom[0xc79e]:x2}, cues=[{string.Join(',',sounds.Requests)}]/[{string.Join(',',rom.Sounds)}].");
                if (part != null) FailIf(SomariaPrivate<bool>(part,"_initialized") != (rom[0xd1c4] != 0) ||
                    SomariaPrivate<int>(part,"_counter") != rom[0xd1c6] || SomariaPrivate<int>(part,"_remaining") != rom[0xd1c7],
                    "New bridge state0 must initialize and decrement once under text, then freeze in state1.");
                var random = _random.CaptureState();
                FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls,
                    "Bridge allocation attempts must preserve original global RNG consumption.");
            });
            try
            {
                // Another writer's bit7: the handler tests the entire byte.
                _runtimeState.SetWramByte(OracleRuntimeState.ToggleBlocksStateAddress,0x80); rom[0xcdd2] = 0x80;
                _dialogue.ShowGameplayMessage("Full bridge PART pool",120); rom[0xcba0] = 1;
                Step(3);
                FailIf(controller.Finished || sounds.Requests.Any(),"Failed bridge allocation must retry silently even under text.");
                slots.Remove(reservations[0]); reservations[0].Node.Free(); reservations.RemoveAt(0);
                for (int offset = 0; offset < 0x40; offset++) rom[0xd1c0+offset] = 0;
                Step();
                FailIf(!controller.Finished || rom[0xd1c4] != 0,"Successful controller retry must allocate a pending bridge after the PART pass.");
                Step(3);
                FailIf(rom[0xd1c6] != 7 || sounds.Requests.Count(cue => cue != SoundId.SndText) != 1,"Text must admit only the bridge's state0 fallthrough update.");
            }
            finally
            {
                foreach (var reservation in reservations) { slots.Remove(reservation); reservation.Node.Free(); }
                for (int index = 2; index < 16; index++) rom[0xd0c0+index*0x100] = 0;
            }
            _dialogue.Close(); rom[0xcba0] = 0;
            Step(7);
            FailIf(_currentRoom.GetMetatile(new(56,24)) != 0x6e ||
                sounds.Requests.Count(cue => cue == SoundId.SndDoorClose) != 1,"Bridge must resume its retained counter and publish one half before cancellation.");
            LoadValidationRoom(2,0x9f); LoadValidationRoom(2,0x9e);
            FailIf(_runtimeState.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress) != 0 ||
                Enumerable.Range(3,6).Any(x => _currentRoom.GetMetatile(new(x*16+8,24)) != 0x6d) ||
                _entities.Entities<BridgeSpawnerRoomEntity>().Count != 0,
                "Interrupted bridge construction must reconstruct all six completed tiles from flag$40 on re-entry.");
        }
    }
}
