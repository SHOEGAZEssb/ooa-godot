using Godot;
using System.Collections.Generic;

namespace Oracles;

// The original $32 slot owns facing, script execution and its speedZ tail.
internal sealed class SeaFairyNpcRoomEntity(NpcCharacter actor)
    : NpcCharacterRoomEntityAdapter(actor, actor.SetTransitionDrawOffset),
      IFixedRoomEntity, IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity
{
    private bool _initialized;
    internal SeaFairyEvent? Owner { get; set; }
    public NpcCharacter Npc => Entity;
    public bool RefreshStoryState => !_initialized;
    public void CompleteStoryInitialization() => _initialized = true;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Owner?.UpdateOctorokSlot();
    public bool BlocksLink(Vector2 center) => Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) => Entity.CanTalkTo(player) ? Entity : null;
}
