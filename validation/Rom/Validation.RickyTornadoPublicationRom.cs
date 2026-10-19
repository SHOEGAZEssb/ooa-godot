using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRickyTornadoPublicationRom()
    {
        foreach(int direction in new[]{0,1,2,3})
        foreach(bool batched in new[]{false,true})
        {
            var (actor,rom)=PrepareMountedCompanionRom(0x0b);
            // Real diagonal companion motion supplies reachable fractional
            // words; turn while charging without moving the held position.
            StepCompanionRom(actor,rom,6,new(1,1),batched);
            StepCompanionRom(actor,rom,30,Vector2.Zero,batched);
            FailIf(rom[0xd10a]==0&&rom[0xd10c]==0,"Tornado setup must reach fractional companion coordinates through actual movement.");
            StepCompanionRom(actor,rom,79,Vector2.Zero,batched,attack:true,edge:true);
            Vector2 turn=direction switch {0=>Vector2.Up,1=>Vector2.Right,2=>Vector2.Down,_=>Vector2.Left};
            StepCompanionRom(actor,rom,1,turn,batched,attack:true);
            byte[] observed=new byte[4],expected=new byte[4];int probes=0,updates=0,moves=0;
            var probe=new PartMovementScratchProbe(()=>
            {probes++;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);});
            _entities.RegisterEnemySlot(probe,15);_entities.AddEntity(probe);
            void Seed(){for(int i=0;i<4;i++){byte value=(byte)(0xa5+i);rom[0xcec0+i]=value;_runtimeState.SetWramByte(0xcec0+i,value);}}
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                bool moving=Enumerable.Range(0xd7,5).Any(page=>rom[page*256]!=0&&rom[page*256+1]==0x2a&&rom[page*256+4]!=0);
                rom.Update(0xff,afterItems:()=>{for(int i=0;i<4;i++)expected[i]=rom[0xcec0+i];});updates++;
                CompareCompanionMotion(actor,rom,$"Ricky tornado dir{direction}/update{updates}");
                foreach(var tornado in _entities.EntityAdapters<RickyTornadoRoomEntity>().Where(item=>!item.Finished))
                {
                    int at=Enumerable.Range(0xd7,5).Single(page=>rom[page*256]!=0&&rom[page*256+1]==0x2a)*256;
                    Vector2 position=new(rom[at+13]+rom[at+12]/256f,rom[at+11]+rom[at+10]/256f);
                    FailIf(CompanionField<Vector2>(tornado,"_precisePosition")!=position||
                        CompanionField<int>(tornado,"_zFixed")!=rom.SignedWord(at+14),
                        $"Ricky tornado dir{direction}/update{updates}: source high-byte-only companion copy into clean ITEM low bytes differs ({CompanionField<Vector2>(tornado,"_precisePosition")}/{position}).");
                }
                if(moving)
                {
                    moves++;
                    FailIf(probes!=1||!observed.SequenceEqual(expected),
                        "ITEM$2a must publish original movement scratch before the enemy/part phases, including its terminal collision update.");
                }
                probes=0;Seed();
            });
            Seed();Step(1);
            _dialogue.ShowGameplayMessage("Tornado item hold.",100);rom[0xcba0]=1;Step(3);_dialogue.Close();rom[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,0x10);rom[0xcc8a]=0x10;Step(3);
            _runtimeState.SetWramByte(0xcc8a,0);rom[0xcc8a]=0;
            _runtimeState.SetWramByte(0xcd00,8);rom[0xcd00]=8;Step(3);
            _runtimeState.SetWramByte(0xcd00,1);rom[0xcd00]=1;
            Step(45);
            FailIf(moves<5||_entities.EntityAdapters<RickyTornadoRoomEntity>().Any(tornado=>!tornado.Finished),
                "The initialized tornado must complete native flight and delete at the collision-buffer boundary.");
            StepCompanionRom(actor,rom,80,Vector2.Zero,batched,attack:true,edge:true);
            probes=0;Seed();Step(1);Step(45);
            FailIf(_entities.EntityAdapters<RickyTornadoRoomEntity>().Any(tornado=>!tornado.Finished),
                "Repeated charged release must reuse the cleared native ITEM slot and complete its flight again.");
            _entities.Clear();
        }
    }
}
