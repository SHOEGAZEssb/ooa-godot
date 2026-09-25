using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class OldManRupeesRoomEntity : NpcCharacterRoomEntityAdapter,
    IFixedRoomEntity, IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity
{
    internal OldManRupeesRoomEntity(NpcCharacter npc)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        npc.InitializeCollisionRadii();
        npc.SetScriptButtonSensitive(true);
    }

    public NpcCharacter Npc => Entity;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.FaceLinkAndAnimateOneUpdate(frame.Player);
    public bool BlocksLink(Vector2 linkCenter) => Entity.BlocksLinkCenter(linkCenter);
    public NpcCharacter? FindTalkTarget(Player player) => Entity.CanTalkTo(player) ? Entity : null;
}
