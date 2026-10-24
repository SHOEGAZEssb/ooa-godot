using Godot;
using System.Collections.Generic;

namespace oracleofages;

// common/interactions/finalDungeonEnergy.s:$b5:$00. Keep the original
// interaction slot; PART children born here first run in the next PART pass.
internal sealed class FinalDungeonEnergyRoomEntity(RoomEventContext context,
    FinalDungeonEntryDatabase data, FinalDungeonEntryEvent owner)
    : IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IScreenTransitionPreloadRoomEntity,
        IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    internal int State { get; private set; }
    internal int Substate { get; private set; }
    internal int Counter { get; private set; }
    private int _inheritedObjects, _inheritedMenu;
    private IReadOnlyList<BlueEnergyBeadRoomEntity> _beads = [];
    public Node2D? PresentationNode => null;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => State == 0;
    public bool UpdatesDuringRoomEntityFreeze => State == 0;
    public void SetTransitionDrawOffset(Vector2 offset) { }
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns) => ScreenTransitionPresentation.Hidden;
    private int Read(int address) => context.Entities.RuntimeState.ReadWramByte(address);
    private void Write(int address, int value) => context.Entities.RuntimeState.SetWramByte(address, (byte)value);
    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns)
    {
        if (Finished) return;
        if (State == 0)
        {
            State = 1;
            var rooms = context.Rooms;
            if (rooms.SaveData.HasRoomFlag(rooms.ActiveGroup, rooms.CurrentRoom.Id, data.CompletionFlag))
            { Finished = true; return; }
            rooms.SaveData.SetRoomFlag(rooms.ActiveGroup, rooms.CurrentRoom.Id, data.CompletionFlag);
            context.Entities.RecordDeathRespawnPoint();
            context.CloseDialogue();
            Write(WramAddress.wTextIsActive, 0);
            _inheritedObjects = Read(WramAddress.wDisabledObjects);
            _inheritedMenu = Read(WramAddress.wMenuDisabled);
            Counter = data.WaitUpdates;
            _beads = context.Entities.CreateEnergySwirl(data.Center, (byte)data.WaitUpdates);
            return;
        }
        switch (Substate)
        {
            case 0:
                Counter = (Counter - 1) & 255;
                if (Counter != 0) return;
                Substate = 1; Counter = data.RevealDelay;
                Write(0xcbb3, 0); Write(0xcbba, 255);
                return;
            case 1:
                if (Counter == 0)
                {
                    context.Player.RequestNativeState08Control(this);
                    context.Player.Visible = true;
                }
                Counter = (Counter - 1) & 255;
                if (!owner.Flash()) return;
                Substate = 2; owner.FadeIn(data.FadeDelay);
                return;
            case 2:
                if (context.Transitions.NativePaletteFadeActive) return;
                Write(WramAddress.wDisabledObjects, 0);
                Write(WramAddress.wMenuDisabled, 0);
                Write(0xcbc3, 0); // include/wram.s:wUseSimulatedInput.
                Finished = true; owner.Complete();
                return;
        }
    }
    internal void Cancel()
    {
        foreach (var bead in _beads) context.Entities.RetireEntity(bead);
        _beads = [];
        if (State != 0 && !Finished)
        {
            if (Read(WramAddress.wDisabledObjects) == _inheritedObjects) Write(WramAddress.wDisabledObjects, 0);
            if (Read(WramAddress.wMenuDisabled) == _inheritedMenu) Write(WramAddress.wMenuDisabled, 0);
        }
        context.Player.EndCutsceneControl(this);
        Finished = true;
    }
}
