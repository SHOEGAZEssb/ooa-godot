namespace oracleofages;

public partial class Player
{
    private int _pendingContactDamageRaw;
    private bool _nativeContactSignal;

    // w1Link.var2a is a one-object-pass publication, independently of the
    // damageToApply byte which remains pending until a consuming handler runs.
    internal bool NativeContactSignal => _nativeContactSignal;
    internal int PendingContactDamageRaw => _pendingContactDamageRaw;

    private void ClearContactDamage()
    {
        // clearLinkObject/full room loading and resetLinkInvincibility clear
        // both bytes. Coordinate-only scrolling does not reset either byte.
        _pendingContactDamageRaw = 0;
        _nativeContactSignal = false;
    }

    private bool ConsumeContactDamageBeforeStateDispatch()
    {
        if (WallmasterGrabActive) return false; // linkState0c never calls updateLinkDamageTaken.
        // commonCode.updateLinkDamageTaken is called by state01, transformed
        // state1, animal-rider state1 and grabbed state0d substate1. The latter
        // has no text/palette/scroll gate. Other represented states retain the
        // pending byte; their explicit damage calls remain immediate.
        if (EnemyGrabActive)
        {
            if (_enemyGrabSubstate != 1) return false;
        }
        else
        {
            if (_deathPending || _deathAnimationActive || _getItemStatePhase >= 2 ||
                _forcedRespawnPhase >= 2 || _forcedState08Phase >= 2 ||
                _forcedRoomEntryMovement || _spinnerControlled ||
                _ledgeJumpState != LedgeJumpState.None || _squishAnimation is not null ||
                _sideScrollInstantRespawnCounter != 0 || _instantRespawnRecoveryCounter != 0 ||
                _fallingInHole || _drowning && _topDownDrownPhase >= 2 ||
                _newGameSlowFalling || _roomWarpFallActive || _roomWarpFallCollapsed ||
                _cutsceneControlled || GaleActive && _galePending < 2)
                return false;
            if (_world.NativePaletteChanging || _world.ScreenScrolling && !_companionRideControlled)
                return true; // Native handler returns before damage, input and parents.
        }

        if (_pendingContactDamageRaw != 0)
        {
            int raw = RingEffects.ModifyIncomingDamageRaw(_inventory, _pendingContactDamageRaw);
            _pendingContactDamageRaw = 0; // linkApplyDamage clears before arithmetic/death/potion.
            ApplyRawDamage(raw);
        }
        // linkApplyDamage uses the live var2a signal, even for harmless
        // shield recoil. A palette/scroll update may have already cleared it.
        if (_nativeContactSignal)
            _enemyKnockbackFrames = RingEffects.KnockbackFrames(_inventory, (int)_enemyKnockbackFrames);
        return false;
    }
}
