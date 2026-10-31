using Godot;

namespace Oracles;

public partial class Player
{
    internal void RequestNativeScriptFacing(object owner,int direction)
    {
        Face(direction switch {0=>Vector2I.Up,1=>Vector2I.Right,2=>Vector2I.Down,3=>Vector2I.Left,
            _=>throw new System.NotSupportedException($"forceLinkDirection ${direction:x2}.")});
        RequestNativeState08Control(owner);
    }
    internal void RequestNativeState08Control(object owner, byte parameter = 0)
    {
        RequestState08Control(owner, parameter);
        // forceLinkDirection does not perform the portal's visible=$82 write.
        _forcedState08LowPriority=false;
    }
    private void RetireState08Control(bool cancelRequest=false)
    {
        if(cancelRequest && _inventory.RuntimeState.ReadField("wLinkForceState")==8)
            _inventory.RuntimeState.SetField("wLinkForceState",0);
        _forcedState08Phase=0;
        if(_scriptControl!=ScriptControl.ForcedState08)return;
        _scriptControl=ScriptControl.None;_cutsceneControlOwner=null;_forcedState08LowPriority=false;
    }

    private bool ConsumeNativeForcedState()
    {
        var memory=_inventory.RuntimeState;
        byte state=memory.ReadField("wLinkForceState");
        if(state==0)return false;
        if(state is not (2 or 4 or 8 or 0x0b))
            throw new System.NotSupportedException($"link.s: checkLinkForceState state${state:x2} requires its native dispatch binding.");
        if(_getItemStatePhase==1 && state!=4)CancelGetItemState();
        if(state!=8)RetireState08Control();
        // State02 substate4 can hand off after its terminal marker. Retire
        // its presentation owner while preserving native collision/visibility.
        if (_forcedRespawnPhase >= 2)
        {
            _forcedRespawnPhase=0;
            _fallingInHole=false;_fallInHoleRespawning=false;_fallInHoleWarpPending=false;
            _drowning=false;_topDownDrownPhase=0;_drownRespawning=false;
        }
        memory.SetField("wLinkForceState",0);
        NativeLinkSubstate=0;
        switch(state)
        {
            case 2:_forcedRespawnPhase=2;break;
            case 4:
                _getItemStatePhase=2;break;
            case 8:
                _forcedState08Phase=2;_scriptControl=ScriptControl.ForcedState08;break;
            case 0x0b:
                _nativeForcedMovementSelected=true;
                _walking=true;QueueRedraw();break;
        }
        return true;
    }

    private void RetireSupersededNativeRequests()
    {
        byte request=_inventory.RuntimeState.ReadField("wLinkForceState");
        if(_forcedState08Phase==0 && request!=8)RetireState08Control();
        if(!_nativeForcedMovementSelected && request!=0x0b)_nativeForcedMovementOwner=null;
        if(_getItemStatePhase==1 && request!=4)CancelGetItemState();
    }
    private void AdvanceState08Control()
    {
        // State08 initialization precedes its first checkLinkForceState visit.
        if(NativeLinkSubstate==0)
        {
            NativeLinkSubstate=1;_forcedState08Phase=3;
            byte parameter = _inventory.RuntimeState.ReadField("wcc50");
            _inventory.RuntimeState.SetField("wcc50", 0);
            if (parameter == 0)
            {
                NativeAdjacentWalls=0;CancelNativeItemUsage();
                SetScriptedLinkAnimationMode(0x10);
            }
            return;
        }
        if(NativeLinkSubstate!=1)
            throw new System.NotSupportedException($"link.s: state$08 substate${NativeLinkSubstate:x2}.");
        if(ConsumeNativeForcedState())return;
        byte animation = _inventory.RuntimeState.ReadField("wcc50");
        if (animation != 0)
        {
            _inventory.RuntimeState.SetField("wcc50", 0);
            SetScriptedLinkAnimationMode(animation);
        }
        if (_inventory.RuntimeState.ReadField("wcc63") != 0)
            AdvanceState08ItemUse();
        if(_world.NativeObjectDisableMask!=0)return;
        RetireState08Control();NativeLinkSubstate=0;
        _inventory.RuntimeState.SetWramByte(0xd035,0);Visible=true;
        SetScriptedLinkAnimationMode(null);ResetLinkWalkAnimation();
    }

