using Godot;
using System.Collections.Generic;

namespace oracleofages;

// forestFairy.s:$05-$0a/$0c-$0d. State zero selects the table and
// visibility once; later updates never repeat the progression checks.
internal sealed class ForestNpcRoomEntity(NpcCharacter npc)
    : NpcCharacterRoomEntityAdapter(npc, npc.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity
{
    private bool _initialized;
    private System.Func<bool>? _pendingDialogue;
    public bool TryQueueDialogue(System.Func<bool> tryShowDialogue)
    { _pendingDialogue = tryShowDialogue; return true; }
    public NpcCharacter Npc => Entity;
    public bool RefreshStoryState => !_initialized;
    public void CompleteStoryInitialization()
    {
        _initialized = true;
        Entity.SetCollisionRadii(4, 4);
        Entity.SetScriptDrawOffset(new Vector2(0, -4));
        Entity.ShowInitializedPresentation();
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (!Entity.Active) return;
        if (_pendingDialogue?.Invoke() == true) _pendingDialogue = null;
        Entity.AdvanceAnimationUpdates(1);
        Entity.PreventPlayerPassing(frame.Player);
    }
    public bool BlocksLink(Vector2 center) => Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) => Entity.CanTalkTo(player) ? Entity : null;
}
