using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateHeadThwompFireballPublicationRom()
    {
        var seeds=new Dictionary<int,(byte Rng1,byte Rng2)>();
        var trial=new OracleRandom();var state=trial.CaptureState();
        for(int value=0;value<65536&&seeds.Count<8;value++)
        {
            trial.RestoreState(state with {Rng1=(byte)value,Rng2=(byte)(value>>8)});
            int key=trial.Next().Value&0x10;key|=trial.Next().Value&3;
            seeds.TryAdd(key,((byte)value,(byte)(value>>8)));
        }
        FailIf(seeds.Count!=8,"PART$39's two native RNG calls must cover both angles and four speeds.");
        int impacts=0,wrapped=0;
        foreach(var test in seeds.OrderBy(pair=>pair.Key))
        foreach(Vector2 origin in new Vector2[]{new(120,80),new(120,3),new(3,80),new(237,80),new(120,173)})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0x2b);_entities.Clear();
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(16,160));_player.BeginCutsceneControl();
            _entities.DisableLinkCollisionsAndMenu();_runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            _runtimeState.SetWramByte(WramAddress.wScrollMode,0);
            var seed=_random.CaptureState() with {Rng1=test.Value.Rng1,Rng2=test.Value.Rng2};_random.RestoreState(seed);
            Vector2 start=origin+(fractional?new Vector2(.25f,.5f):Vector2.Zero);
            // Fractional starts are declared retained native words, separate
            // from the boss's fresh high-byte-only allocation boundary.
            var actor=_entities.Spawn<HeadThwompProjectile>(new HeadThwompProjectileSpawn(start,HeadThwompProjectileKind.Fireball,0,0,RandomizeLaunch:true));
            var native=new SomariaRom(_saveData,seed,_currentRoom,0,16,160);native.InitializeLinkGameplay();native[0xcd00]=0;
            native[0xcc08]=0xa4;native[0xcc0a]=0x83;
            const int at=0xd0c0;native[at]=1;native[at+1]=0x39;
            native.Word(at+10,(int)(start.Y*256));native.Word(at+12,(int)(start.X*256));
            byte[] observed=new byte[4];int probes=0,updates=0,moves=0;var sounds=_sound.AttachPlayRequestAudit();
            _entities.AddEntity(new PartMovementScratchProbe(()=>
            {probes++;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);}));
            void Seed(){for(int i=0;i<4;i++){byte value=(byte)(0xa5+i);_runtimeState.SetWramByte(0xcec0+i,value);native[0xcec0+i]=value;}}
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                bool wasAlive=native[at]!=0;int oldY=native.Word(at+10),oldState=native[at+4];
                native.AdvanceParts(_entities.FrameCounter);updates++;
                if(wasAlive&&native[at]!=0&&oldState==1&&oldY!=native.Word(at+10))moves++;
                if(wasAlive&&native[at]!=0&&Math.Abs((oldY>>8)-native[at+11])>128)wrapped++;
                if(oldState==1&&native[at+4]==2)impacts++;
                var animation=SomariaPrivate<EnemyAnimationPlayer>(actor,actor.Breaking?"_impactAnimation":"_animation");
                int sourceState=SomariaPrivate<bool>(actor,"_initialized")?actor.Breaking?2:1:0;
                FailIf(actor.Finished!=(native[at]==0)||!actor.Finished&&(
                    actor.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||sourceState!=native[at+4]||
                    actor.Angle!=native[at+9]||actor.Speed!=native[at+16]||SomariaPrivate<int>(actor,"_counter")!=native[at+6]||
                    (SomariaPrivate<int>(actor,"_speedZ")&65535)!=native.Word(at+0x14)||actor.ZFixed!=unchecked((short)native.Word(at+14))||
                    actor.Visible!=((native[at+0x1a]&128)!=0)||animation.CurrentParameter!=native[at+0x21]||
                    SomariaPrivate<int>(animation,"_frameCounter")!=native[at+0x20]||
                    actor.CollisionBounds.Size!=new Vector2(native[at+0x27]*2,native[at+0x26]*2))||
                    probes!=1||Enumerable.Range(0,4).Any(i=>observed[i]!=native[0xcec0+i]),
                    $"Head fireball$39/route{test.Key}/start{origin}/fraction{fractional}/update{updates}: XY{actor.Position}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, Z{actor.ZFixed}/{native.Word(at+14):x4}, state{sourceState}/{native[at+4]}, counter{SomariaPrivate<int>(actor,"_counter")}/{native[at+6]}, scratch[{string.Join(',',observed)}]/[{string.Join(',',Enumerable.Range(0,4).Select(i=>native[0xcec0+i]))}].");
                var rng=_random.CaptureState();FailIf(rng.Rng1!=native[0xff94]||rng.Rng2!=native[0xff95]||rng.Calls-seed.Calls!=native.RandomCalls||!sounds.Requests.SequenceEqual(native.Sounds),
                    "PART$39 must preserve two launch draws and ordered launch/impact cues through every hold and terminal visit.");
                probes=0;Seed();
            });
            int hold=test.Key&3;
            if(hold==0){_dialogue.ShowMessage("Head fireball state0.",104);native[0xcba0]=1;}
            if(hold==1){_runtimeState.SetWramByte(0xcc8a,8);native[0xcc8a]=8;}
            if(hold==2){_runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;}
            Seed();Step(1);
            if(hold==0){_dialogue.Close();native[0xcba0]=0;}
            _runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            Step(1);
            _dialogue.ShowMessage("Head fireball hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,8);native[0xcc8a]=8;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            while(native[at]!=0&&updates<160)Step(1);
            FailIf(!actor.Finished||moves<29,"The original counter30 must retain pre-gate motion, then complete its impact or boundary deletion.");
            Step(2);_entities.Clear();StepGameplayUpdates(2,Vector2.Zero);_entities.EnableLinkCollisionsAndMenu();_player.EndCutsceneControl();
        }
        FailIf(impacts==0||wrapped==0,"PART$39 must exercise original impacts and wrapped physical Y, not only an on-screen rendered arc.");
    }
}
