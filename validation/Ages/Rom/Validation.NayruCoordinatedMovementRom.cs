using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private void ValidateNayruCoordinatedMovementRom()
    {
        // Source movement operands from Nayru/Ralph/Impa scripts and the
        // ghost var38 table. Counter values are independent of imported
        // synthetic host durations; each original positive counter gives N-1.
        var cases = new[] { (10,"Nayru",10,16,32), (27,"Impa",40,8,32), (29,"Impa",40,0,16),
            (43,"Impa",20,22,72), (82,"GhostVeran",20,11,80), (88,"GhostVeran",10,16,41),
            (100,"Ralph",40,24,16), (103,"Ralph",40,0,24), (113,"Ralph",5,16,129),
            (125,"Ralph",40,0,48), (127,"Ralph",40,24,49), (131,"Nayru",40,0,17),
            (158,"AftermathRalph",5,8,129), (167,"AftermathRalph",5,24,65), (168,"AftermathRalph",10,24,37),
            (177,"AftermathRalph",80,8,25), (183,"AftermathRalph",120,16,40),
            (216,"AftermathImpa",40,8,65), (218,"AftermathImpa",40,16,33),
            (52,"GhostVeran",80,28,17), (55,"GhostVeran",80,11,37), (58,"GhostVeran",80,24,19),
            (61,"GhostVeran",80,2,25), (64,"GhostVeran",80,10,12), (67,"GhostVeran",80,20,17),
            (91,"Parallel",0,0,0), (50,"VeranReaction",0,0,0) };
        foreach (var (index, name, speed, angle, counter) in cases)
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation(); _saveData.SetGlobalFlag(GlobalFlag.IntroDone, false); LoadValidationRoom(0, 0x39);
            var owner = _roomEvents.Get<NayruIntroEvent>(); var host = (ICutsceneCommandHost)owner;
            host.RunNativeHandler("SetupNayruPossessionScene");
            var database = new NayruIntroEventDatabase();
            var original = (CutsceneNativeBlockingCommand)database.Commands[index];
            bool doubleFlight = original.Handler == "GhostFlight";
            var motions = new List<(string Actor,int Speed,int Angle,int Counter,int Delay,int Start,int Applications)>();
            if (name == "Parallel")
            {
                motions.Add(("GhostVeran",120,0,34,0,0,1)); motions.Add(("Nayru",10,0,32,4,0,1));
                FailIf(original.Handler != "ParallelObjectMovement" || original.Frames != 36,
                    "Source concurrent Ghost/Nayru move must retain its independent counters and four-update Nayru lead-in.");
            }
            else if (name == "VeranReaction")
                motions.Add(("Ralph",60,16,22,1,3,1));
            else
            {
                int delay = doubleFlight ? 0 : 1;
                motions.Add((name,speed,angle,counter,delay,0,doubleFlight ? 2 : 1));
                string[] fields = original.Payload.Split(',');
                FailIf(original.Actor?.Value != name || original.Frames != (doubleFlight ? counter * 2 + 1 : counter + 1) ||
                    fields[0] != speed.ToString() || fields[1] != angle.ToString() || fields[3] != (doubleFlight ? "2" : "1") || fields[4] != "1" || fields[5] != delay.ToString(),
                    $"Source movement operands/counter changed at initial Nayru command{index}.");
            }
            var native = new Dictionary<string,ObjectMotionRom>();
            foreach (var motion in motions)
            {
                if (!owner.ActorRegistry.ContainsKey(motion.Actor)) owner.ActorRegistry.Spawn(motion.Actor,motion.Actor);
                Vector2 initial = new(254.25f, 2.5f); owner.ActorRegistry[motion.Actor].Position = initial;
                var rom = new ObjectMotionRom(); rom.Word(0xd14a,640); rom.Word(0xd14c,65088);
                rom[0xd149] = (byte)motion.Angle; rom[0xd150] = (byte)motion.Speed; native.Add(motion.Actor,rom);
            }
            F1Set(owner,"_nayruStage",NayruStage.Script);
            var command = original with { Source = original.Source with { CommandIndex = 0 } };
            owner.CommandRunner.Start([command,new CutsceneWaitCommand(command.Source with { CommandIndex=1,Opcode="wait" },0xff),
                new CutsceneEndCommand(command.Source with { CommandIndex=2,Opcode="scriptend" })]);
            _player.WarpTo(new(40,112)); _player.BeginCutsceneControl(owner:owner);
            _runtimeState.SetWramByte(AgesWramAddress.wDisabledObjects,1); _runtimeState.SetWramByte(AgesWramAddress.wWarpsDisabled,1);
            int update = 0, steps = 0; bool text = false;
            byte[] expected = new byte[4];
            void Seed() { for(int i=0;i<4;i++) { expected[i]=(byte)(0xa5+i); _runtimeState.SetWramByte(0xcec0+i,expected[i]); } }
            void Step(int count) => StepGameplayUpdates(count,Vector2.Zero,batched:batched,afterUpdate:()=>
            {
                bool moved = false;
                if (!text && update < original.Frames)
                {
                    foreach(var motion in motions)
                    {
                        int local = update-motion.Start;
                        if(doubleFlight) { if((local&1)==0) continue; local/=2; }
                        if(local < motion.Delay || local >= motion.Counter + motion.Delay - 1) continue;
                        var rom = native[motion.Actor];
                        for(int application=0;application<motion.Applications;application++) { rom.Call(ObjectMotionRom.ApplySpeed,slot:0x40); steps++; }
                        for(int i=0;i<4;i++) expected[i]=rom[0xcec0+i];
                        moved=true;
                    }
                    update++;
                }
                foreach(var motion in motions)
                {
                    var rom=native[motion.Actor];
                    FailIf(owner.ActorRegistry[motion.Actor].Position != new Vector2(rom.Word(0xd14c)/256f,rom.Word(0xd14a)/256f),
                        $"Nayru command{index}/{motion.Actor} update{update}: source counter/wrapping/native position differs.");
                }
                if(name!="VeranReaction" || moved)
                    FailIf(Enumerable.Range(0,4).Any(i=>_runtimeState.ReadWramByte(0xcec0+i)!=expected[i]),
                        $"Nayru command{index} update{update}: executed NPC movement must publish native scratch before later phases.");
                Seed();
            });
            Seed(); Step(4); text=true; _dialogue.ShowMessage("Coordinated NPC script text hold.",_player.Position.Y);
            int before=steps; Step(3); FailIf(steps!=before,"Coordinated NPC script movement must retain its counter during text.");
            text=false; _dialogue.Close(); Step(original.Frames-4+2);
            int required=motions.Sum(motion=>(motion.Counter-1)*motion.Applications);
            FailIf(steps!=required || owner.CommandRunner.CurrentCommand is not CutsceneWaitCommand,
                $"Nayru command{index}: source must execute every positive counter exactly once ({steps}/{required}).");
            owner.Cancel(); _entities.Clear();
        }
    }
}
