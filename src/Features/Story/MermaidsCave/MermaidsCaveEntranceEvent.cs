using System;

namespace oracleofages;

/// <summary>miscPuzzles_subid12, including the yielding jump to miscPuzzles_justOpenedKeyDoor.</summary>
internal sealed class MermaidsCaveEntranceEvent : RoomCutsceneCommandHost, IRoomEntryEvent
{
    private readonly MermaidsCaveEntranceDatabase _database = new();
    private readonly CutsceneCommandRunner _runner;
    private MermaidsCaveEntranceRecord _entrance;
    private bool _armed;
    internal int Counter => _runner.Counter;
    public override RoomEventContext Context { get; }
    public bool HasState => _armed || _runner.Active;
    public bool BlocksGameplay => _runner.Active;
    public bool FreezesNonInteractionObjects => BlocksGameplay;
    public bool MenusDisabled => BlocksGameplay;

    internal MermaidsCaveEntranceEvent(RoomEventContext context)
    {
        Context = context;
        _runner = new(this);
    }

    public bool Matches(int group, OracleRoomData room) =>
        _database.TryGet(group, room.Id, out _) &&
        !Context.Rooms.SaveData.HasRoomFlag(group, room.Id, OracleSaveData.RoomFlag80);

    public void Start(OracleRoomData room)
    {
        Cancel();
        if (!_database.TryGet(Context.Rooms.ActiveGroup, room.Id, out _entrance))
            throw new InvalidOperationException($"miscPuzzles_subid12 has no placement in {Context.Rooms.ActiveGroup:x}:{room.Id:x2}.");
        _armed = true;
    }

    internal bool CanTrigger(int group, int room) =>
        _armed && _entrance.Group == group && _entrance.Room == room;

    internal void Trigger(int group, int room)
    {
        if (!CanTrigger(group, room) || !Context.Rooms.SaveData.HasRoomFlag(group, room, OracleSaveData.RoomFlag80))
            throw new InvalidOperationException($"miscPuzzles_subid12 cannot trigger in {group:x}:{room:x2}.");
        _armed = false;
        _runner.Start(_database.Commands);
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
        // enableinput clears the $81 mask, then scriptend retires the owner
        // in this same update. Link and item parents resume on the next pass.
    }

    public override void SetMusic(int music)
    {
        if (music == 0xff) Context.Sound.PlayRoomMusic(_entrance.Group, _entrance.Room);
        else Context.Sound.PlaySound(music);
    }

    // scriptend deletes this invisible interaction; it has no extra native tail.
    public override void ScriptEnded() { }

    public override void RunNativeHandler(string handler)
    {
        if (handler == "KeyholeSignal")
        {
            // The keyhole owner delivers cfc0 bit 0 through Trigger. The
            // successful scriptCmd_checkCFC0Bit read clears carry, yielding
            // even though it advances the script pointer (scripting.s).
            if (!Context.Rooms.SaveData.HasRoomFlag(_entrance.Group, _entrance.Room, OracleSaveData.RoomFlag80))
                throw UnsupportedCommand($"keyhole signal without unlock flag at {_entrance.Source}");
            return;
        }
        if (handler != "OpenDoor") throw UnsupportedCommand($"Mermaid's Cave handler '{handler}'");
        // settilehere uses the placed controller's coordinates, not Link's.
        // setTile changes layout/collision immediately and queues graphics.
        // A full queue rejects the write; the original script still continues.
        Context.Rooms.TrySetTile(
            (byte)Context.Rooms.CurrentRoom.GetPackedPosition(_entrance.Position), _entrance.OpenTile);
    }
}
