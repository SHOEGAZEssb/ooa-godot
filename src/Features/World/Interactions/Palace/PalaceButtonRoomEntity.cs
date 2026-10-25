using Godot;
using System.Collections.Generic;

namespace Oracles;

internal sealed class PalaceButtonRoomEntity(RoomEntityManager owner, RoomSession rooms, PalaceButtonRecord record)
    : IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
      IScreenTransitionPreloadRoomEntity
{
    public Node2D Node { get; } = new() { Position = record.Position, Visible = false };
    internal int State { get; private set; }
    internal int Counter { get; private set; }
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;
    public void SetTransitionDrawOffset(Vector2 offset) { }
    private void Initialize()
    {
        if (State != 0) return;
        Finished = rooms.SaveData.HasRoomFlag(record.Group, record.Room, 0x80);
        State = 1;
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    {
        // ambisPalaceButton.s state 0 checks persistence and initializes its
        // collision radius; Link contact belongs to the frozen state 1.
        Initialize();
        return ScreenTransitionPresentation.Hidden;
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (State == 0)
        { Initialize(); return; }
        if (State == 1)
        {
            Player link = frame.Player;
            if (link.IsDying || link.GrabStateActive || !link.IsGroundedForFloorButton || !link.NativeObjectVulnerable ||
                !Player.EnemyCollisionOverlaps(link.Position, new Rect2(record.Position - Vector2.One * 2, Vector2.One * 4)) ||
                owner.RuntimeState.ReadWramByte(AgesWramAddress.wActiveTilePos) != rooms.CurrentRoom.GetPackedPosition(record.Position)) return;
            owner.RuntimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0x81);
            owner.RuntimeState.SetWramByte(AgesWramAddress.wMenuDisabled, 0x81);
            Counter = 45; State = 2;
            rooms.TrySetTile((byte)rooms.CurrentRoom.GetPackedPosition(record.Position), 0x9e);
            owner.OnRoomTileChanged(); owner.OnSoundRequested(SoundId.SndOpenChest); return;
        }
        Counter = (Counter - 1) & 255;
        if (Counter != 0) return;
        owner.RuntimeState.SetWramByte(0xcbbb, (byte)record.Room);
        owner.RuntimeState.SetWramByte(0xcbbc, owner.RuntimeState.ReadWramByte(AgesWramAddress.wActiveTilePos));
        owner.RuntimeState.SetWramByte(0xcbbd, (byte)record.Subid);
        owner.RequestPalacePassage(); Finished = true;
    }
}
