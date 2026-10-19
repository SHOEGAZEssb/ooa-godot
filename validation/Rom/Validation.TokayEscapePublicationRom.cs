using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTokayEscapePublicationRom()
    {
        foreach(int segment in new[]{0,1,2})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");save.SetLinkedGame(true);
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);_inventory.LoseTreasure(TreasureId.Shovel);_saveData.SetRoomFlag(1,0xbb,0x80,false);
            LoadValidationRoom(1,0xbb);var owner=_roomEvents.Get<TokayRunningFromRosaEvent>();
            var actor=_entities.Entities<TokayCharacter>().Single(a=>a.Record.SubId==0x0b);
            _player.WarpTo(new(112,80));_player.BeginCutsceneControl(owner:owner);_runtimeState.SetWramByte(0xcd00,0);
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,112,80);
            // bank1 initializes live wIsLinkedGame from the file byte before
            // room objects. SomariaRom otherwise declares only save-backed RAM.
            native[0xcc01]=1;
            int at=0xd040+_entities.InteractionSlot(actor)*256;
            native[at]=1;native[at+1]=0x48;native[at+2]=0x0b;native[at+11]=(byte)actor.Position.Y;native[at+13]=(byte)actor.Position.X;
            native.AdvanceInteractions();
            FailIf(native[at]==0||native[at+4]!=1,"Original linked Tokay state0 must retain and initialize this source actor.");
            native[0xcc8a]=0x81;native[0xcc02]=0x80;native[0xcba0]=0;
            int frames=segment switch{0=>17,1=>57,_=>43},angle=segment==2?0x18:0;
            // Original post-move bytes: wait30 at$60ca, wait6 at$60e5,
            // enableinput/orroomflag80/scriptend at$60e9.
            int pc=segment switch{0=>0x60ca,1=>0x60e5,_=>0x60e9};
            Vector2 initial=actor.Position+(fractional?new Vector2(.25f,.5f):Vector2.Zero);
            F1Set(owner,"_position",initial);actor.SetStatePosition(initial.Floor());
            F1Set(owner,"_stage",TokayRunningFromRosaStage.ActorMove);F1Set(owner,"_counter",frames);
            F1Set(owner,"_moveDirection",segment==2?Vector2.Left:Vector2.Up);
            F1Set(owner,"_nextStage",segment switch{0=>TokayRunningFromRosaStage.FirstPause,1=>TokayRunningFromRosaStage.SecondPause,_=>TokayRunningFromRosaStage.Done});
            native.Word(at+10,(int)(initial.Y*256));native.Word(at+12,(int)(initial.X*256));
            native[at+6]=0;native[at+7]=(byte)frames;native[at+9]=(byte)angle;native[at+16]=0x3c;native.Word(at+0x18,pc);
            byte[] observed=new byte[4],expected=new byte[4];bool armed=false;int updates=0,moves=0;
            void Capture()
            {
                if(!armed||_runtimeState.ReadWramByte(0xcec3)==0xa8)return;
                for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);armed=false;
            }
            _runtimeState.Changed+=Capture;
            _entities.AddEntity(new PartMovementScratchProbe(()=>
            {
                armed=false;for(int i=0;i<4;i++)
                {observed[i]=(byte)(0xa5+i);_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));}armed=true;
            }));
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                native.AdvanceInteractions(_entities.FrameCounter);updates++;
                if(native[0xcec3]!=0xa8)moves++;
                for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];
                FailIf(actor.Active!=(native[at]!=0)||actor.Active&&
                    (SomariaPrivate<Vector2>(owner,"_position")!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                        actor.Position!=new Vector2(native[at+13],native[at+11])||
                        SomariaPrivate<int>(owner,"_counter")!=(native[at+7]!=0?native[at+7]:native[at+6]))||!observed.SequenceEqual(expected),
                    $"Escape segment{segment}/update{updates}: original movement/admission/counter differs: active{actor.Active}/{native[at]}, XY{SomariaPrivate<Vector2>(owner,"_position")}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, counter{SomariaPrivate<int>(owner,"_counter")}/{native[at+6]},{native[at+7]}, stage{owner.Stage}, allowed{_entities.InitializedInteractionUpdatesAllowed}, scratch[{string.Join(',',observed)}]/[{string.Join(',',expected)}].");
                FailIf(_saveData.HasRoomFlag(1,0xbb,0x80)!=((native[0xc8bb]&0x80)!=0),
                    "Escape room flag must follow the original final enableinput/orroomflag/scriptend chain.");
                FailIf(owner.MenusDisabled!=(native[0xcc02]!=0)||_player.CutsceneControlled!=((native[0xcc8a]&0x81)!=0),
                    "Escape menu/input ownership must end before the final scriptend update.");
                armed=false;
            });
            Step(4);_dialogue.ShowMessage("Ordinary escaping Tokay hold.",80);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,2);native[0xcc8a]=0x83;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0x81;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            Step(frames-3);FailIf(moves!=frames-1,"Each escaping Tokay segment must execute counter-1 original steps, then a zero and continuation update.");
            if(segment==2)
            {
                FailIf(!actor.Active||!owner.HasState||_player.CutsceneControlled,
                    "The escaping Tokay must retain its own slot for the flag-setting yield after releasing Link.");
                Step(1);FailIf(actor.Active||owner.HasState,"The next admitted source scriptend must delete the escaping Tokay.");
            }
            _runtimeState.Changed-=Capture;owner.Cancel();_entities.Clear();
            FailIf(_player.CutsceneControlled,"Escaping Tokay cancellation/completion must release input.");
        }
    }
}
