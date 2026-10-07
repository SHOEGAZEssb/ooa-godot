using Godot;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace oracleofages;

// INTERAC$20's signal scripts preserve command carry and initialization.
// Bridge allocation is intentionally one attempt after flag/sound publication.
internal sealed partial class DungeonSignalScriptRoomEntity(DungeonSignalScriptProfile profile,OracleRuntimeState runtime,
    Func<int> triggers,Action<int,bool> setTrigger,Func<bool> roomFlag,Action setRoomFlag,Func<bool> scriptSuspended,
    Func<BridgeSpawnerSpawn,bool> createBridge,Action<int> sound) : Node2D,
    IRoomEntity, IFixedRoomEntity, IRoomEntityLifetime, IScreenTransitionPreloadRoomEntity,
    IUpdatesDuringDialogueRoomEntity, IUpdatesDuringRoomEntityFreeze, IAlwaysUpdateDuringScreenTransitionRoomEntity
{
    private bool _initialized,_flagChecked,_scriptStopped,_loopStarted;
    internal int Counter { get; private set; }
    internal bool Initialized => _initialized;
    public bool Finished { get; private set; }
    public Node2D Node => this;
    public bool UpdatesDuringDialogue => !_initialized;
    public bool UpdatesDuringRoomEntityFreeze => !_initialized;
    public void UpdateFrame(RoomEntityFrame frame,ICollection<RoomEntitySpawn> spawns) => Advance(frame.Player.IsDying);
    public ScreenTransitionPresentation PrepareForScreenTransition(ICollection<RoomEntitySpawn> spawns) =>
        throw new InvalidOperationException($"{profile.Source}: signal-script preload requires Link's death gate.");
    public ScreenTransitionPresentation PrepareForScreenTransition(Player? player,ICollection<RoomEntitySpawn> spawns)
    {
        if (player is null) throw new InvalidOperationException($"{profile.Source}: signal-script preload requires Link's death gate.");
        if (!_initialized) Advance(player.IsDying);
        return ScreenTransitionPresentation.Hidden;
    }
    public void UpdateDuringScreenTransition(RoomEntityFrame frame)
    { if (!_initialized) Finished=true; } // interactionDeleteAndRetIfEnabled02.
    public void SetTransitionDrawOffset(Godot.Vector2 offset) { }
    private void Advance(bool deathTriggered)
    {
        if (Finished) return;
        bool initializing=!_initialized;
        if (!_initialized)
        {
            _initialized=true;
            runtime.SetWramByte(0xcfc1,0); runtime.SetWramByte(0xcfc2,0);
            Visible=false;
        }
        if (deathTriggered || scriptSuspended()) return;
        if (_scriptStopped) { Finished=true; return; }
        if (profile.Kind == DungeonSignalScriptKind.Bridge)
        {
            if (!_flagChecked)
            {
                _flagChecked=true;
                if (roomFlag()) { _scriptStopped=true; Finished=!initializing; return; }
            }
            // checkflagset advances with carry set, so ASM15 and scriptend
            // execute in the SAME eligible update, unlike checkmemoryeq.
            if ((runtime.ReadWramByte(OracleRuntimeState.ToggleBlocksStateAddress)&profile.TestMask) != profile.TestMask) return;
            setRoomFlag(); sound(SoundId.SndSolvePuzzle);
            _=createBridge(new(profile.Position,profile.HalfSteps,profile.Angle));
            _scriptStopped=true; Finished=!initializing;
            return;
        }
        if (!_loopStarted) { _loopStarted=true; Counter=profile.Wait; return; }
        if (Counter != 0 && --Counter != 0) return;
        setTrigger(BitOperations.TrailingZeroCount((uint)profile.OutputMask),(triggers()&profile.TestMask) == profile.TestMask);
        // ASM15 and scriptjump return carry set; the next wait arms in this
        // dispatch. Text/death gates precede the counter decrement.
        Counter=profile.Wait;
    }
}
