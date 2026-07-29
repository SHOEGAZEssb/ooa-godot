using Godot;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>
/// Fixed-update owner for room 2:2f's Postman. postman.s faces Link until
/// Interaction.var3f is set, then animates relative to SPEED_200 while leaving.
/// </summary>
internal sealed class PostmanRoomEntity
    : RoomEntityAdapter<PostmanCharacter>, IFixedRoomEntity, IRoomBlocker,
        ITalkTarget, IOrdinaryNpcEntity
{
    public PostmanRoomEntity(PostmanCharacter postman)
        : base(postman, postman.SetTransitionDrawOffset)
    {
    }

    public NpcCharacter Npc => Entity;

    public void UpdateFrame(
        RoomEntityFrame frame,
        ICollection<RoomEntitySpawn> spawns) =>
        Entity.UpdatePostman(frame.Player);

    public bool BlocksLink(Vector2 linkCenter) =>
        Entity.BlocksLinkCenter(linkCenter);

    public NpcCharacter? FindTalkTarget(Player player) =>
        Entity.CanTalkTo(player) ? Entity : null;
}
