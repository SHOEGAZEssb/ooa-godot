using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateJabuBubblePublicationRom()
    {
        // partCode16's four source speeds and both native state2 drift sides.
        foreach(int speed in new[]{0x1e,0x28,0x32,0x3c})
        foreach(int driftCounter in new[]{0,24})
        foreach(bool right in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(0,0x33);_entities.Clear();
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(40,104));
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            Vector2 initial=new(right?248.25f:8.25f,136.5f);
            var actor=new JabuBubbleRoomEntity(initial,0,new JabuExteriorDatabase(),_random,_runtimeState);
            _entities.AddEntity(actor);_player.BeginCutsceneControl(owner:actor);
            var seed=_random.CaptureState();var native=new SomariaRom(_saveData,seed,_currentRoom,0,40,104);
            const int at=0xd0c0;native[at]=1;native[at+1]=0x16;native.Word(at+10,0x8880);native.Word(at+12,(int)(initial.X*256));
            StepGameplayUpdates(1,Vector2.Zero,afterUpdate:()=>native.AdvanceParts(_entities.FrameCounter));
            FailIf(SomariaPrivate<int>(actor,"_speed")!=native[at+16]||
                _random.CaptureState().Rng1!=native[0xff94]||_random.CaptureState().Rng2!=native[0xff95],
                "Bubble initialization must consume the original shared RNG and select its source speed.");
            FailIf(_random.CaptureState().Calls-seed.Calls!=1||native.RandomCalls!=1,
                "Bubble initialization must consume exactly one shared RNG draw.");
            // Declared post-initialization delay/register boundary. Keep the
            // real native state1/2/3 dispatch, gravity-free planar and exit logic.
            F1Set(actor,"_speed",speed);F1Set(actor,"_counter",3);F1Set(actor,"_counter2",driftCounter);
            native[at+16]=(byte)speed;native[at+6]=3;native[at+7]=(byte)driftCounter;
            int updates=0,moves=0,state2Visits=0,state3Visits=0;
            void Seed(){for(int i=0;i<4;i++){byte value=(byte)(0xa5+i);native[0xcec0+i]=value;_runtimeState.SetWramByte(0xcec0+i,value);}}
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                int oldState=native[at+4];bool wasLive=native[at]!=0;
                native.AdvanceParts(_entities.FrameCounter);updates++;
                if(wasLive&&oldState>=2){moves++;if(oldState==2)state2Visits++;else state3Visits++;}
                FailIf(actor.Finished!=(native[at]==0)||!actor.Finished&&(
                    SomariaPrivate<Vector2>(actor,"_position")!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    SomariaPrivate<int>(actor,"_state")!=native[at+4]||SomariaPrivate<int>(actor,"_counter")!=native[at+6]||
                    SomariaPrivate<int>(actor,"_counter2")!=native[at+7]||actor.Visible!=((native[at+0x1a]&128)!=0))||
                    Enumerable.Range(0,4).Any(i=>_runtimeState.ReadWramByte(0xcec0+i)!=native[0xcec0+i]),
                    $"Jabu bubble speed${speed:x2} drift{driftCounter} update{updates}: native drift, counter, flicker, publication or deletion differs.");
                Seed();
            });
            Seed();Step(4);
            _dialogue.ShowMessage("Always-updating bubble.",_player.Position.Y);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,4);native[0xcc8a]=4;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,1);native[0xcd00]=1;
            while(!actor.Finished&&updates<260)Step(1);
            Step(2);
            FailIf(!actor.Finished||moves<50||state2Visits!=(driftCounter==0?1:driftCounter)||state3Visits==0,
                "Bubble must exercise the exact state2 drift duration, state3 motion, wrapped upper exit and post-retirement updates.");
            _entities.Clear();_player.EndCutsceneControl(actor);
        }
    }
}
