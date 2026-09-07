using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class ForestHintFairyRoomEntity : NpcCharacterRoomEntityAdapter,
    IFixedRoomEntity, IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity
{
    public NpcCharacter Npc => Entity;
    internal ForestHintFairyRoomEntity(NpcCharacter npc) : base(npc, npc.SetTransitionDrawOffset)
    {
        npc.SetCollisionRadii(4, 4);
        npc.SetScriptDrawOffset(new Vector2(0, -4));
        npc.SetAnimationRate(1);
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.FaceLinkAndAnimateOneUpdate(frame.Player, preventPassing: false);
    public bool BlocksLink(Vector2 center) => Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) => Entity.CanTalkTo(player) ? Entity : null;
}
