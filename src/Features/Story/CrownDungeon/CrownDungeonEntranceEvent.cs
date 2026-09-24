using Godot;
using System;

namespace oracleofages;

/// <summary>miscPuzzles_subid11 and its script's shared miscPuzzles_justOpenedKeyDoor tail.</summary>
internal sealed class CrownDungeonEntranceEvent : RoomCutsceneCommandHost, IRoomEntryEvent
{
    private readonly CrownDungeonEntranceDatabase _database = new();
    private readonly CutsceneCommandRunner _runner;
    private bool _armed;
    internal int Phase { get; private set; }
    internal int Counter => _runner.Counter;
    public override RoomEventContext Context { get; }
    public bool HasState => _armed || _runner.Active;
    public bool BlocksGameplay => _runner.Active;
    public bool FreezesNonInteractionObjects => BlocksGameplay;

    internal CrownDungeonEntranceEvent(RoomEventContext context)
    {
        Context = context;
        _runner = new(this);
    }

    public bool Matches(int group, OracleRoomData room) => group == 0 && room.Id == 0x0a &&
        !Context.Rooms.SaveData.HasRoomFlag(group, room.Id, 0x80);

    public void Start(OracleRoomData room)
    {
        Cancel();
        _armed = true;
    }

    // Graphics restoration is independent of entry-event precedence (remote Maku can share this room).
    internal void RestoreEntrance(int group, OracleRoomData room)
    {
        if (Matches(group, room)) DrawFrame(room, 0);
    }

    internal bool CanTrigger(int group, int room) => group == 0 && room == 0x0a && _armed;

    internal void Trigger(int group, int room)
    {
        if (!CanTrigger(group, room) || !Context.Rooms.SaveData.HasRoomFlag(group, room, 0x80))
            throw new InvalidOperationException($"miscPuzzles_subid11 cannot trigger in {group:x}:{room:x2}.");
        _armed = false;
        Context.Player.BeginCutsceneControl(owner: this);
        _runner.Start(_database.Commands);
    }

    public void UpdateFrame()
    {
        if (!_runner.Active) return;
        _runner.AdvanceFrame();
    }

    public void Cancel()
    {
        bool running = _runner.Active;
        _runner.Clear();
        _armed = false;
        Phase = 0;
        if (running) Context.Entities.SetScreenShake(0, 0, 0);
        Context.Player.EndCutsceneControl(this);
    }

    public override void SetInputEnabled(bool enabled)
    {
        if (!enabled) throw UnsupportedCommand("disable input from script");
        Context.Player.EndCutsceneControl(this);
    }

    public override void SetMusic(int music)
    {
        if (music == 0xff) Context.Sound.PlayRoomMusic(0, 0x0a);
        else Context.Sound.PlaySound(music);
    }

    public override void ScriptEnded()
    {
        Context.Entities.SetScreenShake(0, 0, 0);
    }

    public override void RunNativeHandler(string handler)
    {
        if (handler == "OpenDoor")
        {
            // settilehere uses the placed $90:$11 coordinates ($18,$78).
            Context.Rooms.CurrentRoom.SetPositionTileAndCollision(new Vector2(0x78, 0x18), 0xee, null, Context.AnimationTick());
            return;
        }
        int phase = handler switch { "Frame1" => 1, "Frame2" => 2, "Frame3" => 3,
            _ => throw UnsupportedCommand($"Crown Dungeon handler '{handler}'") };
        DrawFrame(Context.Rooms.CurrentRoom, phase);
        Phase = phase;
        Context.Entities.BeginScreenShake(0x0f);
        Context.Sound.PlaySound(OracleSoundEngine.SndDoorClose);
        for (int x = 0x60; x <= 0x90; x += 0x10)
        {
            // scriptHelp.@spawnPuff returns on a failed allocation; each
            // later position still gets its own attempt.
            if (!Context.Entities.InteractionSlotAvailable) continue;
            // INTERAC_PUFF subid $81 suppresses sound and flickers.
            var puff = Context.Entities.Spawn<PuzzlePuffEffect>(new PuzzlePuffSpawn(new Vector2(x, 0x20), 0, Flickers: true));
            puff.UpdateFrame();
        }
        // Room events follow the entity pass; apply this first shake after
        // the native helper, then let the shared owner advance subsequent updates.
        Context.Entities.UpdateScreenShake();
    }

    private void DrawFrame(OracleRoomData room, int phase)
    {
        CrownDungeonEntranceFrame frame = _database.Frames[phase];
        room.SetBackgroundMappingRectangle(frame.TopLeft, frame.Width, frame.Pairs, Context.AnimationTick());
    }
}
