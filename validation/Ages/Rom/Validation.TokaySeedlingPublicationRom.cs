using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

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
            save.SetGlobalFlag(AgesGlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);_inventory.GiveTreasure(0x4d,1);
            _saveData.SetRoomFlag(0,0xac,0x80,false);_saveData.SetRoomFlag(1,0xac,0x80,false);
            LoadValidationRoom(1,0xac);
            var slot=_entities.EntityAdapters<TokayNpcRoomEntity>().Single(s=>s.Npc.Record.SubId==0x11);
            var actor=(TokayCharacter)slot.Npc;var script=slot.Script!;F1Retain(slot);
            // Isolate facing/arithmetic here. Reachability is tested below.
            Vector2 link=actor.Position+OracleObjectMath.StrictCardinalVector(angle^0x10)*16;
            _player.WarpTo(link);_player.BeginCutsceneControl(owner:slot);
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,(int)link.X,(int)link.Y);
            int at=0xd040+_entities.InteractionSlot(actor)*256;
            native[at]=1;native[at+1]=0x48;native[at+2]=0x11;
            native[at+11]=(byte)actor.Position.Y;native[at+13]=(byte)actor.Position.X;
            for(int i=0;i<13-free;i++)
            {
                var puff=new PuzzlePuffEffect();puff.Initialize(new(24,24),SoundId.MusNone);
                _entities.AddEntity(new InteractionSlotValidationEntity(puff,static _=>{}));
                int occupied=0xd040+_entities.InteractionSlot(puff)*256;
                native[occupied]=1;native[occupied+1]=0;native[occupied+4]=1;native[occupied+0x20]=0xff;
            }
            Vector2 initial=actor.Position+(fractional?new Vector2(.25f,.5f):Vector2.Zero);
            F1Set(script,"_precisePosition",initial);native.Word(at+10,(int)(initial.Y*256));native.Word(at+12,(int)(initial.X*256));
            var sounds=_sound.AttachPlayRequestAudit();sounds.Clear();int sourceSoundStart=native.Sounds.Count;
            var flagOrder=new List<(bool Past,bool Present,bool Seedling)>();
            (bool Past,bool Present,bool Seedling) prior=(false,false,true);
            void ObserveFlags()
            {
                var current=(_saveData.HasRoomFlag(1,0xac,0x80),_saveData.HasRoomFlag(0,0xac,0x80),_inventory.HasTreasure(0x4d));
                if(current!=prior){flagOrder.Add(current);prior=current;}
            }
            _saveData.Changed+=ObserveFlags;
            int updates=0,moves=0;
            string source=$"Seedling angle${angle:x2}/free{free}/fractional{fractional}/batched{batched}";
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                native.AdvanceInteractions(_entities.FrameCounter);updates++;
                if(script.MovementCounter>0&&script.MovementCounter<16)moves++;
                FailIf(script.PrecisePosition!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    actor.Position!=new Vector2(native[at+13],native[at+11])||actor.NativeAngle!=native[at+9]||
                    script.NativeCounter1!=native[at+6]||script.MovementCounter!=native[at+7]||
                    _runtimeState.ReadWramByte(0xcc8a)!=native[0xcc8a]||_runtimeState.ReadWramByte(0xcc02)!=native[0xcc02],
                    source+$" update{updates}: motion/counters/angle/input differ (cmd{script.CurrentCommandIndex}, angle{actor.NativeAngle:x2}/{native[at+9]:x2}, counter{script.NativeCounter1}/{native[at+6]}, move{script.MovementCounter}/{native[at+7]}, XY{script.PrecisePosition}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, mask{_runtimeState.ReadWramByte(0xcc8a):x2}/{native[0xcc8a]:x2}, menu{_runtimeState.ReadWramByte(0xcc02):x2}/{native[0xcc02]:x2}).");
                var children=_entities.Entities<TokaySeedlingDecorationRoomEntity>();
                int[] slots=Enumerable.Range(2,14).Where(s=>native[0xd040+s*256]!=0&&native[0xd041+s*256]==0x80).ToArray();
                FailIf(children.Count!=slots.Length||children.Any(child=>_entities.InteractionSlot(child)!=slots.Single()),
                    source+": checked physical decoration allocation differs.");
                bool seedling=(native[0xc69a+0x4d/8]&(1<<(0x4d&7)))!=0;
                FailIf(_inventory.HasTreasure(0x4d)!=seedling||_saveData.HasRoomFlag(1,0xac,0x80)!=((native[0xc8ac]&0x80)!=0)||
                    _saveData.HasRoomFlag(0,0xac,0x80)!=((native[0xc7ac]&0x80)!=0)||!sounds.Requests.SequenceEqual(native.Sounds.Skip(sourceSoundStart)),
                    source+$" update{updates}: flags, treasure removal or sound order differs.");
                native[0xcba0]=_dialogue.IsOpen?(byte)1:(byte)0;
            });
            Step(3);FailIf(!script.TryInteract(new(actor,null),_player),source+": cannot queue A.");
            FailIf((native[0xc69a+0x4d/8]&(1<<(0x4d&7)))==0,"Seedling ROM fixture lost TREASURE$4d before A.");
            native[at+0x31]=1;Step(1);
            for(int i=0;i<240;i++)
            {
                if(_dialogue.IsOpen){_dialogue.Close();native[0xcba0]=0;}
                Step(1);
            }
            FailIf(moves!=15||_runtimeState.ReadWramByte(0xcc8a)!=0||
                !flagOrder.SequenceEqual(new[]{(true,false,true),(true,true,true),(true,true,false)}),
                source+": must execute15 moves, publish past then present flags, lose TREASURE$4d and release input.");
            _saveData.Changed-=ObserveFlags;_player.EndCutsceneControl(slot);_entities.Clear();
        }
        ValidateTokaySeedlingReachability();
    }

    private void ValidateTokaySeedlingReachability()
    {
        foreach(bool batched in new[]{false,true})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");
            save.SetGlobalFlag(AgesGlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);_inventory.GiveTreasure(0x4d,1);_inventory.SetScriptedEquippedItems(0,0);
            _saveData.SetRoomFlag(0,0xac,0x80,false);_saveData.SetRoomFlag(1,0xac,0x80,false);
            LoadValidationRoom(1,0xac);
            var actor=_entities.Entities<TokayCharacter>().Single(a=>a.Record.SubId==0x11);
            Vector2 initial=actor.Position;
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
            void Press(){StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched);Step(1);}
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched);
            Approach();Press();FailIf(!_dialogue.IsOpen||_runtimeState.ReadWramByte(0xcc8a)!=0x81,"Real A input must start the seedling script.");
            _dialogue.Close();LoadValidationRoom(1,0xac);actor=_entities.Entities<TokayCharacter>().Single(a=>a.Record.SubId==0x11);
            FailIf(_runtimeState.ReadWramByte(0xcc8a)!=0||!_inventory.HasTreasure(0x4d),"Cancelling must release raw control without planting.");
            Approach();Press();
            _dialogue.Close();Step(31);FailIf(!_dialogue.IsOpen,"Seedling wait30 must open its planting text.");
            _dialogue.Close();Step(1);Step(1);Step(16);Step(3);
            FailIf(_inventory.HasTreasure(0x4d)||
                _entities.Entities<TokaySeedlingDecorationRoomEntity>().Count!=1,
                "Actual seedling conversation must finish movement/creation/sound before its full wait120.");
            Step(120);FailIf(!_dialogue.IsOpen,"Seedling completion text must follow wait120.");
            _dialogue.Close();Step(1);FailIf(_runtimeState.ReadWramByte(0xcc8a)!=0,"Seedling completion must release input.");
            Approach();Press();FailIf(!_dialogue.IsOpen||
                _entities.Entities<TokaySeedlingDecorationRoomEntity>().Count!=1||
                _dialogue.CurrentMessage!=DialogueBox.PlainText(new TokayInteractionDatabase().Text(0x0a43)),
                "A repeated reachable conversation must use the planted state and retain exactly one decoration.");
            _dialogue.Close();Step(1);
            _inventory.GiveTreasure(0x21,1);Approach();Press();
            FailIf(!_dialogue.IsOpen||_dialogue.CurrentMessage!=DialogueBox.PlainText(new TokayInteractionDatabase().Text(0x0a44)),
                "The planted loop must test TREASURE_SCENT_SEEDS$21 before selecting TX_0a44.");
            _dialogue.Close();LoadValidationRoom(1,0xac);actor=_entities.Entities<TokayCharacter>().Single(a=>a.Record.SubId==0x11);
            FailIf(actor.Position!=initial+new Vector2(16,0)||_entities.Entities<TokaySeedlingDecorationRoomEntity>().Count!=1,
                "Re-entry must restore the original planted X offset and decoration.");
        }
    }
}
