using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// INTERAC_SHOPKEEPER $46:$00 keeps its broad $06/$14 counter collision and
/// script-selected animation instead of using ordinary generic-NPC defaults.
/// </summary>
internal sealed class LynnaShopkeeperRoomEntity
    : NpcCharacterRoomEntityAdapter, IVariableRoomEntity, IFixedRoomEntity,
        IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity, IPlayerRestriction
{
    public LynnaShopkeeperRoomEntity(
        NpcCharacter npc,
        LynnaShopDatabase database)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        npc.SetDialogue(0, string.Empty, canFace: false, database.TextboxPosition);
        npc.SetScriptButtonSensitive(true);
        npc.SetCollisionRadii(
            database.ShopkeeperRadiusY, database.ShopkeeperRadiusX);
        npc.SetScriptAnimation(database.Animation(InteractionId.Shopkeeper, database.Hidden ? 0 : 3));
    }

    public NpcCharacter Npc => Entity;
    public bool DisablesSword => false;
    public bool DisablesItems => true;
    public bool DisablesRingTransformations => true;

    public void Update(double delta, Player player) =>
        Entity.UpdateNpc(delta, player.Position);

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns)
    {
        Entity.PreventPlayerPassing(frame.Player);
        Entity.UpdateDrawPriority(frame.Player.Position);
    }

    public bool BlocksLink(Vector2 linkCenter) => Entity.BlocksLinkCenter(linkCenter);

    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanTalkTo(player)
            ? Entity
            : null;
}
