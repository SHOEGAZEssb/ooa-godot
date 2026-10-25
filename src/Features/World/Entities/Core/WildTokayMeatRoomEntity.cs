using Godot;
using System.Collections.Generic;

namespace Oracles;

internal sealed class WildTokayMeatRoomEntity(WildTokayMeat meat)
    : RoomEntityAdapter<WildTokayMeat>(meat, meat.SetTransitionDrawOffset),
        IFixedRoomEntity, INativeBraceletRoomEntity, IBraceletChildRoomEntity, IRoomEntityLifetime,
        IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    private System.Action<INativeBraceletRoomEntity>? _publish;
    public bool UpdatesDuringDialogue=>Entity.State==0;
    public bool UpdatesDuringRoomEntityFreeze=>Entity.State==0;
    public bool ReservedBraceletChildActive=>Entity.Thrown;
    public void WriteReservedBraceletAngle(int angle)=>Entity.ReservedThrow!.SetAngle(angle);
    public void BindGrabbablePublisher(System.Action<INativeBraceletRoomEntity> publish)=>_publish=publish;
    public void UpdateBraceletChild(Player player)=>Entity.UpdateBraceletChild(player);
    public void UpdateHeldPosition(Player player)=>Entity.UpdateHeldPosition(player);
    public bool Finished => Entity.Finished;
    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if(Entity.State==1)_publish?.Invoke(this);
        Entity.UpdateFrame(frame.Player,frame.Counter);
    }
    public bool TryUseBracelet(Player player, Vector2I releaseDirection) =>
        Entity.TryUseBracelet(player, releaseDirection);
}

internal sealed record WildTokayMeatSpawn : RoomEntitySpawn;
