using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC $20 exact-trigger script followed by spawnChestAfterPuff.</summary>
internal sealed partial class DungeonTriggerChestScriptRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IScreenTransitionPreloadRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze
{
    private readonly OracleRuntimeState _runtime;
    private readonly Func<int> _triggers;
    private readonly Func<bool> _itemFlag, _scriptSuspended;
    private readonly Action<int> _sound;
    private readonly Action _writeChest;
    private readonly int _expected, _wait;
    private bool _initialized, _itemChecked;
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => !_initialized;
    internal int Counter { get; private set; } = -1;

    internal DungeonTriggerChestScriptRoomEntity(Vector2 position,OracleRuntimeState runtime,
        int expected,int wait,Func<int> triggers,Func<bool> itemFlag,Func<bool> scriptSuspended,
        Action<int> sound,Action writeChest)
    {
        Position = position; Visible = false; Name = "DungeonTriggerChestScript";
        _runtime = runtime; _expected = expected; _wait = wait; _triggers = triggers;
        _itemFlag = itemFlag; _scriptSuspended = scriptSuspended; _sound = sound; _writeChest = writeChest;
    }

    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns)
        => throw new InvalidOperationException("INTERAC $20 chest preload requires Link's live death-trigger state.");

    public ScreenTransitionPresentation PrepareForScreenTransition(Player? player,ICollection<RoomEntitySpawn> spawns)
    {
        if (player is null) throw new InvalidOperationException("INTERAC $20 chest preload requires Link's live death-trigger state.");
        if (!_initialized) Advance(spawns,player.IsDying);
        return ScreenTransitionPresentation.Hidden;
    }

    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns) => Advance(spawns,frame.Player.IsDying);

    private void Advance(ICollection<RoomEntitySpawn> spawns,bool deathTriggered)
    {
        if (Finished) return;
        if (!_initialized)
        {
            _initialized = true;
            _runtime.SetWramByte(0xcfc1,0); _runtime.SetWramByte(0xcfc2,0);
        }
        if (deathTriggered || _scriptSuspended()) return;
        if (!_itemChecked)
        {
            _itemChecked = true;
            if (_itemFlag()) { Finished = true; return; }
        }
        if (Counter < 0)
        {
            if (_triggers() != _expected) return;
            _sound(SoundId.SndSolvePuzzle);
            spawns.Add(new PuzzlePuffSpawn(Position,SoundId.SndPoof));
            Counter = _wait;
            return;
        }
        if (--Counter != 0) return;
        // settilehere advances its script pointer even if setTile's queue is
        // full; scriptend follows without rechecking triggers or item flag.
        _writeChest(); Finished = true;
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }
}
