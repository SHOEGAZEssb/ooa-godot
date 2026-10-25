using Godot;
using System;
using System.Collections.Generic;

namespace Oracles;

// miscPuzzles.s:$90:$1e waits for scrolling to finish, then sounds only
// for northward entry. The source retains direction across that wait.
internal sealed class EyePuzzleExitRoomEntity(OracleRuntimeState memory, Vector2I entryDirection, Action<int> sound)
    : IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IScreenTransitionPreloadRoomEntity,
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    public Node2D? PresentationNode => null;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => true;
    public bool UpdatesDuringRoomEntityFreeze => true;
    public void SetTransitionDrawOffset(Vector2 offset) { }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns) => ScreenTransitionPresentation.Hidden;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished || (memory.ReadWramByte(AgesWramAddress.wScrollMode) & 1) == 0) return;
        Finished = true;
        if (entryDirection == Vector2I.Up || entryDirection == Vector2I.Zero) sound(SoundId.SndSolvePuzzle);
    }
}
