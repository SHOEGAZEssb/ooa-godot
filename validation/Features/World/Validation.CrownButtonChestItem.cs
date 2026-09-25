using Godot;
using System;
using System.Linq;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateCrownButtonChestItem()
    {
        Vector2 Center(int p)=>new((p&15)*16+8,(p>>4)*16+8);
        foreach(bool batch in new[]{false,true})
        {
            void Step(int count=1,Vector2 move=default,bool cane=false) =>
                StepGameplayUpdates(count, move, cane?["attack"]:[], cane?["attack"]:[], batched: batch);
            _saveData.SetRoomFlag(4,0xbc,OracleSaveData.RoomFlagItem,false);
            LoadValidationRoom(4,0xbc);
            // Isolated final-pressure fixture: pre-position the room's three
            // statues. This does not validate their push routes or progression.
            foreach(int p in new[]{0x46,0x48,0x68})
            {
                FailIf(_currentRoom.Layout[p]!=0x2a,"Crown chest fixture lost a source statue.");
                _currentRoom.SetPositionTileAndCollision(Center(p),0xa0,null,0);
            }
            foreach(int p in new[]{0x34,0x3a,0x7a})
                _currentRoom.SetPositionTileAndCollision(Center(p),0x2a,null,0);
            _player.WarpTo(new(56,120));
            _player.Face(Vector2I.Right);
            FailIf(_currentRoom.IsSolid(_player.Position),"Cane must start on actual floor west of button$74.");
            _inventory.GiveTreasure(InventoryState.ItemSomaria,1);
            _inventory.EquipA(InventoryState.ItemSomaria);
            _inventory.EquipB(InventoryState.ItemNone);
            Step();
            FailIf(_entities.ActiveTriggers!=0x0b || _currentRoom.Layout[0x57]==0xf1,
                "Three pre-positioned statues must press only bits0,1,3 without creating the chest.");
            Step(cane:true); Step(26);
            FailIf(_entities.EntityAdapters<SomariaBlockRoomEntity>().Count()!=1 ||
                _currentRoom.Layout[0x74]!=0xda || _entities.ActiveTriggers!=0x0f || _currentRoom.Layout[0x57]!=0xf1,
                "Normal Cane use must cover fourth button$74 and create the exact-trigger chest at$57.");
            for(int i=0;_player.Position.Y>104 && i<32;i++)
            {
                Step(move:Vector2.Up);
                FailIf(_currentRoom.IsSolid(_player.Position),"Cane-to-chest approach crossed solid geometry.");
            }
            for(int i=0;_player.Position.X<120 && i<80;i++)
            {
                Step(move:Vector2.Right);
                FailIf(_currentRoom.IsSolid(_player.Position),"South chest approach crossed solid geometry.");
            }
            for(int i=0;_player.Position.Y>100 && i<16;i++) Step(move:Vector2.Up);
            _player.Face(Vector2I.Up);
            FailIf(_currentRoom.IsSolid(_player.Position),"Small-key chest must be reachable from its south floor.");
            int keys=_inventory.GetDungeonSmallKeys(5);
            FailIf(!TryInteract(_player) || !_interactions.ChestRewardActive,
                "Crown four-button chest must open through the normal interaction path.");
            Step(32);
            FailIf(_inventory.GetDungeonSmallKeys(5)!=keys+1 || !_saveData.HasRoomFlag(4,0xbc,OracleSaveData.RoomFlagItem) ||
                _entities.Entities<RetractableTriggerChestRoomEntity>().Count!=0,
                "Source chestData$bc/$57 Small Key$30:$03 must grant once and retire the retractable controller.");
            _dialogue.Close(); Step();
            int granted=_inventory.GetDungeonSmallKeys(5);
            FailIf(TryInteract(_player) || _inventory.GetDungeonSmallKeys(5)!=granted,
                "Repeating interaction with the opened chest must not grant another key.");
            LoadValidationRoom(0,0x60); LoadValidationRoom(4,0xbc); Step();
            FailIf(_entities.Entities<RetractableTriggerChestRoomEntity>().Count!=0 ||
                _entities.EntityAdapters<SomariaBlockRoomEntity>().Any() || _inventory.GetDungeonSmallKeys(5)!=granted,
                "Re-entry must retain collection and remove the transient Cane block and chest controller.");
        }
        LoadValidationRoom(0,0x60);
    }
}
