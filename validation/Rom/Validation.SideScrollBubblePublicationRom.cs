using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateSideScrollBubblePublicationRom()
    {
        var turnSteps=new HashSet<int>();
        foreach(int randomByte in Enumerable.Range(0,8))
        foreach(bool fractional in new[]{false,true})
        foreach(bool dry in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            PrepareSwimmingRoom();_entities.Clear();_inventory.SetScriptedEquippedItems(0,0);
            _player.BeginCutsceneControl();_runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            var seed=_random.CaptureState() with {Rng1=(byte)randomByte};_random.RestoreState(seed);
            var actor=_entities.Spawn<SideScrollBubbleRoomEntity>(new SideScrollBubbleSpawn(new(40.75f,56.5f)));
            FailIf(actor.PrecisePosition!=new Vector2(40,56),"INTERAC$91 construction must copy only coordinate high bytes.");
            var native=new SomariaRom(_saveData,seed,_currentRoom,0,40,56);
            int at=0xd040+_entities.InteractionSlot(actor)*256;
            native[at]=1;native[at+1]=0x91;native[at+11]=56;native[at+13]=40;
            byte[] observed=new byte[4];int probes=0,updates=0,moves=0;
            var before=new PartMovementScratchProbe(()=>
            {for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));});
            _entities.RegisterEnemySlot(before,15);_entities.AddEntity(before);
            _entities.AddEntity(new PartMovementScratchProbe(()=>
            {probes++;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);}));
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                native.AdvanceInteractions(_entities.FrameCounter);updates++;if(native[0xcec3]!=0xa8)moves++;
                FailIf(actor.Finished!=(native[at]==0)||!actor.Finished&&(
                    actor.PrecisePosition!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    actor.Angle!=native[at+9]||actor.TurnCounter!=native[at+6]||actor.TurnsRemaining!=native[at+0x31]||
                    actor.Visible!=((native[at+0x1a]&128)!=0))||probes!=1||
                    Enumerable.Range(0,4).Any(i=>observed[i]!=native[0xcec0+i]),
                    $"INTERAC$91 side bubble seed${randomByte:x2}/fraction{fractional}/dry{dry}/update{updates}: original XY, turns, admission, deletion or movement scratch differs.");
                var rng=_random.CaptureState();
                FailIf(rng.Rng1!=native[0xff94]||rng.Rng2!=native[0xff95]||rng.Calls-seed.Calls!=native.RandomCalls,
                    "Side bubble must consume only the original initialization RNG draw.");
                probes=0;
            });
            Step(1);turnSteps.Add(SomariaPrivate<int>(actor,"_turnStep"));
            if(fractional)
            {
                F1Set(actor,"_precisePosition",new Vector2(40.25f,56.5f));
                native[at+10]=0x80;native[at+12]=0x40;
            }
            Step(6);_dialogue.ShowMessage("Ordinary bubble hold.",104);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,2);native[0xcc8a]=2;Step(3);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,4);native[0xcd00]=4;Step(3);
            _runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            if(dry)
            {
                Vector2 point=actor.Position;int packed=((int)point.Y&0xf0)|((int)point.X>>4);
                _currentRoom.SetPositionTileAndCollision(point,0,0,0);native[0xcf00+packed]=0;
            }
            while(!actor.Finished&&updates<240)Step(1);
            Step(2);FailIf(!actor.Finished||moves<9,"Side bubble must finish through its source water/bounds check after admitted movement.");
            _entities.Clear();_player.EndCutsceneControl();
        }
        FailIf(!turnSteps.SetEquals(new[]{-1,1}),"Side bubble cases must exercise both original turn directions.");
    }
}
