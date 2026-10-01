using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// miscellaneous1.s: interaction6b_subid13/14. The state-0 WRAM writes
// change logical layout/collision only; the loaded background stays intact.
internal sealed class GoronBombStatueRoomEntity
    : NpcCharacterRoomEntityAdapter, IFixedRoomEntity, IRoomBlocker
{
    internal GoronBombStatueRoomEntity(
        NpcCharacter npc, OracleRoomData room, Func<long> animationTick)
        : base(npc, npc.SetTransitionDrawOffset)
    {
        // No collision-radius initializer runs: retain the cleared object
        // radii rather than the ordinary NPC adapter's $06/$06 defaults.
        npc.SetCollisionRadii(0, 0);
        npc.SetAnimationRate(0);
        room.SetPositionTileAndCollision(
            npc.Position, 0x00, 0x0f, animationTick(), preserveRenderedTile: true);
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) =>
        Entity.PushPlayerAwayAndUpdateDrawPriority(frame.Player);

    public bool BlocksLink(Vector2 linkCenter) => Entity.BlocksLinkCenter(linkCenter);
}
