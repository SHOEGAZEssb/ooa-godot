using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class MamamuRoomEntity(NpcCharacter actor)
    : NpcCharacterRoomEntityAdapter(actor, actor.SetTransitionDrawOffset), IRoomBlocker, ITalkTarget, IFixedRoomEntity
{
    internal MamamuEvent? Script { get; set; }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Script?.AdvanceObjectFrame();
    public bool BlocksLink(Vector2 center) => Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) => Entity.CanTalkTo(player) ? Entity : null;
}
