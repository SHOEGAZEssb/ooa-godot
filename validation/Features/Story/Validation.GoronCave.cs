using Godot;
using System.Linq;
using System.Collections.Generic;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateRoom5c3GoronBoundaries()
    {
        LoadValidationRoom(5,0xc3);
        var cave=_roomEvents.Get<GoronCaveEvent>();
        void Step(int frames) => StepGameplayUpdates(frames,Vector2.Zero);
        var pacing=cave.Actors.Single(a=>a.Actor.Record.SubId==4);
        var napper=cave.Actors.Single(a=>a.Actor.Record is {SubId:5,Var03:2});
        Step(1);
        FailIf(pacing.Actor.Position!=new Vector2(0x98,0x80)||pacing.MovementCounter!=63,
            "Goron $04 initialization must yield at var3c=$3f before applying SPEED_80.");
        Step(62);
        FailIf(pacing.Actor.Position.X!=121||pacing.MovementCounter!=1,
            "Goron $04 must move 31 pixels in 62 subsequent updates.");
        Step(1);
        FailIf(pacing.Actor.Position.X!=120||pacing.MovementCounter!=127,
            "Goron $04 zero counter must reverse and decrement the new $80 counter in the same update.");
        Step(1);
        FailIf(pacing.Actor.Position.X!=121||pacing.MovementCounter!=126,
            "Goron $04 did not begin the return leg on the following update.");
        // The napping rectangle uses $18 radii plus Link's $06, not the
        // misleading '12 pixels' comment in scriptHelper.s.
        _player.WarpTo(new Vector2(169,152)); Step(4);
        FailIf(napper.Actor.CurrentScriptAnimationSource!=cave.Database.Animation(0x66,4),
            "Goron $05 must nap outside the $1e collision range.");
        StepGameplayUpdates(1,Vector2.Right,["move_right"],["move_right"]); Step(3);
        FailIf(napper.Actor.CurrentScriptAnimationSource==cave.Database.Animation(0x66,4),
            "Goron $05 did not wake at the inclusive negative $1e boundary.");
        // Approach the pacing actor from the lower floor, correcting X by
        // walking while the actor continues its independent half-pixel pace.
        _player.WarpTo(pacing.Actor.Position+new Vector2(0,24));
        StepGameplayUpdates(12,Vector2.Up,["move_up"],["move_up"]);
        StepGameplayUpdates(6,Vector2.Right,["move_right"],["move_right"]);
        StepGameplayUpdates(1,Vector2.Up,["move_up"],["move_up"]); Step(1);
        FailIf(!pacing.Actor.CanTalkTo(_player),"Room 5:c3 pacing Goron is unreachable from its lower floor.");
        StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
        for(int i=0;i<8&&!_dialogue.IsOpen;i++) Step(1);
        FailIf(!_dialogue.IsOpen||!_dialogue.CurrentMessage.Contains("It's terrible!"),
            "Pacing Goron did not select TX_2479 before rescue.");
        _dialogue.Close(); Step(10);
        FailIf(!cave.BlocksGameplay,"Pacing Goron failed to retain input during its wait 30.");
        cave.Cancel(); cave.Cancel();
        FailIf(_player.CutsceneControlled||cave.HasState||_saveData.HasGlobalFlag(0x2f),
            "Cancellation during the Goron wait committed rescue or retained input.");
        _saveData.SetGlobalFlag(0x2f);
        _saveData.SetRoomFlag(5,0xc3,0x40);
        LoadValidationRoom(5,0xc3); Step(3);
        var elder=cave.Actors.Single(a=>a.Actor.Record.Id==0x8b);
        FailIf(!elder.Actor.Active||_dialogue.IsOpen||cave.BlocksGameplay,
            "Saved elder re-entry replayed the Crown Key cutscene.");
        _saveData.SetGlobalFlag(0x14);
        LoadValidationRoom(5,0xc3); Step(3);
        FailIf(cave.Actors.Any(a=>a.Actor.Record.Id==0x8b&&a.Actor.Active),
            "Goron elder ignored GLOBALFLAG_FINISHEDGAME suppression.");
    }

    private void ValidateRoom5c3Gorons()
    {
        string first=RunGoronCave(false);
        ReinitializeGameplayForValidation();
        string batched=RunGoronCave(true);
        FailIf(first!=batched,"Room 5:c3 state/RNG/counters differ between single and batched host updates.");
    }

    private string RunGoronCave(bool batched)
    {
        var observations=new List<string>();
        _saveData.SetGlobalFlag(0x2f, false);
        _saveData.SetGlobalFlag(0x14, false);
        _saveData.SetRoomFlag(5,0xc3,0x40,false);
        LoadValidationRoom(5,0xc3);
        var cave=_roomEvents.Get<GoronCaveEvent>();
        void Step(int count) => StepGameplayUpdates(count,Vector2.Zero,batched:batched);
        void Observe() => observations.Add(string.Join(";",cave.Actors.Select(a=>
            $"{a.Actor.Record.Id:x2}:{a.Actor.Record.SubId:x2}:{a.Actor.Record.Var03:x2}:{a.Actor.Active}:{a.CommandIndex}:{a.Counter}:{a.MovementCounter}"))+
            $"/{_entities.RandomCalls}/{_player.Position}/{_saveData.GetRoomFlags(5,0xc3)}");
        void TalkStationary(GoronCaveScriptHost host,string fragment)
        {
            bool fromLeft=host.Actor.Position.Y>128;
            Vector2 direction=fromLeft?Vector2.Right:Vector2.Up;
            string action=fromLeft?"move_right":"move_up";
            Vector2 start=host.Actor.Position-direction*24;
            FailIf(_rooms.CurrentRoom.GetTerrainInfo(start).Collision!=0,
                $"Room 5:c3 approach starts outside floor at {start}.");
            _player.WarpTo(start);
            StepGameplayUpdates(20,direction,[action],[action],batched);
            Step(2);
            FailIf(!host.Actor.CanTalkTo(_player)||_player.Position.DistanceTo(host.Actor.Position)<12,
                $"Room 5:c3 Goron ${host.Actor.Record.SubId:x2}/v${host.Actor.Record.Var03:x2} unreachable from floor.");
            for(int repeat=0;repeat<2;repeat++)
            {
                StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
                for(int i=0;i<10&&!_dialogue.IsOpen;i++) Step(1);
                FailIf(!_dialogue.IsOpen||!_dialogue.CurrentMessage.Contains(fragment),
                    $"Room 5:c3 ambient Goron expected '{fragment}', got '{_dialogue.CurrentMessage}'.");
                _dialogue.Close(); Step(35);
            }
        }
        Step(4);
        FailIf(cave.Actors.Count!=6 || cave.Actors.Count(a=>a.Actor.Active)!=4,
            "Room 5:c3 must initialize six source slots and retain $06/v$00,$06/v$01,$05/v$02,$04 before D5.");
        var left=cave.Actors.Single(a=>a.Actor.Record is {SubId:6,Var03:0});
        var right=cave.Actors.Single(a=>a.Actor.Record is {SubId:6,Var03:1});
        var pacing=cave.Actors.Single(a=>a.Actor.Record.SubId==4);
        FailIf(left.Actor.Position!=new Vector2(0x28,0x60) || right.Actor.Position!=new Vector2(0x48,0x60),
            "Room 5:c3 punching Gorons lost source coordinates.");
        Vector2 old=pacing.Actor.Position;
        Step(8);
        FailIf(pacing.Actor.Position.X>=old.X,
            "Goron $66:$04 must pace left initially.");
        TalkStationary(cave.Actors.Single(a=>a.Actor.Record is {SubId:5,Var03:2}),"This is the home");
        _player.WarpTo(new Vector2(0x78,0x98));
        StepGameplayUpdates(48,Vector2.Left,["move_left"],["move_left"],batched);
        StepGameplayUpdates(48,Vector2.Up,["move_up"],["move_up"],batched);
        Step(1);
        FailIf(!right.Actor.CanTalkTo(_player),
            $"Room 5:c3 worker unreachable through floor: Link {_player.Position}, Goron {right.Actor.Position}.");
        for(int repeat=0;repeat<2;repeat++)
        {
            StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
            for(int i=0;i<8&&!_dialogue.IsOpen;i++) Step(1);
            FailIf(!_dialogue.IsOpen || !_dialogue.CurrentMessage.Contains("Help! Hurry!"),
                $"Room 5:c3 right worker expected TX_247d, got '{_dialogue.CurrentMessage}'.");
            Observe();
            int frame=right.Actor.CurrentAnimationFrame;
            Step(12);
            FailIf(right.Actor.CurrentAnimationFrame!=frame,"Non-always-update Goron animated during text.");
            _dialogue.Close(); Step(30);
            FailIf(!cave.BlocksGameplay||right.Counter!=1,"Goron TX_247d wait 30 ended early.");
            Step(5);
            FailIf(cave.BlocksGameplay||_player.CutsceneControlled,
                "Goron $66:$06 conversation retained input after wait 30.");
        }
        _inventory.GiveTreasure(0x49,0);
        _player.WarpTo(new Vector2(0x78,0x98));
        StepGameplayUpdates(40,Vector2.Left,["move_left"],["move_left"],batched);
        for(int i=0;i<300&&!_dialogue.IsOpen;i++) Step(1);
        FailIf(!_dialogue.IsOpen||!_dialogue.ChoiceActive,
            $"Room 5:c3 Bomb Flower approach did not reach TX_247e (Link {_player.Position}, worker {right.Actor.Position}, command {right.CommandIndex}).");
        Observe();
        _dialogue.SubmitChoiceForValidation(1); Step(31);
        FailIf(!_dialogue.IsOpen||!_dialogue.ChoiceActive,
            "Goron Bomb Flower refusal must insist with TX_247f.");
        _dialogue.SubmitChoiceForValidation(0); Step(31);
        FailIf(!_dialogue.IsOpen,"Goron acceptance lost TX_2480.");
        _dialogue.Close();
        for(int i=0;i<1500&&!_dialogue.IsOpen;i++) Step(1);
        FailIf(!_dialogue.IsOpen,
            $"Room 5:c3 rescue did not reach elder dialogue; worker command {right.CommandIndex}.");
        for(int i=0;i<1000&&!_saveData.HasGlobalFlag(0x2f);i++)
        { if(_dialogue.IsOpen) _dialogue.Close(); Step(1); }
        FailIf(!_saveData.HasGlobalFlag(0x2f)||!_inventory.HasTreasure(0x43)||!_saveData.HasRoomFlag(5,0xc3,0x40),
            "Goron elder must award Crown Key $43 and persist global $2f / room $40.");
        Observe();
        if(_dialogue.IsOpen) _dialogue.Close(); Step(40);
        if(_dialogue.IsOpen) _dialogue.Close(); Step(4);
        FailIf(_player.CutsceneControlled,"Goron rescue did not release input.");
        LoadValidationRoom(5,0xc3); Step(4);
        FailIf(cave.Actors.Count(a=>a.Actor.Record.Id==0x8b&&a.Actor.Active)!=1,
            "Saved elder must respawn once on room 5:c3 re-entry.");
        TalkStationary(cave.Actors.Single(a=>a.Actor.Record is {SubId:5,Var03:2}),"full of energy");
        for(int y=3;y<=5;y++) for(int x=1;x<=5;x++)
            FailIf(_rooms.CurrentRoom.GetMetatile(new Vector2(x*16+8,y*16+8))!=(((x+y)&1)==0?0xa2:0xa1),
                $"Room 5:c3 persisted barrier tile ${y*16+x:x2} differs from source.");
        Observe();
        _inventory.GiveTreasure(TreasureDatabase.TreasureEssence,4);
        LoadValidationRoom(5,0xc3); Step(4);
        FailIf(cave.Actors.Count(a=>a.Actor.Active)!=2 || cave.Actors.Any(a=>a.Actor.Active &&
            a.Actor.Record is not {SubId:5,Var03:3 or 4}),
            "After D5 room 5:c3 must retain only $66:$05/v$03 and v$04.");
        TalkStationary(cave.Actors.Single(a=>a.Actor.Record is {SubId:5,Var03:3}),"traveling around");
        TalkStationary(cave.Actors.Single(a=>a.Actor.Record is {SubId:5,Var03:4}),"full of energy");
        Observe();
        cave.Cancel(); cave.Cancel();
        FailIf(_player.CutsceneControlled||cave.HasState,"Goron room cancellation retained state/input.");
        return string.Join("\n",observations);
    }
}
