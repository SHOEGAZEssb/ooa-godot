using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class GanonRevivalRoomEntity : CombatEnemyRoomEntityAdapter<GanonRevivalCharacter>,IFixedRoomEntity,INativeEnemySlotRoomEntity
{
    internal GanonRevivalCharacter Actor=>Entity;
    internal int Slot {get;private set;}
    internal GanonRevivalRoomEntity(GanonRevivalCharacter actor):base(actor,actor.SetTransitionDrawOffset,
        EnemyCombatDescriptor.Special(EnemyCombatComponent.WithContactDamage(()=>actor.IsDead,()=>actor.CollisionBounds,
            (_,_)=>false,_=>false,actor.OverlapsLink,()=>actor.Position,0,()=>null),false,0,
            ()=>RoomEnemyOutcome.SilentDeletion(false))) {}
    public override int DimitriCollisionType=>0x60;
    public override int DimitriCollisionMode=>0;
    public void BindEnemySlot(int slot,Func<int,IRoomEntity?> resolve){Slot=slot;Entity.Bind(resolve);}
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns)=>Entity.UpdateFrame(frame);
}
