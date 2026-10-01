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
            if(state>=1) { _inventory.GiveTreasure(TreasureId.Essence,3); _saveData.SetGlobalFlag(GlobalFlag.SavedGoronElder); }
            if(state>=2) _saveData.SetGlobalFlag(GlobalFlag.MoblinsKeepDestroyed);
            if(state==3) _saveData.SetGlobalFlag(GlobalFlag.FinishedGame);
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
        _saveData.SetGlobalFlag(GlobalFlag.SavedGoronElder);
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
        // Some scripts open a follow-up textbox after the first one closes
        // (the Big Bang attendant's missing-Goronade branch does this).
        // Finish that exchange before trying to walk up for another talk;
        // directional input cannot dismiss continuation text.
        _dialogue.Close(); AdvanceGoronDialogue(35);
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
        ValidateGoronWallLayout();
        // Before accepting any trade, the guards must preserve the solid staircase.
        foreach(var room in new[]{0xfd,0xff})
        {
            LoadValidationRoom(2,room); StepGameplayUpdates(4,Vector2.Zero);
            var guard=_roomEvents.Get<GoronCaveEvent>().Actors.Single(a=>a.Actor.Record.SubId==8);
            FailIf(!guard.Actor.Active||_saveData.HasRoomFlag(2,room,0x80),$"Goron guard 2:{room:x2} moved without Brother's Emblem.");
            bool past=(_rooms.CurrentRoom.TilesetFlags&0x80)!=0;
            _inventory.GiveTreasure(TreasureId.BrotherEmblem,0); _inventory.GiveTreasure(past?TreasureId.GoronVase:TreasureId.RockBrisket,0);
            Vector2 start=guard.Actor.Position;
            ApproachGoronFromFloor(guard); StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
            AdvanceGoronDialogue(400,1);
            FailIf(!_saveData.HasRoomFlag(2,room,0x80)||_saveData.HasRoomFlag(2,room,0x40)||
                guard.Actor.Position!=start+Vector2.Left*16||_entities.RuntimeState.ReadWramByte(WramAddress.wTmpcfc0)!=1,
                $"Guard 2:{room:x2} did not move 32 SPEED_80 updates and retain the rejected-trade signal.");
            ApproachGoronFromFloor(guard); StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
            AdvanceGoronDialogue(350);
            FailIf(!_saveData.HasRoomFlag(2,room,0x40)||_inventory.HasTreasure(past?TreasureId.GoronVase:TreasureId.RockBrisket)||!_inventory.HasTreasure(past?TreasureId.Goronade:TreasureId.GoronVase),
                $"Guard 2:{room:x2} did not exchange its era-specific quest item.");
            TalkGoronFromFloor(guard);
            _inventory.LoseTreasure(TreasureId.BrotherEmblem);
        }
        LoadValidationRoom(3,0x1f); StepGameplayUpdates(4,Vector2.Zero);
        var letter=_roomEvents.Get<GoronCaveEvent>().Actors.Single();
        _inventory.GiveTreasure(TreasureId.OldMermaidKey,0); _inventory.GiveTreasure(TreasureId.LavaJuice,0);
        ApproachGoronFromFloor(letter); StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
        AdvanceGoronDialogue(450);
        FailIf(!_inventory.HasTreasure(TreasureId.GoronLetter)||_inventory.HasTreasure(TreasureId.LavaJuice)||!_saveData.HasRoomFlag(3,0x1f,0x40),
            "Introduction-letter Goron did not consume Lava Juice and preserve the Old Mermaid Key.");
        TalkGoronFromFloor(letter);
        ReinitializeGameplayForValidation();
        LoadValidationRoom(2,0xf7); StepGameplayUpdates(4,Vector2.Zero);
        var digger=_roomEvents.Get<GoronCaveEvent>().Actors.Single();
        FailIf(_currentRoom.GetMetatile(new Vector2(0x48,0x18))!=0xa7 ||
            _currentRoom.GetMetatile(new Vector2(0x68,0x18))!=0xa7,
            "Digging Goron 2:f7 exposed either chest before receiving bombs and seeds.");
        _inventory.GiveTreasure(TreasureId.SeedSatchel,1); _inventory.GiveTreasure(TreasureId.EmberSeeds,0x30); _inventory.GiveTreasure(TreasureId.Bombs,0x30);
        _inventory.ApplyFairyBombCapacityUpgrade(0x30);
        ApproachGoronFromFloor(digger); StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
        AdvanceGoronDialogue(180);
        FailIf(!_saveData.HasRoomFlag(2,0xf7,0x40)||_inventory.Bombs!=0x10||_inventory.EmberSeeds!=0,
            $"Wall Goron did not take exactly 20 bombs and Ember seeds: {_inventory.Bombs:x2}/{_inventory.EmberSeeds:x2}.");
        TalkGoronFromFloor(digger);
        FailIf(_currentRoom.GetMetatile(new Vector2(0x48,0x18))!=0xa7,
            "Paying or repeating the Goron's dialogue revealed the wall in the same visit.");
        LoadValidationRoom(2,0xf7); StepGameplayUpdates(5,Vector2.Zero);
        FailIf(_saveData.HasRoomFlag(2,0xf7,0x80)||_currentRoom.GetMetatile(new Vector2(0x68,0x18))!=0xa7,
            "Paid Goron 2:f7 revealed its chests on re-entry before refill progress.");
        _entities.RuntimeState.SetWramByte(WramAddress.wSeedTreeRefilledBitset,1);
        LoadValidationRoom(2,0xf7); StepGameplayUpdates(5,Vector2.Zero);
        digger=_roomEvents.Get<GoronCaveEvent>().Actors.Single();
        FailIf(!_saveData.HasRoomFlag(2,0xf7,0x80)||digger.Actor.Position!=new Vector2(0x58,0x38),
            "Seed-tree refill did not complete the wall Goron's room flag and position.");
        FailIf(_currentRoom.GetMetatile(new Vector2(0x48,0x18))!=0xf1 ||
            _currentRoom.GetMetatile(new Vector2(0x68,0x18))!=0xf1,
            "Completed Goron 2:f7 did not reveal both source chests at $14/$16.");
        ApproachGoronFromFloor(digger); StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
        AdvanceGoronDialogue(160);
        FailIf(digger.Actor.Position!=new Vector2(0x68,0x28)||_entities.RuntimeState.ReadWramByte(WramAddress.wTmpcfc0)!=1,
            $"Choosing the left chest did not move the Goron up then right by 16 pixels each: {digger.Actor.Position}, signal {_entities.RuntimeState.ReadWramByte(WramAddress.wTmpcfc0):x2}.");
        TalkGoronFromFloor(digger);
        // Approach the left chest from real floor after the Goron moves aside.
        Vector2 chestApproach=new(0x48,0x28);
        FailIf(_currentRoom.IsSolid(chestApproach),
            "Goron 2:f7 left chest's southern approach must be real floor.");
        _player.WarpTo(chestApproach);
        StepGameplayUpdates(12,Vector2.Up,["move_up"],["move_up"]);
        StepGameplayUpdates(1,Vector2.Zero,["attack"],["attack"]);
        AdvanceGoronDialogue(120);
        FailIf(!_saveData.HasRoomFlag(2,0xf7,0x20),
            $"Revealed Goron 2:f7 chest could not be opened from its southern floor approach: Link {_player.Position}, facing {_player.FacingVector}, dialogue {_dialogue.IsOpen}.");
        _entities.RuntimeState.SetWramByte(WramAddress.wSeedTreeRefilledBitset,0);
        LoadValidationRoom(2,0xf7); StepGameplayUpdates(5,Vector2.Zero);
        FailIf(_currentRoom.GetMetatile(new Vector2(0x48,0x18))!=0xf0 ||
            _currentRoom.GetMetatile(new Vector2(0x68,0x18))!=0xf0,
            "Collecting one Goron 2:f7 chest did not reopen both chests on re-entry.");
        _inventory.GiveTreasure(TreasureId.Essence,4);
        LoadValidationRoom(5,0xde); StepGameplayUpdates(4,Vector2.Zero);
        var elder=_roomEvents.Get<GoronCaveEvent>().Actors.Single(a=>a.Actor.Record.Id==0x8b);
        TalkGoronFromFloor(elder);
        _saveData.SetGlobalFlag(GlobalFlag.FinishedGame);
        LoadValidationRoom(5,0xde); StepGameplayUpdates(4,Vector2.Zero);
        FailIf(_roomEvents.Get<GoronCaveEvent>().Actors.Any(a=>a.Actor.Record.Id==0x8b&&a.Actor.Active),
            "Wandering Goron Elder ignored finished-game deletion.");
    }

    private void ValidateGoronWallLayout()
    {
        // Independent literals from roomSpecificTileChanges.s:
        // tileReplacement_group2Mapf7, including its ITEM-first branch and
        // @wallInsertion. ROOMFLAG $80 alone does not suppress the wall.
        byte[] wall = [0xb9,0xa7,0xa7,0xa7,0xb8,
            0xb1,0xa7,0xa7,0xa7,0xb3, 0xb1,0xa7,0xa7,0xa7,0xb3,
            0xb6,0xb0,0xb0,0xb0,0xb7];
        foreach(int flags in new[]{0,0x40,0x80,0xc0,0x20,0x60,0xa0,0xe0})
        foreach(int refill in new[]{0,1,2,3})
        {
            foreach(byte mask in new byte[]{0x20,0x40,0x80})
                _saveData.SetRoomFlag(2,0xf7,mask,(flags&mask)!=0);
            _runtimeState.SetWramByte(WramAddress.wSeedTreeRefilledBitset,(byte)refill);
            // Room preparation must resolve the layout before the Goron's
            // first eligible update; it must not write his completion flag.
            OracleRoomData room=_rooms.GetRoom(2,0xf7);
            bool insert=(flags&0x20)==0&&(flags&0xc0)!=0xc0&&
                ((flags&0x40)==0||(refill&1)==0);
            for(int y=0;y<8;y++)
            for(int x=0;x<10;x++)
            {
                int position=(y<<4)|x;
                Vector2 point=new(x*16+8,y*16+8);
                byte expected=insert&&y<4&&x is >=3 and <=7 ? wall[y*5+x-3] :
                    (flags&0x20)!=0&&position is 0x14 or 0x16 ? (byte)0xf0 :
                    room.GetOriginalMetatile(point);
                FailIf(room.GetMetatile(point)!=expected,
                    $"Goron 2:f7 flags ${flags:x2}/refill ${refill:x2}, tile ${position:x2}: expected ${expected:x2}, got ${room.GetMetatile(point):x2}.");
            }
            FailIf((_saveData.GetRoomFlags(2,0xf7)&0xe0)!=flags ||
                _runtimeState.ReadWramByte(WramAddress.wSeedTreeRefilledBitset)!=refill,
                "Preparing Goron 2:f7 mutated progress flags or the shared refill bit.");
        }
        foreach(bool batch in new[]{false,true})
        foreach(bool paid in new[]{false,true})
        {
            ReinitializeGameplayForValidation();
            if(paid) _saveData.SetRoomFlag(2,0xf7,0x40);
            _runtimeState.SetWramByte(WramAddress.wSeedTreeRefilledBitset,1);
            LoadValidationRoom(2,0xf7);
            FailIf(_currentRoom.GetMetatile(new Vector2(0x48,0x18))!=(paid?0xf1:0xa7),
                "Goron 2:f7 prepared the wrong wall before the first interaction update.");
            if(batch) StepGameplayUpdates(5,Vector2.Zero);
            else for(int i=0;i<5;i++) StepGameplayUpdates(1,Vector2.Zero);
            FailIf(_saveData.HasRoomFlag(2,0xf7,0x80)!=paid ||
                (_runtimeState.ReadWramByte(WramAddress.wSeedTreeRefilledBitset)&1)!=(paid?1:0),
                "Goron 2:f7 changed completion/refill initialization between split and batched gameplay updates.");
            // Cancel by leaving, then revisit with the refill bit cleared.
            LoadValidationRoom(2,0xf6);
            _runtimeState.SetWramByte(WramAddress.wSeedTreeRefilledBitset,0);
            LoadValidationRoom(2,0xf7);
            StepGameplayUpdates(5,Vector2.Zero);
            FailIf(_currentRoom.GetMetatile(new Vector2(0x68,0x18))!=(paid?0xf1:0xa7),
                "Goron 2:f7 lost its completed wall layout after cancellation and revisit.");
        }
        ReinitializeGameplayForValidation();
    }
}
