using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTokayDimitriPublicationRom()
    {
        foreach(int scroll in new[]{0,4,8,0x0e})
        foreach(int holdLength in new[]{3,20,36})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);_saveData.WriteWramByte(WramAddress.wDimitriState,1);
            _inventory.GiveTreasure(TreasureId.Essence,2);LoadValidationRoom(0,0xaa);
            var owner=_roomEvents.Get<TokayDimitriEvent>();
            var first=_entities.Entities<TokayCharacter>().Single(a=>a.Record.SubId==0x0f);
            var second=_entities.Entities<TokayCharacter>().Single(a=>a.Record.SubId==0x10);
            _player.WarpTo(new(80,104));_player.BeginCutsceneControl(owner:owner);
            _runtimeState.SetWramByte(0xcd00,0);
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,80,104);
            int a=0xd040+_entities.InteractionSlot(first)*256,b=0xd040+_entities.InteractionSlot(second)*256;
            foreach(var (actor,at,sub) in new[]{(first,a,0x0f),(second,b,0x10)})
            {
                native[at]=1;native[at+1]=0x48;native[at+2]=(byte)sub;
                native.Word(at+10,(int)(actor.Position.Y*256));native.Word(at+12,(int)(actor.Position.X*256));
            }
            native.AdvanceInteractions();
            // Declare both initialized moveleft counters, with original ROM
            // post-move continuations: $61d9 enablemenu/end; $621b ormemory,
            // enableallobjects/end. No native opcode is replaced.
            native[0xcba0]=0;native[0xd13e]=0x0c;native[0xcc8a]=0x81;native[0xcc02]=0x80;
            F1Set(owner,"_actor",first);F1Set(owner,"_departingFirst",first);F1Set(owner,"_departingSecond",second);
            F1Set(owner,"_stage",TokayDimitriStage.Departing);F1Set(owner,"_firstMoveCounter",16);F1Set(owner,"_secondMoveCounter",32);
            foreach(var (actor,at,field,pc,count) in new[]{(first,a,"_firstPosition",0x61d9,16),(second,b,"_secondPosition",0x621b,32)})
            {
                Vector2 position=actor.Position+(fractional?new Vector2(.25f,.5f):Vector2.Zero);
                F1Set(owner,field,position);actor.SetStatePosition(position.Floor());actor.NativeAnimation=TokayAnimationMode.Still;
                native.Word(at+10,(int)(position.Y*256));native.Word(at+12,(int)(position.X*256));
                native[at+6]=0;native[at+7]=(byte)count;native[at+9]=0x18;native[at+16]=0x50;
                native.Word(at+0x18,pc);
            }
            byte[] observed=new byte[4],expected=new byte[4];bool armed=false;int updates=0;
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
                for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];
                foreach(var (actor,at,field,counter) in new[]{(first,a,"_firstPosition","_firstMoveCounter"),(second,b,"_secondPosition","_secondMoveCounter")})
                    FailIf(actor.Active!=(native[at]!=0)||actor.Active&&
                        (SomariaPrivate<Vector2>(owner,field)!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                            actor.Position!=new Vector2(native[at+13],native[at+11])||
                            SomariaPrivate<int>(owner,counter)!=native[at+7]),
                        $"Dimitri Tokay sub${actor.Record.SubId:x2}/scroll${scroll:x2}/hold{holdLength}/update{updates}: original movement/admission/lifetime differs (active{actor.Active}/{native[at]:x2}, counter{SomariaPrivate<int>(owner,counter)}/{native[at+7]}).");
                FailIf(!observed.SequenceEqual(expected)||owner.MenusDisabled!=(native[0xcc02]!=0)||
                    (_saveData.ReadWramByte(WramAddress.wDimitriState)&2)!=(native[WramAddress.wDimitriState]&2),
                    $"Dimitri Tokay update{updates}: original publication/menu/state differs: scratch[{string.Join(',',observed)}]/[{string.Join(',',expected)}], menus{owner.MenusDisabled}/{native[0xcc02]:x2}, state{_saveData.ReadWramByte(WramAddress.wDimitriState):x2}/{native[WramAddress.wDimitriState]:x2}.");
                armed=false;
            });
            Step(4);_dialogue.ShowMessage("Tokay departure hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,2);native[0xcc8a]=0x83;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0x81;
            _runtimeState.SetWramByte(0xcd00,(byte)scroll);native[0xcd00]=(byte)scroll;Step(holdLength);
            _runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;Step(35);
            FailIf(first.Active||second.Active||owner.HasState||_player.CutsceneControlled,
                "Both Tokay source continuations must complete and release input.");
            _runtimeState.Changed-=Capture;owner.Cancel();_entities.Clear();
        }
    }
}
