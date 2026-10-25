using System;
using System.Collections.Generic;
using System.Linq;

namespace Oracles;

internal sealed class LibraryEvent(RoomEventContext context) : InteractiveCutsceneCommandHost, IRoomEntryEvent
{
    private readonly List<LibraryScriptHost> _actors = new();
    internal LibraryDatabase Database { get; } = new();
    internal Func<int, Action<bool>, bool>? OpenSecretMenu { get; set; }
    public override RoomEventContext Context => context;
    public bool HasState => _actors.Count != 0;
    public bool BlocksGameplay => InputControlHeld;
    public bool FreezesNonInteractionObjects => BlocksGameplay;
    public bool MenusDisabled => Context.Entities.RuntimeState.ReadWramByte(AgesWramAddress.wMenuDisabled) != 0;
    public bool ScreenTransitionsDisabled => BlocksGameplay;
    public bool Matches(int group, OracleRoomData room) => group == 5 && room.Id is 0xc8 or 0xe4 or 0xec;
    public void Start(OracleRoomData room)
    {
        Cancel();
        foreach (var slot in Context.Entities.EntityAdapters<LibraryActorRoomEntity>()) Bind(slot);
    }
    private void Bind(LibraryActorRoomEntity slot)
    {
        var host = new LibraryScriptHost(this, slot);
        slot.Host = host;
        _actors.Add(host);
    }
    internal LibraryActorRoomEntity CreatePodium(int subid)
    {
        var p = Database.Path(subid);
        var slot = new LibraryActorRoomEntity(NpcCharacter.CreateFromRecord(
            Database.Actor(Context.Rooms.ActiveGroup, Context.Rooms.CurrentRoom.Id, AgesInteractionId.BookOfSealsPodium, subid, p.Y, p.X)),
            Database, Context.Rooms.SaveData, Context.Entities.RuntimeState);
        Bind(slot);
        return slot;
    }
    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (BlocksGameplay || Context.DialogueOpen) return false;
        var actor = _actors.Find(a => ReferenceEquals(a.Npc, npc));
        if (actor is not { ButtonSensitive: true } || !npc.Active) return false;
        actor.ButtonPressed = true;
        return true;
    }
    // The native/script handlers run in each entity's actual update slot.
    public void UpdateFrame() { }
    public void Cancel()
    {
        foreach (var actor in _actors) actor.Cancel();
        _actors.Clear();
        if (InputControlHeld)
        {
            Context.Entities.RuntimeState.SetWramByte(AgesWramAddress.wDisabledObjects, 0);
            Context.Entities.RuntimeState.SetWramByte(AgesWramAddress.wMenuDisabled, 0);
        }
        ReleaseInputControl();
    }
}
