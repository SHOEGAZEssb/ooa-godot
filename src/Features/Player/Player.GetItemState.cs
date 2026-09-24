using System;

namespace oracleofages;

public partial class Player
{
    private int _getItemStatePhase;
    private byte _getItemStateParameter;
    private Func<bool>? _getItemObjectsDisabled;
    private int? _getItemSavedAnimationMode;
    private bool _getItemSavedWalking;
    private bool _getItemSavedPushing;

    // treasure.s supplies $80/$81; essence.s supplies $01. Keep this
    // force-state request separate from pose-only writes made in state08.
    internal void RequestGetItemState(byte parameter, Func<bool> objectsDisabled)
    {
        if (parameter is not (0x00 or 0x01 or 0x80 or 0x81))
            throw new NotSupportedException($"LINK_STATE_04 wcc50=${parameter:x2} has no represented item-get animation.");
        if (_getItemStatePhase != 0)
            throw new InvalidOperationException("LINK_STATE_04 already owns a pending or active item-get request.");
        _getItemStateParameter = parameter;
        _getItemObjectsDisabled = objectsDisabled ?? throw new ArgumentNullException(nameof(objectsDisabled));
        _getItemStatePhase = 1;
    }

    private bool AdvanceGetItemState()
    {
        if (_getItemStatePhase == 0) return false;
        if (_getItemStatePhase == 1)
        {
            // State08 substate1 also consumes force-state requests, without
            // state01's palette/death gates. Its initialization must run first.
            bool fromState08 = _forcedState08Phase >= 3;
            if (!fromState08 && (_world.IsTransitioning || IsDying || !NativeNormalStateForInteraction))
                return false;
            if (fromState08) _forcedState08Phase = 0;
            _getItemStatePhase = 2; // checkLinkForceState returns before state04 dispatch.
            return true;
        }
        if (_getItemStatePhase == 2)
        {
            CancelNativeItemsForSquishRespawn();
            _getItemSavedAnimationMode = _scriptedLinkAnimationMode;
            _getItemSavedWalking = _walking;
            _getItemSavedPushing = _pushing;
            SetScriptedLinkAnimationMode(null);
            _walking = false;
            _pushing = false;
            _getItemOneHandPose = (_getItemStateParameter & 1) == 0;
            _getItemTwoHandPose = !_getItemOneHandPose;
            _getItemStatePhase = 3;
            QueueRedraw();
            return true;
        }
        // State04 ignores $81 during initialization and waits for text first.
        // wcc50 bit7 skips only the subsequent disabled-object check.
        if (!_world.NativeTextActive &&
            ((_getItemStateParameter & 0x80) != 0 || !_getItemObjectsDisabled!()))
            CancelGetItemState();
        return true; // Restoring state01 does not also dispatch it this update.
    }

    internal void CancelGetItemState()
    {
        if (_getItemStatePhase == 3)
        {
            _getItemOneHandPose = false;
            _getItemTwoHandPose = false;
            SetScriptedLinkAnimationMode(_getItemSavedAnimationMode);
            _walking = _getItemSavedWalking;
            _pushing = _getItemSavedPushing;
            ResetLinkWalkAnimation();
        }
        _getItemStatePhase = 0;
        _getItemObjectsDisabled = null;
        _getItemSavedAnimationMode = null;
        QueueRedraw();
    }
}
