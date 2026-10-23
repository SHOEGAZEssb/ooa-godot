using System.Collections.Generic;

namespace oracleofages;

internal sealed class RaftonRoomEntity : NpcTalkRoomEntity, IScreenTransitionPreloadRoomEntity
{
    internal RaftonRoomEntity(RaftonCharacter rafton) : base(rafton, scriptSensitive: true) =>
        NpcCharacterRoomEntityAdapter.InitializePresentation(rafton);

    public ScreenTransitionPresentation PrepareForScreenTransition(
        ICollection<RoomEntitySpawn> spawns) =>
        NpcCharacterRoomEntityAdapter.PreparePresentation(Npc);
}
