using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateSeaFairyRayPublicationRom()
    {
        foreach(int angle in Enumerable.Range(0,8).Select(index=>index*4))
        foreach(bool cameraInput in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(3,0xef);_roomEvents.Get<SeaFairyEvent>().Cancel();_entities.Clear();
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(40,104));
            _runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);
            Vector2 camera=cameraInput?new(24,16):Vector2.Zero;
            // Explicit camera-origin inputs to the shared gameplay projection;
            // this does not pretend to execute the separate scrolling route.
            _entities.WorldToScreen=point=>point-camera;
            Vector2 initial=camera+new Vector2(80.25f,64.5f);
            _entities.Spawn(new SeaFairySparkleSpawn(new SeaFairyDatabase(),"ray",initial,angle));
            var actor=_entities.EntityAdapters<SeaFairySparkleRoomEntity>().Single();_player.BeginCutsceneControl(owner:actor);
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,40,104);
            int at=0xd040+_entities.InteractionSlot(actor.Node)*256;
            native[at]=1;native[at+1]=0x84;native[at+2]=0x0a;native[at+9]=(byte)angle;
            native.Word(at+10,(int)(initial.Y*256));native.Word(at+12,(int)(initial.X*256));native[at+15]=0xf0;
            int updates=0,moves=0;
            void Seed(){for(int i=0;i<4;i++){byte value=(byte)(0xa5+i);native[0xcec0+i]=value;_runtimeState.SetWramByte(0xcec0+i,value);}}
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                bool wasMoving=native[at]!=0&&native[at+4]!=0;
                native[0xffaa]=(byte)camera.Y;native[0xffac]=(byte)camera.X;
                native.AdvanceInteractions(_entities.FrameCounter);updates++;
                if(wasMoving)moves++;
                FailIf(actor.Finished!=(native[at]==0)||!actor.Finished&&(
                    SomariaPrivate<Vector2>(actor,"_position")!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    actor.Node.Position!=new Vector2(native[at+13],native[at+11])||
                    ((NpcCharacter)actor.Node).CurrentAnimationCounter!=native[at+0x20]||
                    ((NpcCharacter)actor.Node).CurrentAnimationParameter!=native[at+0x21])||
                    Enumerable.Range(0,4).Any(i=>_runtimeState.ReadWramByte(0xcec0+i)!=native[0xcec0+i]),
                    $"Fairy ray angle${angle:x2} update{updates},camera={camera}: native motion/boundary/animation/publication differs (finished={actor.Finished}/{native[at]==0}, XY={SomariaPrivate<Vector2>(actor,"_position")}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, animation={((NpcCharacter)actor.Node).CurrentAnimationCounter}/{native[at+0x20]}, scratch={string.Join(',',Enumerable.Range(0,4).Select(i=>_runtimeState.ReadWramByte(0xcec0+i).ToString("x2")))}/{native.Word(0xcec0):x4},{native.Word(0xcec2):x4}).");
                Seed();
            });
            Seed();Step(4);
            _dialogue.ShowMessage("Native ray always updates.",_player.Position.Y);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,2);native[0xcc8a]=2;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,1);native[0xcd00]=1;
            if(cameraInput)camera+=new Vector2(8,8);
            while(!actor.Finished&&updates<80)Step(1);
            Step(2);
            FailIf(!actor.Finished||moves<10,"Ray must move through the original unsigned camera-relative exit and stay retired.");
            _entities.Clear();_player.EndCutsceneControl(actor);
        }
        ReinitializeGameplayForValidation();
    }
}
