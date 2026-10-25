using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateTokayVineScriptRom()
    {
        foreach(bool batched in new[]{false,true})
        foreach(int free in new[]{0,1})
        foreach(bool cancel in new[]{false,true})
        {
            ReinitializeGameplayForValidation();_saveData.SetRoomFlag(0,0xbb,0x40,false);
            _inventory.SetScriptedEquippedItems(0,0);LoadValidationRoom(0,0xbb);
            var slot=_entities.EntityAdapters<TokayNpcRoomEntity>().Single(s=>s.Npc.Record.SubId==0x1e);
            var parent=(TokayCharacter)slot.Npc;var script=slot.Script!;F1Retain(slot);
            Vector2 outward=new[]{Vector2.Down,Vector2.Right,Vector2.Left}.First(direction=>
                Enumerable.Range(12,13).All(distance=>!_collision.Collides(parent.Position+direction*distance)));
            _player.WarpTo(parent.Position+outward*24);_player.Face(new((int)-outward.X,(int)-outward.Y));
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,(int)_player.Position.X,(int)_player.Position.Y);
            int at=0xd040+_entities.InteractionSlot(parent)*256;
            native[at]=1;native[at+1]=0x48;native[at+2]=0x1e;
            native[at+11]=(byte)parent.Position.Y;native[at+13]=(byte)parent.Position.X;
            while(14-InteractionAdmissionPool.Count>free)
            {
                var puff=new PuzzlePuffEffect();puff.Initialize(new(24,24),SoundId.MusNone);
                _entities.AddEntity(new InteractionSlotValidationEntity(puff,static _=>{}));
                int occupied=0xd040+_entities.InteractionSlot(puff)*256;
                native[occupied]=1;native[occupied+1]=0;native[occupied+4]=1;native[occupied+0x20]=0xff;
            }
            var audit=_sound.AttachPlayRequestAudit();audit.Clear();int updates=0;
            string source=$"Vine Tokay$48:$1e/free{free}/cancel{cancel}/batched{batched}";
            void Compare()
            {
                native[0xd00b]=(byte)_player.Position.Y;native[0xd00d]=(byte)_player.Position.X;
                native.AdvanceInteractions(_entities.FrameCounter);updates++;
                FailIf(script.NativeCounter1!=native[at+6]||script.NativePressedButton!=native[at+0x31]||
                    SomariaPrivate<int>(script,"_vineZ")!=(short)native.Word(at+0x0e)||
                    (ushort)SomariaPrivate<int>(script,"_vineSpeedZ")!=native.Word(at+0x14)||
                    _saveData.GetRoomFlags(0,0xbb)!=native[0xc7bb]||
                    _runtimeState.ReadWramByte(0xcc8a)!=native[0xcc8a]||_runtimeState.ReadWramByte(0xcc02)!=native[0xcc02],
                    source+$" update{updates}: script counter, proximity byte, gravity, flags or raw control differs (cmd{script.CurrentCommandIndex}, counter{script.NativeCounter1}/{native[at+6]}, pressed{script.NativePressedButton}/{native[at+0x31]}, mask{_runtimeState.ReadWramByte(0xcc8a):x2}/{native[0xcc8a]:x2}).");
                int[] children=Enumerable.Range(2,14).Select(s=>0xd040+s*256).Where(a=>native[a]!=0&&native[a+1]==0x9f).ToArray();
                var actual=_entities.EntityAdapters<ExclamationMarkRoomEntity>().ToArray();
                // This isolated ROM pass does not run the text renderer's
                // inline/default voices. Compare the native reaction cues.
                FailIf(actual.Length!=children.Length||!audit.Requests.Where(s=>s==0x50||s==SoundId.SndJump).SequenceEqual(native.Sounds),
                    source+$" update{updates}: checked exclamation count, expiry or reaction cue differs (child{actual.Length}/{children.Length}).");
                if(actual.Length!=0)
                {
                    int child=children.Single();var entity=actual.Single();
                    FailIf(_entities.InteractionSlot(entity.Node)!=((child>>8)-0xd0)||entity.Node.Position!=new Vector2(native[child+13],native[child+11])||
                        SomariaPrivate<int>(entity,"_counter")!=native[child+6],source+": exclamation slot, offset or counter differs.");
                }
                if(_dialogue.IsOpen)FailIf(_dialogue.CurrentMessage!=DialogueBox.PlainText(new TokayInteractionDatabase().Text(native.Word(0xcba2)-0x400)),source+": text differs.");
                native[0xcba0]=_dialogue.IsOpen?(byte)1:(byte)0;
            }
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:Compare);
            Step(3);FailIf(_saveData.HasRoomFlag(0,0xbb,0x40),source+": excluded Manhattan distance$18 must not run the script.");
            // The original direction return is zero directly above, even
            // inside the range. This is a native gate, not an A-button press.
            _player.WarpTo(parent.Position+Vector2.Up*20);Step(2);
            FailIf(_runtimeState.ReadWramByte(0xcc8a)!=0||_saveData.HasRoomFlag(0,0xbb,0x40),source+": direction zero must not trigger the reaction.");
            _player.WarpTo(parent.Position+outward*24);Step(1);
            StepGameplayUpdates(2,-outward,batched:batched,afterUpdate:Compare);
            Step(10);
            FailIf(_collision.Collides(_player.Position)||!_saveData.HasRoomFlag(0,0xbb,0x40)||_runtimeState.ReadWramByte(0xcc8a)!=0x81,
                source+": a real collision-valid approach must start the original reaction.");
            if(cancel)
            {
                LoadValidationRoom(4,0xa1);StepGameplayUpdates(2,Vector2.Zero,batched:batched);
                LoadValidationRoom(0,0xbb);StepGameplayUpdates(2,Vector2.Zero,batched:batched);
                FailIf(_runtimeState.ReadWramByte(0xcc8a)!=0||!_saveData.HasRoomFlag(0,0xbb,0x40)||
                    _entities.EntityAdapters<ExclamationMarkRoomEntity>().Any(),source+": cancellation must retain flag$40 and release raw control without replaying the reaction.");
                continue;
            }
            Step(45);FailIf(!_dialogue.IsOpen||audit.Requests.Count(s=>s==0x50)!=free,source+": wait30 must finish even when exclamation allocation fails.");
            _dialogue.Close();native[0xcba0]=0;Step(3);
            StepGameplayUpdates(16,-outward,batched:batched,afterUpdate:Compare);
            FailIf(!parent.CanTalkTo(_player)||_collision.Collides(_player.Position),source+": repeat A conversation must remain reachable.");
            native[at+0x31]=1;StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"],batched,Compare);Step(2);
            FailIf(!_dialogue.IsOpen||native.Word(0xcba2)!=0x0e6b||_entities.EntityAdapters<ExclamationMarkRoomEntity>().Any(),source+": repeat A must show TX_0a6b without another reaction.");
            _dialogue.Close();native[0xcba0]=0;Step(3);
        }
    }
}
