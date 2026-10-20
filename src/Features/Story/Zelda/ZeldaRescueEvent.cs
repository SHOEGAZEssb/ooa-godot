using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

// The event binds placed slots; each native actor dispatches its own script.
internal sealed class ZeldaRescueEvent(RoomEventContext context) : IRoomEntryEvent
{
    private readonly ZeldaRescueDatabase _data = new();
    private readonly List<ZeldaRescueActor> _actors = new();
    private bool _active, _ownsInput;
    public bool HasState => _active;
    public bool BlocksGameplay => false; // Raw $81 mask retains Link's native handler.
    public bool MenusDisabled => _ownsInput;
    public bool Matches(int group, OracleRoomData room) => group == 0 && room.Id == 0x65;
    internal ZeldaRescueActor CreatePlaced(NpcRecord record) => new(context, this, _data, record);
    public void Start(OracleRoomData room)
    {
        _actors.Clear();
        _actors.AddRange(context.Entities.EntityAdapters<ZeldaRescueActor>());
        _active = true;
    }
    public void UpdateFrame() { }
    internal void SetInput(bool enabled, bool initialization = false)
    {
        if (initialization)
        {
            context.Entities.RuntimeState.SetWramByte(WramAddress.wMenuDisabled, 0x81);
            context.Entities.RuntimeState.SetWramByte(WramAddress.wDisabledObjects, 0x81);
        }
        else NativeScriptInputControl.SetEnabled(context.Entities.RuntimeState, context.Player, enabled);
        _ownsInput = !enabled;
    }
    internal void CreateZelda()
    {
        if (!context.Entities.InteractionSlotAvailable)
            throw new System.InvalidOperationException("impaInCutscene.s:@init7 unchecked INTERAC_ZELDA $ad:$06 allocation exhausted; retained-pointer alias is unsupported.");
        var actor = new ZeldaRescueActor(context, this, _data, _data.ZeldaRecord());
        _actors.Add(actor);
        context.Entities.AddEntity(actor);
    }
    internal bool TryInteractNpc(NpcCharacter npc) => _actors.Any(actor => actor.QueueButton(npc));
    public void Cancel()
    {
        foreach (var actor in _actors) actor.Cancel();
        _actors.Clear();
        if (_ownsInput)
        {
            if (context.DialogueOpen) context.CloseDialogue();
            context.Player.CancelNativeForcedMovement(this);
            SetInput(true);
            context.Entities.RuntimeState.SetWramByte(WramAddress.wScrollMode, 1);
        }
        _active = false;
    }
}
