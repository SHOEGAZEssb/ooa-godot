using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateBeamosBeamPublicationRom()
    {
        foreach(int angle in Enumerable.Range(0,32))
        foreach(int sub in new[]{0,3})
        foreach(bool fractional in new[]{false,true})
        foreach(bool batched in new[]{false,true})
        {
            var save=OracleSaveData.CreateStandardGame();save.SetLinkName("Link");
            save.SetGlobalFlag(GlobalFlag.PregameIntroDone);save.SetGlobalFlag(GlobalFlag.IntroDone);
            InitializeTransientSession(save);LoadValidationRoom(4,0xa1);_entities.Clear();
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(120,40));
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            Vector2 initial=fractional?new(248.25f,8.5f):new(80,72);
            var actor=_entities.Spawn<BeamosBeamPart>(new BeamosBeamSpawn(initial,angle,sub));
            var adapter=_entities.EntityAdapters<BeamosBeamRoomEntity>().Single();_player.BeginCutsceneControl(owner:actor);
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,120,40);
            native.InitializeLinkGameplay(); // Install real right/bottom collision-buffer sentinels.
            int at=0xd0c0+F1Parts[adapter]*256;native[at]=1;native[at+1]=0x29;native[at+2]=(byte)sub;native[at+9]=(byte)angle;
            native.Word(at+10,(int)(initial.Y*256));native.Word(at+12,(int)(initial.X*256));
            byte[] observed=new byte[4];int probes=0,updates=0,moves=0;
            var before=new PartMovementScratchProbe(()=>
            {for(int i=0;i<4;i++)_runtimeState.SetWramByte(0xcec0+i,(byte)(0xa5+i));});
            _entities.RegisterEnemySlot(before,15);_entities.AddEntity(before);
            _entities.AddEntity(new PartMovementScratchProbe(()=>
            {probes++;for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);}));
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                var old=native.Word(at+12);for(int i=0;i<4;i++)native[0xcec0+i]=(byte)(0xa5+i);
                native.AdvanceParts(_entities.FrameCounter);updates++;if(native[at]!=0&&old!=native.Word(at+12))moves++;
                var position=SomariaPrivate<OracleObjectPosition>(actor,"_position");
                var velocity=SomariaPrivate<OracleObjectVelocity>(actor,"_velocity");
                FailIf(actor.Finished!=(native[at]==0)||!actor.Finished&&(
                    position.XFixed!=native.Word(at+12)||position.YFixed!=native.Word(at+10)||
                    actor.State!=native[at+4]||actor.Counter!=native[at+6]||actor.Visible!=((native[at+0x1a]&128)!=0)||
                    (ushort)velocity.YFixed!=native.Word(at+0x10)||(ushort)velocity.XFixed!=native.Word(at+0x12))||
                    probes!=1||Enumerable.Range(0,4).Any(i=>observed[i]!=native[0xcec0+i]),
                    $"PART$29 angle${angle:x2}/sub${sub:x2}/fraction{fractional}/update{updates}: original cached components, motion/lifetime or phase-local scaled scratch differs: finished{actor.Finished}/{native[at]==0}, XY{position.XFixed:x4}/{position.YFixed:x4} vs {native.Word(at+12):x4}/{native.Word(at+10):x4}, scratch[{string.Join(',',observed)}]/[{string.Join(',',Enumerable.Range(0,4).Select(i=>native[0xcec0+i]))}].");
                probes=0;
            });
            int gate=angle&3;
            if(gate==0){_dialogue.ShowMessage("Beam state0 text.",104);native[0xcba0]=1;}
            else if(gate==1){_runtimeState.SetWramByte(0xcc8a,8);native[0xcc8a]=8;}
            else if(gate==2){_runtimeState.SetWramByte(0xcc8a,0x80);native[0xcc8a]=0x80;}
            else{_runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;}
            Step(3);FailIf(actor.State!=1||actor.Counter!=2,"PART$29 must initialize once under holds, publishing scaled components only on state0.");
            _dialogue.Close();native[0xcba0]=0;_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
            _runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            while(!actor.Finished&&updates<70)Step(1);
            Step(2);FailIf(!actor.Finished,"PART$29 must finish its real source tile/bounds path without republishing component velocity.");
            _entities.Clear();_player.EndCutsceneControl(actor);
        }
    }
}
