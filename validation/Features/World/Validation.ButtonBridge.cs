using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateButtonBridge()
    {
        var data = new CrownDungeonDatabase();
        var rule = data.ButtonBridge;
        var placement = data.GetRoomRecords(4,0xad).Single(row => row.Kind == DungeonObjectKind.ButtonBridge);
        FailIf(placement.Order != 1 || placement.Id != 0x90 || placement.SubId != 0x19 ||
            rule.First != 0x55 || rule.Last != 0x59 || rule.Interval != 8 ||
            rule.Hole != 0xf4 || rule.Bridge != 0x6d || rule.Diamond != 0xdb,
            "INTERAC $90:$19 lost its source placement, scan limits, interval or tile IDs.");
        Vector2 Center(int p) => new((p&15)*16+8,(p>>4)*16+8);
        foreach (bool batch in new[] { false,true })
        {
            void Step(int count=1,Vector2 movement=default) =>
                StepGameplayUpdates(count, movement, [], [], batched: batch);
            LoadValidationRoom(4,0xad);
            _player.WarpTo(new(72,136));
            var bridge = _entities.Entities<ButtonBridgeRoomEntity>().Single();
            FailIf(bridge.State != 0,"Button bridge must start at state0.");
            Step();
            for (int repeat=0;repeat<2;repeat++)
            {
                for (int p=0x55;p<=0x59;p++) FailIf(_currentRoom.Layout[p]!=0xf4,"Crown bridge must begin as five source holes.");
                for (int i=0;bridge.State!=2 && i<40;i++)
                {
                    Step(movement:Vector2.Up);
                    FailIf(_currentRoom.IsSolid(_player.Position),"Bridge button approach entered solid geometry.");
                }
                FailIf(bridge.State!=2 || bridge.Counter!=8 || (_entities.ActiveTriggers&1)==0,
                    "Actual floor button$74 must start the eight-update bridge delay.");
                _sound.ClearPlayRequestAudit();
                Step(7);
                FailIf(_currentRoom.Layout[0x55]!=0xf4 || bridge.Counter!=1,"Bridge extended before its eighth update.");
                Step();
                FailIf(_currentRoom.Layout[0x55]!=0x6d || _currentRoom.GetUnderlyingMetatile(Center(0x55))!=0x6d ||
                    _currentRoom.Layout[0x56]!=0xf4,"Bridge must extend one tile from the left in both buffers.");
                var text = _entities.TextActiveSource;
                try { _entities.TextActiveSource=()=>true; Step(8); FailIf(bridge.Counter!=8,"Text must freeze bridge delay."); }
                finally { _entities.TextActiveSource=text; }
                Step(40);
                FailIf(bridge.State!=3 || _sound.PlayRequestsFor(SoundId.SndDoorClose)!=5,
                    "Five extension writes and a final empty scan must reach the held state.");
                for (int i=0;bridge.State!=4 && i<40;i++) Step(movement:Vector2.Down);
                FailIf(bridge.State!=4 || bridge.Counter!=8,"Releasing the actual button must retain the bridge counter.");
                Step(7);
                FailIf(_currentRoom.Layout[0x59]!=0x6d,"Bridge retracted before its retained eighth update.");
                Step();
                FailIf(_currentRoom.Layout[0x59]!=0xf4 || _currentRoom.Layout[0x58]!=0x6d,
                    "Bridge must retract one tile starting from the right.");
                Step(40);
                FailIf(bridge.State!=1 || _sound.PlayRequestsFor(SoundId.SndDoorClose)!=10,
                    "Bridge must return to waiting after five retractions and the empty scan.");
            }
            // Isolate mid-motion trigger edges without a long movement sequence.
            int triggers=1, writes=0, debris=0;
            var isolated = new ButtonBridgeRoomEntity(rule,_currentRoom,()=>triggers,
                (_,_)=>writes++, _=>{}, point=>{ FailIf(point!=Vector2.Zero,"Source bridge debris uses its unpositioned interaction origin."); debris++; return true; });
            var frame = new RoomEntityFrame(_player,0,false);
            var spawns = new List<RoomEntitySpawn>();
            void Tick(int count=1) { for(int i=0;i<count;i++) isolated.UpdateFrame(frame,spawns); }
            Tick(2); Tick(3); triggers=0; Tick();
            FailIf(isolated.State!=4 || isolated.Counter!=5,"Mid-extension release must preserve the remaining delay.");
            triggers=1; Tick();
            FailIf(isolated.State!=1 || isolated.Counter!=5,"Mid-retraction press must return to state1 without decrementing.");
            Tick();
            FailIf(isolated.State!=2 || isolated.Counter!=8,"The following pressed update must restart the extension delay.");
            _currentRoom.SetPositionTileAndCollision(Center(0x59),0xdb,0,(long)_animationTicks);
            triggers=0; Tick(); Tick(8);
            FailIf(writes!=1 || debris!=1,"Retraction must create debris for a switch diamond before the hole write.");
            isolated.Free();
            for (int i=0;bridge.State!=2 && i<40;i++) Step(movement:Vector2.Up);
            FailIf(bridge.State!=2,"Debris fixture must press the actual bridge button again.");
            for (int i=0;bridge.State!=4 && i<40;i++) Step(movement:Vector2.Down);
            FailIf(bridge.State!=4,"Debris fixture must release the actual button during extension.");
            int remaining = bridge.Counter;
            Step(remaining);
            var liveDebris = _entities.Entities<RockDebrisEffect>();
            FailIf(liveDebris.Count!=1 || _entities.InteractionSlot(liveDebris[0])<0 ||
                _currentRoom.Layout[0x59]!=0xf4,
                "Live bridge retraction must allocate native rock debris and replace the diamond with a hole.");
            LoadValidationRoom(0,0x60);
            FailIf(_entities.Entities<ButtonBridgeRoomEntity>().Count!=0,"Room departure must remove the bridge controller.");
        }
    }
}
