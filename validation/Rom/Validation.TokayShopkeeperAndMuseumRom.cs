using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTokayShopkeeperScriptRom()
    {
        foreach(bool batched in new[]{false,true})
        foreach(bool purchased in new[]{false,true})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);_inventory.SetScriptedEquippedItems(0,0);
            var data=new TokayShopDatabase();_saveData.SetGlobalFlag(data.BoughtFeatherFlag,purchased);
            _saveData.SetGlobalFlag(data.BoughtBraceletFlag,purchased);
            LoadValidationRoom(2,0xe4);
            var npc=_entities.EntityAdapters<TokayNpcRoomEntity>().Single();
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,80,104);
            foreach(var item in _entities.Entities<TokayShopItem>())
            {
                int at=0xd040+_entities.InteractionSlot(item)*256;
                native[at]=1;native[at+1]=0x81;native[at+2]=(byte)item.OriginalSubId;
                native[at+11]=(byte)item.Position.Y;native[at+13]=(byte)item.Position.X;
            }
            int parent=0xd040+_entities.InteractionSlot(npc.Node)*256;
            FailIf(parent!=0xd540,"Room2:$e4 must reserve all three $81 placements before the $48:$0e shopkeeper, even when stock deletes in state0.");
            native[parent]=1;native[parent+1]=0x48;native[parent+2]=0x0e;
            native[parent+11]=0x20;native[parent+13]=0x50;
            string source=$"Tokay shopkeeper purchased={purchased}/batched={batched}";
            void Compare()
            {
                native[0xd00b]=(byte)_player.Position.Y;native[0xd00d]=(byte)_player.Position.X;
                native.AdvanceInteractions(_entities.FrameCounter);
                FailIf(npc.Script!.ReadMemory("ShopItemMatch")!=native[parent+0x3f],source+": FindShopItem must retain A=$81 on a match, not the object page from the source comment.");
                native[0xcba0]=_dialogue.IsOpen?(byte)1:(byte)0;
            }
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:Compare);
            void Approach()
            {
                Vector2I start=new(80,104);
                bool Safe(Vector2I p)=>p.X>=8&&p.Y>=8&&p.X<152&&p.Y<120&&!_collision.Collides(p)&&
                    new[]{new Vector2(-5,-2),new Vector2(5,-2),new Vector2(-5,5),new Vector2(5,5)}
                        .All(o=>_currentRoom.GetTerrainInfo((Vector2)p+o).Hazard==HazardType.None);
                FailIf(!Safe(start),source+": room entrance must be valid floor.");
                var directions=new[]{Vector2I.Up,Vector2I.Down,Vector2I.Left,Vector2I.Right};
                var targets=directions.Select(d=>(Vector2I)npc.Node.Position+d*12).Where(Safe).ToHashSet();
                var previous=new Dictionary<Vector2I,(Vector2I From,Vector2I Direction)>();
                var queue=new Queue<Vector2I>();queue.Enqueue(start);previous[start]=(start,Vector2I.Zero);
                Vector2I? target=null;
                while(queue.TryDequeue(out var p))
                {
                    if(targets.Contains(p)){target=p;break;}
                    foreach(var d in directions)
                    {
                        var next=p+d*4;
                        if(previous.ContainsKey(next)||!Enumerable.Range(1,4).All(i=>Safe(p+d*i)))continue;
                        previous[next]=(p,d);queue.Enqueue(next);
                    }
                }
                if(target is null)throw new InvalidOperationException(source+": no collision-valid entrance-to-NPC approach.");
                var path=new List<Vector2I>();
                for(var p=target.Value;p!=start;p=previous[p].From)path.Add(previous[p].Direction);
                path.Reverse();_player.WarpTo(start);
                foreach(var d in path)
                {
                    string action=d==Vector2I.Up?"move_up":d==Vector2I.Down?"move_down":d==Vector2I.Left?"move_left":"move_right";
                    StepGameplayUpdates(4,(Vector2)d,[action],[action],batched,Compare);
                }
                _player.Face((Vector2I)(npc.Node.Position-_player.Position).Normalized());
                FailIf(_collision.Collides(_player.Position)||!npc.Npc.CanTalkTo(_player),source+": walking from the entrance must reach A range.");
            }
            _player.WarpTo(new(80,104));Step(4);
            foreach(bool empty in new[]{false,true})
            {
                if(empty)
                {
                    foreach(var item in _entities.Entities<TokayShopItem>().ToArray())
                    {
                        int at=0xd040+_entities.InteractionSlot(item)*256;
                        for(int i=0;i<64;i++)native[at+i]=0;
                        item.Remove();
                    }
                    Step(1);
                }
                for(int repeat=0;repeat<2;repeat++)
                {
                    Approach();
                    native[parent+0x31]=1;
                    StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched,Compare);
                    int text=empty?0x0a38:0x0a37;
                    FailIf(!_dialogue.IsOpen||native.Word(0xcba2)!=text+0x400||
                        _dialogue.CurrentMessage!=DialogueBox.PlainText(new TokayInteractionDatabase().Text(text)),source+": real A input must select the original stock-dependent text.");
                    Step(3);_dialogue.Close();native[0xcba0]=0;Step(2);
                }
            }
            _entities.Clear();
        }
    }

    private void ValidateTokayMuseumStatuesRom()
    {
        foreach(bool batched in new[]{false,true})
        foreach(int free in new[]{0,1})
        foreach(int subid in new[]{0x1a,0x1b,0x1c})
        {
            ReinitializeGameplayForValidation();_inventory.SetScriptedEquippedItems(0,0);
            _runtimeState.SetWramByte(0xcfc0,0);LoadValidationRoom(2,0xe5);
            var slot=_entities.EntityAdapters<TokayStatueRoomEntity>().Single(s=>((TokayCharacter)s.Node).Record.SubId==subid);
            F1Retain(slot);var actor=(TokayCharacter)slot.Node;
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,80,104);
            int at=0xd040+_entities.InteractionSlot(actor)*256;
            native[at]=1;native[at+1]=0x48;native[at+2]=(byte)subid;
            native[at+11]=(byte)actor.Position.Y;native[at+13]=(byte)actor.Position.X;
            while(14-InteractionAdmissionPool.Count>free)
            {
                var puff=new PuzzlePuffEffect();puff.Initialize(new(24,24),SoundId.MusNone);
                _entities.AddEntity(new InteractionSlotValidationEntity(puff,static _=>{}));
                int occupied=0xd040+_entities.InteractionSlot(puff)*256;
                native[occupied]=1;native[occupied+1]=0;native[occupied+4]=1;native[occupied+0x20]=0xff;
            }
            string source=$"Museum $48:${subid:x2}/free{free}/batched{batched}";
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                native.AdvanceInteractions(_entities.FrameCounter);
                FailIf(actor.Active!=(native[at]!=0),source+": native statue deletion gate differs.");
                if(actor.Active)FailIf(actor.CurrentAnimationCounter!=native[at+0x20] || actor.Record.Palette!=native[at+0x1c],
                    source+": static animation or red palette differs from original initialization.");
                var children=_entities.Entities<TokayAttachedVisualRoomEntity>();
                int[] originals=Enumerable.Range(2,14).Select(s=>0xd040+s*256).Where(a=>native[a]!=0&&native[a+1]==0x63).ToArray();
                FailIf(children.Count!=originals.Length,source+": checked museum accessory count differs.");
                if(children.Count!=0)
                {
                    int child=originals.Single();var runtime=children.Single();
                    FailIf(_entities.InteractionSlot(runtime)!=((child>>8)-0xd0)||runtime.Position!=new Vector2(native[child+13],native[child+11])||
                        runtime.ZIndex!=ObjectDrawPriority.FromVisible(native[child+0x1a]),source+": accessory slot, source offset or priority differs.");
                }
            });
            _player.WarpTo(new(80,104));
            _dialogue.ShowMessage("Museum admission hold.",104);native[0xcba0]=1;
            _runtimeState.SetWramByte(0xcc8a,2);native[0xcc8a]=2;
            Step(1);Step(3);
            _runtimeState.SetWramByte(0xcfc0,1);native[0xcfc0]=1;Step(2);
            FailIf(!actor.Active,source+": initialized statues must remain frozen under dialogue/mask despite the live signal.");
            _dialogue.Close();native[0xcba0]=0;_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
            Step(2);FailIf(actor.Active,source+": native slot must consume inPresent after admission resumes.");
            _entities.Clear();
        }
    }
}
