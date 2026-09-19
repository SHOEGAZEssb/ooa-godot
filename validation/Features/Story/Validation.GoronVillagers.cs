using Godot;
using System;
using System.Linq;

namespace oracleofages;

public sealed partial class ValidationRoot
{
    private void ValidateGoronVillagers()
    {
        // Independent expectations from scriptHelper.s:goron_determineTextForGenericNpc.
        int[][] expected = [
            [0xff,0x1e,0x1f,0x20, 0xff,0xff,0x21,0x22, 0xff,0xff,0x23,0x23, 0xff,0xff,0x24,0x24,
             0xff,0x25,0x26,0, 0xff,0x27,0xff,0, 0xff,0xff,0x17,0x18, 0xff,0xff,0x19,0x19],
            [0xff,0x10,0x11,0x12, 0xff,0x13,0x14,0xff, 0xff,0x15,0x15,0x16, 0x1a,0x1a,0x1b,0, 0xff,0x1c,0x1d,0],
            [0,0xff,0xff,0xff, 0xff,1,1,1, 0xff,2,2,2, 0xff,0xff,3,4, 0xff,0xff,5,5,
             0xff,0xff,6,6, 0xff,0xff,7,8, 9,10,10,0, 0xff,11,11,0, 0xff,12,13,0, 0xff,14,15,0]
        ];
        var records=new NpcDatabase().AllRecords.Where(n=>n.Id==0x66&&n.SubId is >=0x0c and <=0x0e).ToArray();
        FailIf(records.Length!=24,"Expected 24 placed Goron $0c/$0d/$0e records.");
        for(int state=0;state<4;state++)
        {
            ReinitializeGameplayForValidation();
            _saveData.SetLinkedGame(true);
            if(state>=1) { _inventory.GiveTreasure(TreasureDatabase.TreasureEssence,3); _saveData.SetGlobalFlag(0x2f); }
            if(state>=2) _saveData.SetGlobalFlag(0x1a);
            if(state==3) _saveData.SetGlobalFlag(0x14);
            foreach(var record in records)
            {
                LoadValidationRoom(record.Group,record.Room);
                StepGameplayUpdates(4,Vector2.Zero);
                var host=_roomEvents.Get<GoronCaveEvent>().Actors.Single(a=>a.Actor.Record.Id==0x66&&
                    a.Actor.Record.SubId==record.SubId&&a.Actor.Record.Var03==record.Var03);
                bool past=(_rooms.CurrentRoom.TilesetFlags&0x80)!=0;
                int phase=past ? (state==3?2:state>0?1:0) : state;
                int low=expected[record.SubId-0x0c][record.Var03*4+phase];
                FailIf(host.Actor.Active!=(low!=0xff)||host.LoadedTextId!=(0x3100|low),
                    $"Goron {record.Group}:{record.Room:x2} ${record.SubId:x2}/v${record.Var03:x2} phase {phase} expected TX_31{low:x2}.");
                if(host.Actor.Active) TalkGoronFromFloor(host);
            }
        }
        // TX_3127 is suppressed in an unlinked game even when its table entry is present.
        ReinitializeGameplayForValidation();
        _saveData.SetGlobalFlag(0x2f);
        LoadValidationRoom(2,0xff); StepGameplayUpdates(4,Vector2.Zero);
        FailIf(_roomEvents.Get<GoronCaveEvent>().Actors.Any(a=>a.Actor.Record is {SubId:0x0c,Var03:5}&&a.Actor.Active),
            "Unlinked TX_3127 Goron must delete itself.");
    }

    private void TalkGoronFromFloor(GoronCaveScriptHost host)
    {
        for(int repeat=0;repeat<2;repeat++)
        {
        ApproachGoronFromFloor(host);
        StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
        for(int i=0;i<8&&!_dialogue.IsOpen;i++) StepGameplayUpdates(1,Vector2.Zero);
        FailIf(!_dialogue.IsOpen,"Goron did not open its dialogue through gameplay A-button routing.");
        _dialogue.Close(); StepGameplayUpdates(35,Vector2.Zero);
        }
    }

    private void ApproachGoronFromFloor(GoronCaveScriptHost host) => ApproachGoronActorFromFloor(host.Actor);

    private void ApproachGoronActorFromFloor(NpcCharacter actor)
    {
        bool reached=false;
        foreach(int distance in new[]{32,24,20})
        {
        foreach(var direction in new[]{Vector2.Up,Vector2.Down,Vector2.Left,Vector2.Right})
        {
            Vector2 start=actor.Position-direction*distance;
            if(start.X<8||start.Y<8||start.X>=_rooms.CurrentRoom.WidthInTiles*16-8||
                start.Y>=_rooms.CurrentRoom.HeightInTiles*16-8||_rooms.CurrentRoom.GetTerrainInfo(start).Collision!=0) continue;
            string action=direction==Vector2.Up?"move_up":direction==Vector2.Down?"move_down":direction==Vector2.Left?"move_left":"move_right";
            _player.WarpTo(start);
            for(int tick=0;tick<100&&!actor.CanTalkTo(_player);tick++)
            {
                Vector2 delta=actor.Position-_player.Position;
                Vector2 walk=Math.Abs(delta.X)>Math.Abs(delta.Y)?new Vector2(Math.Sign(delta.X),0):new Vector2(0,Math.Sign(delta.Y));
                action=walk==Vector2.Up?"move_up":walk==Vector2.Down?"move_down":walk==Vector2.Left?"move_left":"move_right";
                StepGameplayUpdates(1,walk,[action],[action]);
            }
            if(actor.CanTalkTo(_player)) { reached=true; break; }
        }
        if(reached) break;
        }
        FailIf(!reached,$"Goron {actor.Record.Group}:{actor.Record.Room:x2} ${actor.Record.SubId:x2}/v${actor.Record.Var03:x2} unreachable through floor: actor {actor.Position}, Link {_player.Position}, active {actor.Active}, dying {_player.IsDying}.");
    }

