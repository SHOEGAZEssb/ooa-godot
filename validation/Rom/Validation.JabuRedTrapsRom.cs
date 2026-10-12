using Godot;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateJabuRedTrapsRom()
    {
        foreach (bool batch in new[] {false,true})
        {
            ReinitializeGameplayForValidation(); LoadValidationRoom(5,0x58);
            var traps=_entities.Entities<BladeTrapCharacter>().ToArray();
            FailIf(traps.Length != 2 || !traps.Select(trap => trap.Position).SequenceEqual(new Vector2[] {new(88,24),new(72,40)}),
                "Source room$5:$58 requires red traps$0e:$00 in order at YX$18/$58 and $28/$48.");
            _inventory.EquipA(0); _inventory.EquipB(0); _player.WarpTo(new(88,88));
            FailIf(_collision.Collides(_player.Position),"Red-trap fixture must start on unchanged room$5:$58 floor.");
            var slots=SomariaPrivate<Dictionary<IRoomEntity,int>>(_entities,"_enemySlots");
            var seed=_random.CaptureState();
            var rom=new EnemyStatusRom(_currentRoom,_saveData,_random.Calls);
            rom[0xcc08]=ValidationRom.LoadCleanUs().Span[0xfdd4b+0x0e*4];
            rom[0xff94]=seed.Rng1; rom[0xff95]=seed.Rng2;
            foreach(var (entity,slot) in slots)
            {
                int address=0xd080+slot*256;
                rom[address]=1; rom[address+1]=0x0e;
                rom.Word(address+10,(int)entity.Node.Position.Y*256);
                rom.Word(address+12,(int)entity.Node.Position.X*256);
            }
            bool frozen=true; _entities.TextActiveSource=()=>frozen;
            int update=0; bool accelerated=false,stopped=false;
            void Step(int count=1) => StepGameplayUpdates(count,Vector2.Zero,batched:batch,afterUpdate:()=>{
                rom[0xcba0]=(byte)(frozen ? 1 : 0);
                rom.Update(_entities.FrameCounter,_player.Position); ++update;
                foreach(var (entity,slot) in slots)
                {
                    var trap=(BladeTrapCharacter)entity.Node;
                    int address=0xd080+slot*256;
                    FailIf((int)trap.State != rom[address+4] || trap.Counter != rom[address+6] ||
                        trap.Angle != rom[address+9] || trap.SpeedRaw != rom[address+16] ||
                        trap.Position != new Vector2(rom.Word(address+12)/256f,rom.Word(address+10)/256f) ||
                        trap.Animation.CurrentParameter != rom[address+0x21],
                        $"Red trap$0e:$00 room5:58 slot${slot:x2}, batch={batch}, update{update}: state/counter/angle/speed/XY/animation differs, state={(int)trap.State}/{rom[address+4]}, counter={trap.Counter}/{rom[address+6]}, XY={trap.Position}/{rom.Word(address+12)/256f},{rom.Word(address+10)/256f}.");
                    if (trap.State == BladeTrapState.Retracting) accelerated=true;
                    if (accelerated && trap.State == BladeTrapState.Waiting) stopped=true;
                }
                var actual=_random.CaptureState();
                FailIf(actual.Rng1 != rom[0xff94] || actual.Rng2 != rom[0xff95] || actual.Calls-seed.Calls != rom.RandomCalls,
                    "Red-trap dispatch must preserve ordered shared initialization RNG.");
            });
            Step(3); frozen=false; Step(160);
            FailIf(!accelerated || !stopped,"Red trap must accelerate after24 updates and stop against original room geometry.");
        }
        GD.Print("Validated clean-US Dungeon7 red traps: source order, modal initialization, 24-update acceleration, real-wall stop, animation and RNG in split/batched gameplay.");
    }
}
