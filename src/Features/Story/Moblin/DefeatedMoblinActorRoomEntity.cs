namespace oracleofages;

// The event dispatches these live native slots, retaining independent scripts.
internal sealed class DefeatedMoblinActorRoomEntity(NpcCharacter actor)
    : NpcCharacterRoomEntityAdapter(actor,actor.SetTransitionDrawOffset), IRoomEntityLifetime, IRoomEntityUpdateFreeze
{
    public bool Finished=>!Entity.Active;
    public bool FreezesRoomEntities=>Entity.Active;
    public bool FreezesInteractions=>false; // wDisabledObjects=$80
}

internal sealed record DefeatedMoblinActorSpawn(NpcRecord Record):RoomEntitySpawn;
