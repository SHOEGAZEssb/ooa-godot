using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTokayEscapePublicationRom()
    {
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        foreach(bool cancel in new[]{false,true})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");save.SetLinkedGame(true);
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);_inventory.LoseTreasure(TreasureId.Shovel);_inventory.SetScriptedEquippedItems(0,0);
            _saveData.SetRoomFlag(1,0xbb,0x80,false);LoadValidationRoom(1,0xbb);
            var slot=_entities.EntityAdapters<TokayNpcRoomEntity>().Single(s=>s.Npc.Record.SubId==0x0b);
            var actor=(TokayCharacter)slot.Npc;var script=slot.Script!;F1Retain(slot);
            var approach=(from startY in new[]{88,72}
                from x in Enumerable.Range(12,_currentRoom.Width-24)
                let direction=Math.Sign(80-startY)
                where Enumerable.Range(0,9).All(i=>
                {
                    Vector2 p=new(x,startY+direction*i);
                    return !_collision.Collides(p)&&!_entities.BlocksLink(p)&&
                        new[]{new Vector2(-5,-2),new Vector2(5,-2),new Vector2(-5,5),new Vector2(5,5)}
                            .All(o=>_currentRoom.GetTerrainInfo(p+o).Hazard==HazardType.None);
                })
                select (X:x,Y:startY,Direction:direction)).First();
            _player.WarpTo(new(approach.X,approach.Y));
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,approach.X,approach.Y);
            native[0xcc01]=1; // live wIsLinkedGame, initialized by bank1.
            int at=0xd040+_entities.InteractionSlot(actor)*256;
            native[at]=1;native[at+1]=0x48;native[at+2]=0x0b;
            Vector2 initial=actor.Position+(fractional?new Vector2(.25f,.5f):Vector2.Zero);
            native.Word(at+10,(int)(initial.Y*256));native.Word(at+12,(int)(initial.X*256));
            F1Set(script,"_precisePosition",initial);
            byte[] observed=new byte[4],expected=new byte[4];bool armed=false,externalHold=false;int updates=0;
            int[] moves=new int[3];int segment=-1,priorCounter=0;
            bool[] heldSegment=new bool[3];
            var sounds=_sound.AttachPlayRequestAudit();sounds.Clear();
            void Capture()
            {
                if(!armed||_runtimeState.ReadWramByte(0xcec3)==0xa8)return;
                for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);armed=false;
            }
            _runtimeState.Changed+=Capture;
            var before=new PartMovementScratchProbe(()=>
            {
                armed=false;for(int i=0;i<4;i++){observed[i]=(byte)(0xa5+i);_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));}armed=true;
            });
            _entities.RegisterEnemySlot(before,15);_entities.AddEntity(before);
            string source=$"Escape$48:$0b/fractional{fractional}/batched{batched}/cancel{cancel}";
            void Compare()
            {
                for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                native[0xd00b]=(byte)_player.Position.Y;native[0xd00d]=(byte)_player.Position.X;
                native.AdvanceInteractions(_entities.FrameCounter);updates++;
                for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];
                if(native[at+7]>0&&priorCounter==0)segment++;
                if(segment>=0&&native[0xcec3]!=0xa8)moves[segment]++;
                priorCounter=native[at+7];
                FailIf(actor.Active!=(native[at]!=0)||actor.Active&&
                    (script.PrecisePosition!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                        actor.Position!=new Vector2(native[at+13],native[at+11])||
                        script.NativeCounter1!=native[at+6]||script.MovementCounter!=native[at+7]||
                        actor.CurrentAnimationCounter!=native[at+0x20]||actor.CurrentAnimationParameter!=native[at+0x21]||
                        SomariaPrivate<int>(script,"_escapeZ")!=(short)native.Word(at+14)||
                        SomariaPrivate<int>(script,"_escapeSpeedZ")!=(short)native.Word(at+0x14))||!observed.SequenceEqual(expected),
                    source+$" update{updates}: movement/gravity/admission/counter differs (cmd{script.CurrentCommandIndex}, counter{script.NativeCounter1}/{native[at+6]}, move{script.MovementCounter}/{native[at+7]}).");
                FailIf(_saveData.GetRoomFlags(1,0xbb)!=native[0xc8bb]||
                    _runtimeState.ReadWramByte(0xcc8a)!=native[0xcc8a]||_runtimeState.ReadWramByte(0xcc02)!=native[0xcc02]||
                    _runtimeState.ReadWramByte(0xccde)!=native[0xccde],
                    source+": original room flag, digging restriction or raw control differs.");
                FailIf(!sounds.Requests.Where(s=>s==SoundId.SndJump).SequenceEqual(native.Sounds),source+": jump cue differs.");
                if(_dialogue.IsOpen&&!externalHold)
                {
                    int text=native.Word(0xcba2)-0x400;
                    FailIf(text is not (0x0a0e or 0x0a0f)||
                        _dialogue.CurrentMessage!=DialogueBox.PlainText(new TokayInteractionDatabase().Text(text)),source+": source text differs.");
                }
                native[0xcba0]=_dialogue.IsOpen?(byte)1:(byte)0;armed=false;
            }
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:Compare);
            Step(3);FailIf(_runtimeState.ReadWramByte(0xcc8a)!=0,source+": Link outside Y$50 must not trigger the script.");
            string action=approach.Direction<0?"move_up":"move_down";
            StepGameplayUpdates(8,new Vector2(0,approach.Direction),[action],[action],batched,Compare);
            int textHeld=0;
            for(int i=0;i<600&&actor.Active;i++)
            {
                if(_dialogue.IsOpen&&++textHeld>3){_dialogue.Close();native[0xcba0]=0;textHeld=0;}
                if(segment>=0&&!heldSegment[segment])
                {
                    heldSegment[segment]=true;
                    if(cancel)break;
                    externalHold=true;_dialogue.ShowMessage("Escape admission hold.",80);native[0xcba0]=1;Step(3);
                    _dialogue.Close();native[0xcba0]=0;externalHold=false;
                    _runtimeState.SetWramByte(0xcc8a,0x83);native[0xcc8a]=0x83;Step(3);
                    _runtimeState.SetWramByte(0xcc8a,0x81);native[0xcc8a]=0x81;
                    _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);
                    _runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
                }
                Step(1);
            }
            _runtimeState.Changed-=Capture;
            if(cancel)
            {
                LoadValidationRoom(4,0xa1);
                FailIf(_runtimeState.ReadWramByte(0xcc8a)!=0||_saveData.HasRoomFlag(1,0xbb,0x80),source+": cancellation must release raw control without committing the escape flag.");
                LoadValidationRoom(1,0xbb);
                FailIf(!_entities.Entities<TokayCharacter>().Single(a=>a.Record.SubId==0x0b).Active,source+": cancelled escape must re-enter as a live actor.");
            }
            else
            {
                FailIf(actor.Active||!moves.SequenceEqual(new[]{16,56,42})||
                    !_saveData.HasRoomFlag(1,0xbb,0x80)||_runtimeState.ReadWramByte(0xcc8a)!=0,
                    source+": full source script must execute16/56/42 moves, release control, set flag$80 and delete on the next eligible update.");
                LoadValidationRoom(1,0xbb);
                FailIf(_entities.Entities<TokayCharacter>().Any(a=>a.Record.SubId==0x0b&&a.Active),source+": completed actor must remain deleted on re-entry.");
            }
        }
    }
}
