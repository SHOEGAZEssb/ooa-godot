using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateGalleryIncomingPublicationRom()
    {
        var speeds=new HashSet<int>();
        foreach(int seedByte in new[]{0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(2,0xe9);
            _roomEvents.Get<ShootingGalleryEvent>().Cancel();_entities.Clear();
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(24,104));_player.BeginCutsceneControl();
            _runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);_runtimeState.SetWramByte(0xcd00,0);
            var seed=_random.CaptureState() with {Rng1=(byte)seedByte};_random.RestoreState(seed);
            var session=new ShootingGallerySession();session.BeginBall();
            var sounds=_sound.AttachPlayRequestAudit();sounds.Clear();
            var ball=_entities.Spawn<ShootingGalleryBall>(new ShootingGalleryBallSpawn(session,new(80,8)));
            var adapter=_entities.EntityAdapters<ShootingGalleryBallRoomEntity>().Single();
            int at=0xd0c0+F1Parts[adapter]*256;
            var native=new SomariaRom(_saveData,seed,_currentRoom,0,24,104);
            native[0xcd00]=0;native[at]=1;native[at+1]=0x38;native[at+11]=8;native[at+13]=80;
            var observedCapture = new MovementScratchCapture(_runtimeState);int updates=0,moves=0;
            _entities.AddEntity(new PartMovementScratchProbe(observedCapture.Capture));
            void Seed(){for(int i=0;i<4;i++){byte value=(byte)(0xa5+i);native[0xcec0+i]=value;_runtimeState.SetWramByte(0xcec0+i,value);}}
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                int oldY=native.Word(at+10);bool live=native[at]!=0;
                native.AdvanceParts(_entities.FrameCounter);updates++;
                if(updates==1)speeds.Add(ball.Speed);
                if(live&&native[at]!=0&&oldY!=native.Word(at+10))moves++;
                FailIf(ball.Finished!=(native[at]==0)||!ball.Finished&&(
                    SomariaPrivate<Vector2>(ball,"_precisePosition")!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    (int)ball.State!=native[at+4]||ball.Speed!=native[at+16])||observedCapture.Samples!=1||
                    Enumerable.Range(0,4).Any(i=>observedCapture.Bytes[i]!=native[0xcec0+i]),
                    $"Gallery PART$38 seed${seedByte:x2}/update{updates}: original incoming motion/admission/publication differs (XY={SomariaPrivate<Vector2>(ball,"_precisePosition")}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, scratch=[{string.Join(',',observedCapture.Bytes)}]).");
                var rng=_random.CaptureState();
                FailIf(rng.Rng1!=native[0xff94]||rng.Rng2!=native[0xff95]||!sounds.Requests.SequenceEqual(native.Sounds),
                    "Gallery ball must retain original RNG consumption and source sound order through initialized/frozen/terminal updates.");
                observedCapture.Samples=0;Seed();
            });
            int initialHold=seedByte&3;
            if(initialHold==1){_dialogue.ShowMessage("Gallery state0 hold.",_player.Position.Y);native[0xcba0]=1;}
            else if(initialHold==2){_runtimeState.SetWramByte(0xcc8a,8);native[0xcc8a]=8;}
            else if(initialHold==3){_runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;}
            Seed();Step(1);
            if(initialHold==1){_dialogue.Close();native[0xcba0]=0;}
            _runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
            _runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            Step(3);
            _dialogue.ShowMessage("Gallery part hold.",_player.Position.Y);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,8);native[0xcc8a]=8;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            while(native[at]!=0&&updates<150)Step(1);
            FailIf(updates>=150||moves<20||!session.BallFinished,
                "Gallery incoming ball must retain setup/RNG admission and complete its original collision/boundary path.");
            _entities.Clear();_player.EndCutsceneControl();
        }
        FailIf(!speeds.SetEquals(new[]{0x3c,0x64}),"The original sixteen-index RNG speed table must exercise both gallery pitch speeds.");
    }
}
