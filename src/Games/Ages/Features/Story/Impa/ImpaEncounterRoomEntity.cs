using Godot;
using System.Collections.Generic;

namespace Oracles;

// The encounter host drives $31:$00. Keep its placed allocation until its
// native visit even when room-entry predicates already hide the presenter.
internal sealed class ImpaEncounterRoomEntity(NpcCharacter npc)
    : RoomEntityAdapter<NpcCharacter>(npc, npc.SetTransitionDrawOffset), INativeInteractionRoomEntity,
      IFixedRoomEntity, IVariableRoomEntity, IRoomEntityLifetime, IRoomBlocker, ITalkTarget,
      IOrdinaryNpcEntity, IScreenTransitionPreloadRoomEntity,
      IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private bool _initialized;
    private bool _positionInitialized;
    internal int State => _initialized ? 1 : 0;
    public bool Finished { get; private set; }
    public NpcCharacter Npc => Entity;
    public bool RefreshPosition => !_positionInitialized;
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;
    public void CompleteStoryInitialization() => _positionInitialized = true;
    public bool BlocksLink(Vector2 center) => Entity.BlocksLinkCenter(center);
    public NpcCharacter? FindTalkTarget(Player player) => Entity.CanTalkTo(player) ? Entity : null;
    public void Update(double delta, Player player) => Entity.UpdateNpc(delta, player.Position, initializeVisibility: false);
    private void Dispatch()
    {
        _initialized = true;
        Finished = !Entity.Active;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Dispatch();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { Dispatch(); return Entity.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden; }
}
