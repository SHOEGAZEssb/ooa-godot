using Godot;
using System;

namespace oracleofages;

internal sealed class SomariaController(Node world,RoomSession rooms,RoomEntityManager entities,Action<int> sound)
{
    private readonly SomariaSwingDatabase _swing=new();
    private readonly SomariaPlacementDatabase _placement=new();
    private readonly SomariaGraphicsDatabase _graphics=new();
    internal SomariaParentAnimation? Parent { get; private set; }
    internal SomariaWeapon? Weapon { get; private set; }
    internal bool Active=>Parent?.Active==true;
    internal void Begin(Player player,bool underwater)
    {
        Cancel();
        Parent=new(_swing,underwater,player.CompanionRideActive||player.MinecartRideActive,player.RaftRideActive);
        Weapon=new(_swing,_placement,_graphics,player.Position,player.EnemyContactZ);
        world.AddChild(Weapon);
        player.NotifyParentItemAnimationStarted(InventoryState.ItemSomaria);
    }
    internal void UpdateParent() => Parent?.Update();
    internal void UpdateItem(Player player,bool frozen) => Weapon?.UpdateItem(frozen,Active?Parent!.Parameter:0,player.Position,
        CarriedObjectMotion.DirectionIndex(player.FacingVector),player.EnemyContactZ,
        player.TransferSwordCollisionKnockback,sound,entities.MarkPreviousSomariaBlock,
        (point,z)=>entities.TryCreateSomariaBlock(player,rooms.ActiveGroup,point,z));
    internal void UpdatePost(Player player)
    {
        // postUpdate.s reads shared wLinkRaisedFloorOffset ($cc69).
        Weapon?.UpdatePost(Active?InventoryState.ItemSomaria:0,Active?Parent!.Parameter:0,
            player.Position,CarriedObjectMotion.DirectionIndex(player.FacingVector),player.EnemyContactZ,
            unchecked((sbyte)entities.RuntimeState.ReadWramByte(0xcc69)));
        if(Weapon?.Finished==true) { Weapon.Free(); Weapon=null; }
    }
    internal void ClearParent() => Parent?.Cancel();
    internal void Cancel()
    {
        Parent?.Cancel(); Parent=null;
        if(Weapon is not null) { Weapon.Free(); Weapon=null; }
    }
}
