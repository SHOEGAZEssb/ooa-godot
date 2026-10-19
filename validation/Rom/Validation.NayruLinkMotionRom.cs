using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateNayruLinkMotionRom()
    {
        CheckNayruLinkCoordinateHandoffs();
        CheckNayruLinkMotionCancellation();
        foreach(int kind in new[]{3,4})
        foreach(int hold in new[]{5,180})
        foreach(bool batched in new[]{false,true})
        foreach(bool wrap in new[]{false,true})
        {
            ReinitializeGameplayForValidation(); _saveData.SetGlobalFlag(GlobalFlag.IntroDone,false); LoadValidationRoom(0,0x39);
            var owner=_roomEvents.Get<NayruIntroEvent>();
            ((ICutsceneCommandHost)owner).RunNativeHandler("SetupNayruPossessionScene");
            F1Set(owner,"_nayruStage",NayruStage.Script);
            var database=new NayruIntroEventDatabase(); var commands=new List<CutsceneCommand>();
            var aftermath=database.Commands.OfType<CutsceneNativeBlockingCommand>().Single(command=>command.Handler=="LinkAftermathMotion");
            IEnumerable<CutsceneCommand> sourceCommands=kind==3
                ? database.Commands.OfType<CutsceneNativeBlockingCommand>().Where(command=>command.Handler=="VeranReaction")
                : new[]{aftermath};
            foreach(var command in sourceCommands) commands.Add(command with { Source=command.Source with { CommandIndex=commands.Count } });
            var source=commands[^1].Source;
            commands.Add(new CutsceneWaitCommand(source with { CommandIndex=commands.Count,Opcode="wait" },0xff));
            commands.Add(new CutsceneEndCommand(source with { CommandIndex=commands.Count,Opcode="scriptend" }));
            owner.CommandRunner.Start(commands);
            Vector2 initial=wrap ? new(4.25f,250.5f) : new(80.25f,40.5f);
            _player.WarpTo(initial); _player.BeginCutsceneControl(owner:owner);
            F1Set(_player,"_topDownMovementSpeedRaw",40); _player.SetNativeCutsceneAngle(16);
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            _runtimeState.SetWramByte(WramAddress.wScrollMode,(byte)(wrap ? 0 : 1));
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,(int)initial.X,(int)initial.Y)
                { CompanionDispatchEnabled=true };
            native.InitializeLinkGameplay(); native[0xcc6e]=1; native[0xcd00]=(byte)(wrap ? 0 : 1);
            native[0xd001]=8; native[0xd002]=(byte)kind; native[0xd004]=1;
            native[0xd005]=(byte)(kind==3 ? 4 : 2); native[0xd006]=(byte)(kind==3 ? 0 : 80);
            native[0xd009]=16; native[0xd010]=40; native[0xcfd0]=(byte)(kind==3 ? 0x11 : 0x20);
            _runtimeState.SetWramByte(0xcfd0,native[0xcfd0]);
            native.Word(0xd00a,(int)(initial.Y*256)); native.Word(0xd00c,(int)(initial.X*256));
            var driver=SomariaPrivate<NayruLinkCutsceneMotion>(owner,"_linkMotion");
            int updates=0,probeVisits=0,moves=0,runtimeCueStart=0;
            Vector2 sampledPosition=default; int sampledSpeed=0,sampledAngle=0,sampledCounter=-1,sampledSubstate=-1,sampledCues=0;
            bool sampledControl=false; byte sampledMask=0,sampledMenu=0,sampledPhase=0; byte[] sampledBytes=new byte[4];
            var probe=new PartMovementScratchProbe(()=>
            {
                probeVisits++; sampledPosition=_player.PrecisePosition; sampledSpeed=_player.NativePlanarSpeed; sampledAngle=_player.NativePlanarAngle;
                sampledControl=_player.CutsceneControlled; sampledMask=_runtimeState.ReadWramByte(WramAddress.wDisabledObjects);
                sampledMenu=_runtimeState.ReadWramByte(WramAddress.wMenuDisabled);
                sampledPhase=_runtimeState.ReadWramByte(0xcfd0);
                sampledCues=_sound.PlayRequestsFor(SoundId.SndUnknown5)-runtimeCueStart;
                sampledCounter=SomariaPrivate<int>(driver,"_counter"); sampledSubstate=SomariaPrivate<int>(driver,"_substate");
                for(int i=0;i<4;i++) sampledBytes[i]=_runtimeState.ReadWramByte(0xcec0+i);
            });
            _entities.RegisterEnemySlot(probe,15); _entities.AddEntity(probe);
            _sound.ClearPlayRequestAudit();
            void Seed() { for(int i=0;i<4;i++) { byte value=(byte)(0xa5+i); native[0xcec0+i]=value; _runtimeState.SetWramByte(0xcec0+i,value); } }
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                int x=native.Word(0xd00c),y=native.Word(0xd00a),cueStart=native.Sounds.Count(sound=>sound==SoundId.SndUnknown5);
                native.UpdateGameplay(0,0,0xff,_entities.FrameCounter,afterLink:()=>
                {
                    bool cutscene=native[0xd001]==8;
                    FailIf(sampledPosition!=new Vector2(native.Word(0xd00c)/256f,native.Word(0xd00a)/256f) || sampledControl!=cutscene ||
                        cutscene && (sampledSpeed!=native[0xd010] || sampledAngle!=native[0xd009] ||
                            sampledCounter!=native[0xd006] || sampledSubstate!=native[0xd005]) ||
                        sampledMask!=native[0xcc8a] || sampledMenu!=native[0xcc02] || sampledPhase!=native[0xcfd0] ||
                        sampledCues!=native.Sounds.Count(sound=>sound==SoundId.SndUnknown5)-cueStart ||
                        Enumerable.Range(0,4).Any(i=>sampledBytes[i]!=native[0xcec0+i]),
                        $"Nayru Link kind{kind},visit{updates+1},hold{hold},wrap{wrap}: native special-object motion differs before later consumers: XY{sampledPosition}/{native.Word(0xd00c)/256f},{native.Word(0xd00a)/256f}; regs{sampledSpeed:x2},{sampledAngle:x2}/${native[0xd010]:x2},${native[0xd009]:x2}; state{sampledSubstate}/{native[0xd005]}, counter{sampledCounter}/{native[0xd006]}; control{sampledControl}/{cutscene}, id${native[0xd001]:x2}, override${native[0xcc57]:x2}; mask${sampledMask:x2}/${native[0xcc8a]:x2}, menu${sampledMenu:x2}/${native[0xcc02]:x2}; cues{sampledCues}/{native.Sounds.Count(sound=>sound==SoundId.SndUnknown5)-cueStart}; scratch{string.Join(',',sampledBytes.Select(value=>value.ToString("x2")))}/{string.Join(',',Enumerable.Range(0,4).Select(i=>native[0xcec0+i].ToString("x2")))}.");
                });
                updates++; if(x!=native.Word(0xd00c)||y!=native.Word(0xd00a)) moves++;
                FailIf(probeVisits!=updates,"Each actual Link update must reach the validation observer before PARTs and interactions.");
                runtimeCueStart=_sound.PlayRequestsFor(SoundId.SndUnknown5); Seed();
            });
            Seed(); Step(kind==3 ? 4 : 84);
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects,2); native[0xcc8a]=2; Step(3);
            _runtimeState.SetWramByte(WramAddress.wDisabledObjects,0); native[0xcc8a]=0;
            // The declared wrap stress disables the post-object boundary
            // pass with mode0. Test raw mode8 admission inside room bounds;
            // changing mode0 would introduce a separate boundary/clamp pass.
            if(!wrap)
            {
                _runtimeState.SetWramByte(WramAddress.wScrollMode,8); native[0xcd00]=8; Step(3);
                _runtimeState.SetWramByte(WramAddress.wScrollMode,1); native[0xcd00]=1;
            }
            _dialogue.ShowMessage("Native Link movement continues during text.",_player.Position.Y); native[0xcba0]=1;
            Step(hold); _dialogue.Close(); native[0xcba0]=0;
            for(int limit=0;updates<(kind==3 ? 45 : 155)&&limit<180;limit++) Step(1);
            FailIf(moves!=(kind==3 ? 28 : 62),"Native Link must perform21+7 reaction or47+15 aftermath steps through text and exact zero/wait boundaries.");
            if(kind==4 && hold==180)
            {
                object successor=new(); _player.BeginCutsceneControl(owner:successor); owner.Cancel();
                FailIf(!_player.IsCutsceneControlOwner(successor) || _runtimeState.ReadWramByte(WramAddress.wDisabledObjects)!=0x81 ||
                    _runtimeState.ReadWramByte(WramAddress.wMenuDisabled)!=0x81,
                    "Outgoing Nayru cancellation must retain its successor's control and current native masks.");
                _player.EndCutsceneControl(successor);
                _runtimeState.SetWramByte(WramAddress.wDisabledObjects,0); _runtimeState.SetWramByte(WramAddress.wMenuDisabled,0);
            }
            else
            {
                owner.Cancel(); owner.Cancel();
                FailIf(_player.CutsceneControlled || _runtimeState.ReadWramByte(WramAddress.wDisabledObjects)!=0 ||
                    _runtimeState.ReadWramByte(WramAddress.wMenuDisabled)!=0 || driver.ReactionStarted || driver.AftermathStarted,
                    "Nayru cancellation must retire native motion and release only its current control/masks.");
            }
            owner.Cancel(); _entities.Clear();
        }
    }

    private void CheckNayruLinkCoordinateHandoffs()
    {
        foreach(int kind in new[]{3,4})
        {
            ReinitializeGameplayForValidation(); _saveData.SetGlobalFlag(GlobalFlag.IntroDone,false); LoadValidationRoom(0,0x39);
            var owner=_roomEvents.Get<NayruIntroEvent>(); Vector2 initial=new(20.25f,70.5f);
            _player.WarpTo(initial); F1Set(_player,"_topDownMovementSpeedRaw",0x5a); _player.SetNativeCutsceneAngle(24);
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,20,70) { CompanionDispatchEnabled=true };
            native.InitializeLinkGameplay(); native[0xcc6e]=1;
            native[0xd001]=8; native[0xd002]=(byte)kind; native[0xd004]=(byte)(kind==3 ? 1 : 0);
            native[0xd005]=0; native[0xcfd0]=9; native[0xd009]=24; native[0xd010]=0x5a;
            native.Word(0xd00a,(int)(initial.Y*256)); native.Word(0xd00c,(int)(initial.X*256));
            native.UpdateGameplay(0,0,0xff,1);
            ((ICutsceneCommandHost)owner).RunNativeHandler(kind==3 ? "SetupNayruPossessionScene" : "BeginNayruAftermath");
            FailIf(_player.PrecisePosition!=new Vector2(native.Word(0xd00c)/256f,native.Word(0xd00a)/256f) ||
                _player.NativePlanarSpeed!=native[0xd010] || _player.NativePlanarAngle!=native[0xd009],
                $"linkCutscene{kind} coordinate-high handoff must preserve incoming low bytes and motion registers.");
            owner.Cancel(); _entities.Clear();
        }
    }

    private void CheckNayruLinkMotionCancellation()
    {
        foreach(int kind in new[]{3,4})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation(); _saveData.SetGlobalFlag(GlobalFlag.IntroDone,false); LoadValidationRoom(0,0x39);
            var owner=_roomEvents.Get<NayruIntroEvent>();
            ((ICutsceneCommandHost)owner).RunNativeHandler("SetupNayruPossessionScene");
            F1Set(owner,"_nayruStage",NayruStage.Script);
            var original=new NayruIntroEventDatabase().Commands.OfType<CutsceneNativeBlockingCommand>()
                .Single(command=>command.Handler==(kind==3 ? "VeranReaction" : "LinkAftermathMotion"));
            var command=original with { Source=original.Source with { CommandIndex=0 } };
            owner.CommandRunner.Start([command,new CutsceneWaitCommand(command.Source with { CommandIndex=1,Opcode="wait" },0xff),
                new CutsceneEndCommand(command.Source with { CommandIndex=2,Opcode="scriptend" })]);
            _player.WarpTo(new(80.25f,40.5f)); _player.BeginCutsceneControl(owner:owner);
            _runtimeState.SetWramByte(WramAddress.wWarpsDisabled,1);
            StepGameplayUpdates(kind==3 ? 4 : 84,Vector2.Zero,batched:batched);
            var motion=SomariaPrivate<NayruLinkCutsceneMotion>(owner,"_linkMotion");
            FailIf(SomariaPrivate<int>(motion,"_counter")<=0,"Cancellation must reach a running native Link counter.");
            Vector2 before=_player.PrecisePosition; owner.Cancel();
            StepGameplayUpdates(2,Vector2.Zero,batched:batched);
            FailIf(owner.HasState || motion.ReactionStarted || motion.AftermathStarted || _player.CutsceneControlled ||
                _player.PrecisePosition!=before || SomariaPrivate<int>(motion,"_counter")!=0 ||
                _runtimeState.ReadWramByte(WramAddress.wDisabledObjects)!=0 || _runtimeState.ReadWramByte(WramAddress.wMenuDisabled)!=0 ||
                _saveData.HasGlobalFlag(GlobalFlag.IntroDone),
                "Cancelling active Nayru Link motion must retire its counter/control without moving or completing the story on later updates.");
            _entities.Clear();
        }
    }
}
