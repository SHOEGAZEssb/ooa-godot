using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedParentMenuButtons(FrontendValidationRoot root, bool batched)
    {
        var session = root._gameplaySession!;
        var rooms = session.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var inventory = player.Inventory;
        var memory = rooms.RuntimeState;
        var menus = session.Menus;
        var app = new ApplicationValidationFixture(root);
        var game = memory.Game;
        var room = rooms.CurrentRoom;
        var tiles = (from y in Enumerable.Range(1,6) from x in Enumerable.Range(1,8)
            let point = new Vector2(x*16+8,y*16+8)
            select (Point:point, Tile:room.GetMetatile(point), Collision:room.GetTerrainInfo(point).Collision)).ToArray();
        try
        {
            int[] items = game == OracleGame.Ages ? [TreasureId.Sword,TreasureId.Shield,TreasureId.Bracelet,TreasureId.Shooter] :
                [TreasureId.Sword,TreasureId.Shield,TreasureId.Bracelet,TreasureId.Boomerang,TreasureId.Feather];
            foreach (int item in items)
            foreach (bool primary in new[] {true,false})
            foreach (bool remove in new[] {false,true})
            {
                player.ClearNativeItemParents(); entities.Clear();
                for (int slot=2;slot<=5;slot++) ParentItemButtons.Set(memory,slot,0);
                foreach (var tile in tiles) room.SetPositionTileAndCollision(tile.Point,(byte)(game==OracleGame.Ages ? 0x3a : 4),0,0);
                player.WarpTo(new(88,72)); player.Face(Vector2I.Up);
                inventory.GiveTreasure(item,item is TreasureId.Boomerang or TreasureId.Feather ? 2 : 1);
                inventory.GiveTreasure(TreasureId.EmberSeeds,0x10); inventory.SelectShooterSeeds(0);
                inventory.EquipA(primary ? item : 0); inventory.EquipB(primary ? 0 : item);
                app.Step(1,Vector2.Zero,batched:batched);
                var native = new BraceletRom(room,player.PrecisePosition,0,0xff,game:game);
                native.LoadSave(rooms.SaveData); native.InitializeWalkingAnimation(); native[0xcc2d]=(byte)rooms.ActiveGroup;
                var random = entities.Random.CaptureState(); native[0xff94]=random.Rng1; native[0xff95]=random.Rng2;
                int updates=0, previousKeys=0, reassignments=0;
                Phase precedingPhase=menus.Lifecycle.CurrentPhase;
                void Compare(string context)
                {
                    for (int slot=2;slot<=5;slot++)
                        FailIf(ParentItemButtons.Read(memory,slot)!=native[((0xd0+slot)<<8)|3],
                            context+$": parent${slot:x2} button byte differs: runtime${ParentItemButtons.Read(memory,slot):x2}, native${native[((0xd0+slot)<<8)|3]:x2}.");
                    bool expected = item==TreasureId.Feather ? (native[0xd300]!=0 || native[0xd400]!=0) :
                        native[(item==TreasureId.Shield ? 0xd500 : item==TreasureId.Boomerang ? 0xd300 : 0xd200)]!=0;
                    bool active = item switch
                    {
                        TreasureId.Sword => player.IsAttacking, TreasureId.Shield => player.ShieldParentButton!=0,
                        TreasureId.Bracelet => ((PlayerWorld)player.World).BraceletParentActive,
                        TreasureId.Shooter => player.IsUsingSeedShooter, TreasureId.Boomerang => entities.BoomerangParent.Active,
                        _ => player.CapeParentActive
                    };
                    // A menu may clear the button byte while retaining the Shield parent until Link runs.
                    if (item!=TreasureId.Shield || !menus.Lifecycle.IsActive)
                        FailIf(active!=expected,context+": retained parent lifetime differs.");
                    FailIf(player.PrecisePosition!=new Vector2(native.Word(0xd00c)/256f,native.Word(0xd00a)/256f),context+": Link moved differently across menu ownership.");
                }
                void Step(int count=1,int keys=0)
                {
                    int edge=keys & ~previousKeys; previousKeys=keys;
                    app.Step(count,Vector2.Zero,held:MenuRomActions(keys),pressed:MenuRomActions(edge),batched:batched,afterUpdate:()=>
                    {
                        // Native menuStateFadeOutOfMenu calls reassignment after graphics reload,
                        // before room fade-in. Parent/object dispatch remains frozen until closing.
                        if (menus.Lifecycle.CurrentPhase==Phase.ClosingFadeIn && precedingPhase==Phase.ClosingFadeOut)
                        {
                            native.LoadSave(rooms.SaveData); native.ReassignParentButtons(); reassignments++;
                        }
                        if (!menus.Lifecycle.IsActive) native.UpdateLinkItems(entities.FrameCounter,keys);
                        Compare($"{game}: menu parent ITEM${item:x2} A={primary} remove={remove}, update{++updates}");
                        precedingPhase=menus.Lifecycle.CurrentPhase;
                    });
                }
                int oldButton=primary ? 1 : 2, newButton=primary ? 2 : 1;
                Step(2,oldButton);
                Step(1,oldButton|8);
                FailIf(!menus.Inventory.IsActive,$"{game}: normal inventory input must open during ITEM${item:x2}.");
                for (int guard=0;!menus.Inventory.IsOpen && guard<30;guard++) Step(1,oldButton);
                FailIf(!menus.Inventory.IsOpen,$"{game}: inventory did not finish its native opening fades.");
                int empty=Enumerable.Range(0,16).FirstOrDefault(index=>inventory.StorageItemAt(index)==0,-1);
                FailIf(empty<0,"Parent menu fixture needs an empty native storage slot.");
                for (int guard=0;root._scene.InventoryScreen.Cursor!=empty && guard<16;guard++) menus.Inventory.MoveCursor(Vector2I.Right);
                Step(); Step(1,oldButton); Step();
                if (!remove)
                {
                    Step(1,newButton); Step();
                    for (int guard=0;root._scene.InventoryScreen.ItemSubmenuActive && guard<40;guard++)
                    {
                        if (root._scene.InventoryScreen.ItemSubmenuReady) { menus.Inventory.ConfirmItemSubmenu(); break; }
                        Step();
                    }
                }
                int expectedButton=remove ? 0 : newButton;
                FailIf((inventory.EquippedA==item ? 1 : inventory.EquippedB==item ? 2 : 0)!=expectedButton,
                    $"{game}: actual inventory equipment swap/removal did not reach the intended button.");
                int parentSlot=item==TreasureId.Shield ? 5 : item is TreasureId.Boomerang or TreasureId.Feather ? 3 : 2;
                FailIf(ParentItemButtons.Read(memory,parentSlot)!=oldButton,"Equipping inside the menu must not reassign parents before room graphics return.");
                Step(1,newButton|8);
                for (int guard=0;menus.Inventory.IsActive && guard<30;guard++) Step(1,newButton);
                FailIf(menus.Inventory.IsActive || reassignments!=1,$"{game}: menu close must perform one native parent-button handoff.");
                Step(12,newButton);
                if (item==TreasureId.Shield && !remove)
                {
                    Step(1,newButton|4);
                    for (int guard=0;!menus.Map.IsOpen && guard<30;guard++) Step(1,newButton);
                    FailIf(!menus.Map.IsOpen,$"{game}: map must open while the reassigned Shield remains held.");
                    Step(1,newButton|4);
                    for (int guard=0;menus.Map.IsActive && guard<30;guard++) Step(1,newButton);
                    FailIf(menus.Map.IsActive || reassignments!=2,$"{game}: map closing must share the native parent-button handoff.");
                }
                // Cancelled lifecycle ownership must never commit another button handoff.
                Step(1,newButton|8);
                FailIf(!menus.Inventory.IsActive,$"{game}: repeated menu opening did not acquire ownership.");
                byte[] masks=Enumerable.Range(2,4).Select(slot=>(byte)ParentItemButtons.Read(memory,slot)).ToArray();
                menus.Lifecycle.Dispose();
                FailIf(Enumerable.Range(2,4).Any(slot=>ParentItemButtons.Read(memory,slot)!=masks[slot-2]),
                    "Menu cancellation must release its lease without reassigning parent buttons.");
                player.ClearNativeItemParents(); entities.ClearPhysicalPlayerItems();
            }
        }
        finally
        {
            if (menus.Lifecycle.IsActive) menus.Lifecycle.Dispose();
            player.ClearNativeItemParents(); entities.ClearPhysicalPlayerItems();
            foreach (var tile in tiles) room.SetPositionTileAndCollision(tile.Point,tile.Tile,tile.Collision,0);
        }
    }
}
