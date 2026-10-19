using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateNayruRescueMovementPublicationRom()
    {
        // scripts.s and scriptHelper.s: independently traced incoming speed,
        // angle and counter at each moving command, including shared lady tail.
        var profiles = new (string Script, int Id, int Sub, int Speed, int[] Angles, int[] Counters)[] {
            ("nayru-farewell",0x36,2,0x1e,[8,16],[0x14,0x4c]),
            ("ralph-greeting",0x37,4,0x28,[16,8],[0x13,0x0a]),
            ("ralph-farewell",0x37,4,0x50,[16],[0x18]),
            ("nayru-escape",0x36,0x10,0x28,[0,24,0,16,16,16],[0x50,0x10,0x22,0x52,0x70,0x18]),
            ("ralph-escape",0x37,0x0e,0x28,[0,0,16,16,16],[0x50,0x10,0x40,0x70,0x18]),
            ("nayru-ending",0x36,3,0x14,[24,8,8,16],[0x20,0x10,0x10,0x28]),
            ("ralph-ending",0x37,5,0x14,[8,24,24,16],[0x20,0x10,0x10,0x28]),
            ("nayru-palace",0x36,4,0x28,[0,0,0,24,0],[0x19,0x21,0x1a,0x11,0x10]),
            ("ralph-palace",0x37,6,0x28,[0,0,0],[0x37,0x21,0x15]),
            ("ralph-credits-maku",0x37,7,0x3c,[5],[0x1e]),
            ("boy",0x3c,5,0x3c,[24,0,8,16,8,0,24],[0x0a,0x21,0x20,0x36,0x16,0x16,0x35]),
            ("old-lady",0x3d,3,0x3c,[24,0,8,16,8,0,24],[0x16,0x21,0x20,0x36,0x16,0x16,0x35]) };
        int checkedCommands=0;
        foreach(string script in new[]{"nayru-arrival","nayru-song","ralph-arrival","maku","cloaked","twinrova",
            "nayru-palace-farewell","ralph-palace-farewell","nayru-credits-maku","ralph-credits-sword","maku-credits"})
            FailIf(new NayruRescueWarpDatabase().Commands(script).Any(command=>command is CutsceneMoveCommand or CutsceneApplySpeedCommand),
                $"{script}: previously static source stream now requires a movement profile.");
        foreach(var profile in profiles)
        {
            var data=new NayruRescueWarpDatabase();
            var commands=data.Commands(profile.Script).Where(command=>command is CutsceneMoveCommand or CutsceneApplySpeedCommand).ToArray();
            FailIf(commands.Length!=profile.Counters.Length,$"{profile.Script}: source motion inventory changed.");
            for(int index=0;index<commands.Length;index++)
            foreach(bool batched in new[]{false,true})
            {
                ReinitializeGameplayForValidation();LoadValidationRoom(0,0x33);_entities.Clear();
                _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(40,104));
                var context=SomariaPrivate<RoomEventContext>(_roomEvents,"_context");
                var actor=new NayruRescueActorRoomEntity(context,data,profile.Id,profile.Sub,new(8.25f,8.5f));
                _entities.AddEntity(actor);
                // Explicit initialized script boundary; initializer, jump setup
                // and surrounding story signals remain covered by scene tests.
                F1Set(actor,"<State>k__BackingField",1);
                F1Set(actor,"<Substate>k__BackingField",profile.Id is 0x3c or 0x3d || profile.Script.EndsWith("ending",StringComparison.Ordinal)?2:0);
                int speed=profile.Speed;
                if(profile.Script.EndsWith("escape",StringComparison.Ordinal)&&index==commands.Length-1)speed=0x50;
                if(profile.Script.EndsWith("ending",StringComparison.Ordinal)&&index>=2)speed=0x28;
                if(profile.Script=="nayru-palace"&&index==1 || profile.Script=="ralph-palace"&&index==1)speed=0x14;
                if(profile.Script=="ralph-palace"&&index==2)speed=0x50;
                int angle=profile.Angles[index],counter=profile.Counters[index];
                CutsceneCommand command=commands[index];
                int incoming=data.Commands(profile.Script).Take(command.Source.CommandIndex).OfType<CutsceneSetSpeedCommand>().Last().Speed;
                FailIf(incoming!=speed,$"{profile.Script} motion{index}: source speed changed.");
                FailIf(command is CutsceneMoveCommand move && (move.Counter!=counter||move.Angle!=angle) ||
                    command is CutsceneApplySpeedCommand apply && apply.Counter!=counter,
                    $"{profile.Script} motion{index}: source operands changed.");
                var runner=SomariaPrivate<CutsceneCommandRunner>(actor,"_runner");
                runner.Start([command with{Source=command.Source with{CommandIndex=0}},
                    new CutsceneWaitCommand(command.Source with{CommandIndex=1,Opcode="wait"},255),
                    new CutsceneEndCommand(command.Source with{CommandIndex=2,Opcode="scriptend"})]);
                runner.SetInitialMotionRegisters("Actor",speed,angle);
                _player.BeginCutsceneControl(owner:actor);_runtimeState.SetWramByte(WramAddress.wDisabledObjects,1);
                var native=new ObjectMotionRom();const int at=ObjectMotionRom.Object+0x40;
                native.Word(at+10,0x0880);native.Word(at+12,0x0840);native[at+9]=(byte)angle;native[at+16]=(byte)speed;
                int eligible=0,movements=0;
                void Seed(){for(int i=0;i<4;i++){byte value=(byte)(0xa5+i);native[0xcec0+i]=value;_runtimeState.SetWramByte(0xcec0+i,value);}}
                void Step(int count,bool frozen=false)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
                {
                    if(!frozen){if(eligible>0&&eligible<counter){native.Call(ObjectMotionRom.ApplySpeed,slot:0x40);movements++;}eligible++;}
                    FailIf(actor.PrecisePosition!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                        Enumerable.Range(0,4).Any(i=>_runtimeState.ReadWramByte(0xcec0+i)!=native[0xcec0+i]),
                        $"{profile.Script} motion{index} eligible{eligible}: source position/publication/stop differs.");
                    Seed();
                });
                Seed();Step(4);
                _dialogue.ShowMessage("Rescue actor script pause.",_player.Position.Y);Step(3,true);_dialogue.Close();
                _runtimeState.SetWramByte(WramAddress.wDisabledObjects,3);Step(3,true);_runtimeState.SetWramByte(WramAddress.wDisabledObjects,1);
                _runtimeState.SetWramByte(0xcd00,8);Step(3,true);_runtimeState.SetWramByte(0xcd00,1);
                Step(counter-4+2);
                FailIf(movements!=counter-1,$"{profile.Script}: motion must exercise the full native counter.");
                _entities.Clear();Step(2,true);_player.EndCutsceneControl(actor);checkedCommands++;
            }
        }
        FailIf(checkedCommands!=94,"Rescue publication must cover all47 source motion commands under both host schedules.");
    }

    private void ValidateNayruRescueNativePublicationRom()
    {
        foreach(int family in new[]{0,1,2,3}) // monkey9, Twinrova arrival/departure, sword
        foreach(bool batched in new[]{false,true})
        {
            ReinitializeGameplayForValidation();LoadValidationRoom(0,0x33);_entities.Clear();
            _inventory.SetScriptedEquippedItems(0,0);_player.WarpTo(new(40,104));
            var context=SomariaPrivate<RoomEventContext>(_roomEvents,"_context");
            int id=family==0?0x39:family==3?0x37:0x93,sub=family==0?4:family==3?8:0;
            var actor=new NayruRescueActorRoomEntity(context,new NayruRescueWarpDatabase(),id,sub,new(72,56),family==0?9:0);
            _entities.AddEntity(actor);_runtimeState.SetWramByte(0xcfd0,8);
            _player.BeginCutsceneControl(owner:actor);_runtimeState.SetWramByte(WramAddress.wDisabledObjects,1);
            var native=new SomariaRom(_saveData,_random.CaptureState(),_currentRoom,0,40,104){CompanionDispatchEnabled=true};
            native.InitializeLinkGameplay();native[0xcc8a]=1;native[0xcfd0]=8;
            int at=0xd040+_entities.InteractionSlot(actor.Node)*256;
            native[at]=1;native[at+1]=(byte)id;native[at+2]=(byte)sub;native[at+3]=(byte)(family==0?9:0);
            StepGameplayUpdates(1,Vector2.Zero,afterUpdate:()=>native.UpdateGameplay(0,0,255,_entities.FrameCounter));
            // Isolate the moving primary. Mirror/linked-sword presentation is
            // checked by existing scene validations, not this shared call.
            F1Retain(actor);
            for(int slot=0;slot<16;slot++){int other=0xd040+slot*256;if(other!=at)native[other]=0;}
            Vector2 initial=new(8.25f,8.5f);F1Set(actor,"_precise",initial);actor.Node.Position=initial.Floor();
            native.Word(at+10,0x0880);native.Word(at+12,0x0840);
            if(family==0)
            {
                F1Set(actor,"<Substate>k__BackingField",4);native[at+5]=4;
            }
            else if(family==2 || family==3)
            {
                F1Set(actor,"<Substate>k__BackingField",family==2?2:1);native[at+5]=(byte)(family==2?2:1);
                F1Set(actor,"_presetIndex",0);F1Set(actor,"_preset",1);
                typeof(NayruRescueActorRoomEntity).GetMethod(family==2?"LoadTwinrovaPreset":"LoadCreditsSwordPreset",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(actor,null);
                native[at+0x3a]=(byte)(family==2?1:2);native[at+7]=0;native[0xffae]=0x40;
                if(family==3)native[at+0x10]=0x1e; // ralphSubid08 native setup after script end.
                byte[] caller=[0x16,(byte)(at>>8),0xcd,0xb4,0x76,0xc9];
                for(int i=0;i<caller.Length;i++)native[0xc100+i]=caller[i];
                SomariaPrivate<FrontendRom>(native,"_rom").Call(0xc100,0x0a);native[0xffae]=0;
            }
            int updates=0,movements=0,landings=0;byte[] expected=new byte[4];
            void Seed(){for(int i=0;i<4;i++){byte value=(byte)(0xa5+i);native[0xcec0+i]=value;_runtimeState.SetWramByte(0xcec0+i,value);}}
            void Step(int count)=>StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                int previousX=native.Word(at+12),previousY=native.Word(at+10),previousZ=native.Word(at+14);bool previouslyLive=native[at]!=0;
                native.UpdateGameplay(0,0,255,_entities.FrameCounter,afterInteractions:()=>{for(int i=0;i<4;i++)expected[i]=native[0xcec0+i];});updates++;
                if(native[at]!=0&&(previousX!=native.Word(at+12)||previousY!=native.Word(at+10)))movements++;
                // Departure applies speed before interactionDelete clears the slot.
                if(family==2&&previouslyLive&&native[at]==0)movements++;
                if(previousZ!=0&&native.Word(at+14)==0)landings++;
                FailIf(actor.Finished!=(native[at]==0)||!actor.Finished&&(
                    actor.PrecisePosition!=new Vector2(native.Word(at+12)/256f,native.Word(at+10)/256f)||
                    actor.Substate!=native[at+5]||actor.ZFixed!=unchecked((short)native.Word(at+14)))||
                    !_runtimeState.ReadWramByte(0xcfd0).Equals(native[0xcfd0])||
                    Enumerable.Range(0,4).Any(i=>_runtimeState.ReadWramByte(0xcec0+i)!=expected[i]),
                    $"Rescue native family{family} update{updates}: source movement/gravity/state/publication differs (XY {actor.PrecisePosition}/{native.Word(at+12)/256f},{native.Word(at+10)/256f}, state{actor.Substate}/{native[at+5]}).");
                Seed();
            });
            Seed();Step(4);
            _dialogue.ShowMessage("Native rescue motion admission.",_player.Position.Y);native[0xcba0]=1;Step(3);_dialogue.Close();native[0xcba0]=0;
            _runtimeState.SetWramByte(0xcc8a,3);native[0xcc8a]=3;Step(3);_runtimeState.SetWramByte(0xcc8a,1);native[0xcc8a]=1;
            _runtimeState.SetWramByte(0xcd00,8);native[0xcd00]=8;Step(3);_runtimeState.SetWramByte(0xcd00,1);native[0xcd00]=1;
            int target=family==0?180:family==3?1:family==2?2:0;
            for(int limit=0;limit<340&&(family==0?updates<target:!actor.Finished&&actor.Substate==target);limit+=2)Step(2);
            if(family==0)FailIf(movements<40||landings<3,"Monkey9 must exercise airborne motion, landing skips, turn waits and repeated hops.");
            else FailIf(movements!=(family==1?106:family==2?126:272),$"Rescue native family{family}: source preset movement count differs ({movements}).");
            _entities.Clear();native[at]=0;
            StepGameplayUpdates(2,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                native.UpdateGameplay(0,0,255,_entities.FrameCounter);
                FailIf(Enumerable.Range(0,4).Any(i=>_runtimeState.ReadWramByte(0xcec0+i)!=native[0xcec0+i]),
                    "Retired rescue actor must stop publishing movement.");Seed();
            });
            _player.EndCutsceneControl(actor);
        }
    }
}
