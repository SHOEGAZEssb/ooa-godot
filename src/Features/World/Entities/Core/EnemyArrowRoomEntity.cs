using System.Collections.Generic;

namespace oracleofages;

internal sealed class EnemyArrowRoomEntity(EnemyArrowProjectile arrow)
    : HostileProjectileRoomEntity<EnemyArrowProjectile>(arrow), IPostObjectLinkContactRoomEntity,
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    public bool UpdatesDuringDialogue => Entity.State == HostileProjectileState.Initializing;
    public bool UpdatesDuringRoomEntityFreeze => Entity.State == HostileProjectileState.Initializing;
    public void HandleLinkContact(Player player) => ((ILinkContactEntity)Entity).HandleLinkContact(player);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Entity.InitializeFlight();
        return ScreenTransitionPresentation.Visible;
    }
}
