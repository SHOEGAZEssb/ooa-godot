using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateCleanSeasZoraPublicationRom()
    {
        foreach(int subid in new[]{0x0a,0x0b,0x0c,0x0d})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(3,0xef);_roomEvents.Get<SeaFairyEvent>().Cancel();
            _entities.Clear();_inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(40,104));
            var data=new SeaFairyDatabase();
            _entities.Spawn(new CleanSeasZoraSpawn(data,new(8.25f,8.5f),subid));
            var actor=_entities.EntityAdapters<CleanSeasZoraRoomEntity>().Single();
            _player.BeginCutsceneControl(owner:actor);_runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            _runtimeState.SetWramByte(0xcfc0,0);
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,40,104);
            int at=0xd040+_entities.InteractionSlot(actor.Node)*256;
            native[at]=1;native[at+1]=0xab;native[at+2]=(byte)subid;native.Word(at+10,0x0880);native.Word(at+12,0x0840);
            var runner=SomariaPrivate<CutsceneCommandRunner>(actor,"_runner");
            int updates=0,moves=0,airborne=0;
            void Seed(){for(int i=0;i<4;i++){byte value=(byte)(0xa5+i);native[0xcec0+i]=value;_runtimeState.SetWramByte(0xcec0+i,value);}}
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                int oldX=native.Word(at+12),oldY=native.Word(at+10);
                native.AdvanceInteractions(_entities.FrameCounter);updates++;
                if(oldX!=native.Word(at+12)||oldY!=native.Word(at+10))moves++;
                if(native.Word(at+14)!=0)airborne++;
                FailIf(SomariaPrivate<Vector2>(actor,"_position")!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    actor.Node.Position!=new Vector2(native[at+13],native[at+11])||
                    SomariaPrivate<int>(actor,"_z")!=unchecked((short)native.Word(at+14))||
                    SomariaPrivate<int>(actor,"_speedZ")!=unchecked((short)native.Word(at+0x14))||
                    SomariaPrivate<int>(actor,"_state")!=native[at+4]||
                    ((NpcCharacter)actor.Node).CurrentAnimationCounter!=native[at+0x20]||
                    subid<0x0c&&SomariaPrivate<int>(actor,"_counter")!=native[at+6]||
                    subid>=0x0c&&runner.MovementCounter("Zora")!=native[at+7]||
                    _runtimeState.ReadWramByte(0xcfc0)!=native[0xcfc0]||
                    Enumerable.Range(0,4).Any(i=>_runtimeState.ReadWramByte(0xcec0+i)!=native[0xcec0+i]),
                    $"Clean-seas $ab:${subid:x2} update{updates}: native XY/Z/counters/animation/signal/publication differs.");
                Seed();
            });
            void Hold(int kind)
            {
                if(kind==0){_dialogue.ShowMessage("Zora script hold; physics continue.",_player.Position.Y);native[0xcba0]=1;}
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
            bool airHold=false,moveHold=false;
            while(updates<390)
            {
                if(!airHold&&native.Word(at+14)!=0){airHold=true;Hold(0);Hold(1);Hold(2);Hold(3);}
                if(subid>=0x0c&&!moveHold&&native[at+7]>2){moveHold=true;Hold(0);Hold(1);Hold(2);Hold(3);}
                Step(1);
            }
            // Source scripts issue $11/$17 counters, both decrement before
            // movement. Native counter actors never apply planar speed.
            FailIf(moves!=(subid>=0x0c?38:0)||airborne==0||!airHold||subid>=0x0c&&!moveHold,
                "Clean-seas comparison must exercise all source motion and airborne gates.");
            if(subid>=0x0c)
            {
                _runtimeState.SetWramByte(0xcfc0,0);native[0xcfc0]=0;
                Hold(0);Step(1);
                FailIf(_runtimeState.ReadWramByte(0xcfc0)!=0x80,"Ungated scriptend must republish the native signal after text closes.");
            }
            _entities.Clear();
            StepGameplayUpdates(2,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                FailIf(Enumerable.Range(0,4).Any(i=>_runtimeState.ReadWramByte(0xcec0+i)!=(byte)(0xa5+i)),
                    "Removed clean-seas actor must stop publishing movement.");
            });
            _player.EndCutsceneControl(actor);
        }
    }
}
