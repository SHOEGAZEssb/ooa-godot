using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateDefeatedMoblinSequence()
    {
        RunDefeatedMoblinSequence(false);
        RunDefeatedMoblinSequence(true);
    }
    private void RunDefeatedMoblinSequence(bool batch)
    {
        void Step(int count=1) =>
            StepGameplayUpdates(count, Vector2.Zero, [], [], batched: batch);
        var sequence=_roomEvents.Get<DefeatedMoblinEvent>();
        _saveData.SetGlobalFlag(0x1a,false);_saveData.SetRoomFlag(0,9,0x40,false);
        LoadValidationRoom(0,9);Step();
        FailIf(sequence.HasState || _entities.Entities<NpcCharacter>().Any(n=>n.Record.Id==0x72 && n.Active),"Room 0:09/$72 must be absent before the keep is destroyed.");
        _saveData.SetGlobalFlag(0x1a);_saveData.WriteWramByte(0xc612,(byte)(batch?1:0));
        LoadValidationRoom(0,9);_player.WarpTo(new Vector2(0x58,0x48));_player.Face(Vector2I.Down);
        Step();
        var king=sequence.Actors.Single(a=>a.Actor.Record.SubId==0);
        FailIf(sequence.Actors.Count!=3 || !_player.CutsceneControlled || king.Actor.Position!=new Vector2(0x58,0x60) ||
            sequence.Actors.Count(a=>a.Initialized)!=3,"Defeated Moblin state0 must allocate and initialize both later-slot followers, and lock input.");
        var before=_player.Position;
        Step(70);
        FailIf(_dialogue.IsOpen || king.Runner.Counter!=1,"Defeated Moblin wait 70 dispatched TX_2f1b early.");
        Step();FailIf(!_dialogue.IsOpen || _player.Position!=before,"Defeated Moblin TX_2f1b missing after the native wait.");
        var position=king.Actor.Position;Step(9);
        FailIf(king.Actor.Position!=position || _entities.RuntimeState.ReadWramByte(0xcfd0)!=0,"Defeated Moblin must not release or move followers during TX_2f1b.");
        // Cancelling before the reward leaves the persistent replay gate clear.
        _dialogue.Close();LoadValidationRoom(0,0x19);Step();
        FailIf(sequence.HasState || _player.CutsceneControlled || _saveData.HasRoomFlag(0,9,0x40),"Defeated Moblin cancellation leaked controls or completed the message.");
        LoadValidationRoom(0,9);_player.WarpTo(new Vector2(0x58,0x48));
        var trace=new ValidationCutsceneTrace();_roomEvents.CommandTraceSink=trace;
        var messages=new List<string>();int updates=0;
        var completed=new Dictionary<DefeatedMoblinScriptHost,Vector2>();
        bool cancelledAfterReward=batch;
        int initialMaku=_saveData.MakuTreeState;
        while(sequence.HasState && updates++<2500)
        {
            if(_dialogue.IsOpen)
            {
                messages.Add(_dialogue.CurrentMessage);Step(3);_dialogue.Close();
            }
            Step();
            foreach(var actor in sequence.Actors)
                if(!actor.Remote && actor.Ended && !actor.Actor.Active && !completed.ContainsKey(actor))
                    completed.Add(actor,actor.Actor.Position);
            if(sequence.Remote.Stage==RemoteMakuEventStage.Tail)
            {
                FailIf(sequence.BlocksGameplay || _player.CutsceneControlled,"Remote Maku's confetti tail must not retain the defeated Moblin input lock.");
            }
            if(!cancelledAfterReward && sequence.Remote.Stage==RemoteMakuEventStage.Running)
            {
                cancelledAfterReward=true;
                LoadValidationRoom(0,0x19);Step();
                FailIf(!_inventory.HasTreasure(0x49) || _saveData.HasRoomFlag(0,9,0x40) || _player.CutsceneControlled,
                    "Cancelling after the Bomb Flower reward must retain the item, release input, and leave the remote-Maku replay flag clear.");
                trace.Entries.Clear();trace.Observations.Clear();messages.Clear();completed.Clear();
                LoadValidationRoom(0,9);_player.WarpTo(new Vector2(0x58,0x48));
            }
        }
        _roomEvents.CommandTraceSink=null;
        FailIf(sequence.HasState,$"Defeated Moblin sequence did not finish after {updates} updates; remote={sequence.Remote.Stage}.");
        Vector2[] expectedGorons=[new(56,152.5f),new(168.5f,88),new(144,136.5f),new(88,136.5f)];
        FailIf(completed.Count!=7,"Defeated Moblin lost an independently updated actor lane.");
        foreach(var (actor,end) in completed)
        {
            Vector2 expected=actor.Actor.Record.SubId switch {
                0=>new Vector2(88,190.5f),1=>new Vector2(actor.Actor.Record.X,198.5f),
                _=>expectedGorons[actor.Actor.Record.Var03]
            };
            FailIf(end!=expected,$"INTERAC $72:{actor.Actor.Record.SubId:x2}/v{actor.Actor.Record.Var03:x2} ended at {end}, expected {expected} from source movement counters.");
        }
        int[] texts=trace.Entries.Where(e=>e.Phase==CutsceneCommandTracePhase.Started)
            .Select(e=>e.Source).Where(s=>s.Opcode=="showtext").Select(s=>sequence.Database.Commands[s.CommandIndex])
            .OfType<CutsceneShowTextCommand>().Select(c=>c.TextId).ToArray();
        FailIf(!texts.SequenceEqual(new[]{0x2f1b,0x3128,0x3129}),"Defeated Moblin dialogue script order differs from TX_2f1b/3128/3129.");
        FailIf(messages.Count!=5 || !messages[1].Contains("Bomb",StringComparison.Ordinal) ||
            !messages[2].Contains("Bomb",StringComparison.Ordinal),$"Defeated Moblin reward or Maku dialogue missing ({messages.Count} messages).");
        FailIf(!messages[4].Replace('\n',' ').Contains("old Goron tales!",StringComparison.Ordinal) ||
            trace.Observations.Count(e=>e.Observation=="Treasure" && e.Value==0x49)!=1,
            "Defeated Moblin must grant Bomb Flower once and select remote Maku TX_05b6/TX_05c6.");
        FailIf(!_inventory.HasTreasure(0x49) || !_saveData.HasRoomFlag(0,9,0x40) ||
            _saveData.MakuTreeState!=initialMaku+1 || _saveData.MakuMapTextPresent!=(batch?0xc6:0xb6) ||
            _player.CutsceneControlled || _entities.RuntimeState.ReadWramByte(0xcfd0)!=2,
            "Defeated Moblin Bomb Flower / remote Maku completion state mismatch.");
        LoadValidationRoom(0,9);Step(100);
        FailIf(sequence.HasState || _dialogue.IsOpen || _player.CutsceneControlled,"Completed defeated Moblin event replayed on re-entry.");
        _saveData.SetRoomFlag(0,9,0x40,false);
        GD.Print($"Validated defeated Moblin room 0:09 complete reward and remote Maku sequence (linked/batched={batch}).");
    }
}
