using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// stonePanel.s: both panels run in physical INTERACTION order. Their shared
// trigger, room flag and mask writes are deliberately independent.
internal sealed partial class StonePanelRoomEntity : DungeonInteractionVisualEntity,
    IRoomEntity, IFixedRoomEntity, IUpdatesDuringDialogueRoomEntity,
    IUpdatesDuringRoomEntityFreeze, IScreenTransitionPreloadRoomEntity
{
    private readonly DungeonObjectRecord _record;
    private readonly OracleRoomData _room;
    private readonly OracleSaveData _save;
    private readonly OracleRuntimeState _runtime;
    private readonly Func<int> _triggers;
    private readonly Action<int, bool> _trigger;
    private readonly Action<int> _sound;
    private readonly Func<int> _music;
    internal int State { get; private set; }
    internal int Substate { get; private set; }
    internal int Counter { get; private set; }
    public Node2D Node => this;
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;

    internal StonePanelRoomEntity(DungeonObjectRecord record, DungeonInteractionVisual visual,
        IReadOnlyDictionary<int, Color[]> palette, OracleRoomData room, OracleSaveData save,
        OracleRuntimeState runtime, Func<int> triggers, Action<int, bool> trigger, Action<int> sound, Func<int> music)
    {
        _record = record; _room = room; _save = save; _runtime = runtime;
        _triggers = triggers; _trigger = trigger; _sound = sound; _music = music;
        InitializeVisual(visual, record.Position, paletteOverrides: palette);
        Visible = false; ZIndex = ObjectDrawPriority.FixedLowPriorityZIndex;
    }
    private void Initialize()
    {
        Visible = true;
        if (_save.HasRoomFlag(_record.Group, _record.Room, 0x40))
        {
            State = 3; Position += new Vector2(_record.SubId == 0 ? -16 : 16, 0);
            OpenCollision();
        }
        else
        {
            for (byte tile = 0x66; tile <= 0x68; tile++) _room.SetPackedTileCollision(tile, 0x0f);
            State = 1;
        }
    }
    private void OpenCollision()
    {
        for (byte tile = 0x66; tile <= 0x68; tile++) _room.SetPackedTileCollision(tile, 0);
        _room.SetPackedTileCollision(0x46, 2); _room.SetPackedTileCollision(0x56, 0x0a);
        _room.SetPackedTileCollision(0x66, 8); _room.SetPackedTileCollision(0x48, 1);
        _room.SetPackedTileCollision(0x58, 5); _room.SetPackedTileCollision(0x68, 4);
    }
    private void PreventPassing(Player player)
    {
        LinkObjectBlocking.PreventPassing(player, Position.Floor(), 14, 8);
    }
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (State == 0) { Initialize(); return; }
        PreventPassing(frame.Player);
        if (State == 3) return;
        if (State == 1)
        {
            if ((_triggers() & 0x80) == 0) return;
            _runtime.SetWramByte(WramAddress.wDisabledObjects, 0x81);
            _runtime.SetWramByte(WramAddress.wMenuDisabled, 0x81);
            Counter = 60; _sound(SoundId.SndCtrlStopMusic); State = 2; return;
        }
        switch (Substate)
        {
            case 0:
                if (--Counter != 0) return;
                Counter = 128; Substate = 1; _sound(SoundId.SndOpening); return;
            case 1:
                if ((frame.Counter & 1) == 0) return;
                Position += new Vector2(_record.SubId == 0 ? -0.125f : 0.125f, 0);
                if (--Counter != 0) return;
                Counter = 30; Substate = 2; return;
            case 2:
                if (--Counter != 0) return;
                _runtime.SetWramByte(WramAddress.wDisabledObjects, 0);
                _runtime.SetWramByte(WramAddress.wMenuDisabled, 0);
                _trigger(7, false); _save.SetRoomFlag(_record.Group, _record.Room, 0x40);
                OpenCollision(); _sound(_music()); State = 3; return;
            default: throw new InvalidOperationException($"stonePanel.s: invalid substate ${Substate:x2}.");
        }
    }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
    { if (State == 0) Initialize(); return ScreenTransitionPresentation.Visible; }
    void IRoomEntity.SetTransitionDrawOffset(Vector2 offset) => SetTransitionDrawOffset(offset);
}
