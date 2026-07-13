using Godot;
using System.Collections.Generic;

namespace oracleofages;

// Compatibility views for existing gameplay and validation callers. Entity
// ownership and lifecycle are generic; new entity types can use Entities<T>()
// without adding another manager field or behavior branch.
public sealed partial class RoomEntityManager
{
    public List<NpcCharacter> Npcs => Entities<NpcCharacter>();
    public IReadOnlyList<NpcCharacter> OutgoingNpcs => OutgoingEntities<NpcCharacter>();
    public List<KeeseCharacter> Keese => Entities<KeeseCharacter>();
    public IReadOnlyList<KeeseCharacter> OutgoingKeese => OutgoingEntities<KeeseCharacter>();
    public List<OctorokCharacter> Octoroks => Entities<OctorokCharacter>();
    public IReadOnlyList<OctorokCharacter> OutgoingOctoroks => OutgoingEntities<OctorokCharacter>();
    public IReadOnlyList<OctorokRockProjectile> OctorokRocks => Entities<OctorokRockProjectile>();
    public IReadOnlyList<OctorokRockProjectile> OutgoingOctorokRocks =>
        OutgoingEntities<OctorokRockProjectile>();
    public List<ZolCharacter> Zols => Entities<ZolCharacter>();
    public IReadOnlyList<ZolCharacter> OutgoingZols => OutgoingEntities<ZolCharacter>();
    public List<GelCharacter> Gels => Entities<GelCharacter>();
    public IReadOnlyList<GelCharacter> OutgoingGels => OutgoingEntities<GelCharacter>();
    public IReadOnlyList<EnemyDeathPuffEffect> DeathPuffs => Entities<EnemyDeathPuffEffect>();
    public IReadOnlyList<EnemyDeathPuffEffect> OutgoingDeathPuffs =>
        OutgoingEntities<EnemyDeathPuffEffect>();
    public IReadOnlyList<KillEnemyPuffEffect> KillPuffs => Entities<KillEnemyPuffEffect>();
    public IReadOnlyList<KillEnemyPuffEffect> OutgoingKillPuffs =>
        OutgoingEntities<KillEnemyPuffEffect>();
    public IReadOnlyList<ItemDropEffect> ItemDrops => Entities<ItemDropEffect>();
    public IReadOnlyList<ItemDropEffect> OutgoingItemDrops => OutgoingEntities<ItemDropEffect>();

    internal GelCharacter SpawnGel(Vector2 position, string name = "Gel") =>
        Spawn<GelCharacter>(new GelSpawn(position, name));
    internal OctorokRockProjectile SpawnOctorokRock(Vector2 position, int angle) =>
        Spawn<OctorokRockProjectile>(new OctorokRockSpawn(position, angle));
    internal KillEnemyPuffEffect SpawnKillEnemyPuff(Vector2 position) =>
        Spawn<KillEnemyPuffEffect>(new KillEnemyPuffSpawn(position));
    internal ItemDropEffect SpawnItemDrop(int subId, Vector2 position) =>
        Spawn<ItemDropEffect>(new ItemDropSpawn(subId, position));
}
