using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCleansedFairyPublicationRom()
    {
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(3,0xef);_roomEvents.Get<SeaFairyEvent>().Cancel();_entities.Clear();
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(40,104));
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            bool fade=false;var data=new SeaFairyDatabase();
            FailIf(data.Timing[2]!=0x40||data.Timing[3]!=2,"Fairy native circle counters must remain $40/$02.");
            _entities.Spawn(new CleansedFairySpawn(data,new(8.25f,8.5f),()=>fade=true,()=>{}));
            var actor=_entities.EntityAdapters<CleansedFairyRoomEntity>().Single();_player.BeginCutsceneControl(owner:actor);
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,40,104);
            int at=0xd040+_entities.InteractionSlot(actor.Node)*256;
            native[at]=1;native[at+1]=0xd5;native[at+2]=1;native.Word(at+10,0x0880);native.Word(at+12,0x0840);
            StepGameplayUpdates(1,Vector2.Zero,afterUpdate:()=>native.AdvanceInteractions(_entities.FrameCounter));
            F1Retain(actor);for(int slot=0;slot<16;slot++){int other=0xd040+slot*256;if(other!=at)native[other]=0;}
            // Declare the post-TX_4109 circle boundary. Initialization/flash/rays
            // retain separate full-scene coverage; this runs the real native tail.
            F1Set(actor,"<State>k__BackingField",1);F1Set(actor,"_counter",0x40);F1Set(actor,"_angleCounter",2);F1Set(actor,"_angle",8);
            native[at+5]=1;native[at+6]=0x40;native[at+7]=2;native[at+9]=8;native[at+16]=0x78;
            int updates=0,moves=0;bool heldDuringRise=false;
            void Seed(){for(int i=0;i<4;i++){byte value=(byte)(0xa5+i);native[0xcec0+i]=value;_runtimeState.SetWramByte(0xcec0+i,value);}}
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                int previousX=native.Word(at+12),previousY=native.Word(at+10);
                native.AdvanceInteractions(_entities.FrameCounter);updates++;
                if(previousX!=native.Word(at+12)||previousY!=native.Word(at+10))moves++;
                FailIf(SomariaPrivate<Vector2>(actor,"_position")!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    actor.Node.Position!=new Vector2(native[at+13],native[at+11])||actor.State!=native[at+5]||actor.Counter!=native[at+6]||
                    actor.Angle!=native[at+9]||actor.Height!=unchecked((sbyte)native[at+15])||
                    SomariaPrivate<int>(actor,"_angleCounter")!=native[at+7]||
                    ((NpcCharacter)actor.Node).CurrentAnimationCounter!=native[at+0x20]||
                    fade!=(native[0xc4ab]!=0)||Enumerable.Range(0,4).Any(i=>_runtimeState.ReadWramByte(0xcec0+i)!=native[0xcec0+i]),
                    $"Restored fairy update{updates}: native circle/rise/counters/animation/fade/publication differs.");
                Seed();
            });
            void Hold(int kind)
            {
                if(kind==0){_dialogue.ShowMessage("Fairy native text hold.",_player.Position.Y);native[0xcba0]=1;}
                if(kind==1){_runtimeState.SetWramByte(0xcc8a,2);native[0xcc8a]=2;}
                if(kind==2){_runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;}
                if(kind==3){_runtimeState.SetWramByte(WramAddress.wTextIsActive,0x80);native[0xcba0]=0x80;}
                Step(3);
                if(kind==0){_dialogue.Close();native[0xcba0]=0;}
                if(kind==1){_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;}
                if(kind==2){_runtimeState.SetWramByte(0xcd00,1);native[0xcd00]=1;}
                if(kind==3){_runtimeState.SetWramByte(WramAddress.wTextIsActive,0);native[0xcba0]=0;}
            }
            Seed();Step(4);Hold(0);Hold(1);Hold(2);Hold(3);
            while(!fade&&updates<200)
            {
                if(!heldDuringRise&&actor.State==2){heldDuringRise=true;Hold(0);Hold(1);Hold(2);Hold(3);}
                Step(1);
            }
            FailIf(!fade||moves!=128||!heldDuringRise||actor.Height!=-82,"Fairy must complete128 circle moves then33 native high-Z steps to the strict below-$b0 fade.");
            _entities.Clear();
            StepGameplayUpdates(2,Vector2.Zero,batched:batched,afterUpdate:()=>
                FailIf(Enumerable.Range(0,4).Any(i=>_runtimeState.ReadWramByte(0xcec0+i)!=(byte)(0xa5+i)),"Removed fairy must stop publishing movement."));
            _player.EndCutsceneControl(actor);
        }
    }
}
