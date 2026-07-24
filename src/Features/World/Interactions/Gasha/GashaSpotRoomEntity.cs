using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class GashaSpotRoomEntity(GashaSpotInteraction interaction)
    : RoomEntityAdapter<GashaSpotInteraction>(
        interaction, interaction.SetTransitionDrawOffset),
        IFixedRoomEntity, ISwordHittableRoomEntity, IPlayerInteractable,
        IPlayerRestriction, IRoomEntityLifetime
{
    public bool DisablesSword => Entity.RestrictsPlayer;
    public bool DisablesItems => Entity.RestrictsPlayer;
    public bool DisablesMovement => Entity.RestrictsPlayer;
    public bool DisablesMenus => Entity.RestrictsPlayer;
    public bool DisablesRingTransformations => Entity.RestrictsPlayer;
    public bool Finished => Entity.Finished;

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Player);

    public bool ApplySwordHit(
        Rect2 hitbox,
        Vector2 sourcePosition,
        int damage,
        EnemyKnockbackStrength knockbackStrength,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.ApplySwordHit(hitbox, sourcePosition);

    public bool TryInteract(Player player) => Entity.TryInteract(player);
    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}
