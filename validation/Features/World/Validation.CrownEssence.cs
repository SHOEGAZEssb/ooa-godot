using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownEssence()
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var data=new CrownDungeonDatabase();
        var visual=new DungeonInteractionVisualDatabase().Visual("sacred-soil");
        // mainData.s4:b8 and essence.s fifth OAM/text/warp rows, independent
        // of the runtime's chosen definition and collection-state machine.
        FailIf(data.GetRoomRecords(4,0xb8) is not [{Id:0x7f,SubId:0,Order:0,Y:0x28,X:0x78,Kind:DungeonObjectKind.Essence}] ||
            data.Essence.Index!=4 || visual.TileBase!=0x0a || visual.Palette!=0 || visual.Animations.Length!=1 ||
            !data.Essence.Message.StartsWith("\\pos(2)",StringComparison.Ordinal) ||
            !data.Essence.Message.Contains("Sacred Soil",StringComparison.Ordinal) ||
            !data.Essence.Message.Contains("nourishing",StringComparison.Ordinal) ||
            data.Essence.ExitWarp is not {DestinationGroup:0,DestinationRoom:0x0a,DestinationPosition:0x17,DestinationTransition:1},
            "Crown Essence lost its source placement, fifth OAM/text row or respawn-setting exit.");
        foreach(bool batch in new[]{false,true})
        {
            void Step(int count=1,Vector2 movement=default) =>
                StepGameplayUpdates(count, movement, [], [], batched: batch);
            _saveData.SetRoomFlag(4,0xb8,OracleSaveData.RoomFlagItem,false);
            LoadValidationRoom(4,0xb8);
            _player.WarpTo(new(120,120));
            FailIf(_currentRoom.IsSolid(_player.Position),"Crown Essence approach must start on real floor below the pedestal.");
            int priorEssences=_inventory.Essences;
            _sound.ClearPlayRequestAudit();
            for(int i=0;!_dialogue.IsOpen && i<280;i++) Step(movement:Vector2.Up);
            var essence=_entities.Entities<DungeonEssence>().Single();
            FailIf(!_dialogue.IsOpen || !essence.ReadyForDialogue || _player.IsHoldingItemTwoHands ||
                _inventory.Essences!=(priorEssences|0x10) || !_saveData.HasRoomFlag(4,0xb8,OracleSaveData.RoomFlagItem) ||
                !_dialogue.CurrentMessage.Contains("Sacred Soil",StringComparison.Ordinal) ||
                _sound.PlayRequestsFor(OracleSoundEngine.SndDropEssence)!=1 ||
                _sound.PlayRequestsFor(OracleSoundEngine.MusGetEssence)!=1,
                "Walking to Crown's pedestal must grant only essence bit$10, persist collection and show TX_0012.");
            FailIf(!_player.NativeNormalStateForInteraction,
                "Essence state4 requests Link state04 without consuming it during the interaction pass.");
            Step();
            FailIf(_player.IsHoldingItemTwoHands || _player.NativeNormalStateForInteraction,
                "Link must consume the essence's state04 request before initializing its pose.");
            Step();
            FailIf(!_player.IsHoldingItemTwoHands || _player.NativeNormalStateForInteraction,
                "The next Link dispatch must initialize the two-handed state04 pose.");
            Step(6);
            FailIf(essence.SwirlActive || IsTransitioning,"Crown Essence must wait for its textbox to close.");
            _dialogue.Close(); Step(); Step();
            FailIf(!essence.SwirlActive || _roomEvents.Get<DungeonEssenceEvent>().Counter!=360,
                "Crown Essence must enter the common360-update swirl after script initialization.");
            Step(360); Step(20); Step(20); Step(40); Step(28);
            FailIf(IsTransitioning,"Crown Essence exited before the final warp-delay update.");
            Step();
            FailIf(!IsTransitioning,"Crown Essence did not begin its source exit warp.");
            for(int i=0;IsTransitioning && i<180;i++) Step();
            FailIf(IsTransitioning || _rooms.ActiveGroup!=0 || _currentRoom.Id!=0x0a ||
                _player.Position!=new Vector2(120,24) || _saveData.RespawnGroup!=0 || _saveData.RespawnRoom!=0x0a,
                "Crown Essence must exit to0:0a/$17 and set the death respawn room.");
            LoadValidationRoom(4,0xb8); Step(3);
            FailIf(_entities.Entities<DungeonEssence>().Single() is not {Collected:true,Visible:false} ||
                _entities.Entities<DungeonEssencePedestal>().Count!=1 || _entities.Entities<DungeonEssenceGlow>().Count!=0,
                "Collected Crown Essence re-entry must retain only the visible pedestal.");
            for(int i=0;i<90;i++) Step(movement:Vector2.Up);
            FailIf(_roomEvents.Get<DungeonEssenceEvent>().HasState || _inventory.Essences!=(priorEssences|0x10),
                "Approaching the collected pedestal again must not grant or replay the Essence.");
            LoadValidationRoom(0,0x60);
            foreach (bool incoming in new[] { false, true })
            {
                _saveData.SetRoomFlag(4,0xb8,OracleSaveData.RoomFlagItem,false);
                LoadValidationRoom(4,incoming ? 0xbc : 0xb8);
                _player.WarpTo(new(120,120));
                Step(2);
                _entities.BeginScreenTransition(4,_world.LoadRoom(4,incoming ? 0xb8 : 0xbc),new(240,0),_player);
                var glow=(incoming ? _entities.Entities<DungeonEssenceGlow>() :
                    _entities.OutgoingEntities<DungeonEssenceGlow>()).Single();
                var parent=(incoming ? _entities.Entities<DungeonEssence>() :
                    _entities.OutgoingEntities<DungeonEssence>()).Single();
                Vector2 position=parent.Position;
                int height=parent.DrawZ;
                // essence.s $d1 enabled=$81, subid02: two-update frames,
                // parameters 0/1/0/1 consumed once, copy parent's high XYZ.
                int[] frames=[0,1,1,2,2,3,3,0];
                bool[] visible=[true,false,false,false,false,true,true,true];
                for(int i=0;i<frames.Length;i++)
                {
                    Step();
                    FailIf(glow.AnimationFrame!=frames[i] || glow.Visible!=visible[i] ||
                        glow.Position!=position.Floor() || glow.Z!=height ||
                        parent.Position!=position || parent.DrawZ!=height,
                        $"Crown glow lost its reserved-slot animation while its parent was scroll-frozen (incoming={incoming}, update={i+1}).");
                }
                Step(2);
                FailIf(glow.AnimationFrame!=1 || glow.Visible,
                    "Batched scroll updates must consume both glow animation updates.");
                var death=typeof(Player).GetField("_deathPending",flags)!;
                death.SetValue(_player,true);
                try
                {
                    // Isolate the native object's wLinkDeathTrigger gate;
                    // the death cutscene/respawn route is a separate system.
                    _entities.Update(2.0/60,_player);
                    FailIf(glow.AnimationFrame!=1 || glow.Visible,
                        "Essence glow must honor wLinkDeathTrigger even with enabled bit$80 during scroll.");
                }
                finally { death.SetValue(_player,false); }
                Step(2);
                FailIf(glow.AnimationFrame!=2 || glow.Visible,
                    "Clearing Link's death trigger must resume, rather than restart, the glow animation.");
                _entities.FinishScreenTransition();
                LoadValidationRoom(0,0x60);
            }
        }
    }
}
