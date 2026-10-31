using Godot;
using System;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedTileInteractionComposition(FrontendValidationRoot root,bool batched)
    {
        var engine=root._gameplaySession!;
        var rooms=engine.Rooms;
        var save=rooms.SaveData;
        var player=root._scene.Player;
        var inventory=player.Inventory;
        bool ages=save.Game==OracleGame.Ages;
        int originalGroup=rooms.ActiveGroup,originalRoom=rooms.CurrentRoom.Id;
        int chestRoom=ages?0x49:0xf5,signRoom=ages?0x2a:0x97,packed=ages?0x51:0x11;
        byte originalChestFlags=save.GetRoomFlags(0,chestRoom),originalSignFlags=save.GetRoomFlags(0,signRoom);
        int equippedA=inventory.EquippedA,equippedB=inventory.EquippedB;
        int originalRupees=inventory.Rupees;
        var app=new ApplicationValidationFixture(root);
        void Load(int group,int room)
        {
            if(ages)root.LoadDebugRoom(group,room);else root._seasonsOpening!.DebugTravel(group,room);
        }
        void Step(int count,Vector2 movement=default,bool attack=false) =>
            app.Step(count,movement,held:attack?["attack"]:[],pressed:attack?["attack"]:[],batched:batched);
        try
        {
            inventory.EquipA(0);inventory.EquipB(0);
            save.SetRoomFlag(0,chestRoom,OracleSaveData.RoomFlagItem,false);
            Load(0,chestRoom);
            var room=rooms.CurrentRoom;
            Vector2 chest=new((packed&15)*16+8,(packed>>4)*16+8);
            player.WarpTo(chest+new Vector2(0,16));player.Face(Vector2I.Up);
            FailIf(room.IsSolid(player.Position) || room.GetMetatile(chest)!=0xf1,
                $"{save.Game}: chest approach floor${room.GetMetatile(player.Position):x2}, solid{room.IsSolid(player.Position)}, chest${room.GetMetatile(chest):x2} must be native walkable floor and a closed chest.");
            Step(16,Vector2.Up);
            int rupees=inventory.Rupees;
            root._sound.ClearPlayRequestAudit();
            Step(1,attack:true);
            FailIf(!engine.Interactions.ChestRewardActive || room.GetMetatile(chest)!=0xf0 ||
                !save.HasRoomFlag(0,chestRoom,OracleSaveData.RoomFlagItem) ||
                rooms.RuntimeState.ReadField("wDisabledObjects")!=0x83 || inventory.Rupees!=rupees,
                $"{save.Game}: the actual A-button approach must enter the common chest service before granting its reward.");
            Step(31);
            FailIf(inventory.Rupees!=rupees,$"{save.Game}: chest reward must retain the source setup and32-update rise.");
            Step(2);
            int amount=ages?30:20; // clean-US chestData: Ages0:49 RUPEES_04; Seasons0:f5 RUPEES_03
            FailIf(inventory.Rupees!=Math.Min(999,rupees+amount) || !root._scene.Dialogue.IsOpen ||
                !root._scene.Dialogue.CurrentMessage.Contains(ages?"30 Rupees":"20 Rupees"),
                $"{save.Game}: shared chest completion must grant and present its own pack's native amount exactly once.");
            root._scene.Dialogue.Close();Step(2);
            FailIf(engine.Interactions.ChestRewardActive || rooms.RuntimeState.ReadField("wDisabledObjects")!=0 ||
                rooms.RuntimeState.ReadField("wDisableLinkCollisionsAndMenu")!=0,
                $"{save.Game}: closing the chest text must retire the reward and release its source locks.");
            Step(1,attack:true);Step(2);
            FailIf(inventory.Rupees!=Math.Min(999,rupees+amount) || root._sound.PlayRequestsFor(SoundId.SndOpenChest)!=1,
                $"{save.Game}: repeating A at the actual opened chest must not regrant or replay opening.");
            Load(0,chestRoom);
            FailIf(rooms.CurrentRoom.GetMetatile(chest)!=0xf0,$"{save.Game}: re-entry must consume the common room flag substitution.");
            // code/interactableTiles.s: shop locks precede setTile; shop/script
            // returns after setTile precede getChestData and ROOMFLAG_ITEM.
            foreach(var gate in new (int Shop,int Script,int Position)[]{(2,0,0x80),(2,0,0),(2,1,0x80),(0,1,0)})
            {
                save.SetRoomFlag(0,chestRoom,OracleSaveData.RoomFlagItem,false);
                Load(0,chestRoom);room=rooms.CurrentRoom;
                player.WarpTo(chest+new Vector2(0,16));player.Face(Vector2I.Up);
                Step(16,Vector2.Up);
                var memory=rooms.RuntimeState;
                memory.SetField("wInShop",(byte)gate.Shop);
                memory.SetField("wcca1",(byte)gate.Script);
                memory.SetField("wcca2",(byte)gate.Position);
                memory.SetField("wChestContentsOverride",TreasureId.Rupees);
                memory.SetField("wChestContentsOverride",1,1);
                root._sound.ClearPlayRequestAudit();
                Step(1,attack:true);Step(2);
                bool blocked=gate.Shop!=0 && gate.Script==0 && gate.Position!=0;
                FailIf(room.GetMetatile(chest)!=(blocked?0xf1:0xf0) ||
                    memory.ReadField("wcca2")!=(blocked?gate.Position:packed) ||
                    engine.Interactions.ChestRewardActive ||
                    save.HasRoomFlag(0,chestRoom,OracleSaveData.RoomFlagItem) ||
                    inventory.Rupees!=Math.Min(999,rupees+amount) ||
                    memory.ReadField("wChestContentsOverride")!=TreasureId.Rupees ||
                    memory.ReadField("wChestContentsOverride",1)!=1 ||
                    root._sound.PlayRequestsFor(SoundId.SndOpenChest)!=(blocked?0:1),
                    $"{save.Game}: native shop/script chest gates must preserve their owner's reward, flag and override bytes.");
                memory.SetField("wInShop",0);memory.SetField("wcca1",0);
                memory.SetField("wChestContentsOverride",0);memory.SetField("wChestContentsOverride",0,1);
            }
            save.SetRoomFlag(0,signRoom,0x40,true); // retire Seasons' opening Din at0:97
            Load(0,signRoom);
            room=rooms.CurrentRoom;
            int signPacked=ages?0x35:0x27;
            Vector2 sign=new((signPacked&15)*16+8,(signPacked>>4)*16+8);
            player.WarpTo(sign+new Vector2(0,32));player.Face(Vector2I.Up);
            FailIf(room.IsSolid(player.Position) || room.GetMetatile(sign)!=0xf2,
                $"{save.Game}: sign fixture must approach through actual southern floor geometry.");
            Step(32,Vector2.Up);Step(1,attack:true);
            string expected=ages?"Rolling Ridge":"Spool Swamp"; // native signText0:2a35 /0:9727
            FailIf(!root._scene.Dialogue.IsOpen || !root._scene.Dialogue.CurrentMessage.Contains(expected),
                $"{save.Game}: shared sign dispatch must select its own compiled source row.");
            root._scene.Dialogue.Close();Step(2);Step(1,attack:true);
            FailIf(!root._scene.Dialogue.IsOpen || !root._scene.Dialogue.CurrentMessage.Contains(expected),
                $"{save.Game}: sign interaction must remain reachable after completion.");
        }
        finally
        {
            root._scene.Dialogue.Close();
            save.SetRoomFlag(0,chestRoom,0xff,false);save.SetRoomFlag(0,chestRoom,originalChestFlags);
            save.SetRoomFlag(0,signRoom,0xff,false);save.SetRoomFlag(0,signRoom,originalSignFlags);
            inventory.EquipA(equippedA);inventory.EquipB(equippedB);
            inventory.AddRupees(originalRupees-inventory.Rupees);engine.StatusBar.SynchronizeRupees();
            Load(originalGroup,originalRoom);
        }
    }
}
