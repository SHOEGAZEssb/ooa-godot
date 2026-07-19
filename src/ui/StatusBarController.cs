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
    private double _updateTicks;
    private int _frameCounter;

    internal int DisplayedRupees => _hud.Rupees;

    internal StatusBarController(
        InventoryState inventory,
        Hud hud,
        Action<int> playSound)
    {
        _inventory = inventory;
        _hud = hud;
        _playSound = playSound;
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
        if (delta <= 0.0)
            return;

        _updateTicks += delta * OracleSoundEngine.UpdatesPerSecond;
        bool changed = false;
        while (_updateTicks >= 1.0)
        {
            _updateTicks -= 1.0;
            _frameCounter = (_frameCounter + 1) & 0xff;
            changed |= UpdateDisplayedHealth();

            int target = _inventory.Rupees;
            if (_hud.Rupees == target)
                continue;

            _hud.Rupees += Math.Sign(target - _hud.Rupees);
            _playSound(OracleSoundEngine.SndRupee);
            changed = true;
        }

        if (changed)
            _hud.Refresh();
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

    private void PlayGainHeartSound() => _playSound(OracleSoundEngine.SndGainHeart);
    private void PlayRupeeSound() => _playSound(OracleSoundEngine.SndRupee);
}
