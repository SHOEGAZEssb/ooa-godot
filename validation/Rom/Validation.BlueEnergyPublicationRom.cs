using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBlueEnergyPublicationRom()
    {
        foreach(int index in Enumerable.Range(0,8))
        foreach(bool outgoing in new[]{false,true})
        foreach(bool fractional in new[]{false,true})
        foreach(bool expiry in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            InitializeCompletedIntroRoom(4, 0xa1);
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(120,40));
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            byte duration=expiry?(byte)80:(byte)0xff;
            Vector2 center=fractional?new(248,8):new(120,88);
            var actor=_entities.Spawn<BlueEnergyBeadRoomEntity>(new BlueEnergyBeadSpawn(index,center,duration,outgoing));
            _player.BeginCutsceneControl(owner:actor);
            var seed=_random.CaptureState();var native=new SomariaRom(_saveData,seed,_currentRoom,0,120,40);
            int at=0xd0c0+F1Parts[actor]*256;
            native[at]=1;native[at+1]=0x53;native[at+3]=(byte)index;native[at+6]=duration;
            native[at+0x30]=(byte)center.Y;native[at+0x31]=(byte)center.X;native[at+0x32]=outgoing?(byte)1:(byte)0;
            var observedCapture = new MovementScratchCapture(_runtimeState);int updates=0,starts=0,moves=0;
            var before=new PartMovementScratchProbe(()=>
            {for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));});
            _entities.RegisterEnemySlot(before,15);_entities.AddEntity(before);
            _entities.AddEntity(new PartMovementScratchProbe(observedCapture.Capture));
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                bool oldLive=native[at]!=0,oldVisible=(native[at+0x1a]&128)!=0;
                for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                native.AdvanceParts(_entities.FrameCounter);updates++;
                if(oldLive&&!oldVisible&&(native[at+0x1a]&128)!=0)starts++;
                if(oldLive&&oldVisible&&native[at]!=0)moves++;
                FailIf(actor.Finished!=(native[at]==0)||!actor.Finished&&(
                    actor.Initialized!=(native[at+4]!=0)||actor.Delay!=native[at+7]||actor.Duration!=native[at+6]||
                    actor.Visible!=((native[at+0x1a]&128)!=0)||
                    actor.PrecisePosition!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    actor.Position!=new Vector2(native[at+13],native[at+11]))||observedCapture.Samples!=1||
                    Enumerable.Range(0,4).Any(i=>observedCapture.Bytes[i]!=native[0xcec0+i]),
                    $"PART$53 index${index:x2}/out{outgoing}/fraction{fractional}/expiry{expiry}/update{updates}: native lifecycle, full XY or phase-local circle/flight scratch differs.");
                FailIf(RandomDiffers(seed, native[0xff94], native[0xff95], native.RandomCalls),
                    "PART$53 must retain original RNG calls at initialization and each animation restart.");
                observedCapture.Samples=0;
            });
            _runtimeState.SetWramByte(WramAddress.wDeleteEnergyBeads,1);native[0xcd2d]=1;
            Step(1);
            FailIf(_runtimeState.ReadWramByte(0xcd2d)!=0||native[0xcd2d]!=0,"PART$53 state0 must clear shared deletion before its first delay.");
            // Explicit retained-low-byte boundary; real restarts below must
            // preserve the fractional result through high-byte-only placement.
            if(fractional)
            {
                F1Set(actor,"_position",new OracleObjectPosition(0x0080,0x0040));
                native[at+10]=0x80;native[at+12]=0x40;
            }
            _dialogue.ShowMessage("Always-update energy beads.",104);native[0xcba0]=1;Step(12);
            _dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,0x88);native[0xcc8a]=0x88;Step(12);
            _runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(12);
            _runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            Step((expiry?82:140)-updates);
            FailIf(starts<2||moves<20||actor.Finished!=expiry,"PART$53 must complete repeated animation flights before expiry or shared deletion.");
            if(!expiry){_runtimeState.SetWramByte(0xcd2d,1);native[0xcd2d]=1;Step(1);}
            Step(2);FailIf(!actor.Finished,"PART$53 must remain retired after its source deletion boundary.");
            _entities.Clear();_player.EndCutsceneControl(actor);
        }
    }
}
