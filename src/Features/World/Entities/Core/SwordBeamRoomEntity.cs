using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class SwordBeamRoomEntity(SwordBeamEffect beam)
    : RoomEntityAdapter<SwordBeamEffect>(beam, beam.SetTransitionDrawOffset),
        IFixedRoomEntity, IPlayerProjectileRoomEntity, IRoomEntityLifetime
{
    public bool Finished => Entity.Finished;
    public bool CollisionEnabled => Entity.CollisionEnabled;
    public Rect2 CollisionBounds => Entity.CollisionBounds;
    public int Damage => Entity.Damage;
    public void UpdateFrame(
        RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.Counter, spawns);
    public void OnEnemyCollision(ICollection<RoomEntitySpawn> spawns) =>
        Entity.OnEnemyCollision(spawns);
    public void OnFinished(ICollection<RoomEntitySpawn> spawns) { }
}
