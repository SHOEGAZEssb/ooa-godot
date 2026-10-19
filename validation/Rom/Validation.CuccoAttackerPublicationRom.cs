using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCuccoAttackerPublicationRom()
    {
        foreach(int edge in Enumerable.Range(0,4))
        foreach(int hits in new[]{0x10,0x18,0x20})
        foreach(int camera in new[]{0,24})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            var native=PrepareEnemyPartAdmissionRom(0x36,0,out var parent);
            F1Set(parent,"_hitCount",hits);native[0xd0b0]=(byte)hits;
            _runtimeState.SetWramByte(0xcc8a,4);native[0xcc8a]=4;
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            _entities.DisableLinkCollisionsAndMenu();
            var seed=_random.CaptureState();bool found=false;
            for(int candidate=0;candidate<256;candidate++)
            {
                var trial=seed with {Rng1=(byte)candidate,Rng2=1};var prediction=new OracleRandom();prediction.RestoreState(trial);
                if(((prediction.Next().Value&0x30)>>4)!=edge)continue;
                seed=trial;found=true;break;
            }
            FailIf(!found,"Cucco flight fixture requires an original RNG seed for each screen edge.");
            _random.RestoreState(seed);native[0xff94]=seed.Rng1;native[0xff95]=seed.Rng2;
            int nativeCalls=native.RandomCalls;
            var oldProjection=_entities.WorldToScreen;
            _entities.WorldToScreen=point=>point-new Vector2(camera,camera);
            native[0xffaa]=native[0xffac]=(byte)camera;
            try
            {
                FailIf(!_entities.TryCreatePart(new CuccoAttackerSpawn(hits)),"Cucco flight requires an admitted PART slot.");
                var actor=_entities.Entities<CuccoAttackerCharacter>().Single();
                var adapter=_entities.EntityAdapters<CuccoAttackerRoomEntity>().Single();
                int at=0xd0c0+F1Parts[adapter]*256;native[at]=1;native[at+1]=0x22;native[at+0x16]=0x80;native[at+0x17]=0xd0;
                byte[] observed=new byte[4];int probes=0,updates=0,moves=0;
                var before=new PartMovementScratchProbe(()=>
                {for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));});
                _entities.RegisterEnemySlot(before,15);_entities.AddEntity(before);
                _entities.AddEntity(new PartMovementScratchProbe(()=>
                {probes++;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);}));
                void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
                {
                    for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                    native[0xffb0]=(byte)_player.Position.Y;native[0xffb1]=(byte)_player.Position.X;
                    native.AdvanceParts(_entities.FrameCounter);updates++;
                    if(native[0xcec3]!=0xa8)moves++;
                    var precise=SomariaPrivate<Vector2>(actor,"_precisePosition");
                    FailIf(actor.IsDead!=(native[at]==0)||!actor.IsDead&&(
                        precise!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                        actor.Position!=new Vector2(native[at+13],native[at+11])||
                        (int)actor.State!=native[at+4]||actor.Counter!=native[at+6]||actor.Speed!=native[at+16]||
                        SomariaPrivate<int>(actor,"_angle")!=native[at+9]||actor.AnimationParameter!=native[at+0x21]||
                        actor.Visible!=((native[at+0x1a]&128)!=0))||probes!=1||
                        Enumerable.Range(0,4).Any(i=>observed[i]!=native[0xcec0+i]),
                        $"PART$22 edge{edge}/hits${hits:x2}/camera${camera:x2}/fraction{fractional}/update{updates}: original flight differs: dead{actor.IsDead}/{native[at]==0}, XY{precise}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, state{(int)actor.State}/{native[at+4]}, counter{actor.Counter}/{native[at+6]}, animation{actor.AnimationParameter}/{native[at+0x21]}, scratch[{string.Join(',',observed)}]/[{string.Join(',',Enumerable.Range(0,4).Select(i=>native[0xcec0+i]))}].");
                    var rng=_random.CaptureState();FailIf(rng.Rng1!=native[0xff94]||rng.Rng2!=native[0xff95]||
                        rng.Calls-seed.Calls!=native.RandomCalls-nativeCalls||rng.Calls-seed.Calls!=1,
                        "Cucco flight must retain the original success-only child RNG draw.");probes=0;
                });
                Step(1);
                if(fractional)
                {
                    F1Set(actor,"_precisePosition",actor.Position+new Vector2(.25f,.5f));
                    native[at+10]=0x80;native[at+12]=0x40;
                }
                Step(12);_dialogue.ShowMessage("Cucco flight hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
                _runtimeState.SetWramByte(0xcc8a,12);native[0xcc8a]=12;Step(3);_runtimeState.SetWramByte(0xcc8a,4);native[0xcc8a]=4;
                _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,4);native[0xcd00]=4;
                while(!actor.IsDead&&updates<260)Step(1);
                Step(2);FailIf(!actor.IsDead||moves<24,"Cucco flight must preserve all24 entry movements and retire through the original screen predicate.");
                _entities.Clear();_entities.EnableLinkCollisionsAndMenu();
            }
            finally{_entities.WorldToScreen=oldProjection;}
        }
    }
}
