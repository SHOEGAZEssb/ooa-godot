using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class TektiteRoomEntity : CombatEnemyRoomEntityAdapter<TektiteCharacter>,
    IFixedRoomEntity, IScreenTransitionPreloadRoomEntity, ISeedHeightAwareHittableRoomEntity
{
    internal TektiteRoomEntity(TektiteCharacter enemy, EnemyCombatSourceDescriptor source,
        Action<int> soundRequested)
        : base(enemy, enemy.SetTransitionDrawOffset,
            EnemyCombatDescriptor.WithContactDamage(source, enemy, enemy.Record.DamageQuarters,
                enemy.TakeSwordHit, enemy.TakeBurnHit, enemy.ApplySwordKnockback,
                soundRequested, EnemySwordResponse.Knockback), collisionZ: () => enemy.ZHigh) { }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player.EnemyContactPosition);

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns) =>
        Entity.PrepareForScreenTransition();

    public override void HandleLinkContact(Player player)
    {
        if (RoomEntityManager.ObjectCollisionZOverlaps(Entity.ZHigh, player.EnemyContactZ, 7))
            base.HandleLinkContact(player);
    }

    public SeedHitResult ApplySeedHitAtHeight(Godot.Rect2 hitbox, Godot.Vector2 sourcePosition,
        int sourceZ, int seedItem, ICollection<RoomEntitySpawn> spawns) =>
        RoomEntityManager.ObjectCollisionZOverlaps(Entity.ZHigh, sourceZ, 7)
            ? ApplySeedHit(hitbox, sourcePosition, seedItem, spawns) : SeedHitResult.None;
}
