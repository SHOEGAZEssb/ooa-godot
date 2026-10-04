using System;

namespace oracleofages;

/// <summary>
/// Owns transient status-bar values which deliberately trail their live
/// inventory values. updateStatusBar_body moves wDisplayedRupees by one BCD
/// rupee on every original update, while displayed health recovers one quarter
/// heart every four updates and requests SND_GAINHEART at full-heart boundaries.
/// </summary>
internal sealed class StatusBarController : IDisposable
{
    private readonly InventoryState _inventory;
    private readonly Hud _hud;
    private readonly Action<int> _playSound;
    private readonly Func<int>? _frameCounterSource;
    private double _updateTicks;
    private int _frameCounter;

    internal int DisplayedRupees => _hud.Rupees;
    internal int DisplayedHealth => _hud.HealthQuarters;

    internal StatusBarController(
        InventoryState inventory,
        Hud hud,
        Action<int> playSound,
        Func<int>? frameCounter = null)
    {
        _inventory = inventory;
        _hud = hud;
        _playSound = playSound;
        _frameCounterSource = frameCounter;
        _inventory.FullHealthRefillAttempted += PlayGainHeartSound;
        _inventory.RupeeCapExceeded += PlayRupeeSound;
        SynchronizeHealth();
        SynchronizeRupees();
    }

    public void Dispose()
    {
        _inventory.FullHealthRefillAttempted -= PlayGainHeartSound;
        _inventory.RupeeCapExceeded -= PlayRupeeSound;
    }

    internal void Update(double delta)
    {
        if (delta <= 0.0 || _hud.StatusBarHidden)
            return;

        _updateTicks += delta * OracleSoundEngine.UpdatesPerSecond;
        bool changed = false;
        while (_updateTicks >= 1.0)
        {
            _updateTicks -= 1.0;
            _frameCounter = _frameCounterSource?.Invoke() ?? ((_frameCounter + 1) & 0xff);
            int target = _inventory.Rupees;
            if (_hud.Rupees != target)
            {
                _hud.Rupees += Math.Sign(target - _hud.Rupees);
                _playSound(SoundId.SndRupee);
                changed = true;
            }
            // Native updateStatusBar_body requests the money cue before
            // advancing health, including coincident full-heart updates.
            changed |= UpdateDisplayedHealth();
        }

        if (changed)
            _hud.Refresh();
    }

    internal void UpdateLowHealthWarning()
    {
        // bank2.s:playHeartBeepAtInterval runs from eligible normal-menu
        // dispatch, before opening input. Use live health and global phase,
        // independently of the trailing displayed heart animation.
        int frame = _frameCounterSource?.Invoke() ?? _frameCounter;
        // The native ID $01 exclusion is unused by supported Link states.
        // Mounted Link is ID $09 and still receives this warning.
        if ((frame & 0x3f) != 0) return;
        int doubled = ((_inventory.HealthQuarters - 1) & 0xff) * 2;
        if (doubled > 0xff) return; // Original first ADD A carry gate.
        if (((doubled * 2) & 0xff) < _inventory.MaxHealthQuarters)
            _playSound(SoundId.SndHeartBeep);
    }

    internal void SynchronizeRupees()
    {
        _updateTicks = 0.0;
        _hud.Rupees = _inventory.Rupees;
        _hud.Refresh();
    }

    internal void SynchronizeHealth()
    {
        _hud.HealthQuarters = _inventory.HealthQuarters;
        _hud.MaxHealthQuarters = _inventory.MaxHealthQuarters;
        _hud.Refresh();
    }

    private bool UpdateDisplayedHealth()
    {
        int target = _inventory.HealthQuarters;
        if (_hud.HealthQuarters == target)
            return false;

        if (_hud.HealthQuarters > target)
        {
            _hud.HealthQuarters--;
            return true;
        }

        // updateStatusBar_body fills one quarter-heart only when the global
        // frame counter is divisible by four.
        if ((_frameCounter & 3) != 0)
            return false;

        _hud.HealthQuarters++;
        if ((_hud.HealthQuarters & 3) == 0)
            PlayGainHeartSound();
        return true;
    }

    private void PlayGainHeartSound() => _playSound(SoundId.SndGainHeart);
    private void PlayRupeeSound() => _playSound(SoundId.SndRupee);
}
