using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRunningBipinPublicationRom()
    {
        foreach(int x in new[]{0,0x27,0x28,0x57,0x58,0xff})
        foreach(int angle in new[]{8,0x18})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            InitializeCompletedIntroRoom(2, 0xea, clearEntities: false);
            var actor=_entities.EntityAdapters<RunningBipinRoomEntity>().Single();F1Retain(actor);
            _player.WarpTo(new(120,104));_player.BeginCutsceneControl(owner:actor);
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            int at=0xd040+_entities.InteractionSlot(actor.Node)*256;
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,120,104);
            native[at]=1;native[at+1]=0x28;native[at+11]=0x48;native[at+13]=0x48;native.AdvanceInteractions();
            // Declared initialized patrol boundary; original handler runs its
            // real script/animation/collision tail, with a noninteractive stub.
            native.Word(at+0x18,0x45ef);
            Vector2 initial=new(x+(fractional?255/256f:0),72+(fractional?.5f:0));
            F1Set(actor,"_precisePosition",initial);F1Set(actor,"_angle",angle);actor.Npc.Position=initial.Floor();
            native.Word(at+10,(int)(initial.Y*256));native.Word(at+12,(int)(initial.X*256));native[at+9]=(byte)angle;
            var observedCapture = new MovementScratchCapture(_runtimeState);int reversals=0,updates=0;
            var before=new PartMovementScratchProbe(()=>
            {for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));});
            _entities.RegisterEnemySlot(before,15);_entities.AddEntity(before);
            _entities.AddEntity(new PartMovementScratchProbe(observedCapture.Capture));
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                int previous=native[at+9];for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                native.AdvanceInteractions(_entities.FrameCounter);updates++;if(previous!=native[at+9])reversals++;
                FailIf(SomariaPrivate<Vector2>(actor,"_precisePosition")!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    actor.Npc.Position!=new Vector2(native[at+13],native[at+11])||SomariaPrivate<int>(actor,"_angle")!=native[at+9]||
                    SomariaPrivate<int>(actor,"_animation")!=native[at+0x3a]||observedCapture.Samples!=1||
                    Enumerable.Range(0,4).Any(i=>observedCapture.Bytes[i]!=native[0xcec0+i]),
                    $"Bipin$28:$00 x${x:x2}/angle${angle:x2}/update{updates}: original patrol/reversal/publication differs.");
                observedCapture.Samples=0;
            });
            Step(4);_dialogue.ShowMessage("Ordinary Bipin hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,2);native[0xcc8a]=2;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,4);native[0xcd00]=4;Step(3);
            _runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;Step(110);
            FailIf(reversals<2,"Bipin patrol must exercise repeated original interval reversals, including wrapping starts.");
            _entities.Clear();_player.EndCutsceneControl(actor);
        }
    }
}
