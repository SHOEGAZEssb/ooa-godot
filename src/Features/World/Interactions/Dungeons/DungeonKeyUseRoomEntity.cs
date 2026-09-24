namespace oracleofages;

// INTERAC$17 does not set enabled bit$80. Only state0 remains eligible
// in bank0's restricted interaction dispatcher; visible80 is separate.
internal sealed class DungeonKeyUseRoomEntity(DungeonKeyUseEffect effect) :
    FixedEffectRoomEntityAdapter<DungeonKeyUseEffect>(effect),
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    public bool UpdatesDuringDialogue => !Entity.Initialized;
    public bool UpdatesDuringRoomEntityFreeze => !Entity.Initialized;

    public void UpdateDuringScreenTransition(RoomEntityFrame frame)
    {
        if (!Entity.Initialized) Entity.UpdateFrame();
    }
}
