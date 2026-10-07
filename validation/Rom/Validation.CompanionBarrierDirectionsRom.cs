using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void CompareCompanionBarrierDirectionsRom()
    {
        var database = new CompanionBarrierDatabase();
        // Independent mainData.s placements, including the two objects in $6a.
        (int Room,int Order,int Sub,int Y,int X)[] placements = [
            (0x5c,0,5,0x40,0x98),
            (0x6a,2,4,0x08,0x58),(0x6a,3,5,0x40,0x98),
            (0x6b,0,1,0x38,0x08),(0x6c,4,2,0x6d,0x38),
            (0x89,1,2,0x6d,0x38),(0xa8,0,1,0x38,0x08),
            (0xb8,0,1,0x38,0x08),(0xc8,0,1,0x38,0x08),(0xd8,0,1,0x38,0x08)
        ];
        FailIf(database.Count != 10,"mainData.s must import all ten generic $71 barriers.");
        foreach (var expected in placements)
        {
            var row = database.GetRoomRecords(0,expected.Room).Single(r => r.Order == expected.Order);
            FailIf(row.Id != 0x71 || row.SubId != expected.Sub || row.Y != expected.Y || row.X != expected.X ||
                !row.StateAddresses.SequenceEqual(new[] {0xc646,0xc647,0xc648}) ||
                !row.TextIds.SequenceEqual(new[] {0x2007,0x2105,0x2209}),
                $"Original room$0:${expected.Room:x2} barrier order{expected.Order} differs.");
        }
        int fixture = 0;
        foreach (var spec in new (int Room,int Id,int Sub,int Direction,int Angle,Vector2 Start,int Clamp)[] {
            (0x5c,0x0c,5,1,8,new Vector2(144,24),152),
            (0x6a,0x0b,4,0,0,new Vector2(88,16),9),
            (0x6b,0x0d,1,3,24,new Vector2(16,72),9)
        })
        foreach (bool batch in RomHostSchedules(fixture++))
        {
            ReinitializeGameplayForValidation();
            // Native Impa state0 suppresses her completed encounter in $6a;
            // keep that independent story script from owning these inputs.
            _saveData.SetRoomFlag(0,0x6a,0x40);
            CompanionRuntimeState.Begin(_runtimeState,spec.Id,spec.Room,spec.Start,spec.Direction);
            LoadValidationRoom(0,spec.Room);
            FailIf(_currentRoom.IsSolid(spec.Start),"Generic barrier approach must start on original room floor.");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var active = (List<IRoomEntity>)typeof(RoomEntityManager).GetField("_activeEntities",flags)!.GetValue(_entities)!;
            foreach (var entity in active.Where(e => e is not ICompanionBarrierTarget and not CompanionBarrierRoomEntity).ToArray())
            {
                active.Remove(entity);
                typeof(RoomEntityManager).GetMethod("FreeEntity",flags)!.Invoke(_entities,[entity]);
            }
            var actor = active.OfType<ICompanionBarrierTarget>().Single();
            var barriers = active.OfType<CompanionBarrierRoomEntity>().ToArray();
            var target = barriers.Single(b => b.Record.SubId == spec.Sub);
            var mounted = new CompanionRom(spec.Id,spec.Start,spec.Direction,_currentRoom);
            var seed = _random.CaptureState();
            var rom = new SomariaRom(_saveData,seed,_currentRoom,spec.Direction,(int)spec.Start.X,(int)spec.Start.Y)
                { CompanionDispatchEnabled = true };
            for (int address = 0xd000; address < 0xd040; address++) rom[address] = mounted[address];
            for (int address = 0xd100; address < 0xd140; address++) rom[address] = mounted[address];
            rom[0xcc2c] = 0xd1; rom[0xcc96] = 1; rom[0xccaa] = 0xff;
            rom[0xcc21] = rom[0xcc22] = 40; rom[0xcc23] = (byte)spec.Direction;
            foreach (var barrier in barriers)
            {
                int slot = (0xd2 + barrier.Record.Order) * 256 + 0x40;
                rom[slot] = 1; rom[slot+1] = 0x71; rom[slot+2] = (byte)barrier.Record.SubId;
                rom[slot+0xb] = (byte)barrier.Record.Y; rom[slot+0xd] = (byte)barrier.Record.X;
            }
            int update = 0;
            void Step(int count = 1,int angle = 0xff)
            {
                int keys = angle switch {0 => 0x40,8 => 0x10,24 => 0x20,_ => 0};
                StepGameplayUpdates(count,angle == 0xff ? Vector2.Zero : OracleObjectMath.StrictCardinalVector(angle),
                    batched:batch,afterUpdate:() => {
                        rom.UpdateGameplay(0,keys,angle,_entities.FrameCounter-1);
                        string context = $"Barrier$0:${spec.Room:x2}/$71:${spec.Sub:x2}, update={++update}, batch={batch}";
                        FailIf(actor.BarrierPosition != new Vector2(rom.Word(0xd10c)/256f,rom.Word(0xd10a)/256f) ||
                            _player.Position != new Vector2(rom[0xd00d],rom[0xd00b]) ||
                            _dialogue.IsOpen != (rom[0xcba0] != 0) || CompanionField<int>(actor,"_speed") != rom[0xd110],
                            context + $": native XY=${rom.Word(0xd10c):x4},${rom.Word(0xd10a):x4}, Link={rom[0xd00d]},{rom[0xd00b]}, speed=${rom[0xd110]:x2}; runtime={actor.BarrierPosition}, Link={_player.Position}, speed={CompanionField<int>(actor,"_speed")}, text={_dialogue.IsOpen}; Link/text/speed differ.");
                        var random = _random.CaptureState();
                        FailIf(random.Rng1 != rom[0xff94] || random.Rng2 != rom[0xff95] ||
                            random.Calls-seed.Calls != rom.RandomCalls,context + ": RNG differs.");
                    });
            }
            Step();
            FailIf(target.State != 1 || _dialogue.IsOpen,"Source generic barrier state0 must yield after mount initialization.");
            for (int repeat = 0; repeat < 2; repeat++)
            {
                for (int wait = 0; !_dialogue.IsOpen && wait < 32; wait++) Step(angle:spec.Angle);
                float coordinate = spec.Sub == 4 ? actor.BarrierPosition.Y : actor.BarrierPosition.X;
                FailIf(!_dialogue.IsOpen || Mathf.FloorToInt(coordinate) != spec.Clamp || rom[0xd110] != 0,
                    "Source cardinal barrier must clamp high byte, stop speed and open text.");
                Step(3); _dialogue.Close(); rom[0xcba0] = 0;
                Step(6,angle:(spec.Angle+16)&31); Step();
            }
        }
    }
}
