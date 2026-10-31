using Godot;
using System.Collections.Generic;

namespace Oracles;

internal sealed class SharedEnemyCoreRoomEntity(SharedEnemyCoreCharacter character)
    : RoomEntityAdapter<SharedEnemyCoreCharacter>(character,character.SetTransitionDrawOffset), IFixedRoomEntity
{
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Entity.Advance();
}
