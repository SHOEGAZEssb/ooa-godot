using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class RosaNpcRoomEntity(NpcCharacter npc, InventoryState inventory,
    TokayAttachedVisualRoomEntity? shovel)
    : NpcCharacterRoomEntityAdapter(npc, npc.SetTransitionDrawOffset),
      IFixedRoomEntity, IOrdinaryNpcEntity, IRoomBlocker, ITalkTarget
{
    internal TokayAttachedVisualRoomEntity? Shovel => shovel;
    internal bool ScriptOwnsNativeUpdate { get; set; }
    public NpcCharacter Npc => Entity;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!ScriptOwnsNativeUpdate) RunNativeUpdate(frame.Player);
    }
    internal void RunNativeUpdate(Player player)
    {
        if (inventory.HasTreasure(TreasureDatabase.TreasureShovel))
            Entity.FaceLinkAndAnimateOneUpdate(player);
        else
            Entity.AnimateAsNpcOneUpdate(player);
    }
    public bool BlocksLink(Vector2 center) => Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) => Entity.CanTalkTo(player) ? Entity : null;
}
