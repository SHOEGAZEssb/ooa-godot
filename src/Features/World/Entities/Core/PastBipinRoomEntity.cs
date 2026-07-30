using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Native fixed-update owner for past INTERAC_BIPIN $28:$0a. Its loaded
/// script supplies A-button sensitivity, while the interaction handler
/// advances animation and resolves Link collision and priority every update.
/// </summary>
internal sealed class PastBipinRoomEntity
    : NpcCharacterRoomEntityAdapter, IFixedRoomEntity, IRoomBlocker,
        ITalkTarget, IOrdinaryNpcEntity
{
    public PastBipinRoomEntity(NpcCharacter npc)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        npc.SetScriptButtonSensitive(true);
    }

    public NpcCharacter Npc => Entity;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.Active)
            return;

        Entity.AnimateAsNpcOneUpdate(frame.Player);
    }

    public bool BlocksLink(Vector2 linkCenter) =>
        Entity.BlocksLinkCenter(linkCenter);

    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanTalkTo(player) ? Entity : null;
}
