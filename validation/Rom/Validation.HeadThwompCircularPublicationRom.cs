using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateHeadThwompCircularPublicationRom(int firstAngle, int angleCount)
    {
        int flights=0,immediateDeletes=0,turns=0,counterEdges=0,cases=0;
        foreach(int angle in Enumerable.Range(firstAngle,angleCount))
        foreach(int turnDelta in new[]{-2,2})
        foreach(Vector2 origin in new Vector2[]{new(120,88),new(120,3),new(3,80),new(237,80),new(120,173)})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(4,0x2b);_entities.Clear();
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(16,160));_player.BeginCutsceneControl();
            _entities.DisableLinkCollisionsAndMenu();_runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            _runtimeState.SetWramByte(WramAddress.wScrollMode,0);
            // Isolate original motion from the separate post-object Link scan.
            _entities.AddEntity(new ProjectileMotionContactSuppression());
            Vector2 start=origin+(fractional?new Vector2(.25f,.5f):Vector2.Zero);
            var actor=_entities.Spawn<HeadThwompProjectile>(new HeadThwompProjectileSpawn(start,HeadThwompProjectileKind.Circular,angle,turnDelta));
            var seed=_random.CaptureState();var native=new SomariaRom(_saveData,seed,_currentRoom,0,16,160);
            native.InitializeLinkGameplay();native[0xcd00]=0;native[0xcc08]=0x8e;
            const int at=0xd0c0;native[at]=1;native[at+1]=0x3c;native[at+2]=unchecked((byte)turnDelta);native[at+9]=(byte)angle;
            native.Word(at+10,(int)(start.Y*256));native.Word(at+12,(int)(start.X*256));
            byte[] observed=new byte[4];int probes=0,updates=0,moves=0;var sounds=_sound.AttachPlayRequestAudit();
            _entities.AddEntity(new PartMovementScratchProbe(()=>
            {probes++;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);}));
            void Seed(){for(int i=0;i<4;i++){byte value=(byte)(0xa5+i);_runtimeState.SetWramByte(0xcec0+i,value);native[0xcec0+i]=value;}}
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                bool wasAlive=native[at]!=0;int oldAngle=native[at+9],oldState=native[at+4];
                native.AdvanceParts(_entities.FrameCounter);updates++;
                if(wasAlive&&native[at]!=0&&oldState==1)moves++;
                if(wasAlive&&native[at]!=0&&oldAngle!=native[at+9])turns++;
                var animation=SomariaPrivate<EnemyAnimationPlayer>(actor,"_animation");
                FailIf(actor.Finished!=(native[at]==0)||!actor.Finished&&(
                    actor.Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||actor.Angle!=native[at+9]||
                    actor.Speed!=unchecked((sbyte)native[at+2])||SomariaPrivate<int>(actor,"_counter")!=native[at+6]||
                    SomariaPrivate<int>(actor,"_turnPeriod")!=native[at+7]||SomariaPrivate<int>(actor,"_alternateCounter")!=native[at+0x30]||
                    actor.Visible!=((native[at+0x1a]&128)!=0)||animation.CurrentParameter!=native[at+0x21]||
                    SomariaPrivate<int>(animation,"_frameCounter")!=native[at+0x20])||
                    probes!=1||Enumerable.Range(0,4).Any(i=>observed[i]!=native[0xcec0+i]),
                    $"Head circular$3c/angle${angle:x2}/turn{turnDelta}/start{origin}/fraction{fractional}/update{updates}: finished{actor.Finished}/{native[at]==0}, XY{actor.Position}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, angle{actor.Angle}/{native[at+9]}, C1{SomariaPrivate<int>(actor,"_counter")}/{native[at+6]}, C2{SomariaPrivate<int>(actor,"_turnPeriod")}/{native[at+7]}, scratch[{string.Join(',',observed)}]/[{string.Join(',',Enumerable.Range(0,4).Select(i=>native[0xcec0+i]))}].");
                var rng=_random.CaptureState();FailIf(rng.Rng1!=native[0xff94]||rng.Rng2!=native[0xff95]||rng.Calls-seed.Calls!=native.RandomCalls||!sounds.Requests.SequenceEqual(native.Sounds),
                    "Circular source motion must preserve zero RNG and one admitted launch cue.");
                probes=0;Seed();
            });
            int hold=angle&3;
            if(hold==0){_dialogue.ShowMessage("Head circular state0.",104);native[0xcba0]=1;}
            if(hold==1){_runtimeState.SetWramByte(0xcc8a,8);native[0xcc8a]=8;}
            if(hold==2){_runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;}
            Seed();Step(1);
            if(hold==0){_dialogue.Close();native[0xcba0]=0;}
            _runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            Step(1);if(native[at]==0)immediateDeletes++;
            if(origin==new Vector2(120,88)&&angle==0&&fractional)
            {
                // Declared arithmetic boundary, beyond naturally reached period
                // values in this room: byte C2 overflow and saturating C1 zero.
                F1Set(actor,"_counter",0);F1Set(actor,"_turnPeriod",255);F1Set(actor,"_alternateCounter",0);
                native[at+6]=0;native[at+7]=255;native[at+0x30]=0;Step(2);counterEdges++;
            }
            _dialogue.ShowMessage("Head circular hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,8);native[0xcc8a]=8;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            while(native[at]!=0&&updates<600)Step(1);
            FailIf(!actor.Finished,"Circular native boundary probes must eventually complete the expanding flight.");
            if(moves>8)flights++;
            Step(2);_entities.Clear();StepGameplayUpdates(2,Vector2.Zero);_entities.EnableLinkCollisionsAndMenu();_player.EndCutsceneControl();
            cases++;
        }
        FailIf(flights==0||immediateDeletes==0||turns<angleCount||counterEdges!=(firstAngle==0?4:0),"Circular coverage must include expanding flight, initial wrapped edge rejection, both turn directions and declared byte-counter arithmetic.");
        VerifyValidationMatrixCases(nameof(ValidateHeadThwompCircularPublicationRom), firstAngle, angleCount, 32, cases, angleCount * 40);
    }
}
