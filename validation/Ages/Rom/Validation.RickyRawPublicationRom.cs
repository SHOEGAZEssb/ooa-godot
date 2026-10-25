using Godot;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateRickyRawPublicationRom()
    {
        foreach(int terrain in new[]{0,1,2})
        foreach(bool batched in new[]{false,true})
        {
            var (actor,rom)=PrepareMountedCompanionRom(0x0b,terrain==0?2:0);
            Vector2 input=terrain==0?Vector2.Down:Vector2.Up;
            if(terrain==0) SetCompanionRomTile(rom,4,4,0xd4,3);
            else
            {
                SetCompanionRomTile(rom,4,3,0,terrain==1?(byte)3:(byte)15);
                if(terrain==2)SetCompanionRomTile(rom,4,2,0,3);
            }
            byte[] observed=new byte[4],expected=new byte[4];
            int probes=0,moves=0,selectedUpdates=0,updates=0;
            var probe=new PartMovementScratchProbe(()=>
            {probes++;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);});
            _entities.RegisterEnemySlot(probe,15);_entities.AddEntity(probe);
            void Seed(){for(int i=0;i<4;i++){byte value=(byte)(0xa5+i);rom[0xcec0+i]=value;_runtimeState.SetWramByte(0xcec0+i,value);}}
            void Step(int count)=>StepGameplayUpdates(count,input,batched:batched,afterUpdate:()=>
            {
                int state=rom[0xd104];Vector2 before=rom.Position;
                rom.Update(CompanionMovement.AngleForInput(input),afterLink:()=>
                {for(int i=0;i<4;i++)expected[i]=rom[0xcec0+i];});updates++;
                CompareCompanionMotion(actor,rom,$"Ricky raw terrain{terrain}/update{updates}");
                if(state is 2 or 7)
                {
                    if(rom[0xd104] is 2 or 7)
                        FailIf(((RickyCompanionRoomEntity)actor).AirborneDelay!=rom[0xd106],
                            "Ricky's native cliff delay must retain its exact zero/sound boundary through admission holds.");
                    selectedUpdates++;if(before!=rom.Position)moves++;
                    FailIf(probes!=1||!observed.SequenceEqual(expected),
                        $"Ricky raw state${state:x2} terrain{terrain}/update{updates}: native pre-PART motion publication differs (runtime=[{string.Join(',',observed)}], native=[{string.Join(',',expected)}]).");
                }
                probes=0;Seed();
            });
            Seed();Step(4);
            _dialogue.ShowGameplayMessage("Ricky cliff hold.",100);rom[0xcba0]=1;Step(3);_dialogue.Close();rom[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,0x20);rom[0xcc8a]=0x20;Step(3);
            _runtimeState.SetWramByte(0xcc8a,0);rom[0xcc8a]=0;
            _runtimeState.SetWramByte(0xcd00,8);rom[0xcd00]=8;Step(3);
            _runtimeState.SetWramByte(0xcd00,1);rom[0xcd00]=1;
            for(int group=0;group<24;group++)
            {
                Step(3);
                // End held directional input at the landing handoff. This
                // fixture executes companion/rider code, not room scrolling.
                if(moves>0&&rom[0xd104] is not (2 or 7))input=Vector2.Zero;
            }
            input=Vector2.Zero;Step(30);
            FailIf(selectedUpdates<5||moves<4||((RickyCompanionRoomEntity)actor).Phase is
                RickyCompanionPhase.JumpingUpCliff or RickyCompanionPhase.JumpingDownCliff,
                "Ricky's actual cliff probes must enter and finish each selected raw route.");
            _entities.Clear();
        }
    }
}
