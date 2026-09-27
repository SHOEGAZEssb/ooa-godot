using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class PoeRoomEntity(PoeCharacter poe, PoeEventRecord record, OracleSaveData? saveData)
    : RoomEntityAdapter<PoeCharacter>(poe, poe.SetTransitionDrawOffset),
        IRoomBlocker, ITalkTarget, IScreenTransitionPreloadRoomEntity
{
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns) =>
        PrepareForScreenTransition(null, spawns);

    public ScreenTransitionPresentation PrepareForScreenTransition(Player? player, ICollection<RoomEntitySpawn> spawns)
    {
        Entity.PrepareTransition(record, saveData, player);
        return Entity.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }

    public bool BlocksLink(Vector2 linkCenter) =>
        !Entity.Disappearing && !Entity.NoFace && Entity.BlocksLinkCenter(linkCenter);

    public NpcCharacter? FindTalkTarget(Player player) =>
        !Entity.Disappearing && !Entity.NoFace &&
        Entity.CanScriptTalkTo(player, NpcCharacter.CollisionRadius,
            NpcCharacter.CollisionRadius, NpcCharacter.AButtonPointOffset) ? Entity : null;
}
