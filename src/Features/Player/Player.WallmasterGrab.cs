using System;

namespace oracleofages;

public partial class Player
{
    private bool _wallmasterGrabRequested;
    private int _wallmasterGrabSubstate = -1;
    internal bool WallmasterGrabActive => _wallmasterGrabSubstate >= 0;
    internal bool WallmasterGrabPending => _wallmasterGrabRequested;
    internal int WallmasterGrabSubstate => _wallmasterGrabSubstate;

    internal bool RequestWallmasterGrab()
    {
        // collisionEffect37 reads Link.state after ordinary collision gates.
        if (!PatchCollisionsEnabled || _enemyInvincibilityFrames != 0 ||
            _world.NativeWarpsDisabled || _wallmasterGrabRequested || !NativeNormalStateForInteraction) return false;
        _wallmasterGrabRequested = true;
        return true;
    }
    internal void CompleteWallmasterGrab()
    {
        if (!WallmasterGrabActive)
            throw new InvalidOperationException("floorMaster.s: terminal grab animation requires LINK_STATE_GRABBED_BY_WALLMASTER $0c.");
        _wallmasterGrabSubstate = 2;
    }
    private bool AdvanceWallmasterGrab()
    {
        if (_wallmasterGrabRequested)
        { _wallmasterGrabRequested = false; _wallmasterGrabSubstate = 0; }
        if (!WallmasterGrabActive) return false;
        _enemyGrabUpdated = true;
        if (_wallmasterGrabSubstate == 0)
        {
            _wallmasterGrabSubstate = 1;
            _world.SetNativeWarpsDisabled(true);
            _enemyGrabCollisionDisabled = true;
            ClearNativeItemParents();
            ClearShieldParent();
            CancelSwordAttack();
            _startedParentItemAnimations = 0;
            _world.PlaySound(SoundId.SndBossDead);
        }
        else if (_wallmasterGrabSubstate == 2)
        {
            _world.SetNativeWarpsDisabled(false);
            _world.RequestWallmasterReturn();
        }
        QueueRedraw();
        return true;
    }
    private void CancelWallmasterGrab()
    {
        if (WallmasterGrabActive) _world.SetNativeWarpsDisabled(false);
        _wallmasterGrabRequested = false;
        _wallmasterGrabSubstate = -1;
    }
}
