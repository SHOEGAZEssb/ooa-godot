using System.Collections.Generic;

namespace oracleofages;

internal sealed class CandleFlameRoomEntity(CandleFlame flame)
    : RoomEntityAdapter<CandleFlame>(flame, flame.SetTransitionDrawOffset), IFixedRoomEntity,
        IRoomEntityLifetime, IPostObjectLinkContactRoomEntity, IObjectCollisionHeightRoomEntity,
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    public bool Finished => Entity.Finished;
    public int CollisionZ => Entity.ZHigh;
    public bool UpdatesDuringDialogue => Entity.State == 0;
    public bool UpdatesDuringRoomEntityFreeze => Entity.State == 0;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame();
    public void HandleLinkContact(Player player)
    {
        if (Entity.Finished || Entity.State == 0 || Entity.PendingCollision || !player.NativeObjectVulnerable ||
            !player.EnemyContactHeightOverlaps(CollisionZ)) return;
        // partActiveCollisions[$36] admits only Link. The parent's item
        // responses remain separate; PART$36 ignores JUST_HIT and health.
        if (player.OverlapsEnemyCollision(Entity.CollisionBounds, CollisionZ) &&
            player.ApplyEnemyContactDamage(Entity.Position, (0x100 - EnemyBehaviorTables.Shared.Candle.FlameData[3].Value) / 2,
                RingDamageSource.Generic, 34, 15)) Entity.PublishCollision();
    }
}
