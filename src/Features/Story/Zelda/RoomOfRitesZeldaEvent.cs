using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

internal sealed partial class RoomOfRitesZeldaEvent(RoomEventContext context) : IRoomEntryEvent
{
    private readonly List<RoomOfRitesZeldaActor> _actors = new();
    public bool HasState { get; private set; }
    public bool BlocksGameplay => WarpActive;
    public bool OwnsGameLogic => WarpActive;
    public bool MenusDisabled => WarpActive;
    public bool AllScreenTransitionsDisabled => WarpActive;
    public bool SkipsObjectUpdate { get; private set; }
    public bool Matches(int group, OracleRoomData room) => group == 5 && room.Id == 0xf1;
    internal RoomOfRitesZeldaActor CreatePlaced(NpcRecord record) => new(context, record);
    internal IRoomEntity CreateApproach(DungeonObjectRecord record, RoomOfRitesRitualDatabase data) =>
        new ZeldaApproachRoomEntity(context, record, data, BeginApproach);
    public void Start(OracleRoomData room)
    {
        _actors.Clear();
        _actors.AddRange(context.Entities.EntityAdapters<RoomOfRitesZeldaActor>());
        HasState = true;
    }
    internal bool TryInteractNpc(NpcCharacter npc) => _actors.Any(actor => actor.QueueButton(npc));
    public void UpdateFrame() { }
    public void Cancel()
    {
        CancelWarp();
        foreach (var actor in _actors) actor.Cancel();
        _actors.Clear();
        HasState = false;
    }
}
