using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSpecialMovementPublicationRom()
    {
        var native=new LinkCollisionRom();int cases=0;
        foreach(int speed in new[]{0,0x1e,0x28,0x50})
        foreach(int angle in Enumerable.Range(0,32).Concat(new[]{0x80,0xff}))
        foreach(int walls in Enumerable.Range(0,256))
        foreach(Vector2 initial in new[]{new Vector2(64.5f,72.25f),new Vector2(.125f,255.875f)})
        {
            native.Word(0xd10a,(int)(initial.Y*256));native.Word(0xd10c,(int)(initial.X*256));native[0xd133]=(byte)walls;
            for(int i=0;i<4;i++){native[0xcec0+i]=(byte)(0xa5+i);_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));}
            native.Call(LinkCollisionRom.Move,speed,angle,objectPage:0xd1);
            Vector2 actual=initial;SpecialObjectMovement.ApplySpeed(_runtimeState,ref actual,speed,angle,walls);
            FailIf(actual!=new Vector2(native.Word(0xd10c)/256f,native.Word(0xd10a)/256f)||
                Enumerable.Range(0,4).Any(i=>_runtimeState.ReadWramByte(0xcec0+i)!=native[0xcec0+i]),
                $"Special movement speed${speed:x2}/angle${angle:x2}/walls${walls:x2}: original masked XY or scratch differs, including blocked/idle visits.");
            cases++;
        }
        FailIf(cases!=69632,"Special movement must cover all declared speeds/angles/walls/alignment combinations.");
    }

    private void ValidateCompanionGroundPublicationRom()
    {
        foreach(int id in new[]{0x0b,0x0c,0x0d})
        foreach(int direction in Enumerable.Range(0,8).Select(n=>n*4))
        foreach(bool batched in new[]{false,true})
        {
            var (actor,native)=PrepareMountedCompanionRom(id);
            byte[] observed=new byte[4],expected=new byte[4];int visits=0,motions=0,updates=0;
            var probe=new PartMovementScratchProbe(()=>
            {visits++;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);});
            _entities.RegisterEnemySlot(probe,15);_entities.AddEntity(probe);
            void Seed(){for(int i=0;i<4;i++){native[0xcec0+i]=(byte)(0xa5+i);_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));}}
            void Step(int count,int angle)=>StepGameplayUpdates(count,angle==0xff?Vector2.Zero:OracleObjectMovement.Shared.Direction(angle),batched:batched,afterUpdate:()=>
            {
                native.Update(angle,afterLink:()=>{for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];});updates++;
                CompareCompanionMotion(actor,native,$"Ground publication${id:x2}/dir${direction:x2}/update{updates}");
                FailIf(visits!=1||!observed.SequenceEqual(expected),
                    $"Companion${id:x2} ground dir${direction:x2}/update{updates}: pre-PART native movement scratch differs: [{string.Join(',',observed)}]/[{string.Join(',',expected)}].");
                if(expected[3]!=0xa8)motions++;visits=0;Seed();
            });
            Seed();Step(4,direction);Step(4,direction^0x10);
            _dialogue.ShowGameplayMessage("Ground companion hold.",100);native[0xcba0]=1;Step(3,direction);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,0x20);native[0xcc8a]=0x20;Step(3,direction);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
            Step(4,direction);Step(3,0xff);Step(4,direction^0x10);
            FailIf(motions<5,"Ground companion must publish on repeated admitted movement updates.");_entities.Clear();
        }
    }
}
