using Godot;
using System.Linq;
using System.Collections.Generic;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateGoronDanceScrollEntry()
    {
        var results = new List<string>();
        foreach (bool batched in new[] { false, true })
        {
            ReinitializeGameplayForValidation();
            _player.ApplicationUpdateOwned = true;
            var observations = new List<string>();
            for (int repeat = 0; repeat < 2; repeat++)
            {
                LoadValidationRoom(2, 0xfd);
                StepGameplayUpdates(1, Vector2.Zero);
                // The open north passage in group2Mapfd leads to group2Maped.
                _player.WarpTo(new Vector2(0x58, 0x18));
                FailIf(_rooms.CurrentRoom.GetTerrainInfo(_player.Position).Collision != 0,
                    "Room 2:fd dance approach is not floor.");
                var outgoing = _entities.Entities<NpcCharacter>().Where(n => n.Record.Id == 0x66 && n.Active).ToArray();
                for (int i = 0; i < 40 && !IsTransitioning; i++)
                    StepGameplayUpdates(1, Vector2.Up, ["move_up"], ["move_up"]);
                FailIf(!IsTransitioning || _rooms.CurrentRoom.Id != 0xed,
                    "Room 2:fd north passage failed to scroll to 2:ed.");
                var incoming = _entities.Entities<NpcCharacter>().Where(n => n.Record.Id == 0x66).ToArray();
                FailIf(_player.FacingVector != Vector2I.Up || outgoing.Any(n => !n.Active || !n.Visible) ||
                    incoming.Length != 8 || incoming.Any(n => !n.Active || !n.Visible),
                    $"Goron scroll 2:fd -> 2:ed: facing {_player.FacingVector}; " +
                    $"outgoing {outgoing.Count(n => n.Active && n.Visible)}/{outgoing.Length}; " +
                    $"incoming {incoming.Count(n => n.Active && n.Visible)}/{incoming.Length} (expected 8).");
                string Snapshot() => string.Join(";", incoming.Select(n =>
                    $"{n.Position}:{n.CurrentAnimationFrame}:{n.CurrentScriptAnimationSource}"));
                string frozen = Snapshot();
                // After setup and some scrolling, the lower dancer row (source
                // yh=$68) must already be inside the viewport before completion.
                for (int tick = 0; tick < 16; tick += batched ? 4 : 1)
                    StepGameplayUpdates(batched ? 4 : 1, Vector2.Zero, batched: batched);
                observations.Add($"{_player.PrecisePosition}:{incoming[0].TransitionDrawOffset}:{Snapshot()}");
                FailIf(incoming.Any(n => n.TransitionDrawOffset != incoming[0].TransitionDrawOffset) ||
                    incoming[0].TransitionDrawOffset.Y is <= -128 or >= 0,
                    "Room 2:ed dancers do not share the incoming room's scroll offset.");
                foreach (var dancer in incoming.Where(n => n.Record.SubId == 1 && n.Position.Y == 0x68))
                {
                    Vector2 screen = _transitions.WorldToGameplayScreen(dancer.Position) +
                        dancer.TransitionDrawOffset + dancer.SourceOamWrapOffset;
                    FailIf(screen.Y is < 0 or >= 128 || !dancer.IsVisibleInTree(),
                        $"Room 2:ed dancer ${dancer.Record.Var03:x2} is not presented during scrolling: {screen}.");
                }
                for (int i = 0; i < 100 && IsTransitioning; i++)
                {
                    FailIf(outgoing.Any(n => !n.Active || !n.Visible) || incoming.Any(n => !n.Active || !n.Visible) ||
                        Snapshot() != frozen || _player.FacingVector != Vector2I.Up,
                        "Goron scroll changed frozen actors or Link's facing before completion.");
                    StepGameplayUpdates(1, Vector2.Zero);
                }
                FailIf(IsTransitioning || _entities.OutgoingEntities<NpcCharacter>().Count != 0 ||
                    Snapshot() != frozen || incoming.Any(n => n.TransitionDrawOffset != Vector2.Zero),
                    "Goron scroll failed to retire outgoing actors and retain destination poses at completion.");
                StepGameplayUpdates(4, Vector2.Zero, batched: batched);
                FailIf(incoming.Any(n => !n.Active || !n.Visible), "Room 2:ed lost dancers after scroll completion.");
                observations.Add($"{_player.PrecisePosition}:{Snapshot()}");
                _roomEvents.Get<GoronCaveEvent>().Cancel();
                FailIf(incoming.Any(n => n.Active || n.Visible),
                    "Explicit Goron event cancellation retained destination actors.");
            }
            results.Add(string.Join("\n", observations));
        }
        FailIf(results[0] != results[1], "Goron scroll differs between individual and batched gameplay updates.");
    }

    private void ValidateRoom5c3GoronEntry()
    {
        foreach(int progress in new[]{0,1,2})
        {
            string single=RunGoronEntry(progress,false);
            ReinitializeGameplayForValidation();
            string batched=RunGoronEntry(progress,true);
            FailIf(single!=batched,$"Room $5:c3 entry differs across host batching at progress {progress}.");
            ReinitializeGameplayForValidation();
        }
    }
    private string RunGoronEntry(int progress,bool batched)
    {
        _player.ApplicationUpdateOwned = true;
        _saveData.SetGlobalFlag(0x2f,progress!=0);
        _saveData.SetRoomFlag(5,0xc3,0x40,progress!=0);
        if(progress==2) _inventory.GiveTreasure(TreasureDatabase.TreasureEssence,4);
        var observations=new List<string>();
        for(int repeat=0;repeat<2;repeat++)
        {
            LoadValidationRoom(1,0x28);
            var room=_rooms.CurrentRoom;
            var warps=new WarpDatabase();
            Vector2 entrance=Vector2.Zero;
            for(int y=0;y<room.Height;y+=16)
            for(int x=0;x<room.Width;x+=16)
            {
                var position=new Vector2(x+8,y+8);
                if(warps.TryGetTileWarp(1,room,room.GetPackedPosition(position),room.GetMetatile(position),out var warp)&&
                    warp.DestinationGroup==5&&warp.DestinationRoom==0xc3)
                    entrance=position;
            }
            FailIf(entrance==Vector2.Zero,"Room $1:28 lacks its imported cave entrance to $5:c3.");
            Vector2 approach=entrance+Vector2.Down*20;
            FailIf(room.GetTerrainInfo(approach).Collision!=0,$"Goron cave approach {approach} is not floor.");
            _player.WarpTo(approach);
            for(int i=0;i<40&&!IsTransitioning;i++)
                StepGameplayUpdates(1,Vector2.Up,["move_up"],["move_up"]);
            FailIf(!IsTransitioning,"Walking into the Goron cave failed to begin the $1:28 -> $5:c3 warp.");
            for(int i=0;i<150&&_rooms.ActiveGroup!=5;i++) StepGameplayUpdates(1,Vector2.Zero);
            FailIf(_rooms.ActiveGroup!=5||_rooms.CurrentRoom.Id!=0xc3||!IsTransitioning,
                "Goron cave regression missed the destination-entry transition.");
            // initializeRoom parses objects; state-zero interaction dispatch
            // belongs to the next updateAllObjects, after warp selection.
            StepGameplayUpdates(1,Vector2.Zero);
            var cave=_roomEvents.Get<GoronCaveEvent>();
            string Snapshot()=>string.Join(";",cave.Actors.Where(a=>a.Actor.Active).Select(a=>
                $"{a.Actor.Record.Id:x2}:{a.Actor.Record.SubId:x2}:{a.Actor.Record.Var03:x2}:{a.Actor.Position}:{a.Actor.CurrentScriptAnimationSource}:{a.Actor.CurrentAnimationFrame}:{a.CommandIndex}:{a.Counter}:{a.MovementCounter}"));
            var active=cave.Actors.Where(a=>a.Actor.Active).ToArray();
            // goron_subid05Script_A/B and subid04/06 delete opposite D5
            // populations on their initial dispatch. The distant $05 actors
            // select goron_beginNappingLoop animation $04 before appearing.
            FailIf(active.Length!=(progress==0?4:progress==1?5:2)||
                active.Any(a=>a.Actor.Record.Id==0x66&&
                    (progress==2?a.Actor.Record is not {SubId:5,Var03:3 or 4}:a.Actor.Record is {SubId:5,Var03:3 or 4})),
                $"Room $5:c3 exposed the wrong Goron population during entry, progress {progress}.");
            FailIf(active.Where(a=>a.Actor.Record is {Id:0x66,SubId:5}).Any(a=>
                a.Actor.CurrentScriptAnimationSource!=cave.Database.Animation(0x66,4)),
                "Room $5:c3 exposed standing sprites before the initial Goron nap animation $04.");
            string frozen=Snapshot();
            StepGameplayUpdates(4,Vector2.Zero,batched:batched);
            FailIf(Snapshot()!=frozen,"Goron scripts or poses advanced during the cave entry transition.");
            for(int i=0;i<150&&IsTransitioning;i++)
            {
                FailIf(Snapshot()!=frozen,"Goron scripts advanced before the cave entrance walk completed.");
                StepGameplayUpdates(1,Vector2.Zero);
            }
            FailIf(IsTransitioning,"Goron cave entrance walk did not finish.");
            StepGameplayUpdates(4,Vector2.Zero,batched:batched);
            observations.Add(Snapshot());
        }
        return string.Join("\n",observations);
    }

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
