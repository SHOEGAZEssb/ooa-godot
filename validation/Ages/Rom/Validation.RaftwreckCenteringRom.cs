using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateRaftwreckCenteringRom()
    {
        foreach(int x in new[]{0,64,80,96,208,255})
        foreach(bool mounted in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();
            if(mounted)CompanionRuntimeState.Begin(_runtimeState,0x13,0xa8,new(x,118),3);
            LoadValidationRoom(1,0xa8);_inventory.SetScriptedEquippedItems(0,0);
            var owner=_roomEvents.Get<RaftwreckEvent>();var raft=_entities.Entities<RaftRoomEntity>().SingleOrDefault();
            if(raft is not null)F1Set(raft,"_precisePosition",new Vector2(x+0.25f,118.5f));
            _player.WarpTo(new(x+0.75f,113.25f));owner.Start(_currentRoom);
            _runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);_runtimeState.SetWramByte(0xcd00,0);
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,3,x,113);
            const int at=0xd240;int copy=mounted?0xd100:0xd000;
            native[0xcc2c]=(byte)(copy>>8);native.Word(copy+10,mounted?0x7680:0x7140);native.Word(copy+12,x*256+(mounted?0x40:0xc0));
            native[at]=1;native[at+1]=0x9b;native.AdvanceInteractions();native[0xcd00]=0;
            int updates=0,moves=0;bool observing=false;byte[] observed=new byte[4];
            void Observe()
            {
                if(!observing||_runtimeState.ReadWramByte(0xcec3)==0xa8)return;
                for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);
                observing=false;
            }
            void Seed(){observing=false;for(int i=0;i<4;i++){byte value=(byte)(0xa5+i);native[0xcec0+i]=value;_runtimeState.SetWramByte(0xcec0+i,value);}observing=true;}
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                int previous=native.Word(at+12);native.AdvanceInteractions(_entities.FrameCounter);updates++;
                bool moved=previous!=native.Word(at+12);if(moved)moves++;
                Vector2 copied=mounted?raft!.PrecisePosition:_player.PrecisePosition;
                FailIf(owner.PrecisePosition!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    native[at+5]<2&&owner.CenterCounter!=native[at+6]||
                    SomariaPrivate<bool>(owner,"_initializing")!=(native[at+5]==0)||
                    copied!=new Vector2(native.Word(copy+12)/256f,native.Word(copy+10)/256f)||
                    moved&&(observing||Enumerable.Range(0,4).Any(i=>observed[i]!=native[0xcec0+i]))||!moved&&!observing,
                    $"Raftwreck x${x:x2}/mounted{mounted} update{updates}: native centering/readiness/high-copy/counter/publication differs (center={owner.CenterCounter}/{native[at+6]}, substate={native[at+5]}, position={owner.PrecisePosition}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, copy={copied}/{native.Word(copy+12)/256f},{native.Word(copy+10)/256f}, publication={!observing}).");
                Seed();
            });
            _runtimeState.Changed+=Observe;
            try
            {
            if(x is 0 or 208 or 255)
            {
                // Explicit substate1 boundary for wrapped-byte probes. Run
                // the original readiness initializer, then omit the unrelated
                // post-object boundary clamp before comparing actual updates.
                _runtimeState.SetWramByte(0xcd00,1);native[0xcd00]=1;
                owner.UpdateFrame();native.AdvanceInteractions();
            }
            else
            {
                Seed();Step(2); // Ready-clear substate0 still copies high bytes.
                _runtimeState.SetWramByte(0xcd00,1);native[0xcd00]=1;Step(1);
            }
            _runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            Seed();
            int expected=((Math.Abs(x-0x50))*2)&255;
            if(expected>0)
            {
                Step(1);
                _dialogue.ShowMessage("Ordinary raftwreck hold.",_player.Position.Y);native[0xcba0]=1;Step(2);_dialogue.Close();native[0xcba0]=0;
                _runtimeState.SetWramByte(0xcc8a,3);native[0xcc8a]=3;Step(2);_runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;
                if(x is not (0 or 208 or 255))
                {
                    _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(2);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
                }
                Step(expected-1);
            }
            FailIf(moves!=expected||owner.CenterCounter!=0||native[at+5]!=1,
                "Raftwreck counter1 is a byte, and its final centering move occurs on decrement to zero.");
            Step(1); // Install script only; no wait command yet.
            FailIf(SomariaPrivate<CutsceneCommandRunner>(owner,"_runner").Counter!=0||native[at+6]!=0||native[at+5]!=2,
                "Raftwreck must install its script without executing the initial wait8 on this update.");
            Step(1);
            FailIf(SomariaPrivate<CutsceneCommandRunner>(owner,"_runner").Counter!=8||native[at+6]!=8,
                "Raftwreck's original wait8 starts on the update after script installation.");
            }
            finally{_runtimeState.Changed-=Observe;}
            owner.Cancel();
            if(raft is not null)
            {
                // Cancellation retains the live ride; explicitly dismantle
                // the validation vehicle before clearing its entity owner.
                _player.EndRaftRide(_player.PrecisePosition,raft.Direction);
                CompanionRuntimeState.Clear(_runtimeState,0x13);
            }
            _entities.Clear();StepGameplayUpdates(2,Vector2.Zero,batched:batched);
            FailIf(owner.HasState||_player.CutsceneControlled||_saveData.HasRoomFlag(1,0xa8,0x40),
                "Cancelling centering must release ownership without marking the wreck complete.");
        }
    }
}
