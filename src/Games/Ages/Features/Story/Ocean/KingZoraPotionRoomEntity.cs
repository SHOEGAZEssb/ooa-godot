using System.Collections.Generic;

namespace Oracles;

// kingZora.s:@subid2: graphics-only state 0, then exactly $24 decrements.
internal sealed class KingZoraPotionRoomEntity(NpcCharacter actor)
    : NpcCharacterRoomEntityAdapter(actor, actor.SetTransitionDrawOffset), IFixedRoomEntity, IRoomEntityLifetime
{
    private bool _initialized;
    private int _counter = 0x24;
    public bool Finished { get; private set; }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized)
        {
            _initialized = true;
            Entity.SetBlocksLink(false);
            Entity.SetFixedDrawPriority(ObjectDrawPriority.InFrontOfLinkZIndex);
            Entity.ShowInitializedPresentation();
            return;
        }
        if (--_counter == 0) Finished = true;
    }
}

internal sealed record KingZoraPotionSpawn(NpcRecord Record) : RoomEntitySpawn;
