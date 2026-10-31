using System;

namespace Oracles;

public partial class Player
{
    private int _getItemStatePhase;

    // treasure.s supplies $80/$81; essence.s supplies $01. Keep this
    // force-state request separate from pose-only writes made in state08.
    internal void RequestGetItemState(byte parameter)
    {
        if (parameter is not (0x00 or 0x01 or 0x80 or 0x81))
            throw new NotSupportedException($"LINK_STATE_04 wcc50=${parameter:x2} has no represented item-get animation.");
        if (_getItemStatePhase != 0)
            throw new InvalidOperationException("LINK_STATE_04 already owns a pending or active item-get request.");
        _inventory.RuntimeState.SetField("wcc50", parameter);
        _inventory.RuntimeState.SetField("wLinkForceState", 4);
        _getItemStatePhase = 1;
    }

    private bool AdvanceGetItemState()
    {
        if(_getItemStatePhase<2)return false;
        if (_getItemStatePhase == 2)
        {
            CancelNativeItemUsage();
            _inventory.RuntimeState.SetField("wcc52", _inventory.RuntimeState.ReadWramByte(0xd030));

            SetScriptedLinkAnimationMode(null);
            _walking = false;

            byte parameter = _inventory.RuntimeState.ReadField("wcc50");
            if ((parameter & 0x7f) > 1)
                throw new NotSupportedException($"LINK_STATE_04 live wcc50=${parameter:x2} has no represented item-get animation.");
            _getItemOneHandPose = (parameter & 1) == 0;
            _getItemTwoHandPose = !_getItemOneHandPose;
            LinkNativeAnimationDatabase.For(AssetRoot).Set(_inventory.RuntimeState, (parameter & 0x0f) + 0x0e);
            _getItemStatePhase = 3;NativeLinkSubstate=1;
            QueueRedraw();
            return true;
        }
        // State04 ignores $81 during initialization and waits for text first.
        // wcc50 bit7 skips only the subsequent disabled-object check.
        if (!_world.NativeTextActive &&
            ((_inventory.RuntimeState.ReadField("wcc50") & 0x80) != 0 || (_inventory.RuntimeState.ReadField("wDisabledObjects")&0x81)==0))
            CancelGetItemState();
        return true; // Restoring state01 does not also dispatch it this update.
    }

    internal void CancelGetItemState()
    {
        if (_getItemStatePhase == 1 && _inventory.RuntimeState.ReadField("wLinkForceState") == 4)
            _inventory.RuntimeState.SetField("wLinkForceState", 0);
        if (_getItemStatePhase == 3)
        {
            int mode = _inventory.RuntimeState.ReadField("wcc52");
            bool airBody = (_topDownAirborne || _sideScrollAirborne) && mode is 0x18 or 0x19;
            SetScriptedLinkAnimationMode(mode == 0x10 || airBody ? null : mode);
            if (mode == 0x10 || airBody)
                LinkNativeAnimationDatabase.For(AssetRoot).Set(_inventory.RuntimeState, mode);
            if (_topDownAirborne || _sideScrollAirborne)
                _airborneLinkAnimationMode = mode switch
                {
                    0x10 => AirborneLinkAnimationMode.Walk,
                    0x18 => AirborneLinkAnimationMode.Jump,
                    0x19 => AirborneLinkAnimationMode.RocsCape,
                    _ => _airborneLinkAnimationMode
                };
            _getItemOneHandPose = false;
            _getItemTwoHandPose = false;
            _walking = false;
        }
        _getItemStatePhase = 0;
        QueueRedraw();
    }
}
