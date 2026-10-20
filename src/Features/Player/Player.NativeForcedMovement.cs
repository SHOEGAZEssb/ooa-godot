using Godot;

namespace oracleofages;

public partial class Player
{
    internal void RequestNativeScriptFacing(object owner,int direction)
    {
        Face(direction switch {0=>Vector2I.Up,1=>Vector2I.Right,2=>Vector2I.Down,3=>Vector2I.Left,
            _=>throw new System.NotSupportedException($"forceLinkDirection ${direction:x2}.")});
        RequestState08Control(owner);
        // forceLinkDirection does not perform the portal's visible=$82 write.
        _forcedState08LowPriority=false;
        _state08ReleasedByObjectMask=true;
    }
    private bool _state08RequestPending;
    private void AdvanceState08Control()
    {
        // checkLinkForceState consumes a request separately from the active
        // state/substate. Ralph can queue another during every interaction
        // pass; state08/substate0 must still run before consuming it.
        if(_forcedState08Phase==1){_state08RequestPending=false;_forcedState08Phase=2;return;}
        if(_forcedState08Phase==2)
        {
            _forcedState08Phase=3;_tilePushWalls=0;CancelNativeItemUsage();
            SetScriptedLinkAnimationMode(0x10);return;
        }
        if(_forcedState08Phase==3&&_state08RequestPending)
        {_state08RequestPending=false;_forcedState08Phase=2;return;}
        if(!_state08ReleasedByObjectMask||_world.NativeObjectDisableMask!=0)return;
        _forcedState08Phase=0;_state08ReleasedByObjectMask=false;_state08RequestPending=false;
        _scriptControl=ScriptControl.None;_cutsceneControlOwner=null;_forcedState08LowPriority=false;
        SetScriptedLinkAnimationMode(null);ResetLinkWalkAnimation();
    }
    private int _nativeForcedMovementRequest = -1, _nativeForcedMovementCounter;
    private object? _nativeForcedMovementOwner;
    internal bool NativeForcedMovementActive => _nativeForcedMovementCounter != 0;
    private Vector2I _nativeForcedMovementDirection, _nativeForcedMovementFacing;
    internal void RequestNativeForcedMovement(int parameter, int angle, int direction, object? owner = null)
    {
        _nativeForcedMovementOwner = owner;
        _nativeForcedMovementRequest = parameter;
        _nativeForcedMovementDirection = angle switch { 0 => Vector2I.Up, 8 => Vector2I.Right,
            16 => Vector2I.Down, 24 => Vector2I.Left, _ => throw new System.NotSupportedException($"LINK_STATE_FORCE_MOVEMENT $0b angle${angle:x2}.") };
        _nativeForcedMovementFacing = direction switch { 0 => Vector2I.Up, 1 => Vector2I.Right, 2 => Vector2I.Down, 3 => Vector2I.Left,
            _ => throw new System.NotSupportedException($"LINK_STATE_FORCE_MOVEMENT $0b direction${direction:x2}.") };
        Face(_nativeForcedMovementFacing);
    }
    private void CancelNativeForcedMovement()
    { _nativeForcedMovementRequest = -1; _nativeForcedMovementCounter = 0; _nativeForcedMovementOwner = null; }
    internal void CancelNativeForcedMovement(object owner)
    {
        if (ReferenceEquals(_nativeForcedMovementOwner, owner)) EndForcedRoomEntryMovement();
    }
    private bool AdvanceNativeForcedMovement()
    {
        if (_world.DialogueOpen || _world.IsTransitioning) return _nativeForcedMovementCounter != 0;
        if (_nativeForcedMovementRequest >= 0)
        {
            _nativeForcedMovementCounter = _nativeForcedMovementRequest;
            _nativeForcedMovementRequest = -1;
            BeginForcedRoomEntryMovement(_nativeForcedMovementFacing, deferInitialization: true);
            // Consume wLinkForceState; substate0 runs on the next update.
            _forcedRoomEntryInitializationPhase = 0;
            return true;
        }
        if (_nativeForcedMovementCounter == 0) return false;
        if (_forcedRoomEntryInitializationPhase == 0) { InitializeForcedRoomEntryMovement(); _forcedRoomEntryInitializationPhase = 3; }
        _nativeForcedMovementCounter = (_nativeForcedMovementCounter - 1) & 255;
        if (_nativeForcedMovementCounter == 0) EndForcedRoomEntryMovement();
        else { AdvanceForcedRoomEntryMovement(_nativeForcedMovementDirection); Face(_nativeForcedMovementFacing); }
        return true;
    }
}
