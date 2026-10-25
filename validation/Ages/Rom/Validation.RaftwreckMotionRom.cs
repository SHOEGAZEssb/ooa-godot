using Godot;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateRaftwreckWindRom()
    {
        foreach(int sub in new[]{0,1,2})
        foreach(int speed in new[]{0x50,0x78})
        foreach(int x in new[]{1,239})
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(1,0xa8);
            var owner=_roomEvents.Get<RaftwreckEvent>();F1Set(owner,"_initializing",false);F1Set(owner,"_scriptEnded",true);
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(80,104));
            _runtimeState.SetWramByte(0xcd00,0);_runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);
            var row=new RaftwreckHelperRecord(3,0,248,x,sub,0);
            typeof(RaftwreckEvent).GetMethod("SpawnWind",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,[row,speed]);
            var effect=((IList)typeof(RaftwreckEvent).GetField("_interactionEffects",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!)[0]!;
            var actor=_entities.Entities<NpcCharacter>().Single(n=>n.Record.Id==0x64);
            Vector2 initial=new(x+0.25f,248.5f);F1Set(effect,"Position",initial);actor.Position=initial.Floor();
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,80,104);
            int at=0xd040+_entities.InteractionSlot(actor)*256;
            native[at]=1;native[at+1]=0x64;native[at+2]=(byte)sub;native[at+16]=(byte)speed;
            native.Word(at+10,0xf880);native.Word(at+12,x*256+0x40);native[0xcd00]=0;
            int updates=0,moves=0;bool frozen=false,observing=false;byte[] observed=new byte[4];
            void Observe()
            {
                if(!observing||_runtimeState.ReadWramByte(0xcec3)==0xa8)return;
                for(int i=0;i<4;i++)observed[i]=_runtimeState.ReadWramByte(0xcec0+i);
                observing=false;
            }
            void Seed(){observing=false;for(int i=0;i<4;i++){byte value=(byte)(0xa5+i);native[0xcec0+i]=value;_runtimeState.SetWramByte(0xcec0+i,value);}observing=true;}
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                bool moved=!frozen&&native[at]!=0&&native[at+4]==1;
                native.AdvanceInteractions(_entities.FrameCounter);updates++;if(moved)moves++;
                FailIf(actor.Active!=(native[at]!=0)||actor.Active&&(
                    SomariaPrivate<Vector2>(effect,"Position")!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    SomariaPrivate<int>(effect,"Angle")!=native[at+9]||SomariaPrivate<int>(effect,"AngleCounter")!=native[at+6]||
                    actor.CurrentAnimationCounter!=native[at+0x20]||actor.ZIndex!=ObjectDrawPriority.FromVisible(native[at+0x1a]))||
                    moved&&(observing||Enumerable.Range(0,4).Any(i=>observed[i]!=native[0xcec0+i]))||!moved&&!observing,
                    $"Raftwreck wind$64:${sub:x2}/speed${speed:x2}/x${x:x2} update{updates}: native preset/move/animation/lifetime/publication differs (active={actor.Active}/{native[at]}, position={SomariaPrivate<Vector2>(effect,"Position")}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, angle={SomariaPrivate<int>(effect,"Angle")}/{native[at+9]}, counter={SomariaPrivate<int>(effect,"AngleCounter")}/{native[at+6]}, animation={actor.CurrentAnimationCounter}/{native[at+0x20]}, priority={actor.ZIndex}/{ObjectDrawPriority.FromVisible(native[at+0x1a])}).");
                Seed();
            });
            _runtimeState.Changed+=Observe;
            try
            {
                Seed();Step(1);
                if(x==239)
                {
                    Step(3);frozen=true;
                    _dialogue.ShowMessage("Wind's ordinary interaction hold.",_player.Position.Y);native[0xcba0]=1;Step(2);_dialogue.Close();native[0xcba0]=0;
                    _runtimeState.SetWramByte(0xcc8a,2);native[0xcc8a]=2;Step(2);_runtimeState.SetWramByte(0xcc8a,0);native[0xcc8a]=0;
                    _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(2);_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;frozen=false;
                }
                while(actor.Active&&updates<160)Step(1);
                FailIf(actor.Active||moves==0||moves>114||speed==0x50&&x==239&&moves!=114,
                    "Wind must move before its X-boundary check and retire at boundary or the six-preset terminator.");
                Step(2);
            }
            finally{_runtimeState.Changed-=Observe;}
            owner.Cancel();_entities.Clear();
        }
    }

    private void ValidateRaftwreckFlashCountersRom() => ValidateRaftwreckControllerBoundaryRom(false);
    private void ValidateRaftwreckControllerRom() => ValidateRaftwreckControllerBoundaryRom(true);
    private void ValidateRaftwreckMountedRom() => ValidateRaftwreckControllerBoundaryRom(true,true);

    private void ValidateRaftwreckControllerBoundaryRom(bool complete,bool mounted=false)
    {
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();
            if(mounted)CompanionRuntimeState.Begin(_runtimeState,0x13,0xa8,new(96,118),3);
            LoadValidationRoom(1,0xa8);
            int initialX=complete?96:80;
            var raft=_entities.Entities<RaftRoomEntity>().SingleOrDefault();
            if(mounted)F1Set(raft!,"_precisePosition",new Vector2(96.25f,118.5f));
            _inventory.SetScriptedEquippedItems(0,0);
            _player.WarpTo(mounted?new(96.75f,113.25f):new(initialX+0.25f,118.5f));
            var owner=_roomEvents.Get<RaftwreckEvent>();owner.Start(_currentRoom);
            var runner=SomariaPrivate<CutsceneCommandRunner>(owner,"_runner");
            var sourceMotion=new (int Index,int Speed,int Angle,int Counter)[]{(4,0x14,0,0x6c),(23,0x14,24,0x61),
                (28,0x1e,8,0x41),(35,0x32,24,0x31),(41,0x14,8,0xff)};
            FailIf(owner.Database.Commands.OfType<CutsceneApplySpeedCommand>().Count()!=5||
                sourceMotion.Any(spec=>owner.Database.Commands[spec.Index] is not CutsceneApplySpeedCommand move||move.Counter!=spec.Counter),
                "Raftwreck's five source applyspeed boundaries changed.");
            var trace=new ScriptMovementPublicationTrace(_runtimeState,source=>
            {
                var spec=sourceMotion.SingleOrDefault(spec=>spec.Index==source.CommandIndex);
                return spec.Counter==0?null:(spec.Speed,spec.Angle,owner.PrecisePosition);
            },comparePositions:false); // Native controller comparison below includes oscillation between moves.
            _roomEvents.CommandTraceSink=trace;
            _runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);
            var randomStart=_random.CaptureState();
            var native=new SomariaRom(_saveData,randomStart,_currentRoom,0,initialX,118)
                {CompanionDispatchEnabled=mounted,HostilePartsEnabled=mounted};
            if(mounted)
            {
                native.InitializeLinkGameplay();native[0xd100]=3;native[0xd101]=0x13;native[0xd108]=3;
                native.Word(0xd10a,0x7680);native.Word(0xd10c,0x6040);
                native.UpdateGameplay(0,0,0xff,0); // Original SPECIALOBJECT_RAFT state0.
                native.Word(0xd00a,0x7140);native.Word(0xd00c,0x60c0);
            }
            const int at=0xd240;native[at]=1;native[at+1]=0x9b;native.AdvanceInteractions();
            var soundRequests=_sound.AttachPlayRequestAudit();
            // Both original controllers reach the installed-script boundary
            // at centered X$50. Surrounding mounted Link setup is separate.
            if(!complete)
            {
                owner.UpdateFrame();native.AdvanceInteractions();owner.UpdateFrame();native.AdvanceInteractions();
                _runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;
            }
            int updates=0,finishedMotionVisits=0;bool finished=false;
            while((complete?!finished:native[at+5]<7)&&updates<1600)
            {
                StepGameplayUpdates(3,Vector2.Zero,batched:batched,afterUpdate:()=>
                {
                    if(finished)
                    {
                        FailIf(owner.HasState||trace.MotionVisits!=finishedMotionVisits,
                            "The completed raftwreck controller must not move again during its warp fade.");
                        return;
                    }
                    native.AdvanceWarpPalette();
                    if(mounted)native.UpdateGameplay(0,0,0xff,_entities.FrameCounter);
                    else{native.AdvanceParts(_entities.FrameCounter);native.AdvanceInteractions(_entities.FrameCounter);}
                    updates++;
                    native.AdvanceScreenShake();
                    int phase=native[at+5]<3?0:Math.Min(5,native[at+5]-2);
                    int counter1=native[at+5]<2?owner.CenterCounter:runner.NativeCounter1;
                    FailIf(owner.PrecisePosition!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                        runner.MovementCounter("Raftwreck")!=native[at+7]||counter1!=native[at+6]||
                        owner.FlashPhase!=phase||_runtimeState.ReadWramByte(AgesWramAddress.wTmpcfc0)!=native[0xcfc0],
                        $"Raftwreck controller update{updates}: source script/native state differs (command={runner.Instruction}, counter1={counter1}/{native[at+6]}, counter2={runner.MovementCounter("Raftwreck")}/{native[at+7]}, phase={owner.FlashPhase}/{phase}, XY={owner.PrecisePosition}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}).");
                    finished=(native[AgesWramAddress.wGroup0RoomFlags+0x100+0xa8]&0x40)!=0;
                    if(finished)finishedMotionVisits=trace.MotionVisits;
                    FailIf(_saveData.HasRoomFlag(1,0xa8,0x40)!=finished||complete&&owner.HasState==finished,
                        $"Raftwreck update{updates}: source flag/retirement differs (flag={_saveData.HasRoomFlag(1,0xa8,0x40)}/{finished}, active={owner.HasState}).");
                    FailIf(_random.Calls-randomStart.Calls!=native.RandomCalls||
                        _entities.ScreenShakeCounter!=native[0xcd18]||_entities.HorizontalScreenShakeCounter!=native[0xcd19],
                        $"Raftwreck update{updates}: original post-object shake/RNG differs (RNG={_random.Calls-randomStart.Calls}/{native.RandomCalls}, shake={_entities.ScreenShakeCounter},{_entities.HorizontalScreenShakeCounter}/{native[0xcd18]},{native[0xcd19]}).");
                    FailIf(!soundRequests.Requests.SequenceEqual(native.Sounds),
                        $"Raftwreck update{updates}: source sound order differs ({string.Join(',',soundRequests.Requests)}/{string.Join(',',native.Sounds)}).");
                    FailIf(mounted&&(_player.PrecisePosition!=new Vector2(native.Word(0xd00c)/256f,native.Word(0xd00a)/256f)||
                        _player.RaftRideActive!=(native[0xcc2c]==0xd1)),
                        $"Raftwreck update{updates}: original mounted Link copy/handoff differs ({_player.PrecisePosition}/{native.Word(0xd00c)/256f},{native.Word(0xd00a)/256f}, ride={_player.RaftRideActive}/{native[0xcc2c]:x2}).");
                    if(complete&&updates==1){_runtimeState.SetWramByte(0xcd00,0);native[0xcd00]=0;}
                });
            }
            FailIf(complete?(!finished||trace.Visited.Count!=5||!_transitions.IsTransitioning):native[at+5]!=7||owner.FlashPhase!=5,
                "Raftwreck must retain original flash gates, five motion sites and the terminal counter/warp handoff.");
            FailIf(complete&&(updates!=1078||native.Word(at+12)!=0x5580||native.Word(at+10)!=0x4280||native.RandomCalls!=31||
                native.Sounds.Count(sound=>sound==SoundId.SndKillEnemy)!=5),
                "Original raftwreck must finish at update1078/XY$5580,$4280 after31 RNG draws and five debris sounds.");
            owner.Cancel();_entities.Clear();
        }
    }
}
