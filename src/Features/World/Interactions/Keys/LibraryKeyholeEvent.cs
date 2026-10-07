using System;

namespace oracleofages;

// Original shared miscPuzzles_subid13 dispatch; invisible $90 is allocated
// by the entity manager, while this owner runs its script and modal lifetime.
internal sealed class LibraryKeyholeEvent : RoomCutsceneCommandHost, IRoomEntryEvent
{
    private LibraryKeyholeDatabase? _database;
    private KeyholeControllerRecord _placement;
    private readonly CutsceneCommandRunner _runner;
    private bool _armed;
    public override RoomEventContext Context { get; }
    public bool HasState => _armed || _runner.Active;
    public bool BlocksGameplay => _runner.Active;
    public bool FreezesNonInteractionObjects => BlocksGameplay;
    public bool MenusDisabled => BlocksGameplay;
    internal int Counter => _runner.Counter;

    internal LibraryKeyholeEvent(RoomEventContext context)
    {
        Context = context;
        _runner = new(this);
    }

    public bool Matches(int group, OracleRoomData room) =>
        Context.Entities.KeyholeControllers.TryGet(group,room.Id,out var record) && record.SubId == 0x13 &&
        !Context.Rooms.SaveData.HasRoomFlag(group,room.Id,OracleSaveData.RoomFlag80);

    public void Start(OracleRoomData room)
    {
        Cancel();
        if (!Matches(Context.Rooms.ActiveGroup,room))
            throw new InvalidOperationException($"miscPuzzles.s:$90:$13: invalid Library keyhole room entry {Context.Rooms.ActiveGroup:x}:{room.Id:x2}.");
        Context.Entities.KeyholeControllers.TryGet(Context.Rooms.ActiveGroup,room.Id,out _placement);
        _armed = true;
    }

    internal bool CanTrigger(int group, int room) =>
        _armed && group == _placement.Group && room == _placement.Room;

    internal void Trigger(int group, int room)
    {
        if (!CanTrigger(group,room) || !Context.Rooms.SaveData.HasRoomFlag(group,room,OracleSaveData.RoomFlag80))
            throw new InvalidOperationException($"{_placement.Source}: cannot trigger $90:$13 in {group:x}:{room:x2}.");
        _armed = false;
        _runner.Start((_database ??= new()).Commands);
    }

    public void UpdateFrame()
    {
        if (_runner.Active) _runner.AdvanceFrame();
    }

    public void Cancel()
    {
        _runner.Clear();
        _armed = false;
    }

    public override void SetInputEnabled(bool enabled)
    {
        if (!enabled) throw UnsupportedCommand("disable input from script");
        // enableinput clears $81; ordinary Link/parents resume next pass.
    }

    public override void SetMusic(int music)
    {
        if (music == 0xff) Context.Sound.PlayRoomMusic(_placement.Group,_placement.Room);
        else Context.Sound.PlaySound(music);
    }

    public override void ScriptEnded() { } // Native $90 ignores carry and retains its physical slot.

    public override void RunNativeHandler(string handler)
    {
        switch (handler)
        {
            case "KeyholeSignal":
                if ((Context.Entities.RuntimeState.ReadWramByte(WramAddress.wTmpcfc0) & 1) == 0)
                    throw UnsupportedCommand("keyhole signal without cfc0 bit0");
                break;
            case "OpenLeft": Context.Rooms.TrySetTile(0x22,0xee); break;
            case "OpenRight": Context.Rooms.TrySetTile(0x23,0xef); break;
            default: throw UnsupportedCommand($"Library keyhole handler '{handler}'");
        }
    }
}
