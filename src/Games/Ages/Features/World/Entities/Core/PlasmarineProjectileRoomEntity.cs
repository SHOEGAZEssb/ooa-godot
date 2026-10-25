using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// PART$43's native active mask admits only Link ($00). Weapons, shields,
// Switch Hook and seeds therefore continue through the common collision scan.
internal sealed class PlasmarineProjectileRoomEntity(PlasmarineProjectile part)
    :RoomEntityAdapter<PlasmarineProjectile>(part,part.SetTransitionDrawOffset),IFixedRoomEntity,
        IRoomEntityLifetime,INativePartHealthRoomEntity,IPostObjectLinkContactRoomEntity,
        IUpdatesDuringDialogueRoomEntity,IUpdatesDuringRoomEntityFreeze
{
    private readonly PlasmarineProjectileDatabase _data=PlasmarineProjectileDatabase.Shared;
    public bool Finished=>Entity.Finished;
    public bool UpdatesDuringDialogue=>Entity.State==0;
    public bool UpdatesDuringRoomEntityFreeze=>Entity.State==0;
    public void ClearHealthAndCollision()=>Entity.PublishCollision();
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)=>
        Entity.UpdateFrame(frame.ScentSeedTarget??frame.Player.Position,frame.Counter);
    public void HandleLinkContact(Player player)
    {
        if(!Entity.CollisionEnabled || Entity.InvincibilityCounter!=0 || !player.NativeObjectVulnerable ||
            !player.EnemyContactHeightOverlaps(0) || !player.OverlapsEnemyCollision(Entity.CollisionBounds))return;
        var collision=_data.Collision(ItemCollisionType.Link);
        if(!collision.Enabled || collision.Effect!=CollisionEffect.DamageLink)
            throw new NotSupportedException("plasmarineProjectile.s: PART$43 requires active Link effect$02.");
        if(player.ApplyEnemyContactDamage(Entity.Position,(256-_data.Properties[3])/2,RingDamageSource.Generic,34,15))Entity.PublishCollision();
    }
}
