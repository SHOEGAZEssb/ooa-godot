using Godot;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class PirateCaptainRoomEntity(NpcCharacter actor, RoomEntityManager owner)
    : NpcCharacterRoomEntityAdapter(actor, actor.SetTransitionDrawOffset),
      IFixedRoomEntity, IRoomBlocker, ITalkTarget, IUpdatesDuringDialogueRoomEntity
{
    private bool _initialized;
    public bool UpdatesDuringDialogue => !_initialized;
    public bool BlocksLink(Vector2 center) => _initialized && Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) => _initialized && Entity.CanTalkTo(player) ? Entity : null;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!_initialized)
        {
            _initialized = true;
            Entity.SetFixedDrawPriority(ObjectDrawPriority.BehindLinkZIndex);
            owner.InitializePirateCaptain(Entity);
            return;
        }
        owner.UpdatePirateCaptain();
        Entity.AdvanceAnimationUpdates(1);
    }
}
