using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateDimitriConstructionRom()
    {
        foreach(bool batched in new[]{false,true})
        foreach(byte initial in new byte[]{0,0x80})
        foreach(byte flags in new byte[]{0,0x20,0x40,0x80})
        {
            ReinitializeGameplayForValidation();PrepareCompanionFidelityRoom();_entities.Clear();
            _saveData.WriteField("wDimitriState",initial);
            _saveData.SetGlobalFlag(AgesGlobalFlag.SavedCompanionFromForest,false);
            Vector2 position=new(72,64);
            byte[] expected=Enumerable.Range(0,64).Select(i=>_runtimeState.ReadWramByte(0xd100+i)).ToArray();
            expected[0]=1;expected[1]=0x0c;expected[11]=64;expected[13]=72;
            var birth=_entities.Spawn(new DimitriCompanionSpawn(position,2,0,0x2a));
            FailIf(!_entities.EntityAdapters<AgesDimitriScriptedCompanionRoomEntity>().Any(e=>e.Npc==birth),
                "A cold Ages $0c must bind its native state0 owner independently of construction-time flags.");
            for(int i=0;i<64;i++)FailIf(_runtimeState.ReadWramByte(0xd100+i)!=expected[i],
                $"Dimitri cold constructor wrote outside its native enabled/id/XY-high footprint at byte${i:x2}.");
            _saveData.WriteField("wDimitriState",flags);
            var native=new CompanionRom(0x0c,position,2,_currentRoom);
            native.InitializeRemembered(_player.PrecisePosition,new(40,40),false);
            // Declared constructed input, with the independent footprint above.
            // Original state0 owns all later initialization and animation writes.
            for(int i=0;i<64;i++)native[0xd100+i]=expected[i];
            native[0xc647]=flags;
            var seed=_random.CaptureState();native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;
            int updates=0;
            StepGameplayUpdates(6,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                native.Update(0xff);updates++;
                foreach(int address in new[]{0xd104,0xd105,0xd108,0xd109,0xd124,0xd126,0xd127,0xd13f,0xd11a,0xd130,0xd120,0xd121})
                    FailIf(_runtimeState.ReadWramByte(address)!=native[address],
                        $"Dimitri cold flags${initial:x2}->${flags:x2}/batch{batched}/update{updates}: byte${address:x4} runtime=${_runtimeState.ReadWramByte(address):x2}/native=${native[address]:x2}.");
                FailIf(_entities.Entities<DimitriCompanionRoomEntity>().Any()!=(native[0xd104]==1),
                    "State0 must transfer normal gameplay presentation only when the executed native flags select state1.");
            });
            FailIf(_dialogue.IsOpen || _runtimeState.ReadField("wDisabledObjects")!=0 ||
                _saveData.ReadField("wDimitriState")!=flags,
                "Binding a native body must not fabricate Ages rescue dialogue, control masks or story writes.");
        }
    }
}
