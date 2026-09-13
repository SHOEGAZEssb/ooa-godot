using System.Collections.Generic;

namespace oracleofages;

internal sealed class MoldormSpawnerRoomEntity(MoldormSpawnerCharacter spawner)
    : RoomEntityAdapter<MoldormSpawnerCharacter>(spawner, spawner.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEntityLifetime, IRoomEnemyCounterEntity,
        IScreenTransitionPreloadRoomEntity, IUpdatesDuringDialogueRoomEntity,
        IUpdatesDuringRoomEntityFreeze
{
    public bool Finished => Entity.IsDead;
    public bool CountsAsEnemy => Entity.CountsAsEnemy;
    public bool UpdatesDuringDialogue => Entity.State == 0;
    public bool UpdatesDuringRoomEntityFreeze => Entity.State == 0;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Entity.UpdateFrame(spawns);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        if (Entity.State == 0) Entity.UpdateFrame(spawns);
        return Entity.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }
}
