using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTokayShieldScriptRom()
    {
        foreach(bool batched in new[]{false,true})
        foreach(int level in new[]{1,2})
        foreach(int initFree in new[]{0,1})
        foreach(int free in new[]{0,1})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);_inventory.GiveTreasure(TreasureId.Shield,level);
            _inventory.SetScriptedEquippedItems(0,0);_saveData.SetRoomFlag(5,0xe9,0x40,false);
            LoadValidationRoom(5,0xe9);
            var slot=_entities.EntityAdapters<TokayNpcRoomEntity>().Single(s=>s.Npc.Record.SubId==0x1d);
            var actor=(TokayCharacter)slot.Npc;F1Retain(slot);
            Vector2 safe=(from y in Enumerable.Range(12,_currentRoom.Height-24)
                from x in Enumerable.Range(12,_currentRoom.Width-24)
                let p=new Vector2(x,y)
                where !_collision.Collides(p)&&(p-actor.Position).LengthSquared()>24*24&&
                    new[]{new Vector2(-5,-2),new Vector2(5,-2),new Vector2(-5,5),new Vector2(5,5)}
                    .All(o=>_currentRoom.GetTerrainInfo(p+o).Hazard==HazardType.None)
                select p).First();
            _player.WarpTo(safe);
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,(int)safe.X,(int)safe.Y);
            int at=0xd040+_entities.InteractionSlot(actor)*256;
            native[at]=1;native[at+1]=0x48;native[at+2]=0x1d;
            native[at+11]=(byte)actor.Position.Y;native[at+13]=(byte)actor.Position.X;
            while(14-InteractionAdmissionPool.Count>initFree)
            {
                var puff=new PuzzlePuffEffect();puff.Initialize(new(24,24),SoundId.MusNone);
                _entities.AddEntity(new InteractionSlotValidationEntity(puff,static _=>{}));
                int occupied=0xd040+_entities.InteractionSlot(puff)*256;
                native[occupied]=1;native[occupied+1]=0;native[occupied+4]=1;native[occupied+0x20]=0xff;
            }
            int updates=0;
            string source=$"Shield Tokay$48:$1d/level{level}/initFree{initFree}/free{free}/batched{batched}";
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                native.AdvanceInteractions(_entities.FrameCounter);updates++;
                FailIf(slot.Script!.NativeCounter1!=native[at+6]||_inventory.ShieldLevel!=native[0xc6af]||
                    _saveData.GetRoomFlags(5,0xe9)!=native[0xcae9]||
                    _runtimeState.ReadWramByte(0xcc8a)!=native[0xcc8a]||_runtimeState.ReadWramByte(0xcc02)!=native[0xcc02],
                    source+$" update{updates}: script counter, shield level, flags or raw input differs.");
                var children=_entities.Entities<TokayAttachedVisualRoomEntity>();
                int[] originals=Enumerable.Range(2,14).Select(s=>0xd040+s*256).Where(a=>native[a]!=0&&native[a+1]==0x63).ToArray();
                FailIf(children.Count!=originals.Length,source+": accessory allocation/lifetime differs.");
                if(children.Count!=0)
                {
                    int child=originals.Single();var runtime=children.Single();
                    FailIf(_entities.InteractionSlot(runtime)!=((child>>8)-0xd0)||runtime.Position!=new Vector2(native[child+13],native[child+11])||
                        runtime.ZIndex!=ObjectDrawPriority.FromVisible(native[child+0x1a]),source+": accessory slot, offset or priority differs.");
                }
                native[0xcba0]=_dialogue.IsOpen?(byte)1:(byte)0;
            });
            Step(3);
            FailIf((actor.Accessory is not null)!=(initFree!=0),source+": state0 must use checked accessory allocation.");
            if(free!=0)
            {
                var filler=InteractionAdmissionPool.First(pair=>pair.Key is InteractionSlotValidationEntity);
                int released=0xd040+filler.Value*256;
                for(int i=0;i<64;i++)native[released+i]=0;
                _entities.RetireEntity(filler.Key);
            }
            FailIf(!slot.Script!.TryInteract(new(actor,null),_player),source+": cannot queue A.");native[at+0x31]=1;Step(1);
            FailIf(!_dialogue.IsOpen,source+": initial conversation must open TX_0a68.");
            for(int i=0;i<140;i++)
            {
                if(_dialogue.IsOpen){_dialogue.Close();native[0xcba0]=0;}
                Step(1);
            }
            int expected=initFree!=0||free!=0 ? level+1 : level;
            FailIf(_inventory.ShieldLevel!=expected||!_saveData.HasRoomFlag(5,0xe9,0x40)||_runtimeState.ReadWramByte(0xcc8a)!=0,
                source+": failed treasure allocation must preserve the level while the script still sets flag$40 and releases input.");
            _entities.Clear();
        }
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();_inventory.SetScriptedEquippedItems(0,0);
            _inventory.GiveTreasure(TreasureId.Shield,1);_saveData.SetRoomFlag(5,0xe9,0x40,false);
            LoadValidationRoom(5,0xe9);
            var actor=_entities.Entities<TokayCharacter>().Single(a=>a.Record.SubId==0x1d);
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched);
            void Approach()
            {
                foreach(int distance in new[]{40,32,24,20})
                foreach(var (direction,action) in new[]{(Vector2.Up,"move_up"),(Vector2.Down,"move_down"),(Vector2.Left,"move_left"),(Vector2.Right,"move_right")})
                {
                    Vector2 start=actor.Position-direction*distance;
                    if(!Enumerable.Range(0,distance-12).All(i=>
                    {
                        Vector2 p=start+direction*i;
                        return p.X>=6&&p.Y>=6&&p.X<_currentRoom.Width-6&&p.Y<_currentRoom.Height-6&&!_collision.Collides(p)&&
                            new[]{new Vector2(-5,-2),new Vector2(5,-2),new Vector2(-5,5),new Vector2(5,5)}
                                .All(o=>_currentRoom.GetTerrainInfo(p+o).Hazard==HazardType.None);
                    }))continue;
                    _player.WarpTo(start);StepGameplayUpdates(distance,direction,[action],[action],batched);
                    FailIf(_collision.Collides(_player.Position)||!actor.CanTalkTo(_player),"Shield Tokay must be reachable through actual room collision.");return;
                }
                throw new InvalidOperationException($"Room5:$e9 has no safe shield Tokay approach (actor{actor.Position}, room{_currentRoom.Width}x{_currentRoom.Height}).");
            }
            void Press()=>StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched);
            Approach();Press();FailIf(!_dialogue.IsOpen||_runtimeState.ReadWramByte(0xcc8a)!=0x81,"Actual A input must open the shield-upgrade script.");
            _dialogue.Close();LoadValidationRoom(5,0xe9);actor=_entities.Entities<TokayCharacter>().Single(a=>a.Record.SubId==0x1d);
            FailIf(_runtimeState.ReadWramByte(0xcc8a)!=0||_inventory.ShieldLevel!=1||_saveData.HasRoomFlag(5,0xe9,0x40),"Cancellation must release input without upgrading the shield.");
            Approach();Press();
            for(int i=0;i<160;i++){if(_dialogue.IsOpen)_dialogue.Close();Step(1);}
            FailIf(_inventory.ShieldLevel!=2||!_saveData.HasRoomFlag(5,0xe9,0x40)||_runtimeState.ReadWramByte(0xcc8a)!=0,"Actual shield handoff must complete once and release input.");
            Approach();Press();FailIf(!_dialogue.IsOpen||_dialogue.CurrentMessage!=DialogueBox.PlainText(new TokayInteractionDatabase().Text(0x0a69)),"Repeated shield conversation must use TX_0a69.");
            _dialogue.Close();Step(2);LoadValidationRoom(5,0xe9);Step(2);
            FailIf(_entities.Entities<TokayAttachedVisualRoomEntity>().Count!=0||_inventory.ShieldLevel!=2,"Completed shield holder must re-enter without another accessory or upgrade.");
        }
    }
}
