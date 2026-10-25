using Godot;
using System.Collections.Generic;

namespace Oracles;

// common/interactions/twinrovaFlame.s. Enabled bit 7 admits every initialized
// flame even during text and object masks; state zero returns before animating.
internal sealed partial class TwinrovaFlameRoomEntity : DungeonInteractionVisualEntity,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IScreenTransitionPreloadRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly OracleSaveData _save;
    private readonly int _group, _room;
    private readonly int _saveRoom, _saveAdd;
    internal int Subid { get; }
    internal int State { get; private set; }
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => true;
    public bool UpdatesDuringRoomEntityFreeze => true;
    internal void ApplyNativePalette(IReadOnlyDictionary<int, Color[]>? palettes) => SetNativePalette(palettes);
    internal TwinrovaFlameRoomEntity(OracleSaveData save, int group, int room, int subid,
        (Vector2 Position, DungeonInteractionVisual Visual, int SaveRoom, int SaveAdd) data)
    {
        _save = save; _group = group; _room = room; Subid = subid;
        _saveRoom = data.SaveRoom; _saveAdd = data.SaveAdd;
        InitializeVisual(data.Visual, data.Position, positionedOam: true);
        ZIndex = ObjectDrawPriority.FromVisible(0xc2); Visible = false;
    }
    private void Initialize()
    {
        if (State != 0) return;
        State = Subid >= 6 ? 2 : 1;
        if (Subid >= 3)
        {
            if (_save.HasRoomFlag(_group, _room, 0x80)) { Finished = true; return; }
            // HL is now the save flag page. LD L,animCounter uses absolute
            // interaction offset $60. E still points at initial OAM high
            // byte $5f, so the added value is derived from $4a, not subid.
            _save.AddRoomFlagByte(_group, _saveRoom, _saveAdd);
        }
        else AddAnimationCounter((Subid & 3) * 8);
        Visible = true;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (State == 0) { Initialize(); return; }
        AdvanceAnimation();
        if (State == 2) Visible = (frame.Counter & 1) != 0;
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        Initialize();
        return Finished ? ScreenTransitionPresentation.Hidden : ScreenTransitionPresentation.Visible;
    }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
}
