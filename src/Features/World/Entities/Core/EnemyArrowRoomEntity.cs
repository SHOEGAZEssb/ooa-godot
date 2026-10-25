using System.Collections.Generic;

namespace Oracles;

internal sealed class EnemyArrowRoomEntity(EnemyArrowProjectile arrow)
    : HostileProjectileRoomEntity<EnemyArrowProjectile>(arrow), IPostObjectLinkContactRoomEntity,
        IScreenTransitionPreloadRoomEntity
{
    public void HandleLinkContact(Player player) => ((ILinkContactEntity)Entity).HandleLinkContact(player);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Entity.InitializeFlight();
        return ScreenTransitionPresentation.Visible;
    }
}