    private void AdvanceState08ItemUse()
    {
        _world.AdvanceSwordJinxCounter();
        // parentItemUsage.s initializes $f1 on the $ff request; swordParent
        // state1 enters its spin on the following checkUseItems call.
        bool initialize = _inventory.RuntimeState.ReadField("wcc63") == 0xff;
        if (initialize)
        {
            CancelNativeItemUsage();
            NotifyParentItemAnimationStarted(TreasureId.Sword);
            _inventory.RuntimeState.SetField("wcc63", 0x80);
            _forcedSwordParentInitializing = true;
        }
        _swordUpdatedInPhysics = true;
        if (_world.SwordDisabled || _world.SwordJinxed)
        {
            if (initialize) _world.PlaySound(SoundId.SndError);
            CancelSwordAttack();
        }
        else if (!initialize && _forcedSwordParentInitializing)
        {
            _forcedSwordParentInitializing = false;
            SetScriptedLinkAnimationMode(null);
            BeginSwordSpin();
        }
        else if (!initialize && IsAttacking)
            AdvanceSwordFrame(IsSwordButtonHeld(), Vector2.Zero);
    }
    private bool _nativeForcedMovementSelected;
    private object? _nativeForcedMovementOwner;
    internal bool NativeForcedMovementActive => _nativeForcedMovementSelected;
    internal void RequestNativeForcedMovement(int parameter, int angle, int direction, object? owner = null)
    {
        if((uint)parameter>255)throw new System.ArgumentOutOfRangeException(nameof(parameter));
        if((uint)angle>31 && angle!=0xff)throw new System.NotSupportedException($"LINK_STATE_FORCE_MOVEMENT $0b angle${angle:x2}.");
        var facing = direction switch { 0 => Vector2I.Up, 1 => Vector2I.Right, 2 => Vector2I.Down, 3 => Vector2I.Left,
            _ => throw new System.NotSupportedException($"LINK_STATE_FORCE_MOVEMENT $0b direction${direction:x2}.") };
        _nativeForcedMovementOwner=owner;
        Face(facing);_topDownMovementAngle=angle;
        _inventory.RuntimeState.SetWramByte(0xd009,(byte)angle);
        _inventory.RuntimeState.SetField("wLinkStateParameter",(byte)parameter);
        _inventory.RuntimeState.SetField("wLinkForceState",0x0b);
    }
    private void CancelNativeForcedMovement()
    {
        if(_inventory.RuntimeState.ReadField("wLinkForceState")==0x0b)
            _inventory.RuntimeState.SetField("wLinkForceState",0);
        _nativeForcedMovementSelected=false;_nativeForcedMovementOwner=null;
    }
    internal void CancelNativeForcedMovement(object owner)
    {
        if (ReferenceEquals(_nativeForcedMovementOwner, owner)) EndForcedRoomEntryMovement();
    }
    private bool AdvanceNativeForcedMovement()
    {
        if(!_nativeForcedMovementSelected)return false;
        if(NativeLinkSubstate==0)
        {
            NativeLinkSubstate=1;
            NativeLinkCounter=_inventory.RuntimeState.ReadField("wLinkStateParameter");
            InitializeForcedRoomEntryMovement();
            SetScriptedLinkAnimationMode(null);ResetLinkWalkAnimation();
        }
        if(NativeLinkSubstate!=1)
            throw new System.NotSupportedException($"link.s: state$0b substate${NativeLinkSubstate:x2}.");
        LinkNativeAnimationDatabase.For(AssetRoot).Animate(_inventory.RuntimeState);NativeAdjacentWalls=0;
        NativeLinkCounter=unchecked((byte)(NativeLinkCounter-1));
        if(NativeLinkCounter==0)
        {
            _nativeForcedMovementSelected=false;
            if(_inventory.RuntimeState.ReadField("wLinkForceState")!=0x0b)_nativeForcedMovementOwner=null;
            _walking=false;
            NativeLinkSubstate=0;_inventory.RuntimeState.SetWramByte(0xd035,0);
            Visible=true;ResetLinkWalkAnimation();
        }
        else
        {
            _walking=true;
            _precisePosition=_world.ApplyNativeUnblockedMovement(_precisePosition,_topDownMovementSpeedRaw,
                _inventory.RuntimeState.ReadWramByte(0xd009));
            Position=OracleObjectMath.ToPixelPosition(_precisePosition);
        }
        QueueRedraw();
        return true;
    }
}
