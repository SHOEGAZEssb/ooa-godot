using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

internal sealed class ZeldaApproachRoomEntity(RoomEventContext context, DungeonObjectRecord record,
    RoomOfRitesRitualDatabase data, Action trigger) : IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime,
    IScreenTransitionPreloadRoomEntity, IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    internal int State { get; private set; }
    public Node2D? PresentationNode => null;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;
    public void SetTransitionDrawOffset(Vector2 offset) { }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns) => ScreenTransitionPresentation.Hidden;
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (State == 0)
        {
            State = 1;
            if (context.Rooms.SaveData.HasRoomFlag(record.Group, record.Room, 0x80)) { Finished = true; return; }
            context.Entities.LoadNativeObjectPaletteSlots(6, data.Palette);
            return;
        }
        var player = frame.Player;
        if (!player.NativePuzzleResetVulnerable ||
            (context.Entities.RuntimeState.ReadWramByte(WramAddress.wScrollMode) & 0x0e) != 0 ||
            (int)player.Position.Y > record.Y || Math.Abs(record.X - (int)player.Position.X) >= 9) return;
        context.Entities.RuntimeState.SetWramByte(0xcc04, 0x17); // wCutsceneTrigger.
        context.Entities.RuntimeState.SetWramByte(WramAddress.wMenuDisabled, 0x17);
        foreach (var flame in context.Entities.EntityAdapters<TwinrovaFlameRoomEntity>()) flame.Visible = false;
        trigger(); Finished = true;
    }
}
