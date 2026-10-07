using Godot;
using System.Collections.Generic;

namespace oracleofages;

// miscPuzzles_subid0f, INTERAC$90:$0f. The encounter survives room reloads
// through the authoritative WRAM owner. In particular this command does not
// clear $cfd7: the underwater body can retain its surface-position fix request.
internal sealed partial class OctogonEncounterInitializerRoomEntity(
    IReadOnlyList<KeyValuePair<int,byte>> writes,OracleRuntimeState memory)
    : Node2D, IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
        IScreenTransitionPreloadRoomEntity
{
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => !Finished;
    public bool UpdatesDuringRoomEntityFreeze => !Finished;
    public void SetTransitionDrawOffset(Vector2 offset) { }
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns) => Initialize();
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Initialize();
        return ScreenTransitionPresentation.Hidden;
    }
    private void Initialize()
    {
        if (Finished) return;
        foreach (var write in writes) memory.SetWramByte(write.Key,write.Value);
        Visible = false;
        Finished = true;
    }
}
