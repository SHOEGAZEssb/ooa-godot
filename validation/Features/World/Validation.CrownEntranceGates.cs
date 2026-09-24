using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownEntranceGates()
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var death=typeof(Player).GetField("_deathPending",flags)!;
        var root=new Node(); AddChild(root);
        var save=OracleSaveData.CreateStandardGame();
        using var fixture=RoomEntityValidationFixture.ForRoot(root,new(){SaveData=save});
        var manager=fixture.Manager;
        var room=_world.LoadRoom(4,0xbb);
        int requests=0;
        manager.DungeonEntranceTriggered+=(id,message)=>
        {
            FailIf(id!=0x0205 || !message.Contains("Crown",StringComparison.Ordinal),
                "Crown entrance must dispatch TX_0205.");
            requests++;
        };
        void Arrange(Vector2 position)
        {
            requests=0;
            _player.WarpTo(position);
            manager.LoadRoom(4,room,EnemyPlacementContext.FromWarpDestination(0xff));
        }
        try
        {
            // dungeonStuff.s radius8 + Link radius6: [-14,+14), high bytes.
            foreach(var (x,expected) in new[]{(105.99f,0),(106f,1),(133.99f,1),(134f,0)})
            {
                Arrange(new(x,136));
                manager.Update(1.0/60,_player);
                FailIf(requests!=expected,$"Crown entrance XY boundary differs at X={x}.");
            }
            // _checkCollidedWithLink: (Link.zh - object.zh +7)&255 <14.
            foreach(var (height,expected) in new[]{(-8,0),(-7,1),(6,1),(7,0)})
            {
                Arrange(new(120,136));
                _player.SetScriptedZHigh(unchecked((byte)height));
                manager.Update(1.0/60,_player);
                FailIf(requests!=expected,$"Crown entrance height gate differs at zh={height}.");
            }
            Arrange(new(120,136));
            death.SetValue(_player,true);
            manager.Update(1.0/60,_player);
            FailIf(requests!=0,"Crown entrance must not show text while wLinkDeathTrigger is set.");
            death.SetValue(_player,false);
            manager.Update(1.0/60,_player);
            manager.Update(2.0/60,_player);
            FailIf(requests!=1,"Clearing the death gate must allow one entrance title, with no repeat.");
            foreach(bool defeated in new[]{false,true})
            {
                save.SetRoomFlag(4,0xb4,OracleSaveData.RoomFlag80,defeated);
                _player.WarpTo(new(120,136));
                manager.LoadRoom(4,room);
                var freeze=new CrownEntranceFreeze();
                typeof(RoomEntityManager).GetMethod("AddEntity",flags)!.Invoke(manager,[freeze]);
                manager.Update(1.0/60,_player);
                var portals=manager.Entities<MinibossPortal>();
                FailIf(portals.Count!=(defeated?1:0) || defeated && !portals[0].Visible,
                    "Crown portal state0 must check Smasher's room flag even while interactions are disabled.");
                if(defeated)
                {
                    var portal=portals.Single();
                    int frame=portal.AnimationFrame;
                    manager.Update(8.0/60,_player);
                    FailIf(portal.AnimationFrame!=frame,
                        "An initialized Crown portal must stop updating while interactions are disabled.");
                    freeze.FreezesRoomEntities=false;
                    for(int i=0;portal.AnimationFrame==frame && i<32;i++) manager.Update(1.0/60,_player);
                    FailIf(portal.AnimationFrame==frame,
                        "The Crown portal must resume animation when the interaction freeze is released.");
                }
            }
        }
        finally
        {
            death.SetValue(_player,false);
            _player.SetScriptedZHigh(0);
            manager.Clear(); root.QueueFree();
        }

        // Approach the actual entrance trigger on its room floor, then repeat
        // the contact after closing the title. This is one room interaction.
        LoadValidationRoom(4,0xbb);
        _entities.LoadRoom(4,_currentRoom,EnemyPlacementContext.FromWarpDestination(0xff));
        _player.WarpTo(new(120,160));
        FailIf(_currentRoom.IsSolid(_player.Position),"Crown title approach must start on real floor.");
        for(int i=0;!_dialogue.IsOpen && i<40;i++) StepGameplayUpdates(1,Vector2.Up);
        FailIf(!_dialogue.IsOpen || !_dialogue.CurrentMessage.Contains("Crown",StringComparison.Ordinal) ||
            _saveData.RespawnGroup!=4 || _saveData.RespawnRoom!=0xbb,
            "Walking into Crown's entrance trigger must show its title and set the dungeon respawn.");
        _dialogue.Close();
        // Source rooms/ages/large/room04bb.bin places tile$ee at these
        // positions; statueEyeball.s scans descending and retains OAM$04.
        var eyes=_entities.Entities<StatueEyeball>();
        int[] eyeTiles=[0x7a,0x74,0x5a,0x54,0x3a,0x34];
        FailIf(eyes.Count!=6 || !eyes.Select(eye=>((int)eye.Position.Y/16)*16+(int)eye.Position.X/16).SequenceEqual(eyeTiles) ||
            eyes.Any(eye=>!eye.Initialized || !eye.Visible || eye.AnimationIndex!=4 || eye.PixelHash==0),
            "Crown's six entrance eyes must retain source tile order and fixed animation$04 presentation.");
        StepGameplayUpdates(8,Vector2.Down);
        StepGameplayUpdates(8,Vector2.Up);
        FailIf(_dialogue.IsOpen,"Crown's deleted title interaction must not replay on repeated contact.");
        LoadValidationRoom(0,0x60);
    }

    private sealed class CrownEntranceFreeze() :
        RoomEntityAdapter<Node2D>(new Node2D(),static _=>{}), IRoomEntityUpdateFreeze
    {
        public bool FreezesRoomEntities { get; set; }=true;
    }
}
