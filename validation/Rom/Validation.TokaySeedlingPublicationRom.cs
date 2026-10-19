using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTokaySeedlingPublicationRom()
    {
        foreach(int angle in new[]{0,8,0x10,0x18})
        foreach(int free in new[]{0,1})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);_inventory.GiveTreasure(0x4d,1);
            _saveData.SetRoomFlag(0,0xac,0x80,false);_saveData.SetRoomFlag(1,0xac,0x80,false);
            LoadValidationRoom(1,0xac);var owner=_roomEvents.Get<TokaySeedlingPlotEvent>();
            var actor=_entities.Entities<TokayCharacter>().Single(a=>a.Record.SubId==0x11);
            F1Retain(_entities.EntityAdapters<IRoomEntity>().Single(e=>ReferenceEquals(e.Node,actor)));
            _player.WarpTo(new(120,104));_player.BeginCutsceneControl(owner:owner);_runtimeState.SetWramByte(0xcd00,0);
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,120,104);
            int at=0xd040+_entities.InteractionSlot(actor)*256;
            native[at]=1;native[at+1]=0x48;native[at+2]=0x11;native[at+11]=(byte)actor.Position.Y;native[at+13]=(byte)actor.Position.X;
            native.AdvanceInteractions();FailIf(native[at]==0,"Original seedling Tokay state0 must retain its actor.");
            for(int i=0;i<13-free;i++)
            {
                var puff=new PuzzlePuffEffect();puff.Initialize(new(24,24),SoundId.MusNone);
                _entities.AddEntity(new InteractionSlotValidationEntity(puff,static _=>{}));
                int occupied=0xd040+_entities.InteractionSlot(puff)*256;
                native[occupied]=1;native[occupied+1]=0;native[occupied+4]=1;native[occupied+0x20]=0xff;
            }
            Vector2 initial=actor.Position+(fractional?new Vector2(.25f,.5f):Vector2.Zero);
            F1Set(owner,"_actor",actor);F1Set(owner,"_precisePosition",initial);actor.SetStatePosition(initial.Floor());
            F1Set(owner,"_stage",TokaySeedlingPlotStage.Moving);F1Set(owner,"_counter",16);F1Set(owner,"_moveAngle",angle);
            F1Set(owner,"_towardDirection",new Vector2I((int)OracleObjectMath.StrictCardinalVector(angle^0x10).X,(int)OracleObjectMath.StrictCardinalVector(angle^0x10).Y));
            native.Word(at+10,(int)(initial.Y*256));native.Word(at+12,(int)(initial.X*256));
            native[at+6]=0;native[at+7]=16;native[at+9]=(byte)angle;native[at+16]=0x28;
            native.Word(at+0x18,0x6241); // Original flip/plant/spawn/sound/wait120 continuation.
            native[0xcc8a]=0x81;native[0xcc02]=0x80;native[0xcba0]=0;
            var sounds=_sound.AttachPlayRequestAudit();sounds.Clear();int sourceSoundStart=native.Sounds.Count;
            var flagOrder=new List<(bool Past,bool Present,bool Seedling)>();
            (bool Past,bool Present,bool Seedling) prior=(false,false,true);
            void ObserveFlags()
            {
                var current=(_saveData.HasRoomFlag(1,0xac,0x80),_saveData.HasRoomFlag(0,0xac,0x80),_inventory.HasTreasure(0x4d));
                if(current!=prior){flagOrder.Add(current);prior=current;}
            }
            _saveData.Changed+=ObserveFlags;
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
                FailIf(SomariaPrivate<Vector2>(owner,"_precisePosition")!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    actor.Position!=new Vector2(native[at+13],native[at+11])||
                    owner.Counter!=(native[at+7]!=0?native[at+7]:native[at+6])||!observed.SequenceEqual(expected),
                    $"Seedling angle${angle:x2}/free{free}/update{updates}: native full-word motion, counter/continuation or publication differs (counter{owner.Counter}/{native[at+6]},{native[at+7]}, stage{owner.Stage}).");
                var children=_entities.Entities<TokaySeedlingDecorationRoomEntity>();
                int[] slots=Enumerable.Range(2,14).Where(s=>native[0xd040+s*256]!=0&&native[0xd041+s*256]==0x80).ToArray();
                FailIf(children.Count!=slots.Length||children.Any(child=>_entities.InteractionSlot(child)!=slots.Single()),
                    "Seedling spawn must use original checked physical INTERACTION capacity and preserve failure without a logical child.");
                bool seedling=(native[0xc69a+0x4d/8]&(1<<(0x4d&7)))!=0;
                FailIf(_inventory.HasTreasure(0x4d)!=seedling||_saveData.HasRoomFlag(1,0xac,0x80)!=((native[0xc8ac]&0x80)!=0)||
                    _saveData.HasRoomFlag(0,0xac,0x80)!=((native[0xc7ac]&0x80)!=0)||!sounds.Requests.SequenceEqual(native.Sounds.Skip(sourceSoundStart)),
                    $"Seedling update{updates}: original both-era flags, treasure removal or sound/creation ordering differs.");
                armed=false;
            });
            Step(4);_dialogue.ShowMessage("Seedling script hold.",104);_dialogue.RevealCurrentPageForValidation();native[0xcba0]=1;
            Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,2);native[0xcc8a]=0x83;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0x81;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            while(native[at+6]==0&&updates<30)Step(1);
            FailIf(moves!=15||native[at+6]!=120||owner.Stage!=TokaySeedlingPlotStage.DoneTextWait,
                "Seedling must execute15 moves and all original post-move yields before installing wait120.");
            FailIf(!flagOrder.SequenceEqual(new[]{(true,false,true),(true,true,true),(true,true,false)}),
                "tokayPlantScentSeedling must set past then present flags before losing TREASURE$4d.");
            _saveData.Changed-=ObserveFlags;
            _runtimeState.Changed-=Capture;owner.Cancel();_entities.Clear();
        }
        ValidateTokaySeedlingReachability();
    }

    private void ValidateTokaySeedlingReachability()
    {
        foreach(bool batched in new[]{false,true})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);_inventory.GiveTreasure(0x4d,1);_inventory.SetScriptedEquippedItems(0,0);
            LoadValidationRoom(1,0xac);var owner=_roomEvents.Get<TokaySeedlingPlotEvent>();
            var actor=_entities.Entities<TokayCharacter>().Single(a=>a.Record.SubId==0x11);
            void Approach()
            {
                foreach(var (direction,action) in new[]{(Vector2.Right,"move_right"),(Vector2.Left,"move_left"),(Vector2.Up,"move_up"),(Vector2.Down,"move_down")})
                {
                    Vector2 start=actor.Position-direction*40;
                    bool safe=Enumerable.Range(0,29).All(i=>
                    {
                        Vector2 point=start+direction*i;
                        return point.X>=6&&point.Y>=3&&point.X<154&&point.Y<122&&!_collision.Collides(point)&&
                            new[]{new Vector2(-5,-2),new Vector2(5,-2),new Vector2(-5,5),new Vector2(5,5)}
                                .All(offset=>_currentRoom.GetTerrainInfo(point+offset).Hazard==HazardType.None);
                    });
                    if(!safe)continue;
                    _player.WarpTo(start);StepGameplayUpdates(40,direction,[action],[action],batched);
                    FailIf(_collision.Collides(_player.Position)||!actor.CanTalkTo(_player),
                        "The seedling Tokay must be reached by normal movement from safe room terrain.");
                    return;
                }
                throw new InvalidOperationException("Room1:ac has no declared safe straight approach to the seedling Tokay.");
            }
            void Press()=>StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched);
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched);
            Approach();Press();FailIf(!_dialogue.IsOpen||owner.Stage!=TokaySeedlingPlotStage.Intro,"Real A input must start the seedling script.");
            _dialogue.Close();Step(31);FailIf(!_dialogue.IsOpen||owner.Stage!=TokaySeedlingPlotStage.PlantText,"Seedling wait30 must open its planting text.");
            _dialogue.Close();Step(1);Step(16);Step(3);
            FailIf(owner.Stage!=TokaySeedlingPlotStage.DoneTextWait||owner.Counter!=120||_inventory.HasTreasure(0x4d)||
                _entities.Entities<TokaySeedlingDecorationRoomEntity>().Count!=1,
                "Actual seedling conversation must finish movement/creation/sound before its full wait120.");
            Step(120);FailIf(!_dialogue.IsOpen||owner.Stage!=TokaySeedlingPlotStage.DoneText,"Seedling completion text must follow wait120.");
            _dialogue.Close();Step(1);FailIf(owner.HasState||_player.CutsceneControlled,"Seedling completion must release input.");
            Approach();Press();FailIf(!_dialogue.IsOpen||owner.Stage!=TokaySeedlingPlotStage.DialogueOnly||
                _entities.Entities<TokaySeedlingDecorationRoomEntity>().Count!=1,
                "A repeated reachable conversation must use the planted state and retain exactly one decoration.");
            _dialogue.Close();Step(1);
        }
    }
}
