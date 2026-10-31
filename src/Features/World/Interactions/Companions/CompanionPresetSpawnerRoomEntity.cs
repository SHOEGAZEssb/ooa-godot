using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// companionSpawner.s: the placed interaction remains allocated until dispatch,
// including when its game-specific preset predicate or reserved-slot check fails.
internal sealed class CompanionPresetSpawnerRoomEntity(int id, int subId, Action spawn)
    : INativeInteractionRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
      IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    internal int Id { get; } = id;
    internal int SubId { get; } = subId;
    public Node2D? PresentationNode => null;
    public bool Finished { get; private set; }
    public bool NativeInitializationPending => !Finished;
    public bool UpdatesDuringDialogue => true;
    public bool UpdatesDuringRoomEntityFreeze => true;
    public void SetTransitionDrawOffset(Vector2 offset) { }
    private void Initialize()
    {
        if (Finished) return;
        spawn();
        Finished = true;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Initialize();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { Initialize(); return ScreenTransitionPresentation.Hidden; }
}
