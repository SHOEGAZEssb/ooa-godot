using System.Collections.Generic;

namespace oracleofages;

internal sealed class BeamosRoomEntity(BeamosCharacter enemy, bool countsAsEnemy)
    : RoomEntityAdapter<BeamosCharacter>(enemy, enemy.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomEnemyCounterEntity, IScreenTransitionPreloadRoomEntity
{
    public bool CountsAsEnemy => countsAsEnemy;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdateFrame(frame.ScentSeedTarget ?? frame.Player.Position, spawns);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Entity.InitializeState();
        return ScreenTransitionPresentation.Visible;
    }
}
