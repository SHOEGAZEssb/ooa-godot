using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownButtons()
    {
        foreach(bool batch in new[]{false,true})
        {
            void Step(int count=1,Vector2 movement=default) =>
                StepGameplayUpdates(count, movement, [], [], batched: batch);
            LoadValidationRoom(4,0xbc);
            _player.WarpTo(new(72,88));
            var button=_entities.Entities<GroundButtonRoomEntity>().Single(b=>b.PackedPosition==0x34);
            FailIf(_currentRoom.IsSolid(_player.Position),"Crown button approach must start on floor.");
            for(int repeat=0;repeat<2;repeat++)
            {
                for(int i=0;!button.Pressed && i<40;i++)
                {
                    Step(movement:Vector2.Up);
                    FailIf(_currentRoom.IsSolid(_player.Position),"Button approach crossed solid room geometry.");
                }
                FailIf(!button.Pressed || (_entities.ActiveTriggers&1)==0 || _currentRoom.Layout[0x34]!=0x0d,
                    "Actual Link movement must press reusable Crown button$34.");
                for(int i=0;button.Pressed && i<40;i++) Step(movement:Vector2.Down);
                FailIf(button.Pressed || (_entities.ActiveTriggers&1)!=0 || _currentRoom.Layout[0x34]!=0x0c,
                    "Walking off a Link-held button must release it without the object delay.");
            }
            LoadValidationRoom(4,0xbc);
            _player.WarpTo(new(24,24));
            button=_entities.Entities<GroundButtonRoomEntity>().Single(b=>b.PackedPosition==0x34);
            // Isolate the original object-pressure branch without claiming a push route.
            _currentRoom.SetPositionTileAndCollision(button.Position,0x2a,null,0);
            Step();
            FailIf(!button.Pressed || button.ReleaseCounter!=28 || (_entities.ActiveTriggers&1)==0 ||
                _currentRoom.Layout[0x34]!=0x2a || _currentRoom.GetUnderlyingMetatile(button.Position)!=0x0d,
                "PART$09 state0 must fall through, press under the object and write$0d only to the underlying buffer.");
            Step(5);
            FailIf(button.ReleaseCounter!=28,"Object occupancy must hold the release counter without decrementing.");
            _currentRoom.SetPositionTileAndCollision(button.Position,_currentRoom.GetUnderlyingMetatile(button.Position),null,0);
            Step(27);
            FailIf(!button.Pressed || button.ReleaseCounter!=1 || _currentRoom.Layout[0x34]!=0x0d,
                "Uncovered reusable button must retain its trigger and pressed tile through update27.");
            Step();
            FailIf(button.Pressed || button.ReleaseCounter!=0 || (_entities.ActiveTriggers&1)!=0 || _currentRoom.Layout[0x34]!=0x0c,
                "Uncovered button must release on update28.");

            LoadValidationRoom(4,0xbc);
            _player.WarpTo(new(72,88));
            button=_entities.Entities<GroundButtonRoomEntity>().Single(b=>b.PackedPosition==0x34);
            for(int i=0;!button.Pressed && i<40;i++) Step(movement:Vector2.Up);
            FailIf(!button.Pressed || _currentRoom.IsSolid(_player.Position),"Preload contact fixture must approach its button through floor.");
            // Reparse the same isolated room with Link still overlapping its button.
            _entities.BeginScreenTransition(4,_currentRoom,new(240,0),_player);
            button=_entities.Entities<GroundButtonRoomEntity>().Single(b=>b.PackedPosition==0x34);
            FailIf(!button.Pressed || button.UpdatesDuringDialogue || button.Visible || (_entities.ActiveTriggers&1)==0,
                "Scrolling state0 must fall through into live Link pressure, then finish initialization.");
            _player.WarpTo(new(24,24));
            Step(8);
            FailIf(!button.Pressed || (_entities.ActiveTriggers&1)==0,
                "Initialized incoming buttons must not release during scrolling even after Link moves away.");
            _entities.FinishScreenTransition();
            Step();
            FailIf(button.Pressed || (_entities.ActiveTriggers&1)!=0,
                "The first post-scroll update must resume pressure handling without repeating state0.");
            LoadValidationRoom(0,0x60);
        }
        LoadValidationRoom(4,0xbc);
        var data = new DungeonMechanicDatabase();
        var record = data.GetRoomRecords(4,0xbc).Single(r=>r.Id==0x09 && r.SubId==0x80);
        // Arithmetic-only contact fixtures, not movement/reachability claims.
        // Expectations come from byte SUB/ADD/SLA/CP in checkObjectsCollidedFromVariables.
        foreach(var test in new (Vector2 Offset,bool Hit)[] {
            (new(-8,0),true),(new(-9,0),false),(new(7,0),true),(new(8,0),false),
            (new(0,-8),true),(new(0,-9),false),(new(0,7),true),(new(0,8),false),
            (new(-8,-8),true),(new(-8,8),false),
            (new(-8.25f,0),false),(new(-7.75f,0),true),(new(7.99f,0),true),(new(8.01f,0),false),
            (new(248,0),true),(new(0,248),true),(new(-264,0),true),(new(256,256),true)
        })
        {
            bool trigger=false; int writes=0; int sounds=0;
            var button = new GroundButtonRoomEntity(record,_currentRoom,data,(_,value)=>trigger=value,
                (_,_)=>writes++,_=>sounds++);
            _player.WarpTo(button.Position+test.Offset);
            button.UpdateFrame(new RoomEntityFrame(_player,0,false),new List<RoomEntitySpawn>());
            FailIf(button.Pressed!=test.Hit || trigger!=test.Hit || writes!=(test.Hit?1:0) || sounds!=(test.Hit?1:0),
                $"PART$09 byte overlap at relative {test.Offset} must be {test.Hit}, with one press write/sound only on contact.");
            button.Free();
        }
        LoadValidationRoom(0,0x60);
    }
}
