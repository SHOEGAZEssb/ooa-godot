using Godot;
using System.Collections.Generic;

namespace Oracles;

internal sealed class PoeRoomEntity(PoeCharacter poe, PoeEventRecord record, OracleSaveData? saveData)
    : NpcCharacterRoomEntityAdapter(poe, poe.SetTransitionDrawOffset),
        IRoomBlocker, ITalkTarget, IScreenTransitionPreloadRoomEntity
{
    public new ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns) =>
        PrepareForScreenTransition(null, spawns);

    public ScreenTransitionPresentation PrepareForScreenTransition(Player? player, ICollection<RoomEntitySpawn> spawns)
    {
        poe.PrepareTransition(record, saveData, player);
        return poe.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }

    public bool BlocksLink(Vector2 linkCenter) =>
        !poe.Disappearing && !poe.NoFace && poe.BlocksLinkCenter(linkCenter);

    public NpcCharacter? FindTalkTarget(Player player) =>
        !poe.Disappearing && !poe.NoFace &&
        poe.CanScriptTalkTo(player, NpcCharacter.CollisionRadius,
            NpcCharacter.CollisionRadius, NpcCharacter.AButtonPointOffset) ? poe : null;
}
