namespace oracleofages;

// Migrate only projectiles whose post-object handoff is implemented. Other
// hostile PART adapters retain their own contact dispatch boundary.
internal sealed class PostObjectHostileProjectileRoomEntity<TProjectile>(TProjectile projectile)
    : HostileProjectileRoomEntity<TProjectile>(projectile), IPostObjectLinkContactRoomEntity
    where TProjectile : TransitionOffsetNode2D, IHostileProjectile, ILinkContactEntity
{
    public void HandleLinkContact(Player player) => Entity.HandleLinkContact(player);
}
