using Godot;
using System.Linq;

namespace oracleofages;

internal sealed class GoronTargetCartsController(GoronCaveScriptHost host)
{
    private RoomEventContext Context=>host.Context;
    private OracleRuntimeState Wram=>Context.Entities.RuntimeState;
    internal void Initialize()
    {
        if(host.Actor.Record.Var03!=0) return;
        Wram.SetWramByte(0xcfdf,0); Wram.SetWramByte(0xcfdb,0);
        if(host.RoomFlagSet(0x80)) LoadCrystals(true);
    }
    internal void LoadCrystals(bool reload)
    {
        if(!reload) Wram.SetWramByte(0xcfd4,(byte)(host.RoomFlagSet(0x20)?1+(Context.Entities.NextRandomValue()&1):0));
        for(int i=0;i<5;i++)
            if(!reload||(Wram.ReadWramByte(0xcfdd)&(1<<i))==0)
            {
                if(!Context.Entities.EnemySlotAvailable) return;
                if(!Context.Entities.TrySpawnEnemy(0x63,i,Vector2.Zero,"scriptHelper.s:goron_targetCarts_loadCrystals",out string error))
                    throw new System.InvalidOperationException(error);
            }
    }
    internal void DeleteCrystals()
    { foreach(var crystal in Context.Entities.EntityAdapters<TargetCartCrystalRoomEntity>()) crystal.Finish(); }
    internal void ConfigureInventory()
    {
        var inv=Context.Inventory;
        Wram.SetWramByte(0xcfd7,(byte)inv.EquippedB); Wram.SetWramByte(0xcfd8,(byte)inv.EquippedA);
        Wram.SetWramByte(0xcfd9,(byte)inv.ScentSeeds); Wram.SetWramByte(0xcfda,(byte)inv.ShooterSelectedSeeds);
        inv.SetScriptedEquippedItems(inv.EquippedA==0x0f?0:0x0f,inv.EquippedA==0x0f?0x0f:0);
        inv.SetScentSeedsFromScript(0x99);
        inv.SelectShooterSeeds(1);
    }
    internal void RestoreInventory()
    {
        Context.Inventory.SetScriptedEquippedItems(Wram.ReadWramByte(0xcfd7),Wram.ReadWramByte(0xcfd8));
        Context.Inventory.SetScentSeedsFromScript(Wram.ReadWramByte(0xcfd9));
        Context.Inventory.SelectShooterSeeds(Wram.ReadWramByte(0xcfda));
    }
    internal void SpawnCart()
    {
        if(!Context.Entities.InteractionSlotAvailable) return;
        int slot=MinecartRuntimeState.FinishRide(Wram,Context.Rooms.CurrentRoom.Id,new(0x38,0x78));
        Context.Entities.Spawn<MinecartRoomEntity>(new GoronMinecartSpawn(new(slot,Context.Rooms.CurrentRoom.Id,0x78,0x38,-1,false)));
    }
    internal void DeleteCart()
    {
        foreach(var cart in Context.Entities.Entities<MinecartRoomEntity>()) cart.DeleteForScript();
        MinecartRuntimeState.Reset(Wram,[]);
    }
    internal void Begin()
    {
        foreach(int address in new[]{0xcfdb,0xcfdd,0xcfde,0xcfdc}) Wram.SetWramByte(address,0);
        host.OrRoomFlag(0x80);
    }
    internal void End()=>Context.Rooms.SaveData.SetRoomFlag(Context.Rooms.ActiveGroup,Context.Rooms.CurrentRoom.Id,0x80,false);
    internal void Cancel()
    {
        // The course crosses two rooms. Its saved equipment belongs to the
        // WRAM session, and must survive the normal $5:d8 <-> $5:d9 scroll.
        if(Context.Rooms.ActiveGroup==5&&Context.Rooms.CurrentRoom.Id is 0xd8 or 0xd9) return;
        if(!Context.Rooms.SaveData.HasRoomFlag(5,0xd8,0x80)) return;
        RestoreInventory();
        Context.Rooms.SaveData.SetRoomFlag(5,0xd8,0x80,false);
    }
}
