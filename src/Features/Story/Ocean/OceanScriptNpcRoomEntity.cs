using Godot;
using System.Collections.Generic;

namespace oracleofages;

// Script and native tails are dispatched by their original interaction owner.
internal sealed class OceanScriptNpcRoomEntity(NpcCharacter actor)
    : NpcCharacterRoomEntityAdapter(actor, actor.SetTransitionDrawOffset),
      IFixedRoomEntity, IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity
{
    private bool _initialized;
    public NpcCharacter Npc => Entity;
    public bool RefreshStoryState => !_initialized;
    public void CompleteStoryInitialization()=>_initialized=true;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) { }
    public bool BlocksLink(Vector2 center) => Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) => Entity.CanTalkTo(player) ? Entity : null;
}
