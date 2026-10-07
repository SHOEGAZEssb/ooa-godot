using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareCompanionBarrierRom()
    {
        int fixture = 0;
        foreach (int id in new[] { 0x0b,0x0c,0x0d })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(0,0x89);
            _inventory.EquipA(0); _inventory.EquipB(0);
            Vector2 start = new(104.75f,96.25f);
            FailIf(_currentRoom.IsSolid(start),"Room$0:$89 barrier probe must start on its original floor above the southern cliff.");
            IRoomEntity actor = id switch
            {
                0x0b => _entities.Spawn<RickyCompanionRoomEntity>(new RickyCompanionSpawn(start,2,0,0x89,Riding:true)),
                0x0c => _entities.Spawn<DimitriCompanionRoomEntity>(new DimitriCompanionSpawn(start,2,0,0x89,Riding:true)),
                _ => _entities.Spawn<MooshCompanionRoomEntity>(new MooshCompanionSpawn(start,2,0,0x89,Riding:true))
            };
            CompanionRuntimeState.Begin(_runtimeState,id,0x89,start,2);
            var mounted = new CompanionRom(id,start,2,_currentRoom);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,2,(int)start.X,(int)start.Y) { CompanionDispatchEnabled = true };
            for (int address = 0xd000; address < 0xd040; address++) rom[address] = mounted[address];
            for (int address = 0xd100; address < 0xd140; address++) rom[address] = mounted[address];
            rom[0xcc2c] = 0xd1; rom[0xcc96] = 1; rom[0xccaa] = 0xff;
            rom[0xcc21] = rom[0xcc22] = 40; rom[0xcc23] = 2;
            // Original room stream: tutorial$d0:$00 then barrier$71:$02.
            // Both production owners occupy the original native slots.
            rom[0xd240] = 1; rom[0xd241] = 0xd0; rom[0xd24b] = 0x38; rom[0xd24d] = 0x30;
            rom[0xd340] = 1; rom[0xd341] = 0x71; rom[0xd342] = 2;
            rom[0xd34b] = 0x6d; rom[0xd34d] = 0x38;
            var barrier = _entities.Entities<CompanionBarrierRoomEntity>().Single();
            var tutorial = _entities.Entities<CompanionTutorialRoomEntity>().Single();
            FailIf(_entities.InteractionSlot(tutorial) != 2 || _entities.InteractionSlot(barrier) != 3,
                "Room$0:$89 tutorial/barrier must allocate in original$d2/$d3 order.");
            FailIf(barrier.Record is not { Group: 0, Room: 0x89, Order: 1, Id: 0x71, SubId: 2, Y: 0x6d, X: 0x38 },
                "Original room$0:$89 barrier placement/order changed.");
            var sounds = _sound.AttachPlayRequestAudit(); int update = 0;
            void Step(int count = 1,int angle = 0xff,int pressed = 0)
            {
                int edge = pressed;
                int keys = angle switch { 0 => 0x40,8 => 0x10,16 => 0x80,24 => 0x20,_ => 0 };
                StepGameplayUpdates(count,angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle),
                    MenuRomActions(pressed),MenuRomActions(pressed),batch,() =>
                    {
                        rom.UpdateGameplay(edge,pressed | keys,angle,_entities.FrameCounter - 1); edge = 0;
                        Vector2 actual = ((ICompanionBarrierTarget)actor).BarrierPosition;
                        string context = $"Barrier$0:$89/$71:$02 companion${id:x2}, batch={batch}, update={++update}";
                        FailIf(actual != new Vector2(rom.Word(0xd10c)/256f,rom.Word(0xd10a)/256f) ||
                            _player.Position != new Vector2(rom[0xd00d],rom[0xd00b]) ||
                            barrier.State != rom[0xd344] || _dialogue.IsOpen != (rom[0xcba0] != 0),
                            $"{context}: native companion/Link={rom.Word(0xd10c):x4},{rom.Word(0xd10a):x4}/{rom[0xd00d]},{rom[0xd00b]}, runtime={actual}/{_player.Position}; barrier/text differ.");
                        FailIf(_saveData.ReadWramByte(0xc649) != rom[0xc649],context + ": shared tutorial flags differ.");
                        int z = actor switch { RickyCompanionRoomEntity r => r.ZFixed,
                            DimitriCompanionRoomEntity d => d.ZFixed,MooshCompanionRoomEntity m => m.ZFixed,_ => 0 };
                        int animation = actor switch { RickyCompanionRoomEntity r => r.AnimationIndex,
                            DimitriCompanionRoomEntity d => d.AnimationIndex,MooshCompanionRoomEntity m => m.AnimationIndex,_ => 0 };
                        FailIf((z & 0xffff) != rom.Word(0xd10e) || animation != rom[0xd130],
                            context + ": vertical arc or animation differs after clamping.");
                        FailIf(CompanionField<int>(actor,"_speed") != rom[0xd110],
                            context + ": retained horizontal speed differs.");
                        var random = _random.CaptureState();
                        FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                            random.Calls-seed.Calls != rom.RandomCalls ||
                            !sounds.Requests.Where(cue => cue != SoundId.SndText).SequenceEqual(rom.Sounds),
                            context + ": shared cues/RNG differ.");
                    });
            }
            Step();
            FailIf(barrier.State != 1 || _dialogue.IsOpen,"Mounted barrier initialization must yield without clamping/showing text.");
            Step();
            if (_dialogue.IsOpen) { Step(3); _dialogue.Close(); rom[0xcba0] = 0; }
            if (id == 0x0b)
                FailIf((_saveData.ReadWramByte(0xc649) & 1) != 0,
                    "Tutorial$d0:$00 must retain its flag while its first message freezes the room.");
            for (int wait = 0; !_dialogue.IsOpen && wait < 80; wait++) Step(angle:16);
            FailIf(!_dialogue.IsOpen || rom[0xd10b] != 0x6d || rom[0xd10a] == 0 || rom[0xd110] != 0 ||
                rom[0xcba2] != (id == 0x0b ? 7 : id == 0x0c ? 5 : 9) ||
                rom[0xcba3] != 0x24 + id - 0x0b,
                $"Source barrier companion${id:x2}: native Y=${rom.Word(0xd10a):x4}, speed=${rom[0xd110]:x2}, text=${rom[0xcba3]:x2}{rom[0xcba2]:x2}, runtime={((ICompanionBarrierTarget)actor).BarrierPosition}, open={_dialogue.IsOpen}; must retain coordinate fraction, stop horizontal speed and select captured text.");
            Step(3); _dialogue.Close(); rom[0xcba0] = 0;
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Step(6,angle:0); Step();
                for (int wait = 0; !_dialogue.IsOpen && wait < 80; wait++) Step(angle:16);
                FailIf(!_dialogue.IsOpen || rom[0xd10b] != 0x6d,
                    $"Repeated barrier companion${id:x2}: native Y=${rom.Word(0xd10a):x4}, state=${rom[0xd104]:x2}/${rom[0xd105]:x2}, speed=${rom[0xd110]:x2}, text={_dialogue.IsOpen}.");
                Step(3); _dialogue.Close(); rom[0xcba0] = 0;
            }
            if (id == 0x0b)
            {
                FailIf((_saveData.ReadWramByte(0xc649) & 1) == 0,
                    "Source tutorial must finish/set bit0 after dialogue dismissal below its marker.");
                // Replace the one live SPECIALOBJECT slot while retaining the
                // initialized barrier. Its var30 text index belongs to state0.
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var active = (List<IRoomEntity>)typeof(RoomEntityManager).GetField("_activeEntities",flags)!.GetValue(_entities)!;
                active.Remove(actor);
                typeof(RoomEntityManager).GetMethod("FreeEntity",flags)!.Invoke(_entities,[actor]);
                actor = _entities.Spawn<DimitriCompanionRoomEntity>(new DimitriCompanionSpawn(start,2,0,0x89,Riding:true));
                CompanionRuntimeState.Begin(_runtimeState,0x0c,0x89,start,2);
                mounted = new CompanionRom(0x0c,start,2,_currentRoom);
                for (int address = 0xd000; address < 0xd040; address++) rom[address] = mounted[address];
                for (int address = 0xd100; address < 0xd140; address++) rom[address] = mounted[address];
                Step();
                for (int wait = 0; !_dialogue.IsOpen && wait < 80; wait++) Step(angle:16);
                FailIf(!_dialogue.IsOpen || rom.Word(0xcba2) != 0x2407 ||
                    _dialogue.CurrentMessage != DialogueBox.PlainText(barrier.Record.Message(0x0b)),
                    "A retained barrier must clamp the new live companion but show captured Ricky TX_2007.");
                Step(3); _dialogue.Close(); rom[0xcba0] = 0; Step(8,angle:0);
            }
        }
    }
}
