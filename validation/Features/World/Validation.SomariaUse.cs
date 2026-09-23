using Godot;
using System;
using System.Linq;
using System.Reflection;

namespace oracleofages;

public partial class ValidationRoot
{
    private void ValidateSomariaUse()
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var input=(ApplicationInputBuffer)typeof(GameRoot).GetField("_applicationInput",flags)!.GetValue(this)!;
        var scheduler=(ApplicationFixedUpdateScheduler)typeof(GameRoot).GetField("_applicationUpdates",flags)!.GetValue(this)!;
        var update=(Action)typeof(GameRoot).GetMethod("AdvanceApplicationUpdate",flags)!.CreateDelegate(typeof(Action),this);
        foreach(bool batch in new[]{false,true})
        foreach(string button in new[]{"attack","item"})
        {
            LoadValidationRoom(0,0x60); _entities.Clear(); _player.WarpTo(new(72,40)); _player.Face(Vector2I.Down);
            _currentRoom.SetPositionTileAndCollision(new(72,54),0x0c,0,0);
            _inventory.GiveTreasure(InventoryState.ItemSomaria,1);
            _inventory.EquipA(button=="attack"?InventoryState.ItemSomaria:InventoryState.ItemNone);
            _inventory.EquipB(button=="item"?InventoryState.ItemSomaria:InventoryState.ItemNone);
            void Step(int count,bool pressed=false,bool turn=false)
            {
                input.CaptureForValidation([button],pressed?[button]:[],turn?Vector2.Right:Vector2.Zero);
                if(batch) scheduler.Advance(count/60.0,update);
                else for(int i=0;i<count;i++) scheduler.Advance(1.0/60.0,update);
            }
            var cane=_entities.Somaria!;
            Step(1,true);
            FailIf(!cane.Active || cane.Weapon?.State!=1 || cane.Parent?.Parameter!=0,
                "Cane input must initialize parent mode$22 and reserved ITEM$04 in the same gameplay update.");
            Step(13,turn:true);
            FailIf(_entities.EntityAdapters<SomariaBlockRoomEntity>().Any() || cane.Parent!.Parameter!=0x64 || _player.Position!=new Vector2(72,40) || _player.FacingVector!=Vector2I.Down,
                "Cane must immobilize Link and withhold the block through parent update13.");
            Step(1);
            var block=_entities.EntityAdapters<SomariaBlockRoomEntity>().Single().Block;
            FailIf(block.State!=1 || block.Position!=new Vector2(72,54) || cane.Weapon!.State!=2,
                "Parent update14's parameter$06 must create and initialize ITEM$18 later in the same native item pass.");
            Step(3);
            FailIf(!cane.Active || cane.Parent!.Parameter!=0x86,"Cane terminal animation must remain active for one complete update.");
            Step(1);
            FailIf(cane.Active || cane.Weapon is not null,"The next parent update must clear Cane and its post pass must delete the weapon.");
            Step(5);
            FailIf(block.State!=3 || _currentRoom.GetMetatile(new(72,54))!=0xda,
                "Normal Cane use must finish block phase-in at update23 after parent initialization.");
            input.CaptureForValidation([],[],Vector2.Zero); scheduler.Advance(1.0/60.0,update);
            Step(1,true); Step(14);
            FailIf(!block.Finished || _entities.EntityAdapters<SomariaBlockRoomEntity>().Count()!=1,
                "A completed Cane use must permit another use and replace the previous block.");
            Step(9);
            var bombs=new BombDatabase().Data;
            for(int i=0;i<4;i++) _entities.Spawn<BombEffect>(new BombSpawn(_player,bombs,0,_=>{}));
            input.CaptureForValidation([],[],Vector2.Zero); scheduler.Advance(1.0/60.0,update);
            Step(1,true); Step(14);
            FailIf(_entities.EntityAdapters<SomariaBlockRoomEntity>().Any() || cane.Weapon?.State!=2,
                "Full-pool Cane use must mark the old block before failed allocation, then let that block retire in its own slot.");
            Step(10);
            FailIf(_entities.EntityAdapters<SomariaBlockRoomEntity>().Any() || !_entities.DynamicItemSlotAvailable,
                "Cane must not retry allocation after the previous block's slot becomes free later in the update.");
            _entities.Clear();
        }
        LoadValidationRoom(0,0x60); _entities.Clear(); _player.WarpTo(new(72,40)); _player.Face(Vector2I.Down);
        _currentRoom.SetPositionTileAndCollision(new(72,54),0x0c,0,0);
        _inventory.GiveTreasure(InventoryState.ItemSword,1);
        _inventory.EquipA(InventoryState.ItemSomaria); _inventory.EquipB(InventoryState.ItemSword);
        void Buttons(int count,string[] held,string[] pressed)
        { input.CaptureForValidation(held,pressed,Vector2.Zero); scheduler.Advance(count/60.0,update); }
        Buttons(1,["attack"],["attack"]); Buttons(12,[],[]);
        Buttons(1,["attack"],["attack"]);
        FailIf(_entities.Somaria!.Parent!.Frame!=0 || _entities.EntityAdapters<SomariaBlockRoomEntity>().Any(),
            "A fresh Cane press must replace its equal-priority parent before the old swing creates a block.");
        Buttons(13,[],[]);
        FailIf(_entities.EntityAdapters<SomariaBlockRoomEntity>().Any(),"Restarted Cane must restart its complete creation delay.");
        Buttons(1,["item"],["item"]);
        FailIf(_entities.Somaria.Active || !_player.IsAttacking || _entities.EntityAdapters<SomariaBlockRoomEntity>().Any(),
            $"Higher-priority sword input must replace Cane before the weapon creation pass: cane{_entities.Somaria.Active}, sword{_player.IsAttacking}, blocks{_entities.EntityAdapters<SomariaBlockRoomEntity>().Count()}, B${_inventory.EquippedB:x2}.");
        _entities.Clear();
        Buttons(40,[],[]);
        _player.WarpTo(new(72,40)); _player.Face(Vector2I.Down);
        Buttons(1,["attack"],["attack"]); Buttons(12,[],[]);
        _entities.ClearPhysicalPlayerItems();
        FailIf(_entities.Somaria.Active || _entities.Somaria.Weapon is not null,
            "Physical item clearing must immediately clear the Cane parent and reserved weapon, not wait for post-update.");
        Buttons(20,[],[]);
        FailIf(_entities.EntityAdapters<SomariaBlockRoomEntity>().Any(),
            "Cleared Cane must never reach its pending block-creation trigger.");
        Buttons(1,["attack"],["attack"]); Buttons(14,[],[]);
        var phasing=_entities.EntityAdapters<SomariaBlockRoomEntity>().Single().Block;
        _entities.ClearPhysicalPlayerItems();
        var textSource=_entities.TextActiveSource;
        try
        {
            _entities.TextActiveSource=()=>true;
            Buttons(2,[],[]);
            FailIf(phasing.State!=1 || phasing.Visible || phasing.Flags!=0x30,
                "Initialized marked Somaria must remain frozen and hidden during text.");
        }
        finally { _entities.TextActiveSource=textSource; }
        // ITEM$18 state1 deliberately does not test var2f bit5. It finishes
        // phase-in, then state3 restores the tile on its following update.
        Buttons(8,[],[]);
        FailIf(phasing.State!=1,"Marked phase-in must preserve its remaining animation duration.");
        Buttons(1,[],[]);
        FailIf(phasing.State!=3 || phasing.Visible || _currentRoom.GetMetatile(new(72,54))!=0xda,
            "Marked phase-in must still create its hidden solid tile before state3 processes deletion.");
        Buttons(1,[],[]);
        FailIf(!phasing.Finished || _currentRoom.GetMetatile(new(72,54))!=0x0c,
            "State3 must restore the marked phase-in block's floor on the next eligible update.");
        _entities.Clear();
        GD.Print("Validated normal A/B Cane input, movement lock, exact14/17/18/23 boundaries, repeat use, equal-priority restart and sword replacement through single/batched gameplay updates.");
    }
}
