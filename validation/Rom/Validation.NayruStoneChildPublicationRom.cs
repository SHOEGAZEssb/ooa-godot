using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateNayruStoneChildPublicationRom()
    {
        foreach(bool batched in new[]{false,true})
        foreach(bool holds in new[]{false,true})
        {
            ReinitializeGameplayForValidation();_saveData.SetGlobalFlag(GlobalFlag.IntroDone,false);LoadValidationRoom(0,0x39);
            var owner=_roomEvents.Get<NayruIntroEvent>();((ICutsceneCommandHost)owner).RunNativeHandler("BeginNayruVignette2");
            FieldInfo stage=typeof(NayruIntroEvent).GetField("_nayruStage",BindingFlags.Instance|BindingFlags.NonPublic)!;
            stage.SetValue(owner,Enum.ToObject(stage.FieldType,12));
            owner.CommandRunner.Start([new CutsceneWaitFramesCommand(new NayruIntroEventDatabase().Commands[0].Source,2000)]);
            var vignette=SomariaPrivate<NayruStoneChildVignette>(owner,"_stoneChildVignette");
            var data=new NayruIntroEventDatabase();
            var actors=new[]{owner.ActorRegistry["VignetteBoy"],owner.ActorRegistry["VignetteLady"]};
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,40,104);
            int[] addresses=actors.Select(actor=>0xd040+_entities.InteractionSlot(actor)*256).ToArray();
            for(int i=0;i<2;i++)
            {
                int at=addresses[i];native[at]=1;native[at+1]=(byte)(i==0?0x3c:0x3d);native[at+2]=1;
                actors[i].Position+=new Vector2(0.25f,0.5f);
                native.Word(at+10,(int)(actors[i].Position.Y*256));native.Word(at+12,(int)(actors[i].Position.X*256));
            }
            native.AdvanceInteractions(); // Declared state-zero graphics/script admission.
            _player.WarpTo(new(40,104));_player.BeginCutsceneControl(owner:owner);
            _runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;
            _runtimeState.SetWramByte(0xcfdf,0);native[0xcfdf]=0;
            int updates=0,moves=0;
            bool openDuringPass=false;int laterTextVisits=0;
            _entities.AddEntity(new PartMovementScratchProbe(()=>
            {
                if(openDuringPass){laterTextVisits++;_dialogue.ShowMessage("Earlier admitted interaction opens text.",_player.Position.Y);}
            }));
            void Seed(){for(int i=0;i<4;i++){byte value=(byte)(0xa5+i);native[0xcec0+i]=value;_runtimeState.SetWramByte(0xcec0+i,value);}}
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                int[] previous=addresses.Select(at=>native.Word(at+12)+native.Word(at+10)*65536).ToArray();
                native.AdvanceInteractions(_entities.FrameCounter);updates++;
                for(int i=0;i<2;i++)
                {
                    int at=addresses[i];if(previous[i]!=native.Word(at+12)+native.Word(at+10)*65536)moves++;
                    FailIf(actors[i].Position!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                        actors[i].CurrentAnimationCounter!=native[at+0x20],
                        $"Stone child actor{i} update{updates}: native XY/animation differs ({actors[i].Position}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, animation{actors[i].CurrentAnimationCounter}/{native[at+0x20]}).");
                }
                int boyState=SomariaPrivate<int>(vignette,"_boyState"),ladyState=SomariaPrivate<int>(vignette,"_ladyState");
                FailIf(boyState!=native[addresses[0]+5]||Math.Min(ladyState,3)!=native[addresses[1]+5]||
                    ladyState>0&&SomariaPrivate<int>(vignette,"_ladyCounter")!=native[addresses[1]+6]||
                    _runtimeState.ReadWramByte(0xcfd1)!=native[0xcfd1]||
                    vignette.Complete!=(native[0xcfdf]==255)||_runtimeState.ReadWramByte(0xcfdf)!=native[0xcfdf]||
                    Enumerable.Range(0,4).Any(i=>_runtimeState.ReadWramByte(0xcec0+i)!=native[0xcec0+i]),
                    $"Stone child update{updates}: native states, signal/completion or movement publication differs.");
                if(boyState>0)
                {
                    var palette=SomariaPrivate<Color[]>(actors[0],"_paletteOverride");
                    FailIf(!palette.SequenceEqual(native[addresses[0]+0x1c]==6?data.StoneSpritePalette:data.BoySpritePalette),
                        $"Stone-child palette must follow native admitted updates (update{updates}, state{boyState}, flags${native[addresses[0]+0x1c]:x2}).");
                }
                Seed();
            });
            void Hold(int kind)
            {
                if(kind==0){_dialogue.ShowMessage("Stone-child ordinary hold.",_player.Position.Y);native[0xcba0]=1;}
                if(kind==1){_runtimeState.SetWramByte(0xcc8a,3);native[0xcc8a]=3;}
                if(kind==2){_runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;}
                Step(3);
                if(kind==0){_dialogue.Close();native[0xcba0]=0;}
                if(kind==1){_runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;}
                if(kind==2){_runtimeState.SetWramByte(0xcd00,1);native[0xcd00]=1;}
            }
            void LateText()
            {
                // A declared earlier initialized boy executes a minimal native
                // showtext stream. The original category walk admits everyone
                // first; the two real handlers then read live script text gates.
                int writer=addresses.Min()-256;
                FailIf(writer<0xd040,"Stone-child late writer requires an earlier interaction slot.");
                native[writer]=1;native[writer+1]=0x3c;native[writer+2]=1;native[writer+4]=1;
                native[writer+5]=0;native[writer+6]=native[writer+7]=0;native.Word(writer+0x18,0xc180);
                native[0xc180]=0x98;native[0xc181]=0x57;native[0xc182]=3;
                openDuringPass=true;Step(1);openDuringPass=false;
                FailIf(native[0xcba0]==0||!_entities.InitializedInteractionUpdatesAllowed,
                    "Native earlier showtext must preserve this update's ordinary admission.");
                Step(2);native[writer]=0;_dialogue.Close();native[0xcba0]=0;
            }
            Seed();Step(4);if(holds){Hold(0);Hold(1);Hold(2);}
            bool paletteHold=false,postHold=false;
            while(!vignette.Complete&&updates<750)
            {
                if(holds&&!paletteHold&&SomariaPrivate<int>(vignette,"_boyState")==1){paletteHold=true;Hold(0);Hold(1);Hold(2);LateText();}
                if(holds&&!postHold&&SomariaPrivate<int>(vignette,"_ladyState")==2){postHold=true;Hold(0);Hold(1);Hold(2);LateText();}
                Step(1);
            }
            // scripts.s:79+79+47+63 boy moves;13+12 lady moves.
            FailIf(!vignette.Complete||moves!=293||holds&&(!paletteHold||!postHold||laterTextVisits!=2),"Stone child must exercise every source movement, signal and native post-script wait.");
            owner.Cancel();_entities.Clear();
            StepGameplayUpdates(2,Vector2.Zero,batched:batched);
            FailIf(_player.CutsceneControlled||owner.ActorRegistry.Count!=0,"Stone vignette cancellation retained actors or control.");
        }
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();_saveData.SetGlobalFlag(GlobalFlag.IntroDone,false);LoadValidationRoom(0,0x39);
            var owner=_roomEvents.Get<NayruIntroEvent>();((ICutsceneCommandHost)owner).RunNativeHandler("BeginNayruVignette2");
            FieldInfo stage=typeof(NayruIntroEvent).GetField("_nayruStage",BindingFlags.Instance|BindingFlags.NonPublic)!;
            stage.SetValue(owner,Enum.ToObject(stage.FieldType,12));
            owner.CommandRunner.Start([new CutsceneWaitFramesCommand(new NayruIntroEventDatabase().Commands[0].Source,2000)]);
            _player.WarpTo(new(40,104));_player.BeginCutsceneControl(owner:owner);
            // Isolate retired interaction publication from the normal
            // post-object warp query, which reuses $cec0 after control release.
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            StepGameplayUpdates(4,Vector2.Zero,batched:batched);
            FailIf(owner.ActorRegistry["VignetteBoy"].Position.X!=118,"Cancellation boundary must follow two real boy movement updates.");
            owner.Cancel();_entities.Clear();
            for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));
            StepGameplayUpdates(2,Vector2.Zero,batched:batched);
            FailIf(_player.CutsceneControlled||owner.ActorRegistry.Count!=0||
                Enumerable.Range(0,4).Any(i=>_runtimeState.ReadWramByte(0xcec0+i)!=(byte)(0xa5+i)),
                $"Mid-motion cancellation must stop stone-child movement/publication and release control (control={_player.CutsceneControlled}, actors={owner.ActorRegistry.Count}, scratch={string.Join(',',Enumerable.Range(0,4).Select(i=>_runtimeState.ReadWramByte(0xcec0+i).ToString("x2")))}).");
        }
    }
}
