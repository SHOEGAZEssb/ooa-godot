using Godot;
using System;
using System.Linq;

namespace Oracles;

public sealed partial class ValidationRoot
{
    private static void CheckSharedBombchus(FrontendValidationRoot root, bool batched)
    {
        var rooms = root._gameplaySession!.Rooms;
        var entities = root._seasonsOpening?.Entities ?? root._entities;
        var player = root._scene.Player;
        var inventory = player.Inventory;
        var room = rooms.CurrentRoom;
        OracleGame game = rooms.SaveData.Game;
        int ammoAddress = game == OracleGame.Ages ? 0xc6b3 : 0xc6ad; // include/wram.s:wNumBombchus.
        var app = new ApplicationValidationFixture(root);
        var data = new BombchuDatabase(rooms.AssetRoot);
        var tiles = (from y in Enumerable.Range(0,room.HeightInTiles) from x in Enumerable.Range(0,room.WidthInTiles)
            let point = new Vector2(x * 16 + 8,y * 16 + 8)
            select (Point:point,Tile:room.GetMetatile(point),Collision:room.GetTerrainInfo(point).Collision)).ToArray();
        // Declared floor isolates native item dispatch from room content/AI.
        foreach (var tile in tiles) room.SetPositionTileAndCollision(tile.Point,(byte)(game == OracleGame.Ages ? 0x3a : 0x04),0,0);
        try
        {
            foreach (var scenario in new[] { (Primary:true,Target:false),(Primary:false,Target:false),(Primary:true,Target:true) })
            {
                bool primary = scenario.Primary;
                entities.Clear(); player.WarpTo(new(72,64)); player.Face(Vector2I.Right);
                inventory.GiveTreasure(TreasureId.Bombchus,0x10);
                int initialAmmo = inventory.Bombchus;
                inventory.EquipA(primary ? TreasureId.Bombchus : 0);
                inventory.EquipB(primary ? 0 : TreasureId.Bombchus);
                var native = new BraceletRom(room,player.PrecisePosition,1,0xff,game:game);
                native.LoadSave(rooms.SaveData); native.InitializeWalkingAnimation();
                native[0xcc2d] = (byte)rooms.ActiveGroup; native[0xcc2e] = 0;
                if (scenario.Target)
                {
                    // Declared target fields exercise capture/explosion; enemy
                    // AI and explosion damage are separate regressions.
                    var target = new DeclaredBombchuTarget(new(104,64)) { Id = 0x0a };
                    entities.RegisterEnemySlot(target,0); entities.AddEntity(target);
                    native[0xd080] = 1; native[0xd081] = 0x0a; native[0xd09a] = 0x80;
                    native[0xd08d] = 104; native[0xd08b] = 64; native[0xd0a6] = native[0xd0a7] = 4; native[0xd0a9] = 8;
                }
                var random = entities.Random.CaptureState();
                native[0xff94] = random.Rng1; native[0xff95] = random.Rng2;
                var sounds = root._sound.AttachPlayRequestAudit();
                int update = 0;
                void Compare()
                {
                    string context = $"{game}: shared ITEM$0d, A={primary}, target={scenario.Target}, update={++update}";
                    var children = entities.Entities<BombchuItem>();
                    int[] slots = Enumerable.Range(0xd7,5).Select(page => page << 8)
                        .Where(slot => native[slot] != 0 && native[slot + 1] == 0x0d).ToArray();
                    var parent = entities.BombchuParent;
                    int parentSlot = Enumerable.Range(0xd3,2).Select(page => page << 8)
                        .FirstOrDefault(slot => native[slot] != 0 && native[slot + 1] == 0x0d);
                    FailIf(children.Count != slots.Length || parent.Active != (parentSlot != 0) || inventory.Bombchus != native[ammoAddress],
                        $"{context}: native parent/child/ammo differs ({children.Count}/{parent.Active}/${inventory.Bombchus:x2} versus {slots.Length}/${parentSlot:x4}/${native[ammoAddress]:x2}).");
                    if (parent.Active)
                        FailIf(parent.Slot != (parentSlot >> 8 & 15) || parent.Counter != native[parentSlot + 0x20] ||
                            parent.Parameter != native[parentSlot + 0x21] || parent.Mode != native[parentSlot + 0x30],context + ": parent slot/clock differs.");
                    if (children.SingleOrDefault() is { } child)
                    {
                        int slot = slots.Single();
                        FailIf(child.AssetRoot != rooms.AssetRoot || child.ItemState != native[slot + 4] ||
                            child.Counter1 != native[slot + 6] || child.Counter2 != native[slot + 7] ||
                            child.PrecisePosition != new Vector2(native.Word(slot + 0x0c) / 256f,native.Word(slot + 0x0a) / 256f) ||
                            (ushort)child.ZFixed != native.Word(slot + 0x0e) || (ushort)child.SpeedZ != native.Word(slot + 0x14) ||
                            child.SpeedRaw != native[slot + 0x10] || child.SpeedTmp != native[slot + 0x11] ||
                            child.Radius != native[slot + 0x26],context + ": selected native child state/counters/XYZ/speed/radius differs.");
                        if (child.ItemState != 0xff)
                            FailIf(child.ExplosionRecord.Collision != native[slot + 0x24] || child.ExplosionRecord.TileBase != native[slot + 0x1d] ||
                                child.ExplosionRecord.Palette != (native[slot + 0x1c] & 7),context + ": native graphics/palette/collision identity differs.");
                    }
                    FailIf(!sounds.Requests.SequenceEqual(native.Sounds),context + ": sound request order differs.");
                    var rng = entities.Random.CaptureState();
                    FailIf(rng.Rng1 != native[0xff94] || rng.Rng2 != native[0xff95] || rng.Calls != random.Calls + native.RandomCalls,
                        context + ": shared RNG consumption differs.");
                }
                void Step(int count, bool press = false)
                {
                    string button = primary ? "attack" : "item";
                    app.Step(count,Vector2.Zero,held:press ? [button] : [],pressed:press ? [button] : [],batched:batched,
                        afterUpdate:() => { native.UpdateLinkItems(entities.FrameCounter,press ? primary ? 1 : 2 : 0); Compare(); });
                }
                rooms.RuntimeState.SetField("wDisabledObjects",0x90); native[0xcc8a] = 0x90;
                Step(1,true); Step(2);
                rooms.RuntimeState.SetField("wDisabledObjects",0); native[0xcc8a] = 0;
                Step(1,true); Step(4);
                int expectedAmmo = initialAmmo - ((initialAmmo & 15) == 0 ? 7 : 1);
                FailIf(entities.Entities<BombchuItem>().Count != 1 || inventory.Bombchus != expectedAmmo,
                    $"{game}: equipped Bombchu must create its native child and consume one BCD unit.");
                rooms.RuntimeState.SetField("wDisabledObjects",0x90); native[0xcc8a] = 0x90;
                Step(3);
                rooms.RuntimeState.SetField("wDisabledObjects",0); native[0xcc8a] = 0;
                root._scene.Dialogue.ShowMessage("Bombchu pause.",player.Position.Y); native[0xcba0] = 1;
                Step(3); root._scene.Dialogue.Close(); native[0xcba0] = 0;
                Step(7); Step(1,true); // Existing child rejects allocation without spending another unit.
                player.ClearNativeItemParents(); native.ClearItemParents();
                FailIf(entities.Entities<BombchuItem>().Count != 1,$"{game}: parent clearing must retain the independent child.");
                Step(220);
                FailIf(entities.Entities<BombchuItem>().Count != 0,$"{game}: native child must finish its bounded lifetime.");
                Step(1,true); Step(3);
                entities.ClearPhysicalPlayerItems();
                native.ClearPhysicalItems();
                Step(2); Step(1,true); Step(2);
            }
            // Execute the selected clean-ROM child over every target ID. The
            // declared enemy fields isolate the mask, not enemy AI or combat.
            var scan = new BraceletRom(room,new(72,64),1,0xff,game:game);
            scan.LoadSave(rooms.SaveData); scan.InitializeWalkingAnimation();
            scan.Update(pressed:true,item:0x0d);
            const int itemSlot = 0xd700;
            for (int id = 0; id < 128; id++)
            {
                scan[itemSlot + 4] = 2; scan[itemSlot + 7] = 0x40; scan[itemSlot + 0x30] = 0xd0;
                scan[itemSlot + 0x26] = scan[itemSlot + 0x27] = 0x18;
                scan[itemSlot + 0x0b] = scan[itemSlot + 0x0d] = 64;
                scan[0xd080] = 1; scan[0xd081] = (byte)id; scan[0xd09a] = 0x80;
                scan[0xd08b] = scan[0xd08d] = 64; scan[0xd0a6] = scan[0xd0a7] = 4;
                scan[0xd0a9] = scan[0xd0a4] = 0; scan[0xd08f] = 0xf0; scan[0xd0ab] = 0x40;
                scan.Update(item:0x0d);
                FailIf(data.CanTarget(id) != (scan[itemSlot + 4] == 3),$"{game}: native bombchuTargets ID ${id:x2} differs.");
            }
        }
        finally
        {
            rooms.RuntimeState.SetField("wDisabledObjects",0); root._scene.Dialogue.Close();
            entities.Clear(); player.ClearNativeItemParents();
            foreach (var tile in tiles) room.SetPositionTileAndCollision(tile.Point,tile.Tile,tile.Collision,0);
            player.WarpTo(new(72,80)); inventory.EquipA(0); inventory.EquipB(0);
        }
    }
}
