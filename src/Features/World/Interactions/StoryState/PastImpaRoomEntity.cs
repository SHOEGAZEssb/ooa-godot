using Godot;
using System.Collections.Generic;

namespace oracleofages;

// impaNpc.s:$01 selects its text and existence in state zero, then faces
// Link within the strict Manhattan radius $28 without the generic cooldown.
internal sealed class PastImpaRoomEntity(NpcCharacter npc)
    : NpcCharacterRoomEntityAdapter(npc, npc.SetTransitionDrawOffset),
        IFixedRoomEntity, IRoomBlocker, ITalkTarget, IOrdinaryNpcEntity
{
    private bool _initialized;
    private System.Func<bool>? _pendingDialogue;
    public bool TryQueueDialogue(System.Func<bool> tryShowDialogue)
    { _pendingDialogue = tryShowDialogue; return true; }
    public NpcCharacter Npc => Entity;
    public bool RefreshStoryState => !_initialized;
    public void CompleteStoryInitialization() { _initialized = true; Entity.ShowInitializedPresentation(); }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (_pendingDialogue?.Invoke() == true) _pendingDialogue = null;
        Entity.FaceLinkImmediatelyAndAnimateAsNpcOneUpdate(frame.Player);
    }
    public bool BlocksLink(Vector2 center) => Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) => Entity.CanTalkTo(player) ? Entity : null;
}
