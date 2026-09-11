using System;
using System.Collections.Generic;
using System.Linq;

namespace oracleofages;

/// <summary>Independent INTERAC_SYMMETRY_NPC slots; script initialization runs on room entry.</summary>
internal sealed class SymmetryEvent : InteractiveCutsceneCommandHost, IRoomEntryEvent,
    ICutsceneCommandHost, IUpdatesDuringDialogueRoomEvent
{
    public override RoomEventContext Context { get; }
    internal SymmetryDatabase Database { get; } = new();
    private readonly List<SymmetryScriptHost> _actors = new();
    private TuniNutRoomEntity? _nut;
    internal Func<int, Action<bool>, bool>? OpenSecretMenu { get; set; }
    public bool MenusDisabled => BlocksGameplay;
    public bool ScreenTransitionsDisabled => BlocksGameplay;
    public bool HasState => _actors.Count != 0;
    public bool BlocksGameplay => InputControlHeld;

    public SymmetryEvent(RoomEventContext context) => Context = context;
    public bool Matches(int group, OracleRoomData room) => Context.Entities.EntityAdapters<SymmetryRoomEntity>().Any();
    public void Start(OracleRoomData room)
    {
        Cancel();
        // objectLoading.s:parseObjectData clears $cfc0 before the room's object stream.
        Context.Entities.RuntimeState.SetWramByte(0xcfc0, 0);
        _nut = Context.Entities.EntityAdapters<TuniNutRoomEntity>().SingleOrDefault();
        foreach (var entity in Context.Entities.EntityAdapters<SymmetryRoomEntity>())
        {
            entity.Bind(this);
            if (entity.Npc.Active) _actors.Add(new(this, entity.Npc));
        }
    }
    public void UpdateFrame()
    {
        _nut?.AdvanceFrame(this);
        foreach (var actor in _actors) actor.AdvanceFrame();
    }
    public void UpdateDuringDialogueFrame() => UpdateFrame();
    internal bool TryInteractNpc(NpcCharacter npc)
    {
        if (BlocksGameplay || Context.DialogueOpen) return false;
        var actor = _actors.Find(a => ReferenceEquals(a.Npc, npc));
        if (actor is null || !actor.ButtonSensitive || !npc.Active) return false;
        actor.ButtonPressed = true;
        return true;
    }
    public void Cancel()
    {
        _nut?.Cancel(Context.Rooms.CurrentRoom);
        _nut = null;
        foreach (var actor in _actors) actor.Cancel();
        _actors.Clear();
        ReleaseInputControl();
    }
}
