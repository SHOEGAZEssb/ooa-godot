using Godot;
using System.Collections.Generic;

namespace Oracles;

// ghiniHarassingMoosh.s: each $73 actor initializes, animates, moves and runs
// its own script in its physical interaction slot, independently of the others.
internal sealed class MooshRescueGhiniRoomEntity(NpcCharacter actor, MooshRescueEvent owner)
    : NpcCharacterRoomEntityAdapter(actor, actor.SetTransitionDrawOffset), INativeInteractionRoomEntity,
      IFixedRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity,
      IUpdatesDuringRoomEntityFreeze, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private bool _initialized;
    internal int State => _initialized ? 1 : 0;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => true;
    public bool UpdatesDuringRoomEntityFreeze => true;
    private void Dispatch()
    {
        if (Finished) return;
        if (!_initialized)
        {
            _initialized = true;
            Finished = !owner.InitializeGhini(Entity);
            return;
        }
        Finished = !owner.AdvanceGhini(Entity);
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Dispatch();
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) => Dispatch();
    public override ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Dispatch();
        return !Finished && Entity.Visible ? ScreenTransitionPresentation.Visible : ScreenTransitionPresentation.Hidden;
    }
}
