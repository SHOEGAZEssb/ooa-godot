using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void CompareNuunSwitchAllocationRom()
    {
        static Vector2 Point(int packed) => new((packed&15)*16+8,(packed>>4)*16+8);
        foreach (bool batch in new[] { false,true })
        {
            ReinitializeGameplayForValidation();
            _saveData.SetRoomFlag(0,0x54,OracleSaveData.RoomFlag40,false);
            _runtimeState.SetWramByte(OracleRuntimeState.SwitchStateAddress,0xff);
            LoadValidationRoom(0,0x54);
            FailIf(_runtimeState.ReadWramByte(OracleRuntimeState.SwitchStateAddress) != 0,
                "Native room initialization must clear wSwitchState before actor allocation.");
            _player.WarpTo(new Vector2(24.25f,104.5f)); _player.Face(Vector2I.Right);
            var bridge = _entities.Entities<NuunBridgeRoomEntity>().Single();
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,1,24,104) { HostilePartsEnabled = true };
            rom.Word(0xd00a,104*256+128); rom.Word(0xd00c,24*256+64);
            rom.InitializeLinkGameplay(); rom.InitializeLinkWalkingAnimation();
            rom[0xd240] = 1; rom[0xd241] = 0x6b; rom[0xd242] = 0x0f;
            rom[0xd24b] = 104; rom[0xd24d] = 136;
            const BindingFlags flags = BindingFlags.Instance|BindingFlags.NonPublic;
            var slots = (Dictionary<IRoomEntity,int>)typeof(RoomEntityManager).GetField("_partSlots",flags)!.GetValue(_entities)!;
            var reservations = new List<EnemyAiSlotReservation>();
            for (int index = 0; index < 16; index++)
            {
                var reservation = new EnemyAiSlotReservation();
                reservations.Add(reservation); slots.Add(reservation,index);
                int address = 0xd0c0+index*0x100;
                rom[address] = 1; rom[address+1] = 0x0a; rom[address+4] = 1; // native partCodeNil: RET
            }
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1)
            {
                StepGameplayUpdates(count,Vector2.Zero,batched:batch,afterUpdate:() => {
                    rom.UpdateGameplay(0,0,0xff,_entities.FrameCounter);
                    CompareSomariaMotionRom(rom,$"Nuun full-pool/queue update{++update}, batch={batch}");
                    rom.AdvanceTileGraphics();
                    FailIf(_entities.Entities<DungeonSwitchRoomEntity>().Count != 0 ||
                        Enumerable.Range(0,16).Any(index => rom[0xd0c0+index*0x100] != 0 && rom[0xd0c1+index*0x100] == 5) ||
                        bridge.Finished != (rom[0xd240] == 0) || !bridge.Finished &&
                        (SomariaPrivate<int>(bridge,"_state") != rom[0xd244] || SomariaPrivate<int>(bridge,"_counter") != rom[0xd246]) ||
                        _saveData.GetRoomFlags(0,0x54) != rom[0xc754] || !sounds.Requests.SequenceEqual(rom.Sounds),
                        $"Nuun full-pool/queue update{update}: allocation, script yields, flag$40 or sound order differs.");
                    var random = _random.CaptureState();
                    FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] || random.Calls-seed.Calls != rom.RandomCalls,
                        "Failed Nuun switch allocation must preserve native RNG consumption.");
                });
            }
            try
            {
                _dialogue.ShowGameplayMessage("Full Nuun PART pool",120); rom[0xcba0] = 1;
                Step(3);
            }
            finally
            {
                for (int index = 0; index < reservations.Count; index++)
                {
                    slots.Remove(reservations[index]); reservations[index].Node.Free();
                    rom[0xd0c0+index*0x100] = 0;
                }
            }
            _dialogue.Close(); rom[0xcba0] = 0; Step(3);
            FailIf(SomariaPrivate<int>(bridge,"_state") != 1,
                "Freeing PART slots must not retry the failed state0 switch allocation.");
            // Declare the shared signal independently of the unavailable switch.
            _runtimeState.SetWramByte(OracleRuntimeState.SwitchStateAddress,1); rom[0xcdd3] = 1;
            Step();
            byte untouched = _currentRoom.GetMetatile(Point(0x68));
            byte tile = _currentRoom.GetMetatile(Point(0x11));
            for (int index = 0; index < 31; index++)
            {
                FailIf(!_rooms.TrySetTile(0x11,tile),"Nuun queue fixture must accept all31 declared writes.");
                rom.SetTile(0x11,tile);
            }
            Step();
            FailIf(SomariaPrivate<int>(bridge,"_counter") != 40 || _currentRoom.GetMetatile(Point(0x68)) != untouched ||
                !_saveData.HasRoomFlag(0,0x54,OracleSaveData.RoomFlag40),
                "Rejected ss_settile must still install wait40 and preserve flag$40 without retrying the tile command.");
            for (int wait = 0; !bridge.Finished && wait < 176; wait++) Step();
            FailIf(!bridge.Finished || bridge.DisablesMovement || _currentRoom.GetMetatile(Point(0x68)) != untouched ||
                sounds.Requests.Count(cue => cue == SoundId.SndDoorClose) != 3 ||
                sounds.Requests.Count(cue => cue == SoundId.SndSolvePuzzle) != 1,
                "Full-queue Nuun script must finish the bridge once while retaining its rejected switch tile.");
        }
    }
}
