using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

// INTERAC $20: state0 runs the script but ignores its end carry.
internal sealed partial class SharedDungeonScriptRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IScreenTransitionPreloadRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly CutsceneCommandRunner _script;
    private readonly OracleRuntimeState _runtime;
    private readonly Func<bool> _textActive;
    private bool _initialized;
    internal bool ScriptStopped { get; private set; }
    internal bool ScriptBlocked { get; private set; }
    internal Func<int> Torches { get; }
    internal Func<int> EnemyCount { get; }
    internal Func<int, bool> RoomFlag { get; }
    internal Action<int> SetRoomFlag { get; }
    internal Action<int> Sound { get; }
    internal Action<string> NativeHandler { get; }
    internal int Counter => _script.Counter;
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;

    internal SharedDungeonScriptRoomEntity(DungeonObjectRecord record, IReadOnlyList<CutsceneCommand> commands,
        OracleRuntimeState runtime, Func<bool> textActive, Func<int> torches, Func<int> enemyCount,
        Func<int, bool> roomFlag, Action<int> setRoomFlag, Action<int> sound, Action<string> nativeHandler)
    {
        Name = $"SharedDungeonScript_{record.SubId:x2}"; Visible = false; Position = record.Position;
        _runtime = runtime; _textActive = textActive; Torches = torches; EnemyCount = enemyCount;
        RoomFlag = roomFlag; SetRoomFlag = setRoomFlag; Sound = sound; NativeHandler = nativeHandler;
        _script = new(new SharedDungeonScriptHost(this)); _script.Start(commands);
        TreeExiting += _script.Clear;
    }

    public void UpdateFrame(RoomEntityFrame frame, ICollection<RoomEntitySpawn> spawns) => Advance(frame.Player.IsDying);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns) =>
        throw new InvalidOperationException("INTERAC $20 preload requires Link's live death state.");
    public ScreenTransitionPresentation PrepareForScreenTransition(Player? player, ICollection<RoomEntitySpawn> spawns)
    {
        if (player is null) throw new InvalidOperationException("INTERAC $20 preload requires Link's live death state.");
        if (!_initialized) Advance(player.IsDying);
        return ScreenTransitionPresentation.Hidden;
    }
    public void UpdateDuringScreenTransition(RoomEntityFrame frame) { if (!_initialized) Finished = true; }
    public void SetTransitionDrawOffset(Vector2 offset) { }
    internal void EndScript() => ScriptStopped = true;

    private void Advance(bool deathTriggered)
    {
        if (Finished) return;
        bool initializing = !_initialized;
        if (initializing)
        {
            _initialized = true;
            _runtime.SetWramByte(0xcfc1, 0); _runtime.SetWramByte(0xcfc2, 0);
        }
        ScriptBlocked = deathTriggered || _textActive();
        if (ScriptBlocked) return;
        if (ScriptStopped) { Finished = true; return; }
        _script.AdvanceFrame();
        if (ScriptStopped && !initializing) Finished = true;
    }
}