    private void ValidateGoronTrades()
    {
        // Before accepting any trade, the guards must preserve the solid staircase.
        foreach(var room in new[]{0xfd,0xff})
        {
            LoadValidationRoom(2,room); StepGameplayUpdates(4,Vector2.Zero);
            var guard=_roomEvents.Get<GoronCaveEvent>().Actors.Single(a=>a.Actor.Record.SubId==8);
            FailIf(!guard.Actor.Active||_saveData.HasRoomFlag(2,room,0x80),$"Goron guard 2:{room:x2} moved without Brother's Emblem.");
            bool past=(_rooms.CurrentRoom.TilesetFlags&0x80)!=0;
            _inventory.GiveTreasure(0x5b,0); _inventory.GiveTreasure(past?0x5c:0x5e,0);
            Vector2 start=guard.Actor.Position;
            ApproachGoronFromFloor(guard); StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
            AdvanceGoronDialogue(400,1);
            FailIf(!_saveData.HasRoomFlag(2,room,0x80)||_saveData.HasRoomFlag(2,room,0x40)||
                guard.Actor.Position!=start+Vector2.Left*16||_entities.RuntimeState.ReadWramByte(0xcfc0)!=1,
                $"Guard 2:{room:x2} did not move 32 SPEED_80 updates and retain the rejected-trade signal.");
            ApproachGoronFromFloor(guard); StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
            AdvanceGoronDialogue(350);
            FailIf(!_saveData.HasRoomFlag(2,room,0x40)||_inventory.HasTreasure(past?0x5c:0x5e)||!_inventory.HasTreasure(past?0x5d:0x5c),
                $"Guard 2:{room:x2} did not exchange its era-specific quest item.");
            TalkGoronFromFloor(guard);
            _inventory.LoseTreasure(0x5b);
        }
        LoadValidationRoom(3,0x1f); StepGameplayUpdates(4,Vector2.Zero);
        var letter=_roomEvents.Get<GoronCaveEvent>().Actors.Single();
        _inventory.GiveTreasure(0x45,0); _inventory.GiveTreasure(0x5a,0);
        ApproachGoronFromFloor(letter); StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
        AdvanceGoronDialogue(450);
        FailIf(!_inventory.HasTreasure(0x59)||_inventory.HasTreasure(0x5a)||!_saveData.HasRoomFlag(3,0x1f,0x40),
            "Introduction-letter Goron did not consume Lava Juice and preserve the Old Mermaid Key.");
        TalkGoronFromFloor(letter);
        ReinitializeGameplayForValidation();
        LoadValidationRoom(2,0xf7); StepGameplayUpdates(4,Vector2.Zero);
        var digger=_roomEvents.Get<GoronCaveEvent>().Actors.Single();
        _inventory.GiveTreasure(0x19,1); _inventory.GiveTreasure(0x20,0x30); _inventory.GiveTreasure(3,0x30);
        _inventory.ApplyFairyBombCapacityUpgrade(0x30);
        ApproachGoronFromFloor(digger); StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
        AdvanceGoronDialogue(180);
        FailIf(!_saveData.HasRoomFlag(2,0xf7,0x40)||_inventory.Bombs!=0x10||_inventory.EmberSeeds!=0,
            $"Wall Goron did not take exactly 20 bombs and Ember seeds: {_inventory.Bombs:x2}/{_inventory.EmberSeeds:x2}.");
        _entities.RuntimeState.SetWramByte(0xcc4d,1);
        LoadValidationRoom(2,0xf7); StepGameplayUpdates(5,Vector2.Zero);
        digger=_roomEvents.Get<GoronCaveEvent>().Actors.Single();
        FailIf(!_saveData.HasRoomFlag(2,0xf7,0x80)||digger.Actor.Position!=new Vector2(0x58,0x38),
            "Seed-tree refill did not complete the wall Goron's room flag and position.");
        ApproachGoronFromFloor(digger); StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
        AdvanceGoronDialogue(160);
        FailIf(digger.Actor.Position!=new Vector2(0x68,0x28)||_entities.RuntimeState.ReadWramByte(0xcfc0)!=1,
            $"Choosing the left chest did not move the Goron up then right by 16 pixels each: {digger.Actor.Position}, signal {_entities.RuntimeState.ReadWramByte(0xcfc0):x2}.");
        TalkGoronFromFloor(digger);
        _inventory.GiveTreasure(TreasureDatabase.TreasureEssence,4);
        LoadValidationRoom(5,0xde); StepGameplayUpdates(4,Vector2.Zero);
        var elder=_roomEvents.Get<GoronCaveEvent>().Actors.Single(a=>a.Actor.Record.Id==0x8b);
        TalkGoronFromFloor(elder);
        _saveData.SetGlobalFlag(0x14);
        LoadValidationRoom(5,0xde); StepGameplayUpdates(4,Vector2.Zero);
        FailIf(_roomEvents.Get<GoronCaveEvent>().Actors.Any(a=>a.Actor.Record.Id==0x8b&&a.Actor.Active),
            "Wandering Goron Elder ignored finished-game deletion.");
    }
}
