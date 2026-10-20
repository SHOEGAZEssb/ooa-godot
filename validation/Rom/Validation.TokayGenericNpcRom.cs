using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateTokayGenericNpcRom()
    {
        // tokay.s:@textIndices and @initSubid1f; all eight placed generic
        // variants execute the common ROM script and npcFaceLinkAndAnimate.
        foreach (bool batched in new[] { false, true })
        foreach (var row in new[] { (0,0xbd,0x12,0x0a64), (0,0xcd,0x13,0x0a65), (0,0xdd,0x14,0x0a66),
            (1,0xad,0x15,0x0a60), (1,0xbd,0x16,0x0a61), (1,0xcd,0x17,0x0a62),
            (1,0xdd,0x18,0x0a63), (1,0xaa,0x1f,0x0a6c) })
        {
            var (group,room,subid,text)=row;
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            save.SetRoomFlag(1,0xaa,0x40);
            InitializeTransientSession(save);_inventory.SetScriptedEquippedItems(0,0);
            LoadValidationRoom(group,room);
            var slot=_entities.EntityAdapters<TokayNpcRoomEntity>().Single(s=>s.Npc.Record.SubId==subid);
            var actor=(TokayCharacter)slot.Npc;
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,120,104);
            int at=0xd040+_entities.InteractionSlot(actor)*256;
            native[at]=1;native[at+1]=0x48;native[at+2]=(byte)subid;
            native[at+11]=(byte)actor.Position.Y;native[at+13]=(byte)actor.Position.X;
            string source=$"Generic Tokay$48:${subid:x2} room${group:x1}:${room:x2}/batched{batched}";
            void Compare()
            {
                // Link's position is an input to this isolated native object
                // pass, sampled after the application's player phase.
                native[0xd00b]=(byte)_player.Position.Y;native[0xd00d]=(byte)_player.Position.X;
                native.AdvanceInteractions(_entities.FrameCounter);
                FailIf(actor.NativeAngle!=native[at+9] || actor.TextId!=text ||
                    slot.Script!.NativeCounter1!=native[at+6],
                    source+": original facing, retained text or script counter differs.");
                native[0xcba0]=_dialogue.IsOpen?(byte)1:(byte)0;
            }
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:Compare);
            void Approach()
            {
                foreach (int distance in new[] { 40,32,24,20 })
                foreach (var (direction,action) in new[] { (Vector2.Up,"move_up"),(Vector2.Down,"move_down"),
                    (Vector2.Left,"move_left"),(Vector2.Right,"move_right") })
                {
                    Vector2 start=actor.Position-direction*distance;
                    if (!Enumerable.Range(0,distance-12).All(i=>
                    {
                        Vector2 p=start+direction*i;
                        return p.X>=6&&p.Y>=6&&p.X<_currentRoom.Width-6&&p.Y<_currentRoom.Height-6&&
                            !_collision.Collides(p)&&new[] { new Vector2(-5,-2),new Vector2(5,-2),new Vector2(-5,5),new Vector2(5,5) }
                            .All(o=>_currentRoom.GetTerrainInfo(p+o).Hazard==HazardType.None);
                    })) continue;
                    _player.WarpTo(start);
                    StepGameplayUpdates(distance,direction,[action],[action],batched,Compare);
                    FailIf(_collision.Collides(_player.Position)||!actor.CanTalkTo(_player),source+": real room collision approach must reach A range.");
                    return;
                }
                throw new InvalidOperationException(source+": no safe approach declared.");
            }
            Step(3);
            for (int repeat=0;repeat<2;repeat++)
            {
                Approach();native[at+0x31]=1;
                StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched,Compare);
                FailIf(!_dialogue.IsOpen || native.Word(0xcba2)!=text+0x400 ||
                    _dialogue.CurrentMessage!=DialogueBox.PlainText(new TokayInteractionDatabase().Text(text)),
                    source+": actual A input must present the original text ID.");
                Step(3);
                _dialogue.Close();native[0xcba0]=0;Step(3);
            }
            _entities.Clear();
        }
    }
}
