using Godot;
using System;
using System.Collections.Generic;

namespace oracleofages;

/// <summary>INTERAC $20 exact-trigger script followed by spawnChestAfterPuff.</summary>
internal sealed partial class DungeonTriggerChestScriptRoomEntity : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IScreenTransitionPreloadRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze,
    IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private readonly OracleRuntimeState _runtime;
    private readonly Func<int> _triggers;
    private readonly Func<bool> _itemFlag, _scriptSuspended;
    private readonly Action<int> _sound;
    private readonly Action _writeChest;
    private readonly int _expected;
    private readonly ChestAfterPuffScript _script;
    private bool _initialized, _itemChecked, _triggerChecked, _scriptStopped;
    public Node2D Node => this;
    public bool Finished { get; private set; }
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;
    internal int Counter => _script.Counter;

    internal DungeonTriggerChestScriptRoomEntity(Vector2 position,OracleRuntimeState runtime,
        int expected,int wait,Func<int> triggers,Func<bool> itemFlag,Func<bool> scriptSuspended,
        Action<int> sound,Action writeChest)
    {
        Position = position; Visible = false; Name = "DungeonTriggerChestScript";
        _runtime = runtime; _expected = expected; _script = new(wait); _triggers = triggers;
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

    public void UpdateDuringScreenTransition(RoomEntityFrame frame)
    {
        if (!_initialized) Finished = true; // interactionDeleteAndRetIfEnabled02.
    }

    private void Advance(ICollection<RoomEntitySpawn> spawns,bool deathTriggered)
    {
        if (Finished) return;
        bool initializing = !_initialized;
        if (!_initialized)
        {
            _initialized = true;
            _runtime.SetWramByte(0xcfc1,0); _runtime.SetWramByte(0xcfc2,0);
        }
        if (deathTriggered || _scriptSuspended()) return;
        if (_scriptStopped) { Finished = true; return; }
        if (!_itemChecked)
        {
            _itemChecked = true;
            if (_itemFlag())
            {
                // state0 ignores the script-end carry; its installed
                // stubScript is deleted by the next eligible state1 pass.
                _scriptStopped = initializing; Finished = !initializing; return;
            }
        }
        if (!_triggerChecked)
        {
            if (_triggers() != _expected) return;
            // checkmemoryeq advances its script pointer but retains carry
            // clear on equality. Its following jump runs next update.
            _triggerChecked = true; return;
        }
        Finished = _script.Advance(true,Position,spawns,_sound,_writeChest);
    }

    public void SetTransitionDrawOffset(Vector2 offset) { }
}
