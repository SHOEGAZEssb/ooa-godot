using System.Collections.Generic;

namespace oracleofages;

internal sealed class MamamuDogRoomEntity : NpcCharacterRoomEntityAdapter, IFixedRoomEntity
{
    internal MamamuDogScriptHost Script { get; }
    internal MamamuDogRoomEntity(NpcCharacter actor, System.Func<byte> random, System.Func<bool> dialogueOpen)
        : base(actor, actor.SetTransitionDrawOffset) => Script = new(actor, random, dialogueOpen);
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Script.Update(frame.Player);
}
